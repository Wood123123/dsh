using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace TacticalSprintWeightGate
{
    public enum WeightGate
    {
        /// <summary>不限制。</summary>
        None = 0,
        /// <summary>有任何负重 debuff 就禁止（encumbered，约 26kg 起，随力量等级上浮）。</summary>
        Encumbered = 1,
        /// <summary>只有"过重" debuff 才禁止（overEncumbered，约 77kg 起）。</summary>
        OverEncumbered = 2,
    }

    /// <summary>
    /// 给 FromHell.TacticalSprint 加一道负重闸门：处于负重/过重状态时不允许战术冲刺。
    ///
    /// 挂载点选在原 mod 的 Plugin.Eligible(Player) —— 那是它所有功能（输入读取、速度倍率、
    /// 动画修饰、耐力消耗）的唯一闸门。把它 Postfix 成 false，原 mod 会在 LateUpdate 里走到
    ///     if (!Eligible(val)) { gate.Reset(); SyncDrain(); Restore(); return; }
    /// 也就是干净地中断：关双击门 + 移除额外耐力消耗 + 恢复动画参数。
    ///
    /// 状态读取：优先用事件订阅（EncumberedChanged / OverEncumberedChanged），
    /// 每帧再做一次字段兜底校验，保证事件万一漏掉也不会失效。
    ///
    /// 为什么用归一化的 Overweight 而不是写死 kg：
    ///     PhysicalBase.UpdateWeightLimits() 里，
    ///         BaseOverweightLimits = globals.BaseOverweightLimits
    ///                              * (Skills.CarryingWeightRelativeModifier          // 力量技能
    ///                                 * HealthController.CarryingWeightRelativeModifier)  // 健康
    ///                              + HealthController.CarryingWeightAbsoluteModifier;
    ///     而 Overweight = InverseLerp(BaseOverweightLimits, 总重)。
    /// 所以 Overweight 天然是"相对该玩家自己上限"的归一化值，自动适配力量等级。
    /// </summary>
    [BepInPlugin(Guid, "Tactical Sprint - 负重限制", "1.0.0")]
    [BepInDependency("com.fromhell.tacticalsprint", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.local.tacticalsprint.weightgate";
        private const string Tag = "[WeightGate]";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<WeightGate> Gate;
        internal static ConfigEntry<bool> Debug;

        private Harmony _harmony;

        // ── 绑定的 Physical 实例与其负重状态缓存 ──────────────────────────
        private PhysicalBase _bound;
        private bool _encumbered;
        private bool _overEncumbered;

        // ── 仅在状态变化时打日志，避免刷屏 ────────────────────────────────
        private bool _lastBlocked;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "总开关。关闭后完全不干预 TacticalSprint。");
            Gate = Config.Bind("General", "Gate", WeightGate.Encumbered,
                "负重门槛：Encumbered = 有任何负重就禁止（约 26kg 起）；" +
                "OverEncumbered = 只有过重才禁止（约 77kg 起）；None = 不限制。" +
                "门槛基于玩家自己的负重上限，已含力量技能与健康状态修正。");
            Debug = Config.Bind("General", "Debug", false,
                "输出调试日志（绑定/解绑、负重状态变化、兜底纠正、拦截与放行）。" +
                "日志只在状态变化时输出，不会每帧刷屏。默认关闭。");

            _harmony = new Harmony(Guid);
            int ok = ApplyPatches();
            if (ok == 0)
            {
                Log.LogError(Tag + " 未找到 FromHell.TacticalSprint 的 Plugin.Eligible —— " +
                                  "负重限制未生效。请确认该 mod 已安装且版本匹配。");
                return;
            }

            Log.LogInfo(Tag + " v1.0.0 已加载。patching=" + ok + "/4, Enabled=" + Enabled.Value +
                        ", Gate=" + Gate.Value + ", Debug=" + Debug.Value);

            if (Debug.Value)
            {
                Log.LogInfo(Tag + " 调试日志已开启。");
            }
        }

        private int ApplyPatches()
        {
            int ok = 0;
            try
            {
                Type t = AccessTools.TypeByName("FromHell.TacticalSprint.Plugin");
                if (t == null)
                {
                    Log.LogWarning(Tag + " 找不到类型 FromHell.TacticalSprint.Plugin。");
                    return 0;
                }

                MethodInfo eligible = AccessTools.Method(t, "Eligible", new[] { typeof(Player) });
                if (eligible == null)
                {
                    Log.LogWarning(Tag + " 找不到 Plugin.Eligible(Player)。");
                    return 0;
                }

                _harmony.Patch(eligible, postfix: new HarmonyMethod(
                    typeof(Patches).GetMethod(nameof(Patches.EligiblePostfix))));
                ok++;

                // 以下三个只是为了让事件订阅跟着原 mod 的生命周期走，缺了也不致命
                MethodInfo bind = AccessTools.Method(t, "Bind", new[] { typeof(Player) });
                if (bind != null)
                {
                    _harmony.Patch(bind, postfix: new HarmonyMethod(
                        typeof(Patches).GetMethod(nameof(Patches.BindPostfix))));
                    ok++;
                }
                else Log.LogWarning(Tag + " 找不到 Plugin.Bind —— 事件订阅改为按需重绑。");

                MethodInfo clear = AccessTools.Method(t, "Clear");
                if (clear != null)
                {
                    _harmony.Patch(clear, postfix: new HarmonyMethod(
                        typeof(Patches).GetMethod(nameof(Patches.ClearPostfix))));
                    ok++;
                }

                MethodInfo destroy = AccessTools.Method(t, "OnDestroy");
                if (destroy != null)
                {
                    _harmony.Patch(destroy, postfix: new HarmonyMethod(
                        typeof(Patches).GetMethod(nameof(Patches.ReleasePostfix))));
                    ok++;
                }
            }
            catch (Exception ex)
            {
                Log.LogError(Tag + " 打补丁失败: " + ex);
            }
            return ok;
        }

        // ════════════════════════════════════════════════════════════════
        //  状态绑定
        // ════════════════════════════════════════════════════════════════
        private void BindTo(PhysicalBase ph)
        {
            if (ReferenceEquals(ph, _bound)) return;

            Unbind();

            if (ph == null) return;
            _bound = ph;

            // ★★ 事件是边沿触发的，订阅前必须先同步一次当前值，
            //    否则"订阅时已经处于负重状态"会永远收不到通知。
            _encumbered = SafeIsEncumbered(ph);
            _overEncumbered = SafeIsOverEncumbered(ph);

            try
            {
                ph.EncumberedChanged += OnEncumberedChanged;
                ph.OverEncumberedChanged += OnOverEncumberedChanged;

                if (Debug.Value)
                    Log.LogInfo(Tag + " 已绑定玩家负重状态 -> " + Describe(ph) +
                                " (初始 enc=" + _encumbered + ", over=" + _overEncumbered + ")");
            }
            catch (Exception ex)
            {
                Log.LogWarning(Tag + " 订阅负重事件失败: " + ex.Message);
            }
        }

        private void Unbind()
        {
            if (_bound == null) return;
            try
            {
                _bound.EncumberedChanged -= OnEncumberedChanged;
                _bound.OverEncumberedChanged -= OnOverEncumberedChanged;
                if (Debug.Value) Log.LogInfo(Tag + " 已解绑玩家负重状态。");
            }
            catch { }
            _bound = null;
            _encumbered = false;
            _overEncumbered = false;
            _lastBlocked = false;
        }

        private void OnEncumberedChanged(bool value)
        {
            if (_encumbered == value) return;
            _encumbered = value;
            if (Debug.Value)
                Log.LogInfo(Tag + " [事件] 负重(encumbered) -> " + value +
                            (_bound != null ? "   " + Describe(_bound) : ""));
        }

        private void OnOverEncumberedChanged(bool value)
        {
            if (_overEncumbered == value) return;
            _overEncumbered = value;
            if (Debug.Value)
                Log.LogInfo(Tag + " [事件] 过重(overEncumbered) -> " + value +
                            (_bound != null ? "   " + Describe(_bound) : ""));
        }

        // ════════════════════════════════════════════════════════════════
        //  闸门判定
        // ════════════════════════════════════════════════════════════════
        internal bool IsBlocked(Player p)
        {
            if (!Enabled.Value) return false;
            if (Gate.Value == WeightGate.None) return false;
            if (p == null) return false;

            PhysicalBase ph;
            try { ph = p.Physical; } catch { return false; }
            if (ph == null) return false;

            // 游戏自己禁用负重影响时（EncumberDisabled，AI / 特殊状态），不干预
            bool disabled;
            try { disabled = ph.EncumberDisabled; } catch { disabled = false; }
            if (disabled) return false;

            if (!ReferenceEquals(ph, _bound)) BindTo(ph);

            // ── 每帧兜底校验 ──────────────────────────────────────────
            // 直接读一次字段，和事件维护的缓存比对。若不一致，说明事件漏了
            // （或我们刚绑定），以字段为准并纠正缓存 —— 保证闸门永不静默失效。
            bool actualEnc = SafeIsEncumbered(ph);
            bool actualOver = SafeIsOverEncumbered(ph);

            if (actualEnc != _encumbered || actualOver != _overEncumbered)
            {
                if (Debug.Value)
                    Log.LogInfo(Tag + " [兜底] 缓存与实况不一致，已纠正: enc " + _encumbered +
                                "->" + actualEnc + ", over " + _overEncumbered + "->" + actualOver +
                                "   " + Describe(ph));
                _encumbered = actualEnc;
                _overEncumbered = actualOver;
            }

            bool blocked = Gate.Value == WeightGate.OverEncumbered ? _overEncumbered : _encumbered;

            // 只在状态翻转时输出，避免每帧刷屏
            if (blocked != _lastBlocked)
            {
                _lastBlocked = blocked;
                if (Debug.Value)
                {
                    Log.LogInfo(Tag + (blocked ? " 拦截战术冲刺 —— " : " 放行战术冲刺 —— ") +
                                Describe(ph));
                }
            }

            return blocked;
        }

        // ════════════════════════════════════════════════════════════════
        //  安全读取 + 描述
        // ════════════════════════════════════════════════════════════════
        private static bool SafeIsEncumbered(PhysicalBase ph)
        {
            try { return ph.IsEncumbered; } catch { return false; }
        }

        /// <summary>overEncumbered 没有公开 getter，复刻游戏判定：!(Overweight &lt; 1f)。</summary>
        private static bool SafeIsOverEncumbered(PhysicalBase ph)
        {
            try { return !(ph.Overweight < 1f); } catch { return false; }
        }

        /// <summary>给调试日志用：把归一化值反推成大致 kg（仅用于显示）。</summary>
        private static string Describe(PhysicalBase ph)
        {
            try
            {
                float x = ph.BaseOverweightLimits.x;
                float y = ph.BaseOverweightLimits.y;
                float ow = ph.Overweight;
                float kg = x + Mathf.Clamp01(ow) * (y - x);
                return string.Format(
                    "enc={0} over={1} OW={2:F3} (~{3:F1}kg, 本人上限 {4:F0}~{5:F0}kg)",
                    SafeIsEncumbered(ph), SafeIsOverEncumbered(ph), ow, kg, x, y);
            }
            catch { return "<无法读取>"; }
        }

        private void OnDestroy()
        {
            Unbind();
            try { _harmony?.UnpatchSelf(); } catch { }
            if (Instance == this) Instance = null;
        }

        // ════════════════════════════════════════════════════════════════
        //  Harmony 补丁
        // ════════════════════════════════════════════════════════════════
        internal static class Patches
        {
            /// <summary>原 mod 的唯一闸门。返回 false 即否决战术冲刺。</summary>
            public static void EligiblePostfix(Player __0, ref bool __result)
            {
                var self = Instance;
                if (self == null || !__result) return;   // 原本就不合格 → 快速退出
                try
                {
                    if (self.IsBlocked(__0)) __result = false;
                }
                catch (Exception ex)
                {
                    // 绝不把异常抛回游戏逻辑里
                    if (Debug.Value) Log.LogWarning(Tag + " 闸门异常（已忽略）: " + ex.Message);
                }
            }

            public static void BindPostfix(Player __0)
            {
                var self = Instance;
                if (self == null || !Enabled.Value) return;
                try { self.BindTo(__0?.Physical); }
                catch (Exception ex) { if (Debug.Value) Log.LogWarning(Tag + " Bind 异常: " + ex.Message); }
            }

            public static void ClearPostfix()
            {
                try { Instance?.Unbind(); }
                catch (Exception ex) { if (Debug.Value) Log.LogWarning(Tag + " Clear 异常: " + ex.Message); }
            }

            public static void ReleasePostfix()
            {
                try { Instance?.Unbind(); }
                catch { }
            }
        }
    }
}
