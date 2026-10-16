using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using UnityEngine;

namespace ShiJiuPriceFast;

/// <summary>
/// Client side of "ShiJiu Price Fast".
///
/// ShiJiu-Tools values every item template with its own POST /client/ragfair/itemMarketPrice,
/// serialized behind a global 0.25s gate (MarketTransport.StartGate) and further throttled to
/// one template per 0.1s inside PriceBook.Tick's foreach/break. That caps pricing at roughly
/// 4 templates/second, so items looted mid-raid can sit unpriced for tens of seconds and show
/// up as "估值无报价" / a stalling total.
///
/// This plugin downloads the whole flea price table once, then pre-fills PriceBook's private
/// per-template entry fields (FleaPrice + FleaDone) so ShiJiu never issues those requests at all.
///
/// Crucial: we do NOT write Price/Done. ShiJiu still runs its own
///     Price = ValuationPolicy.A(FleaPrice, bestTraderOffer)
/// with its own Flea eligibility flag (item.Template.CanSellOnRagfair) and its own trader walk,
/// so every number it reports stays exactly as it would have been - just without the waiting.
/// We only burst its own Tick within a small per-call time budget to get through the backlog
/// faster than one template per 0.1s.
/// </summary>
[BepInPlugin(Guid, "ShiJiu Price Fast", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.local.shijiupricefast";

    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<float> BudgetMs;
    internal static ConfigEntry<int> MaxBurst;
    internal static ConfigEntry<bool> Verbose;
    internal static ConfigEntry<float> RefetchMinutes;

    private const string ShiJiuAssemblyName = "ShiJiu-Tools";
    private const string PriceBookTypeName = "StashTools.PriceBook";
    private const float ReArmSeconds = 5f;

    /// <summary>Swapped wholesale by the fetch routine; readers never lock.</summary>
    internal static volatile Dictionary<string, double> Prices =
        new Dictionary<string, double>(StringComparer.Ordinal);

    internal static volatile bool PricesLoaded;

    internal static string PriceSource = "";

    /// <summary>True once the PriceBook reflection surface was resolved and the patch applied.</summary>
    internal static bool Ready;

    private Harmony _harmony;
    private bool _patchApplied;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true,
            "When enabled, ShiJiu's per-template market quote requests are replaced by a single bulk price table.");

        BudgetMs = Config.Bind("General", "Time Budget Per Call (ms)", 2.0f,
            new ConfigDescription(
                "Upper bound on how long the price backfill may run inside one ShiJiu tick. The backlog drains over a few frames instead of stalling one.",
                new AcceptableValueRange<float>(0.1f, 20f)));

        MaxBurst = Config.Bind("General", "Max Burst Iterations", 64,
            new ConfigDescription(
                "Hard cap on how many of ShiJiu's own pricing iterations may run back-to-back per call.",
                new AcceptableValueRange<int>(1, 512)));

        Verbose = Config.Bind("General", "Verbose Logging", true,
            "Log how many entries were backfilled and how long it took.");

        RefetchMinutes = Config.Bind("General", "Refetch Interval (minutes)", 15f,
            new ConfigDescription(
                "How often to re-download the bulk price table.",
                new AcceptableValueRange<float>(1f, 720f)));

        _harmony = new Harmony(Guid);

        StartCoroutine(ResolveRoutine());
        StartCoroutine(FetchRoutine());

        Logger.LogInfo("[ShiJiuPriceFast] v1.0.0 loaded; waiting for ShiJiu-Tools.");
    }

    private void OnDestroy()
    {
        try { _harmony?.UnpatchSelf(); } catch { }
    }

    // ------------------------------------------------------------------ setup

    private IEnumerator ResolveRoutine()
    {
        // ShiJiu-Tools may load after us depending on plugin ordering, so poll for its
        // assembly instead of assuming it is already present.
        for (int attempt = 0; attempt < 600; attempt++)
        {
            if (TryResolve())
            {
                ApplyPatch();
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        Logger.LogWarning("[ShiJiuPriceFast] ShiJiu-Tools never appeared; plugin is inert.");
    }

    private bool TryResolve()
    {
        try
        {
            Type priceBook = null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(asm.GetName().Name, ShiJiuAssemblyName, StringComparison.Ordinal))
                    continue;

                priceBook = asm.GetType(PriceBookTypeName, throwOnError: false);
                break;
            }

            if (priceBook == null) return false;

            return Reflection.TryBind(priceBook);
        }
        catch (Exception ex)
        {
            Logger.LogError("[ShiJiuPriceFast] resolve failed: " + ex);
            return false;
        }
    }

    private void ApplyPatch()
    {
        if (_patchApplied) return;
        _patchApplied = true;

        try
        {
            var prefix = new HarmonyMethod(
                typeof(PriceBookTickPatch).GetMethod(
                    nameof(PriceBookTickPatch.Prefix),
                    BindingFlags.Static | BindingFlags.Public));

            _harmony.Patch(Reflection.TickMethod, prefix: prefix);

            Ready = true;
            Logger.LogInfo("[ShiJiuPriceFast] patch applied to " + Reflection.TickMethod.DeclaringType?.FullName + "::Tick");
        }
        catch (Exception ex)
        {
            Ready = false;
            Logger.LogError("[ShiJiuPriceFast] patch failed, ShiJiu keeps its original behaviour: " + ex);
        }
    }

    // ------------------------------------------------------------------ prices

    private IEnumerator FetchRoutine()
    {
        while (true)
        {
            Task<bool> task = FetchOnceAsync();

            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
                Logger.LogWarning("[ShiJiuPriceFast] fetch threw: " + task.Exception?.GetBaseException().Message);

            yield return new WaitForSeconds(Mathf.Max(1f, RefetchMinutes.Value) * 60f);
        }
    }

    private async Task<bool> FetchOnceAsync()
    {
        // /shijiu/prices comes from our own server mod (flea base table + live fake-player
        // offer averages). /lootnet/prices is accepted as a fallback if that mod is installed.
        foreach (string route in new[] { "/shijiu/prices", "/lootnet/prices" })
        {
            try
            {
                using HttpRequestMessage request =
                    RequestHandler.HttpClient.CreateNewHttpRequest(HttpMethod.Get, route);
                using HttpResponseMessage response =
                    await RequestHandler.HttpClient.HttpClient.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning($"[ShiJiuPriceFast] {route} -> HTTP {(int)response.StatusCode}");
                    continue;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                string json = DecodeBody(bytes);
                if (string.IsNullOrEmpty(json)) continue;

                JToken data = JObject.Parse(json)["data"];
                if (data == null) continue;

                string inner = data.Type == JTokenType.String
                    ? data.Value<string>() ?? string.Empty
                    : data.ToString();

                var parsed = JsonConvert.DeserializeObject<Dictionary<string, double>>(inner);
                if (parsed == null || parsed.Count == 0) continue;

                var clean = new Dictionary<string, double>(parsed.Count, StringComparer.Ordinal);
                foreach (var kv in parsed)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (double.IsNaN(kv.Value) || double.IsInfinity(kv.Value)) continue;
                    clean[kv.Key] = kv.Value;
                }

                if (clean.Count == 0) continue;

                Prices = clean;
                PriceSource = route;
                PricesLoaded = true;

                Logger.LogInfo($"[ShiJiuPriceFast] loaded {clean.Count} prices from {route}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[ShiJiuPriceFast] {route} failed: {ex.Message}");
            }
        }

        PricesLoaded = false;
        Logger.LogWarning("[ShiJiuPriceFast] no bulk price source; ShiJiu keeps pricing one template at a time.");
        return false;
    }

    /// <summary>
    /// SPT 4.1.3+ streams the large routes instead of building a string, so the body is no
    /// longer guaranteed to be zlib-wrapped. Sniff the header and fall back to plain text.
    /// </summary>
    private static string DecodeBody(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;

        try
        {
            if (IsZlib(bytes))
            {
                using var compressed = new MemoryStream(bytes, 2, bytes.Length - 2);
                using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                using var reader = new StreamReader(deflate);
                return reader.ReadToEnd();
            }

            if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                using var compressed = new MemoryStream(bytes);
                using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                return reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning("[ShiJiuPriceFast] body decompress failed, using raw: " + ex.Message);
        }

        return Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    }

    private static bool IsZlib(byte[] bytes)
    {
        if (bytes.Length <= 2) return false;
        if ((bytes[0] & 0x0F) != 0x08) return false;              // deflate compression method
        return ((bytes[0] << 8) | bytes[1]) % 31 == 0;            // zlib header checksum
    }

    // ------------------------------------------------------------------ patch body

    /// <summary>Reflection surface into ShiJiu's private PriceBook, resolved once.</summary>
    internal static class Reflection
    {
        internal static MethodInfo TickMethod;
        internal static FieldInfo EntriesField;      // Dictionary<string, Entry>
        internal static FieldInfo NextField;         // float _next

        internal static FieldInfo EntryFlea;         // bool
        internal static FieldInfo EntryFleaPrice;    // double
        internal static FieldInfo EntryFleaDone;     // bool
        internal static FieldInfo EntryTemplate;     // string
        internal static FieldInfo EntryDone;         // bool

        private const BindingFlags Instance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static bool TryBind(Type priceBook)
        {
            if (priceBook == null) return false;

            TickMethod = priceBook.GetMethod("Tick", Instance, null, Type.EmptyTypes, null);
            EntriesField = priceBook.GetField("Entries", Instance);
            NextField = priceBook.GetField("_next", Instance);

            Type entry = priceBook.GetNestedType("Entry", BindingFlags.NonPublic | BindingFlags.Public);
            if (entry != null)
            {
                EntryFlea = entry.GetField("Flea", Instance);
                EntryFleaPrice = entry.GetField("FleaPrice", Instance);
                EntryFleaDone = entry.GetField("FleaDone", Instance);
                EntryTemplate = entry.GetField("Template", Instance);
                EntryDone = entry.GetField("Done", Instance);
            }

            return TickMethod != null
                && EntriesField != null
                && NextField != null
                && EntryFlea != null
                && EntryFleaPrice != null
                && EntryFleaDone != null
                && EntryTemplate != null
                && EntryDone != null;
        }
    }

    internal static class PriceBookTickPatch
    {
        private static int _lastEntryCount = -1;
        private static bool _quiesced;
        private static float _lastWorkAt = -999f;

        public static bool Prefix(object __instance)
        {
            try
            {
                return Run(__instance);
            }
            catch (Exception ex)
            {
                // Never let an optimisation break the game: fall back to ShiJiu's own path.
                Ready = false;
                Log.LogError("[ShiJiuPriceFast] disabled after error, ShiJiu continues normally: " + ex);
                return true;
            }
        }

        private static bool Run(object instance)
        {
            if (!Enabled.Value) return true;
            if (!Ready) return true;
            if (!PricesLoaded) return true;
            if (instance == null) return true;

            if (!(Reflection.EntriesField.GetValue(instance) is IDictionary entries)) return true;
            if (entries.Count == 0)
            {
                _quiesced = false;
                _lastEntryCount = 0;
                return true;
            }

            int count = entries.Count;
            float now = Time.unscaledTime;

            // Nothing new to do: same entry count and we worked recently. Re-arms after a
            // price refresh (Entry.Done reset without the table changing) or a new template.
            if (_quiesced && count == _lastEntryCount && now - _lastWorkAt < ReArmSeconds)
                return true;

            _lastEntryCount = count;

            long t0 = Stopwatch.GetTimestamp();
            double budget = BudgetMs.Value;

            // --- 1) backfill the market quote from the bulk table -------------------
            // Setting FleaDone makes ShiJiu's own loop skip MarketQuotes.Get entirely,
            // which is where the serialized HTTP request lives.
            int filled = 0;
            foreach (DictionaryEntry de in entries)
            {
                if (MsSince(t0) >= budget) break;

                object entry = de.Value;
                if (entry == null) continue;
                if (!(bool)Reflection.EntryFlea.GetValue(entry)) continue;   // not flea-sellable, traders only
                if ((bool)Reflection.EntryFleaDone.GetValue(entry)) continue;

                string template = (string)Reflection.EntryTemplate.GetValue(entry);
                if (template == null) continue;
                if (!Prices.TryGetValue(template, out double price) || price <= 0.0) continue;

                Reflection.EntryFleaPrice.SetValue(entry, price);
                Reflection.EntryFleaDone.SetValue(entry, true);
                filled++;
            }

            // --- 2) let ShiJiu finish the valuation, faster than 1 template / 0.1s -
            // Only FleaPrice/FleaDone were written, so the trader walk and
            // ValuationPolicy.A(FleaPrice, traderMax) remain entirely ShiJiu's own.
            int pendingBefore = CountPending(entries);
            int target = Math.Min(pendingBefore, MaxBurst.Value);
            int bursts = 0;

            while (bursts < target && MsSince(t0) < budget)
            {
                Reflection.NextField.SetValue(instance, 0f);          // lift Tick's own 0.1s throttle
                Reflection.TickMethod.Invoke(instance, null);
                bursts++;
            }

            // Only keep hammering while we are actually getting somewhere. If a full
            // burst pass changes nothing, the remainder cannot be priced by ShiJiu
            // either (no quote, no trader session); it has its own 15s/60s retry schedule,
            // so we stand down and re-arm later.
            bool progressed = filled > 0 || (pendingBefore > 0 && CountPending(entries) < pendingBefore);

            if (progressed) _lastWorkAt = now;
            else _quiesced = true;

            if (Verbose.Value && (filled > 0 || bursts > 0))
            {
                Log.LogInfo($"[ShiJiuPriceFast] filled={filled} bursts={bursts} " +
                            $"pending={pendingBefore} entries={count} " +
                            $"({MsSince(t0):F2} ms){(progressed ? "" : " [stalled]")}");
            }

            // The original still runs; since we just set _next = now + 0.1 it returns at once,
            // so the normal 0.1s cadence (and ShiJiu's own retry logic) is preserved.
            return true;
        }

        private static int CountPending(IDictionary entries)
        {
            int n = 0;
            foreach (DictionaryEntry de in entries)
            {
                object entry = de.Value;
                if (entry == null) continue;
                if (!(bool)Reflection.EntryDone.GetValue(entry)) n++;
            }
            return n;
        }

        private static double MsSince(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}
