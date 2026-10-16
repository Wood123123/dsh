using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using EFT.InventoryLogic;
using Diz.LanguageExtensions;
using HarmonyLib;

namespace Dsh.TripwireSyncFix
{
    /// <summary>
    /// 修 ciallo.inftripwire（Tripwire Grenade Improvement）在网络同步下破坏物品状态的问题。
    ///
    /// ★ 问题：
    ///   绊雷的放置是一次【网络物品操作】。发送方打包 PlantTripwireOperationDescriptor，
    ///   接收方在 PlantTripwireOperationDescriptor.ToInventoryOperation 里
    ///   【重新调用 SimulatePlantTripwire】来重建操作。
    ///
    ///   而原 mod 是在 SimulatePlantTripwire 上打 Prefix、Discard 掉绊雷后 return false。
    ///   于是同一颗雷在两端各被 Discard 一次，接收方那边留下未完成的移除记录；
    ///   等玩家把绊雷拆掉、把那颗雷捡回背包之后，它就无法再装备了，
    ///   报 "Cannot apply item … because item Default Inventory … is currently being modified"。
    ///
    /// ★ 修法：
    ///   给 ToInventoryOperation 包一层标志。处于网络接收路径时【不拦截】，
    ///   让接收方走原版逻辑（雷与布设装置都按原版丢弃，所有状态与同步记录都是干净的）。
    ///   只有发送方（本地发起布设的那一端）才套用"不消耗布设装置"。
    ///
    /// ★★★ 加载顺序是硬前提：
    ///   必须排在 ciallo.inftripwire 之后，Unpatch 才能摘掉它的 Prefix。
    ///   如果顺序反了，两个 Prefix 会同时存在 —— 那会让同一颗雷被 Discard 两次，
    ///   比不修还糟。所以：
    ///     · 用 [BepInDependency] 强制依赖（保证加载顺序）
    ///     · 并且保留一个"延迟重试"兜底（见 Update），万一依赖在那台机器上没生效也能自愈
    /// </summary>
    [BepInPlugin(Guid, "Tripwire Sync Fix", "1.0.0")]
    [BepInDependency("ciallo.inftripwire", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class TripwireSyncFixPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.dsh.tripwiresyncfix";

        internal static ManualLogSource Log;

        /// <summary>原 mod 的 Prefix 是否已被成功摘除。false 时每 2 秒重试一次。</summary>
        private static bool _prefixRemoved;
        private float _nextRetry;

        private void Awake()
        {
            Log = Logger;

            // ① 先摘掉原 mod 的 Prefix —— 我们要用自己的实现替换它
            _prefixRemoved = TryRemoveOriginalPrefix();

            // ② 装上我们自己的
            try
            {
                new Harmony(Guid).PatchAll(typeof(TripwireSyncFixPlugin).Assembly);
                Log.LogInfo("[TripwireFix] 已装载修正补丁"
                          + (_prefixRemoved ? "（原 mod 的 Prefix 已被替换）" : "（原 Prefix 尚未出现，将重试摘除）"));
            }
            catch (Exception e)
            {
                Log.LogError("[TripwireFix] 装载失败: " + e);
            }
        }

        /// <summary>
        /// ★ 兜底重试。
        ///
        /// 依赖声明正常情况下能保证加载顺序，但如果那台机器上 ciallo.inftripwire 的
        /// GUID 变了、或依赖被忽略，我们就要在它打完补丁之后再摘一次。
        /// 否则两个 Prefix 并存 → 同一颗雷被 Discard 两次 → 比不修更糟。
        /// </summary>
        private void Update()
        {
            if (_prefixRemoved) return;

            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextRetry) return;
            _nextRetry = now + 2f;

            if (TryRemoveOriginalPrefix())
            {
                _prefixRemoved = true;
                Log.LogInfo("[TripwireFix] ★ 延迟摘除成功 —— 原 mod 的 Prefix 已移除");
            }
        }

        /// <summary>把 ciallo.inftripwire 打在 SimulatePlantTripwire 上的 Prefix 反注册掉。</summary>
        private static bool TryRemoveOriginalPrefix()
        {
            try
            {
                var target = AccessTools.Method(typeof(ItemManipulator), "SimulatePlantTripwire");
                if (target == null)
                {
                    Log.LogWarning("[TripwireFix] 找不到 ItemManipulator.SimulatePlantTripwire");
                    return false;
                }

                // 先看它到底在不在，避免无谓地调用 Unpatch
                if (!HasOriginalPrefix(target)) return false;

                var h = new Harmony("ciallo.inftripwire");
                h.Unpatch(target, HarmonyPatchType.Prefix, "ciallo.inftripwire");

                return !HasOriginalPrefix(target);
            }
            catch (Exception e)
            {
                Log.LogWarning("[TripwireFix] 移除原 Prefix 失败: " + e.Message);
                return false;
            }
        }

        /// <summary>目标方法上是否还挂着 ciallo.inftripwire 的 Prefix。</summary>
        private static bool HasOriginalPrefix(System.Reflection.MethodBase target)
        {
            var info = Harmony.GetPatchInfo(target);
            if (info == null || info.Prefixes == null) return false;

            foreach (var p in info.Prefixes)
            {
                if (string.Equals(p.owner, "ciallo.inftripwire", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 标记"当前正处于网络接收路径"。
    ///
    /// ★ 这是整个修复的关键：ToInventoryOperation 是接收方重建操作的地方，
    ///   它会再跑一次 SimulatePlantTripwire。那个时刻必须放行原版逻辑。
    ///
    ///   用 [ThreadStatic] 深度计数 —— 网络包可能在同一线程上嵌套处理。
    /// </summary>
    internal static class NetReceiveScope
    {
        [ThreadStatic] private static int _depth;

        public static bool Active => _depth > 0;

        public static void Enter() { _depth++; }
        public static void Exit() { if (_depth > 0) _depth--; }
    }

    /// <summary>包住 PlantTripwireOperationDescriptor.ToInventoryOperation。</summary>
    [HarmonyPatch]
    internal static class PatchToInventoryOperation
    {
        private static MethodBase TargetMethod()
        {
            // ★ 不写死参数列表：不同 EFT 版本的签名可能不同，
            //   按名字取唯一重载最稳。
            var t = AccessTools.TypeByName("EFT.InventoryLogic.PlantTripwireOperationDescriptor")
                 ?? AccessTools.TypeByName("PlantTripwireOperationDescriptor");
            if (t == null) return null;
            return AccessTools.Method(t, "ToInventoryOperation");
        }

        private static bool Prepare()
        {
            var m = TargetMethod();
            if (m == null)
            {
                TripwireSyncFixPlugin.Log?.LogWarning(
                    "[TripwireFix] 找不到 PlantTripwireOperationDescriptor.ToInventoryOperation —— "
                  + "网络接收路径将不被标记（修复退化为原行为）");
                return false;
            }
            return true;
        }

        private static void Prefix() { NetReceiveScope.Enter(); }

        // ★ 只用 Finalizer 退出，不用 Postfix。
        //   Harmony 的执行顺序是 Prefix → 原方法 → Postfix → Finalizer，
        //   若两处都 Exit，正常路径下计数会被减两次，标志就会错乱。
        //   Finalizer 无论正常返回还是抛异常都会执行，正好是配对的。
        private static void Finalizer() { NetReceiveScope.Exit(); }
    }

    /// <summary>
    /// 替代原 mod 的 Prefix：同样的"布设装置不消耗"，但网络接收路径放行。
    /// </summary>
    [HarmonyPatch]
    internal static class PatchSimulatePlantTripwireFixed
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ItemManipulator), "SimulatePlantTripwire");
        }

        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        /// <summary>
        /// 读原 mod 的配置（ciallo.inftripwire.cfg 里的 "Infinite Tripwire"）。
        ///
        /// ★ 结果缓存 30 秒。
        ///   这个值只在"布设绊雷"时用到，本来就是低频；但每次布设都去读一次磁盘
        ///   仍然是不必要的 —— 缓存之后，连续布设只会有一次文件访问。
        ///   改配置后最多 30 秒生效，不影响使用。
        /// </summary>
        private static bool _noConsumption;
        private static float _noConsumptionReadAt = float.NegativeInfinity;
        private const float ConfigCacheSeconds = 30f;

        private static bool NoConsumptionEnabled()
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now - _noConsumptionReadAt < ConfigCacheSeconds) return _noConsumption;

            _noConsumptionReadAt = now;
            _noConsumption = ReadNoConsumptionFromDisk();
            return _noConsumption;
        }

        private static bool ReadNoConsumptionFromDisk()
        {
            try
            {
                // ★ 全限定：using EFT.InventoryLogic 会引入同名的 Paths，直接写 Paths 会解析错
                var path = Path.Combine(BepInEx.Paths.ConfigPath, "ciallo.inftripwire.cfg");
                if (!File.Exists(path)) return false;

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line.Substring(0, eq).Trim();
                    if (!string.Equals(key, "Infinite Tripwire", StringComparison.OrdinalIgnoreCase)) continue;

                    var val = line.Substring(eq + 1).Trim();
                    return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        private static bool Prefix(InventoryController controller, ThrowWeap tripwire,
                                   PlantingKit plantingKit,
                                   ref OperationResult<PlantTripwireResult> __result)
        {
            // ── 放行条件 ───────────────────────────────────────────────
            // ★ ① 网络接收路径：接收方重建操作时必须走原版，
            //      否则同一颗雷会被两端各 Discard 一次，物品状态就此损坏。
            if (NetReceiveScope.Active)
            {
                return true;
            }

            // ★ ② 配置没开 → 原版行为
            if (!NoConsumptionEnabled())
            {
                return true;
            }

            // ★ ③ 没有布设装置（bot / 特殊路径）→ 没有可保留的东西，走原版
            if (plantingKit == null)
            {
                return true;
            }

            // ── 以下与原 mod 一致：只丢弃绊雷，保留布设装置 ──────────────
            var discard = ItemManipulator.Discard(tripwire, controller, true);
            if (discard.Failed)
            {
                // ★ OperationResult<T> 没有静态工厂方法，靠隐式转换：
                //     Error            → OperationResult<T>
                //     TValue           → OperationResult<T>
                __result = discard.Error;
                return false;
            }

            var list = new List<ISyncOperationResult>(1) { discard.Value };
            __result = new PlantTripwireResult(tripwire, plantingKit,
                new CombinedOperationResult(list.ToArray()));
            return false;
        }
    }
}
