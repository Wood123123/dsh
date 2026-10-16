using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace TacticalSprintSpeedFix
{
    /// <summary>
    /// 验证版：直接放大【真正】的冲刺速度杠杆，绕开 FromHell.TacticalSprint 的无效补丁。
    ///
    /// 背景（反编译确认）：
    ///   原 mod 改的是 MovementContext::SprintAcceleration 里的 local1，它最终只流向
    ///       set_SprintSpeed → PlayerAnimator.SetCharacterSprintSpeed → Animator.SetFloat(SPRINT_SPEED)
    ///   而 _sprintSpeed 全程序集只有 SprintAcceleration 自己读（+ 一个调试快照），
    ///   没有任何移动代码消费它 —— 所以它只让【腿部动画】变快，位移几乎不变。
    ///
    ///   真正管位移的链：
    ///       SpeedLimiter.GetSpeed(EPlayerState, 力量等级, walkSpeedLimit)
    ///         → SpeedLimiter.Speed
    ///         → OnSpeedChanged 事件
    ///         → MovementContext.OnSpeedLimiterSpeedChanged
    ///         → _player.CharacterController.SpeedLimit = speed      ← 物理层生效
    ///
    /// 本插件 = 在这一条链的源头做 Postfix，只对【本地玩家】的 Sprint 状态生效。
    /// </summary>
    [BepInPlugin(Guid, "Tactical Sprint - 速度修正 (验证版)", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.local.tacticalsprintspeed";
        private const string Tag = "[SpeedFix]";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> SprintMultiplier;
        internal static ConfigEntry<bool> LocalPlayerOnly;
        internal static ConfigEntry<bool> Debug;

        private Harmony _harmony;

        /// <summary>SpeedLimiter 实例 → 它所属的 MovementContext（用于判定是不是本地玩家）</summary>
        private static readonly ConditionalWeakTable<SpeedLimiter, MovementContext> Owner =
            new ConditionalWeakTable<SpeedLimiter, MovementContext>();

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "总开关。");
            SprintMultiplier = Config.Bind("Movement", "SprintMultiplier", 2.0f,
                new ConfigDescription(
                    "冲刺速度倍率。直接放大 SpeedLimiter 算出的 CharacterController.SpeedLimit。",
                    new AcceptableValueRange<float>(1f, 10f)));
            LocalPlayerOnly = Config.Bind("General", "LocalPlayerOnly", true,
                "只对本地玩家的冲刺生效（推荐）。关闭会连 AI / 观察模型一起加速。");
            Debug = Config.Bind("General", "Debug", false,
                "输出每次 Sprint 限速的变化（仅在数值真的变了时输出，不刷屏）。");

            _harmony = new Harmony(Guid);
            int ok = 0;
            try
            {
                _harmony.Patch(
                    AccessTools.Method(typeof(SpeedLimiter), "GetSpeed"),
                    postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.GetSpeedPostfix))));
                ok++;

                var create = AccessTools.Method(typeof(MovementContext), "CreateSpeedLimiter");
                if (create != null)
                {
                    _harmony.Patch(create,
                        postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.CreateSpeedLimiterPostfix))));
                    ok++;
                }
                else Log.LogWarning(Tag + " 找不到 CreateSpeedLimiter —— 将退化为不加门控。");
            }
            catch (Exception ex)
            {
                try { _harmony.UnpatchSelf(); } catch { }
                Log.LogError(Tag + " 打补丁失败: " + ex);
                return;
            }

            Log.LogInfo(Tag + " v0.1.0 已加载。patches=" + ok + "/2, Enabled=" + Enabled.Value +
                        ", SprintMultiplier=" + SprintMultiplier.Value + "x, LocalPlayerOnly=" + LocalPlayerOnly.Value);
            Log.LogInfo(Tag + " 说明：这条链改的是 CharacterController.SpeedLimit —— 位移会真的变快；" +
                              "原 mod 只改动画参数。");
        }

        internal static bool IsLocalContext(SpeedLimiter limiter)
        {
            if (!LocalPlayerOnly.Value) return true;

            MovementContext ctx;
            if (!Owner.TryGetValue(limiter, out ctx) || ctx == null) return false;

            try
            {
                var gw = Singleton<GameWorld>.Instance;
                var main = gw != null ? gw.MainPlayer : null;
                if (main == null) return false;
                return ReferenceEquals(main.MovementContext, ctx);
            }
            catch { return false; }
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
            if (Instance == this) Instance = null;
        }

        internal static class Patches
        {
            /// <summary>SpeedLimiter 创建时登记 它 → 所属 MovementContext 的映射。</summary>
            public static void CreateSpeedLimiterPostfix(MovementContext __instance)
            {
                try
                {
                    var f = AccessTools.Field(typeof(MovementContext), "_speedLimiter");
                    if (f == null) return;
                    var lim = f.GetValue(__instance) as SpeedLimiter;
                    if (lim == null) return;

                    // ConditionalWeakTable 没有 AddOrUpdate，先 Remove 再 Add
                    MovementContext old;
                    if (Owner.TryGetValue(lim, out old)) Owner.Remove(lim);
                    Owner.Add(lim, __instance);
                }
                catch { }
            }

            /// <summary>
            /// ★ 核心：只放大 EPlayerState.Sprint 的返回值。
            /// 这个值会一路流到 _player.CharacterController.SpeedLimit。
            /// </summary>
            public static void GetSpeedPostfix(SpeedLimiter __instance, EPlayerState state, ref float __result)
            {
                try
                {
                    if (!Enabled.Value) return;
                    if (state != EPlayerState.Sprint) return;
                    if (!IsLocalContext(__instance)) return;

                    float before = __result;
                    __result = before * SprintMultiplier.Value;

                    if (Debug.Value && !Mathf.Approximately(before, __result))
                        Log.LogInfo(Tag + " Sprint 限速 " + before.ToString("F3") +
                                    " -> " + __result.ToString("F3"));
                }
                catch { }
            }
        }
    }
}
