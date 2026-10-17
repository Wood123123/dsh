using System;
using EFT.Vaulting;
using HarmonyLib;

namespace Dsh.AirClimb.Patches
{
    /// <summary>
    /// 放开 ① !IsGrounded 和 ② MovementDirection.y &lt; 0。
    ///
    /// 原版 CanVaulting（34 指令，已核对 IL）：
    ///   ① if (!_vaultingSettings.IsActive)                                return false;
    ///   ② if (IsVaulting())                                               return false;
    ///   ③ if (_vaultingContext.MovementDirection.y >= 0 / NaN) 继续，否则 return false;
    ///   ④ if (!_vaultingContext.IsGrounded)                               return false;
    ///   ⑤ if (_vaultingModelDebug.ObstacleCalculatorModel.IsTerrain)      return false;
    ///      return true;
    ///
    /// 做法：只在原版返回 false 时介入，且**先把 ①②⑤ 重新验证一遍**。
    /// 若 ①②⑤ 都满足，那么原版为 false 的原因只能是 ③ 或 ④ —— 这时才按配置放行。
    /// 这样即使 EFT 以后改了判定顺序，本补丁也不会把「功能被关掉」「已在动作中」
    /// 「障碍是地形」这三种真实原因误判成可放行。
    /// </summary>
    [HarmonyPatch(typeof(VaultingComponent), nameof(VaultingComponent.CanVaulting))]
    internal static class CanVaultingPatch
    {
        private static float _lastLogTime;

        [HarmonyPostfix]
        private static void Postfix(VaultingComponent __instance, ref bool __result)
        {
            // 原版已经允许 → 不动
            if (__result)
            {
                return;
            }

            if (!AirClimbPlugin.Enabled.Value)
            {
                return;
            }

            try
            {
                if (__instance == null)
                {
                    return;
                }

                // ── 重新验证原版的 ①②⑤ ──────────────────────────────────
                var settings = __instance._vaultingSettings;
                if (settings == null || !settings.IsActive)
                {
                    return;                      // ① 功能被关（例如战局限制）
                }

                if (__instance.IsVaulting())
                {
                    return;                      // ② 已经在攀爬/翻越中
                }

                var model = __instance._vaultingModelDebug;
                if (model == null)
                {
                    return;
                }

                var obstacle = model.ObstacleCalculatorModel;
                if (obstacle == null || obstacle.IsTerrain)
                {
                    return;                      // ⑤ 障碍是地形（原版就不许爬）
                }

                var context = __instance._vaultingContext;
                if (context == null)
                {
                    return;
                }

                // ── 到这里，原版为 false 的原因只可能是 ③ 或 ④ ────────────
                var grounded = context.IsGrounded;
                var movingDown = context.MovementDirection.y < 0f;

                if (!grounded && !AirClimbPlugin.AllowWhileJumping.Value)
                {
                    return;                      // ④ 没放开
                }

                if (movingDown && !AirClimbPlugin.AllowWhileFalling.Value)
                {
                    return;                      // ③ 没放开
                }

                // 两个原因都不成立却仍是 false → 说明还有我们不知道的条件，别乱改
                if (grounded && !movingDown)
                {
                    return;
                }

                __result = true;
                LogOnce($"[AirClimb] CanVaulting 放行 (grounded={grounded}, movingDown={movingDown})");
            }
            catch (Exception exception)
            {
                AirClimbPlugin.Log.LogWarning($"[AirClimb] CanVaulting postfix failed, keeping vanilla result: {exception.Message}");
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
