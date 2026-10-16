using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace FikaInventoryBusyDiag
{
    /// <summary>
    /// READ-ONLY diagnostics for the "PlayerIsBusyError / is currently being modified" lock-up.
    /// It never modifies game state: every patch is a void Prefix or a Postfix that only logs.
    /// </summary>
    [BepInPlugin("com.local.fikainventorydiag", "Fika Inventory Busy Diag", "1.0.0")]
    public class BusyDiagPlugin : BaseUnityPlugin
    {
        internal const string Tag = "[NekoBusyDiag]";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> LogAddRemove;
        internal static ConfigEntry<float> StaleSeconds;
        internal static ConfigEntry<float> LogCooldown;
        internal static ConfigEntry<float> ScanInterval;

        private static FieldInfo _activeEventsField;

        internal sealed class Rec
        {
            public float Born;
            public int Frame;
            public string Kind = "?";
        }

        // Does not keep the event alive and does not prevent GC.
        internal static readonly ConditionalWeakTable<ItemEventArgs, Rec> Age =
            new ConditionalWeakTable<ItemEventArgs, Rec>();

        internal static readonly HashSet<ItemController> Seen = new HashSet<ItemController>();
        private static readonly Dictionary<string, float> LastLog = new Dictionary<string, float>();
        private static float _nextScan;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Master switch.");
            LogAddRemove = Config.Bind("General", "Log Add/Remove", false,
                "Log every ActiveEvent add and remove. Very noisy - only for deep tracing.");
            StaleSeconds = Config.Bind("General", "Stale Threshold (s)", 30f,
                "An ActiveEvent that lives longer than this is reported as a likely orphan.");
            LogCooldown = Config.Bind("General", "Log Cooldown (s)", 5f,
                "Minimum seconds between two identical log lines.");
            ScanInterval = Config.Bind("General", "Scan Interval (s)", 5f,
                "How often to scan ActiveEvents for stale entries.");

            _activeEventsField = AccessTools.Field(typeof(ItemController), "_activeEvents");
            if (_activeEventsField == null)
            {
                Log.LogError(Tag + " _activeEvents field not found - plugin inert.");
                Enabled.Value = false;
                return;
            }

            var h = new Harmony("com.local.fikainventorydiag");
            int ok = 0;
            ok += Try(h, typeof(Patch_AddActiveEvent), "AddActiveEvent");
            ok += Try(h, typeof(Patch_RemoveActiveEvent), "RemoveActiveEvent");
            ok += Try(h, typeof(Patch_CheckItemAction), "CheckItemAction");

            Log.LogInfo(Tag + " v1.0.0 loaded (read-only). Patches applied: " + ok + "/3. " +
                        "Stale>" + StaleSeconds.Value + "s, scan every " + ScanInterval.Value + "s.");
            if (ok < 3)
                Log.LogWarning(Tag + " some patches failed - see errors above.");
        }

        private static int Try(Harmony h, Type t, string label)
        {
            try { h.CreateClassProcessor(t).Patch(); return 1; }
            catch (Exception ex) { Log.LogError(Tag + " patch " + label + " failed: " + ex.Message); return 0; }
        }

        private void Update()
        {
            if (!Enabled.Value) return;
            if (Time.realtimeSinceStartup < _nextScan) return;
            _nextScan = Time.realtimeSinceStartup + Mathf.Max(1f, ScanInterval.Value);
            try { ScanStale(); } catch (Exception ex) { Log.LogWarning(Tag + " scan error: " + ex.Message); }
        }

        private static void ScanStale()
        {
            if (Seen.Count == 0) return;

            var stale = new List<string>();
            int total = 0;
            float now = Time.realtimeSinceStartup;

            foreach (var ctrl in Seen)
            {
                if (ctrl == null) continue;
                var list = Events(ctrl);
                if (list == null || list.Count == 0) continue;
                total += list.Count;

                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e == null) continue;
                    Rec r;
                    if (!Age.TryGetValue(e, out r))
                    {
                        Age.Add(e, new Rec { Born = now, Frame = Time.frameCount, Kind = "?" });
                        continue;
                    }
                    float age = now - r.Born;
                    if (age < StaleSeconds.Value) continue;
                    stale.Add(string.Format("    {0}#{1} item={2} age={3:F1}s bornFrame={4}",
                        e.GetType().Name, e.EventId, ItemStr(e.Item), age, r.Frame));
                }
            }

            if (stale.Count > 0)
            {
                Log.LogWarning(string.Format("{0} STALE: {1} of {2} ActiveEvent(s) older than {3}s on {4} controller(s):",
                    Tag, stale.Count, total, StaleSeconds.Value, Seen.Count));
                foreach (var s in stale) Log.LogWarning(s);
                Log.LogWarning(Tag + " -> an item appearing above is very likely permanently locked.");
            }
        }

        // ---------------- helpers ----------------

        internal static List<ItemEventArgs> Events(ItemController c)
        {
            try
            {
                if (c == null || _activeEventsField == null) return null;
                return _activeEventsField.GetValue(c) as List<ItemEventArgs>;
            }
            catch { return null; }
        }

        internal static string ItemStr(Item it)
        {
            if (it == null) return "<null>";
            try { return it.Name + "(" + it.Id + ")"; }
            catch { return "<err>"; }
        }

        internal static string AddrStr(ItemAddress a)
        {
            if (a == null) return "<null-addr>";
            try
            {
                var pi = a.Container != null ? a.Container.ParentItem : null;
                return (pi != null ? pi.Name : "<?>" ) + "[" + a.ContainerName + "]";
            }
            catch { return "<err>"; }
        }

        /// <summary>Rate-limited logging so a blocked item does not flood the log.</summary>
        internal static void LogOnce(string key, string msg)
        {
            float now = Time.realtimeSinceStartup;
            float last;
            if (LastLog.TryGetValue(key, out last) && now - last < LogCooldown.Value) return;
            LastLog[key] = now;
            Log.LogWarning(msg);
        }
    }

    // ---------------------------------------------------------------- patches

    [HarmonyPatch(typeof(ItemController), nameof(ItemController.AddActiveEvent))]
    internal static class Patch_AddActiveEvent
    {
        [HarmonyPostfix]
        internal static void Postfix(ItemController __instance, ItemEventArgs activeEvent)
        {
            if (!BusyDiagPlugin.Enabled.Value || activeEvent == null) return;
            try
            {
                BusyDiagPlugin.Seen.Add(__instance);
                BusyDiagPlugin.Age.Remove(activeEvent);
                BusyDiagPlugin.Age.Add(activeEvent, new BusyDiagPlugin.Rec
                {
                    Born = Time.realtimeSinceStartup,
                    Frame = Time.frameCount,
                    Kind = activeEvent.GetType().Name
                });

                if (BusyDiagPlugin.LogAddRemove.Value)
                {
                    var list = BusyDiagPlugin.Events(__instance);
                    BusyDiagPlugin.Log.LogInfo(string.Format("{0} +ADD {1}#{2} item={3} loc={4} active={5}",
                        BusyDiagPlugin.Tag, activeEvent.GetType().Name, activeEvent.EventId,
                        BusyDiagPlugin.ItemStr(activeEvent.Item), BusyDiagPlugin.AddrStr(activeEvent.Location),
                        list != null ? list.Count : -1));
                }
            }
            catch (Exception ex) { BusyDiagPlugin.Log.LogWarning(BusyDiagPlugin.Tag + " add-hook: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(ItemController), nameof(ItemController.RemoveActiveEvent))]
    internal static class Patch_RemoveActiveEvent
    {
        // Prefix: the record is still valid at this point.
        [HarmonyPrefix]
        internal static void Prefix(ItemController __instance, ItemEventArgs activeEvent)
        {
            if (!BusyDiagPlugin.Enabled.Value || activeEvent == null) return;
            try
            {
                BusyDiagPlugin.Rec r;
                float age = -1f;
                if (BusyDiagPlugin.Age.TryGetValue(activeEvent, out r))
                    age = Time.realtimeSinceStartup - r.Born;

                if (BusyDiagPlugin.LogAddRemove.Value)
                {
                    BusyDiagPlugin.Log.LogInfo(string.Format("{0} -REM {1}#{2} item={3} age={4:F2}s",
                        BusyDiagPlugin.Tag, activeEvent.GetType().Name, activeEvent.EventId,
                        BusyDiagPlugin.ItemStr(activeEvent.Item), age));
                }

                BusyDiagPlugin.Age.Remove(activeEvent);
            }
            catch (Exception ex) { BusyDiagPlugin.Log.LogWarning(BusyDiagPlugin.Tag + " rem-hook: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(ItemController), nameof(ItemController.CheckItemAction))]
    internal static class Patch_CheckItemAction
    {
        // Void prefix: cannot skip the original method, purely observational.
        [HarmonyPrefix]
        internal static void Prefix(ItemController __instance, Item item, ItemAddress location)
        {
            if (!BusyDiagPlugin.Enabled.Value || item == null) return;
            try
            {
                BusyDiagPlugin.Seen.Add(__instance);
                var list = BusyDiagPlugin.Events(__instance);
                if (list == null || list.Count == 0) return;

                float now = Time.realtimeSinceStartup;

                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e == null) continue;

                    // Replicate the exact matching logic used by CheckItemAction.
                    bool hit = ReferenceEquals(item, e.Item);
                    if (!hit && location != null)
                    {
                        try { hit = ItemController.CheckLocation(item, location, e.Item, e.Location); }
                        catch { hit = false; }
                    }
                    if (!hit) continue;

                    BusyDiagPlugin.Rec r;
                    float age = -1f;
                    if (BusyDiagPlugin.Age.TryGetValue(e, out r)) age = now - r.Born;
                    string key = "BUSY|" + e.GetType().Name + "|" + item.Id;

                    BusyDiagPlugin.LogOnce(key, string.Format(
                        "{0} BUSY-BLOCK item={1} loc={2}\n" +
                        "{0}    blocking event : {3}#{4}  (age={5:F1}s, eventItem={6})\n" +
                        "{0}    activeEvents  : {7} entry(ies) on this controller\n" +
                        "{0}    => this item is locked; it will keep returning PlayerIsBusyError",
                        BusyDiagPlugin.Tag, BusyDiagPlugin.ItemStr(item), BusyDiagPlugin.AddrStr(location),
                        e.GetType().Name, e.EventId, age, BusyDiagPlugin.ItemStr(e.Item), list.Count));
                }
            }
            catch (Exception ex) { BusyDiagPlugin.Log.LogWarning(BusyDiagPlugin.Tag + " check-hook: " + ex.Message); }
        }
    }
}
