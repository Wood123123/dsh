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
    /// 做法：只在原版返回 None 时介入，把除 ⑤ 以外的所有条件重新算一遍。
    /// 另加诊断日志（DiagnosticLog=true 时），把每个条件的实际值都打出来，
    /// 便于定位到底哪一关在拦 —— 这是排查「放了 IsGrounded 还是爬不了」的唯一可靠办法。
    /// </summary>
    [HarmonyPatch(typeof(VaultingComponent), nameof(VaultingComponent.GetVaultingStrategy))]
    internal static class GetVaultingStrategyPatch
    {
        private static float _lastLogTime;
        private static float _lastDiagTime;

        [HarmonyPostfix]
        private static void Postfix(VaultingComponent __instance, ref EVaultingStrategy __result)
        {
            if (__instance == null)
            {
                return;
            }

            var enabled = AirClimbPlugin.Enabled.Value;
            var diag = AirClimbPlugin.DiagnosticLog.Value;

            // ── 诊断：无论原版返回什么，只要开了诊断就记录一次 ─────────────
            if (diag && ShouldLog(ref _lastDiagTime))
            {
                LogDiagnostics(__instance, __result, "原版结果");
            }

            if (__result != EVaultingStrategy.None)
            {
                return;                          // 原版已给出策略
            }

            if (!enabled || !AirClimbPlugin.BypassJumpAnimationGate.Value)
            {
                return;
            }

            try
            {
                // ── ① CanVaulting 仍必须通过（已带上本插件的 ①② 放行）─────
                if (!__instance.CanVaulting())
                {
                    Diag(diag, "   → 卡在 ① CanVaulting() = false");
                    return;
                }

                var model = __instance._vaultingModelDebug;
                var context = __instance._vaultingContext;
                if (model == null || context == null)
                {
                    return;
                }

                // ── ② 楼梯 ──────────────────────────────────────────────
                var isStairs = model.StairsCalculatorModel != null
                               && model.StairsCalculatorModel.IsStairsCondition();
                if (isStairs)
                {
                    Diag(diag, "   → 卡在 ② IsStairsCondition = true");
                    return;
                }

                // ── ④ 动画过渡中（保留原版这道门）───────────────────────
                if (context.IsAnimatorInTransitionState(0))
                {
                    Diag(diag, "   → 卡在 ④ IsAnimatorInTransitionState(0) = true");
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
                    LogOnce($"[AirClimb] ★ GetVaultingStrategy 放行 Vault (grounded={context.IsGrounded})");
                    return;
                }

                // ── ⑦ Climb ────────────────────────────────────────────
                if (!isSprintEnabled
                    && __instance._canClimbByStaminaGetter?.Invoke() == true
                    && moves?.ClimbState?.CanMove() == true)
                {
                    __result = EVaultingStrategy.Climb;
                    LogOnce($"[AirClimb] ★ GetVaultingStrategy 放行 Climb (grounded={context.IsGrounded})");
                    return;
                }

                // ── 都没过：分别点名 ────────────────────────────────────
                if (diag)
                {
                    var staminaV = __instance._canVaultByStaminaGetter?.Invoke() == true;
                    var staminaC = __instance._canClimbByStaminaGetter?.Invoke() == true;
                    var moveV = moves?.VaultState?.CanMove() == true;
                    var moveC = moves?.ClimbState?.CanMove() == true;
                    Diag(diag, $"   → ⑥/⑦ 都不满足: staminaV={staminaV} moveV={moveV} (sprintNear={isSprintNearObstacle}) | " +
                               $"staminaC={staminaC} moveC={moveC} (sprint={isSprintEnabled})");
                }
            }
            catch (Exception exception)
            {
                AirClimbPlugin.Log.LogWarning($"[AirClimb] GetVaultingStrategy postfix failed: {exception.Message}");
            }
        }

        // ── 诊断输出 ────────────────────────────────────────────────────────
        private static void LogDiagnostics(VaultingComponent c, EVaultingStrategy original, string tag)
        {
            try
            {
                var ctx = c._vaultingContext;
                var model = c._vaultingModelDebug;
                var obs = model?.ObstacleCalculatorModel;
                var moves = model?.VaultingStatesModel;
                var stairsModel = model?.StairsCalculatorModel;

                var sb = new System.Text.StringBuilder();
                sb.Append("[AirClimb][DIAG] ").Append(tag).Append(" = ").Append(original).Append(" | ");
                sb.Append("CanVaulting=").Append(Safe(() => c.CanVaulting())).Append(' ');
                sb.Append("IsVaulting=").Append(Safe(() => c.IsVaulting())).Append(' ');
                sb.Append("IsActive=").Append(Safe(() => c._vaultingSettings?.IsActive == true)).Append(' ');
                if (ctx != null)
                {
                    sb.Append("grounded=").Append(Safe(() => ctx.IsGrounded)).Append(' ');
                    sb.Append("moveDirY=").Append(SafeF(() => ctx.MovementDirection.y)).Append(' ');
                    sb.Append("sprint=").Append(Safe(() => ctx.IsSprintEnabled)).Append(' ');
                    sb.Append("animTransition=").Append(Safe(() => ctx.IsAnimatorInTransitionState(0))).Append(' ');
                    sb.Append("jumpSetted=").Append(Safe(() => ctx.PlayerAnimatorIsJumpSetted())).Append(' ');
                }
                if (obs != null)
                {
                    sb.Append("| obs: terrain=").Append(Safe(() => obs.IsTerrain)).Append(' ');
                    sb.Append("h=").Append(SafeF(() => obs.MainObstacleHeight)).Append(' ');
                    sb.Append("l=").Append(SafeF(() => obs.MainObstacleLength)).Append(' ');
                    sb.Append("d=").Append(SafeF(() => obs.DistanceToMainObstacle)).Append(' ');
                }
                if (stairsModel != null)
                {
                    sb.Append("| stairs=").Append(Safe(() => stairsModel.IsStairsCondition())).Append(' ');
                }
                if (moves != null)
                {
                    sb.Append("| vaultCanMove=").Append(Safe(() => moves.VaultState?.CanMove() == true)).Append(' ');
                    sb.Append("climbCanMove=").Append(Safe(() => moves.ClimbState?.CanMove() == true)).Append(' ');
                }
                sb.Append("| staminaV=").Append(Safe(() => c._canVaultByStaminaGetter?.Invoke() == true)).Append(' ');
                sb.Append("staminaC=").Append(Safe(() => c._canClimbByStaminaGetter?.Invoke() == true));

                AirClimbPlugin.Log.LogInfo(sb.ToString());
            }
            catch (Exception exception)
            {
                AirClimbPlugin.Log.LogWarning($"[AirClimb][DIAG] failed: {exception.Message}");
            }
        }

        private static string Safe(Func<bool> f)
        {
            try { return f() ? "T" : "F"; } catch { return "?"; }
        }

        private static string SafeF(Func<float> f)
        {
            try { return f().ToString("F2"); } catch { return "?"; }
        }

        private static void Diag(bool on, string message)
        {
            if (on) { AirClimbPlugin.Log.LogInfo("[AirClimb][DIAG] " + message); }
        }

        private static bool ShouldLog(ref float lastTime)
        {
            var cooldown = AirClimbPlugin.LogCooldownMs.Value;
            if (cooldown <= 0) { return true; }
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now - lastTime < cooldown / 1000f) { return false; }
            lastTime = now;
            return true;
        }

        private static void LogOnce(string message)
        {
            if (!ShouldLog(ref _lastLogTime)) { return; }
            AirClimbPlugin.Log.LogInfo(message);
        }
    }
}
