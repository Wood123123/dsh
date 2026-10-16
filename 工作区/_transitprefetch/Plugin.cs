using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using HarmonyLib;
using UnityEngine;

namespace TransitPrefetch
{
    /// <summary>
    /// 修复「走到中转点却没有互动按钮」的时序竞态。
    ///
    /// 根因（读 Fika 2.4.4 源码得出）：
    ///   ClientTransitController.OnInitEvent()
    ///     → GetTransitPoints() → IsVisibleTransitPoint()
    ///       → hideIfNoKey == true 的中转点（如 海岸线→迷宫 id=43）
    ///         必须调 TryGetAccessToLocation()
    ///           → Fika 的 [PatchPrefix] 发一个【异步】HTTP 请求
    ///              GetTraderServicesDataFromServer(TransitTraderId)
    ///           → 但 TryGetAccessToLocation 紧接着【同步】检查缓存
    ///           → 请求没回来 → 返回 false → 该点不可见 → 没有互动按钮
    ///
    ///   而真正补齐数据的 UpdateBtrTraderServiceData() 只在
    ///   OnClientPlayerEnter()（玩家【走进】中转区域）里被调用，
    ///   且带一个 !transitPlayers.ContainsKey(...) 的「仅首次」守卫。
    ///
    ///   结果：可见性判定发生在数据到位【之前】，
    ///         如果 UpdateTimers() 没有被周期性调用，按钮就永远不出现。
    ///
    /// 本补丁做的事（只用 public API，不碰私有成员）：
    ///   ① OnInitEvent 前缀：立刻催一次 UpdateBtrTraderServiceData()
    ///   ② 随后 6 秒内每 0.5 秒：再催一次 + 调用 public 的 UpdateTimers()
    ///      重算可见性 → 数据一旦到位，按钮立即出现
    ///
    /// ★ 只在客户端生效（房主/无头端有自己的路径，不需要）。
    /// </summary>
    [BepInPlugin("com.local.transitprefetch", "Transit BTR Prefetch Fix", "1.0.0")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static Plugin _instance;
        private const string Tag = "[TransitPrefetch]";
        private const string FikaClientTransitController = "Fika.Core.Main.ClientClasses.ClientTransitController";

        private void Awake()
        {
            Log = Logger;
            _instance = this;

            if (IsHeadless())
            {
                Log.LogInfo($"{Tag} 无头端进程 —— 不启用（客户端专用补丁）");
                return;
            }

            var harmony = new Harmony("com.local.transitprefetch");
            int ok = 0;

            // ① OnInitEvent：收到房主下发的中转点后立刻催数据 + 启动重试
            ok += Patch(harmony, FikaClientTransitController, "OnInitEvent", nameof(OnInitEvent_Prefix), null);

            // ② OnClientPlayerEnter：去掉「仅首次」的间接影响 —— 每次进区域都催一次
            ok += Patch(harmony, FikaClientTransitController, "OnClientPlayerEnter", nameof(OnClientPlayerEnter_Prefix), null);

            // ③ UpdateTimers：重算前也催一次，保证每次重算都伴随一次数据刷新
            ok += Patch(harmony, FikaClientTransitController, "UpdateTimers", nameof(UpdateTimers_Prefix), null);

            Log.LogWarning($"{Tag} ===== 已装载 {ok} 个补丁 =====");
            if (ok == 0) Log.LogError($"{Tag} 一个都没挂上，请检查 Fika 版本");
        }

        private static int Patch(Harmony h, string typeName, string methodName, string prefix, string postfix)
        {
            try
            {
                Type t = AccessTools.TypeByName(typeName);
                if (t == null) { Log.LogError($"{Tag} 找不到类型 {typeName}"); return 0; }
                MethodInfo m = AccessTools.Method(t, methodName);
                if (m == null) { Log.LogError($"{Tag} 找不到方法 {typeName}::{methodName}"); return 0; }
                h.Patch(m,
                    prefix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Plugin), prefix)),
                    postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Plugin), postfix)));
                Log.LogWarning($"{Tag} ✅ {m.DeclaringType.Name}::{m.Name}({string.Join(", ", SafeParams(m))})");
                return 1;
            }
            catch (Exception e)
            {
                Log.LogError($"{Tag} 挂载 {typeName}::{methodName} 失败 {e.GetType().Name}: {e.Message}");
                return 0;
            }
        }

        private static string[] SafeParams(MethodInfo m)
        {
            try { return Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name); }
            catch { return new[] { "?" }; }
        }

        private static bool IsHeadless()
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies().Any_(a =>
                {
                    try { return (a.GetName().Name ?? "").IndexOf("Fika.Headless", StringComparison.OrdinalIgnoreCase) >= 0; }
                    catch { return false; }
                });
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────
        // 核心：催一次 BTR 商人服务数据
        // ─────────────────────────────────────────────
        internal static void NudgeBtr()
        {
            try
            {
                Player me = GamePlayerOwner.MyPlayer;
                if (me == null) return;
                if (me is LocalPlayer lp)
                {
                    lp.UpdateBtrTraderServiceData();     // public；Fika 已把它改为「发请求即返回」
                }
            }
            catch (Exception e)
            {
                Log?.LogDebug($"{Tag} NudgeBtr 异常 {e.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // ① OnInitEvent —— 收到中转点清单时立刻开始
        // ─────────────────────────────────────────────
        private static void OnInitEvent_Prefix(object __instance)
        {
            try
            {
                Log.LogWarning($"{Tag} ① 收到中转点清单 → 提前催 BTR 数据 + 启动可见性重试");
                NudgeBtr();
                if (_instance != null && __instance != null)
                {
                    _instance.StartCoroutine(RetryVisibility(__instance, 12, 0.5f));
                }
            }
            catch (Exception e) { Log.LogError($"{Tag} ① 异常 {e.Message}"); }
        }

        private static IEnumerator RetryVisibility(object controller, int times, float interval)
        {
            MethodInfo update = null;
            try { update = AccessTools.Method(controller.GetType(), "UpdateTimers"); } catch { }
            if (update == null) { Log.LogError($"{Tag} 找不到 UpdateTimers，重试中止"); yield break; }

            var wait = new WaitForSeconds(interval);
            for (int i = 0; i < times; i++)
            {
                yield return wait;
                NudgeBtr();
                try { update.Invoke(controller, null); }
                catch (Exception e) { Log?.LogDebug($"{Tag} UpdateTimers 调用异常 {e.Message}"); }
            }
            Log.LogWarning($"{Tag} 可见性重试结束（{times} 次 / {times * interval:0.#} 秒）");
        }

        // ─────────────────────────────────────────────
        // ② OnClientPlayerEnter —— 每次进区域都催（绕过 Fika 的「仅首次」守卫）
        // ─────────────────────────────────────────────
        private static void OnClientPlayerEnter_Prefix()
        {
            try { NudgeBtr(); }
            catch (Exception e) { Log?.LogDebug($"{Tag} ② 异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ③ UpdateTimers —— 每次重算前催一次
        // ─────────────────────────────────────────────
        private static void UpdateTimers_Prefix()
        {
            try { NudgeBtr(); }
            catch (Exception e) { Log?.LogDebug($"{Tag} ③ 异常 {e.Message}"); }
        }
    }

    internal static class EnumExt
    {
        public static bool Any_<T>(this System.Collections.Generic.IEnumerable<T> src, Func<T, bool> pred)
        {
            foreach (var x in src) { if (pred(x)) return true; }
            return false;
        }
    }
}
