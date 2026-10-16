using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using EFT;
using HarmonyLib;

namespace HeadlessPatchDiag
{
    /// <summary>
    /// 无头端专用：GameWorld.OnGameStarted 补丁诊断 + 气囊。
    ///
    /// 背景：
    ///   无头端没有本地玩家（GameWorld.MainPlayer == null，唯一写入点是 GameWorld.RegisterPlayer，
    ///   而无头端从不注册本地玩家）。某些 mod 的 OnGameStarted 补丁假定 MainPlayer 非空，
    ///   于是抛 NullReferenceException，导致 "Exception caught during raid init"。
    ///
    /// 本插件做两件事：
    ///   ① 列举 OnGameStarted 上挂着的全部补丁（owner / 方法 / 优先级）
    ///   ② 给"别人的" postfix 套一个 Finalizer —— 谁抛异常就点名，并把异常吞掉，
    ///      让其余补丁继续跑（所以它不只是诊断，本身就是修复）
    ///
    /// ★ 只对无头端生效：非无头端进程直接不挂任何补丁。
    /// </summary>
    [BepInPlugin("com.local.headlesspatchdiag", "Headless Patch Diagnostics", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static Harmony _airbag;
        private const string Tag = "[PatchDiag]";
        private const string OwnOwner = "com.local.headlesspatchdiag";
        private static readonly HashSet<MethodBase> _wrapped = new HashSet<MethodBase>();
        private static bool? _headless;

        private void Awake()
        {
            Log = Logger;

            if (!IsHeadless())
            {
                Log.LogInfo($"{Tag} 非无头端进程 —— 本插件不挂任何补丁（设计如此）");
                return;
            }

            Log.LogWarning($"{Tag} ===== 无头端补丁诊断启动 =====");
            try
            {
                InspectAndProtect();
            }
            catch (Exception e)
            {
                Log.LogError($"{Tag} 初始化异常: {e}");
            }
        }

        private void Start()
        {
            // 再跑一次：有些插件在 Awake 之后才 PatchAll
            if (!IsHeadless()) return;
            try { InspectAndProtect(); } catch (Exception e) { Log.LogError($"{Tag} 二次扫描异常: {e.Message}"); }
        }

        private void OnDestroy()
        {
            try { _airbag?.UnpatchSelf(); } catch { }
        }

        // ─────────────────────────────────────────────
        private static bool IsHeadless()
        {
            if (_headless.HasValue) return _headless.Value;
            try
            {
                bool h = AppDomain.CurrentDomain.GetAssemblies().Any(a =>
                {
                    try { return (a.GetName().Name ?? "").IndexOf("Fika.Headless", StringComparison.OrdinalIgnoreCase) >= 0; }
                    catch { return false; }
                });
                _headless = h;
            }
            catch { _headless = false; }
            return _headless.Value;
        }

        private static void InspectAndProtect()
        {
            MethodInfo target = AccessTools.Method(typeof(GameWorld), "OnGameStarted");
            if (target == null)
            {
                Log.LogError($"{Tag} 找不到 GameWorld.OnGameStarted");
                return;
            }

            Patches info = Harmony.GetPatchInfo(target);
            if (info == null)
            {
                Log.LogWarning($"{Tag} OnGameStarted 上没有任何补丁");
                return;
            }

            Log.LogWarning($"{Tag} ── GameWorld.OnGameStarted 补丁清单 ──");
            Dump("Prefix", info.Prefixes);
            Dump("Postfix", info.Postfixes);
            Dump("Transpiler", info.Transpilers);
            Dump("Finalizer", info.Finalizers);

            // 把所有"别人的 postfix"套上气囊
            _airbag = _airbag ?? new Harmony("com.local.headlesspatchdiag.airbag");
            HarmonyMethod fin = new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(AirbagFinalizer)));

            int wrapped = 0;
            foreach (Patch p in info.Postfixes)
            {
                if (p?.PatchMethod == null) continue;
                string owner = p.owner ?? "";
                if (owner.StartsWith(OwnOwner, StringComparison.OrdinalIgnoreCase)) continue;
                if (!_wrapped.Add(p.PatchMethod)) continue;
                try
                {
                    _airbag.Patch(p.PatchMethod, finalizer: fin);
                    wrapped++;
                    Log.LogWarning($"{Tag} 气囊已套: {p.PatchMethod.DeclaringType?.FullName}::{p.PatchMethod.Name}  (owner={owner})");
                }
                catch (Exception e)
                {
                    Log.LogError($"{Tag} 套气囊失败 {p.PatchMethod.Name}: {e.Message}");
                }
            }
            Log.LogWarning($"{Tag} 本次新套气囊 {wrapped} 个，累计 {_wrapped.Count} 个");
        }

        private static void Dump(string kind, System.Collections.Generic.IEnumerable<Patch> list)
        {
            if (list == null) return;
            int n = 0;
            foreach (Patch p in list)
            {
                n++;
                Log.LogWarning($"{Tag}   {kind,-10} owner={p.owner}  prio={p.priority}  →  {p.PatchMethod?.DeclaringType?.FullName}::{p.PatchMethod?.Name}");
            }
            if (n == 0) Log.LogWarning($"{Tag}   {kind,-10} （无）");
        }

        /// <summary>
        /// 气囊：patch 方法自己抛异常时点名 + 吞掉，让链条继续。
        /// __originalMethod 就是被套的那个补丁方法本身。
        /// </summary>
        private static Exception AirbagFinalizer(Exception __exception, MethodBase __originalMethod)
        {
            if (__exception == null) return null;
            try
            {
                Log.LogError($"{Tag} ★★★ 补丁抛异常: {__originalMethod?.DeclaringType?.FullName}::{__originalMethod?.Name}" +
                             $"  异常={__exception.GetType().Name}: {__exception.Message}");
                Log.LogError($"{Tag}     已吞掉该异常，其余补丁继续执行");
            }
            catch { }
            return null;
        }
    }
}
