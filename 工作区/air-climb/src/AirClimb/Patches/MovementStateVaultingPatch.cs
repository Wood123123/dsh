using System;
using EFT;
using HarmonyLib;

namespace Dsh.AirClimb.Patches
{
    /// <summary>
    /// ★ 这是真正的第一道门，也是前两轮补丁打不中的原因。
    ///
    /// 按键链路（逐层反编译核实）：
    ///   按住空格
    ///     → MoveInputTranslator.TranslateCommand()
    ///     → Player.Vaulting()                       [9 指令]
    ///          if (_isVaultingPressed) → DisableVaulting()  else → EnableVaulting()
    ///     → Player.EnableVaulting()                 [14 指令]
    ///          add_UpdateEvent(ToggleVaulting)      ← 按住期间每帧驱动
    ///          _isVaultingPressed = true
    ///     → Player.ToggleVaulting()                 [10 指令]
    ///     → MovementContext.CurrentState.Vaulting()  ← ★ 只问【当前状态】
    ///
    /// MovementState.Vaulting() 是 public virtual 的 **1 指令空实现**。
    /// 只有四个状态重写了它：
    ///   IdlePlayerState / MovePlayerState / SprintPlayerState → MovementContext.TryVaulting()
    ///   IdleWeaponMountingState                               → 自己的逻辑
    /// 而 JumpState / FallDownState / JumpLandingState / VaultingFallDownState
    /// 全都**没有重写** ⇒ 走基类空实现 ⇒ 按空格什么也不发生。
    ///
    /// 本补丁打的就是那个基类空实现。Harmony 只替换基类方法体，
    /// 所以已重写的三个状态**完全不受影响**，只有落到空实现的空中状态会被我们接管。
    ///
    /// 判断"键是按着的"不需要额外检查：ToggleVaulting 只注册在按住期间
    /// （EnableVaulting 加、DisableVaulting 移除），能进到 Vaulting() 就说明按着。
    ///
    /// 保留的原版检查（在 MovementContext.TryVaulting 内部）：
    ///   · VaultingGameplayRestrictions.CanVaulting()   —— 战局级限制
    ///   · IsForwardInputDirection                      —— 必须朝前
    /// 这两道是合理约束，不放开。
    /// </summary>
    [HarmonyPatch(typeof(MovementState), nameof(MovementState.Vaulting))]
    internal static class MovementStateVaultingPatch
    {
        private static float _lastLogTime;

        [HarmonyPrefix]
        private static bool Prefix(MovementState __instance)
        {
            if (!AirClimbPlugin.Enabled.Value || !AirClimbPlugin.AllowWhileJumping.Value)
            {
                return true;                                  // 走原版空实现
            }

            try
            {
                if (__instance == null)
                {
                    return true;
                }

                var context = __instance.MovementContext;
                if (context == null)
                {
                    return true;
                }

                // 在地面：Idle/Move/Sprint 自己有实现，让它们走原版路径
                if (context.IsGrounded)
                {
                    return true;
                }

                // ── 空中，且当前状态没有重写 Vaulting() ⇒ 我们接管 ──────────
                // TryVaulting 内部仍会校验 CanVaulting（含本插件的 ③④ 放行）
                // 与 IsForwardInputDirection，并负责 OnVaulting / RemoveLeftHandItem
                var stateName = __instance.Name.ToString();
                var ok = context.TryVaulting();

                if (AirClimbPlugin.DiagnosticLog.Value)
                {
                    LogOnce($"[AirClimb] ★ 空中接管 state={stateName} → TryVaulting()={ok}");
                }

                return false;                                 // 跳过基类空实现
            }
            catch (Exception exception)
            {
                AirClimbPlugin.Log.LogWarning($"[AirClimb] MovementState.Vaulting prefix failed, falling back: {exception.Message}");
                return true;
            }
        }

        private static void LogOnce(string message)
        {
            var cooldown = AirClimbPlugin.LogCooldownMs.Value;
            if (cooldown <= 0)
            {
                AirClimbPlugin.Log.LogInfo(message);
                return;
            }

            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now - _lastLogTime < cooldown / 1000f)
            {
                return;
            }

            _lastLogTime = now;
            AirClimbPlugin.Log.LogInfo(message);
        }
    }
}
