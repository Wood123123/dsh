using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Dsh.AirClimb
{
    /// <summary>
    /// 空中攀爬 —— 放开 EFT 攀爬/翻越的「必须站在地上」限制。
    ///
    /// EFT 原版在三处挡掉空中攀爬（顺序即执行顺序）：
    ///   ① VaultingComponent.CanVaulting()
    ///        if (!_vaultingContext.IsGrounded) return false;
    ///   ② VaultingComponent.CanVaulting()
    ///        if (_vaultingContext.MovementDirection.y &lt; 0) return false;   （正在下落）
    ///   ③ VaultingComponent.GetVaultingStrategy()
    ///        if (_vaultingContext.PlayerAnimatorIsJumpSetted()) return None;
    ///
    /// 三处都用 Postfix 改返回值实现，**不修改 VaultingComponent 的任何内部状态**，
    /// 也不改动障碍物扫描 —— 因为 TryVaulting() 会先 Tick() 再 GetVaultingStrategy()，
    /// 判定时障碍数据本来就是刚算好的（已核对 IL）。
    ///
    /// 兼容性：
    ///   · 本补丁打的是 EFT 类型，与 Fika / tarkin-ladders 都不冲突
    ///     （tarkin-ladders 打的是 Physical.CanClimb / GetAvailableActions，不是这里）
    ///   · 所有判定都在 try/catch 内，异常只记一条日志并退回原版行为
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AirClimbPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dsh.airclimb";
        public const string PluginName = "Air Climb";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static AirClimbPlugin Instance;

        // ── 三个门 + 总开关 ─────────────────────────────────────────────────
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> AllowWhileJumping;
        internal static ConfigEntry<bool> AllowWhileFalling;
        internal static ConfigEntry<bool> BypassJumpAnimationGate;
        internal static ConfigEntry<int> LogCooldownMs;
        internal static ConfigEntry<bool> DiagnosticLog;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Air Climb", "Enabled", true,
                "总开关。关掉后完全退回原版行为。");

            AllowWhileJumping = Config.Bind("Air Climb", "Allow While Jumping", true,
                "★ 放开 ①：不在地面（跳起 / 悬空）时也能触发攀爬/翻越。\n" +
                "对应 CanVaulting 里的 !IsGrounded 检查。");

            AllowWhileFalling = Config.Bind("Air Climb", "Allow While Falling", true,
                "★ 放开 ②：正在下落时也能触发。\n" +
                "对应 CanVaulting 里的 MovementDirection.y < 0 检查。");

            BypassJumpAnimationGate = Config.Bind("Air Climb", "Bypass Jump Animation Gate", true,
                "★ 放开 ③：跳跃动画进行中也能触发。\n" +
                "对应 GetVaultingStrategy 里的 PlayerAnimatorIsJumpSetted() 检查。");

            LogCooldownMs = Config.Bind("Air Climb", "Log Cooldown (ms)", 1000,
                "判定日志的最小间隔毫秒数。0 = 每次都记（排障用，会刷屏）。");

            DiagnosticLog = Config.Bind("Air Climb", "Diagnostic Log", true,
                "★ 诊断模式：每次 GetVaultingStrategy 被调用时打印全部判定条件的实际值\n" +
                "（CanVaulting / grounded / moveDirY / sprint / animTransition / jumpSetted /\n" +
                "  障碍高度长度距离 / stairs / vaultCanMove / climbCanMove / 体力）。\n" +
                "定位「放了 IsGrounded 还是爬不了」时打开；确认没问题后关掉以免刷屏。");

            _harmony = new Harmony(PluginGuid);
            TryPatch("MovementState.Vaulting (空中入口)", () => _harmony.PatchAll(typeof(Patches.MovementStateVaultingPatch)));
            TryPatch("CanVaulting", () => _harmony.PatchAll(typeof(Patches.CanVaultingPatch)));
            TryPatch("GetVaultingStrategy", () => _harmony.PatchAll(typeof(Patches.GetVaultingStrategyPatch)));

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded — " +
                        $"jumping={AllowWhileJumping.Value}, falling={AllowWhileFalling.Value}, " +
                        $"jumpAnim={BypassJumpAnimationGate.Value}");
        }

        private void TryPatch(string name, System.Action install)
        {
            try
            {
                install();
                Log.LogInfo($"[AirClimb] {name} patched");
            }
            catch (System.Exception exception)
            {
                Log.LogError($"[AirClimb] failed to patch {name} — that gate stays vanilla: {exception}");
            }
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { /* ignore */ }
        }
    }
}
