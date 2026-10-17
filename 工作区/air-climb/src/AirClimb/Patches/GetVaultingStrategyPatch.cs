using System;
using EFT.Vaulting;
using HarmonyLib;

namespace Dsh.AirClimb.Patches
{
    /// <summary>
    /// 放开 ③ PlayerAnimatorIsJumpSetted()。
    ///
    /// 原版 GetVaultingStrategy（68 指令，已核对 IL）：
    ///   ① if (!CanVaulting())                                        return None;
    ///   ② isStairs = StairsCalculatorModel.IsStairsCondition();
    ///   ③ isSprintNearObstacle = IsSprintEnabled &amp;&amp; BehindObstacleRatio &gt; -0.5
    ///   ④ if (IsAnimatorInTransitionState(0))                        return None;
    ///   ⑤ if (PlayerAnimatorIsJumpSetted())                          return None;   ← 目标
    ///   ⑥ if (_canVaultByStaminaGetter() &amp;&amp; VaultState.CanMove()
    ///         &amp;&amp; !isSprintNearObstacle &amp;&amp; !isStairs)                return Vault;
    ///   ⑦ if (_canClimbByStaminaGetter() &amp;&amp; ClimbState.CanMove()
    ///         &amp;&amp; !IsSprintEnabled &amp;&amp; !isStairs)                      return Climb;
    ///      return None;
    ///
    /// 做法：只在原版返回 None 时介入，**把 ①②③④⑥⑦ 全部重新算一遍**，唯独跳过 ⑤。
    /// 也就是说：跳跃动画这道门被拿掉，其余所有合理性检查（体力、移动可用性、
    /// 疾跑状态、楼梯、动画过渡）一律保留。
    /// </summary>
    [HarmonyPatch(typeof(VaultingComponent), nameof(VaultingComponent.GetVaultingStrategy))]
    internal static class GetVaultingStrategyPatch
    {
        private static float _lastLogTime;

        [HarmonyPostfix]
        private static void Postfix(VaultingComponent __instance, ref EVaultingStrategy __result)
        {
            // 原版已经给出策略 → 不动
            if (__result != EVaultingStrategy.None)
            {
                return;
            }

            if (!AirClimbPlugin.Enabled.Value || !AirClimbPlugin.BypassJumpAnimationGate.Value)
            {
                return;
            }

            try
            {
                if (__instance == null)
                {
                    return;
                }

                // ── ① CanVaulting 仍必须通过（此时它已带上本插件的 ①② 放行）──
                if (!__instance.CanVaulting())
                {
                    return;
                }

                var model = __instance._vaultingModelDebug;
                if (model == null)
                {
                    return;
                }

                var context = __instance._vaultingContext;
                if (context == null)
                {
                    return;
                }

                // ── ② 楼梯 ──────────────────────────────────────────────
                var stairsModel = model.StairsCalculatorModel;
                var isStairs = stairsModel != null && stairsModel.IsStairsCondition();
                if (isStairs)
                {
                    return;
                }

                // ── ④ 动画过渡中（保留原版这道门）───────────────────────
                if (context.IsAnimatorInTransitionState(0))
                {
                    return;
                }

                // ── ③ 疾跑贴墙 ──────────────────────────────────────────
                var isSprintEnabled = context.IsSprintEnabled;
                var isSprintNearObstacle = isSprintEnabled && __instance.BehindObstacleRatio > -0.5f;

                var moves = model.VaultingStatesModel;

                // ── ⑥ Vault ────────────────────────────────────────────
                if (!isSprintNearObstacle
                    && __instance._canVaultByStaminaGetter?.Invoke() == true
                    && moves?.VaultState?.CanMove() == true)
                {
                    __result = EVaultingStrategy.Vault;
                    LogOnce($"[AirClimb] GetVaultingStrategy 放行 Vault (跳跃动画门已跳过, grounded={context.IsGrounded})");
                    return;
                }

                // ── ⑦ Climb ────────────────────────────────────────────
                if (!isSprintEnabled
                    && __instance._canClimbByStaminaGetter?.Invoke() == true
                    && moves?.ClimbState?.CanMove() == true)
                {
                    __result = EVaultingStrategy.Climb;
                    LogOnce($"[AirClimb] GetVaultingStrategy 放行 Climb (跳跃动画门已跳过, grounded={context.IsGrounded})");
                }
            }
            catch (Exception exception)
            {
                AirClimbPlugin.Log.LogWarning($"[AirClimb] GetVaultingStrategy postfix failed, keeping vanilla result: {exception.Message}");
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
