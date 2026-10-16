using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Dsh.ShijiuFikaFix
{
    /// <summary>
    /// 修【拾玖】战绩记录在 Fika 2.4.5 上全部变成「结果未确认」的问题。
    ///
    /// ── 现象 ──
    ///   面板里所有新对局：
    ///     结局 = 结果未确认       地图 = 空       击杀 = 0
    ///   对局记录 JSON 里：
    ///     Route = []   Events = ["进入 ", "未收到正常结束事件：游戏世界正在销毁"]
    ///
    /// ── 根因（反编译 ShiJiu-Tools.Fika.dll 确认）──
    ///   FikaPluginAdapter.Awake 里有一道【严格相等】的版本门：
    ///       Version version = typeof(FikaEventDispatcher).Assembly.GetName().Version;
    ///       if (version != new Version(2, 4, 4, 0)) { 警告; return; }   // ★ 直接返回
    ///       FikaHistory.Install();
    ///
    ///   用户把 Fika 升到了 2.4.5.0 ⇒ 不等于 2.4.4.0 ⇒ Awake 直接 return
    ///   ⇒ FikaHistory.Install() 从未执行
    ///   ⇒ FikaGameEndedEvent / FikaRaidStartedEvent 从未订阅
    ///   ⇒ RaidEarnings.Finish() 永远不会被调用 ⇒ _finished 恒为 false
    ///   ⇒ GameWorld 销毁时 RaidEarnings.End() 走 if (!_finished) 分支
    ///   ⇒ HistoryCapture.Interrupted(null, "游戏世界正在销毁", false)
    ///   ⇒ 状态写成「结果未确认」
    ///
    /// ── 为什么可以安全放行 2.4.5 ──
    ///   已用 Cecil 逐个核对 Fika.Core 2.4.5.0 里 FikaHistory 依赖的全部成员：
    ///     FikaEventDispatcher.SubscribeEvent/UnsubscribeEvent   ✅ public static
    ///     FikaGameCreatedEvent / GameWorldStartedEvent
    ///     FikaRaidStartedEvent / FikaGameEndedEvent             ✅ 全部存在
    ///     FikaBackendUtils.IsTransit / IsHeadless / IsSpectator / ServerGuid  ✅ 全部存在
    ///     CoopGame / IFikaGame / FikaPlayer                     ✅ 全部存在
    ///   ⇒ 作者只是「尚未核对」，API 并没有变动。
    ///
    /// ── 本插件 ──
    ///   不修改拾玖的 DLL，只是在它因版本门退出后，自己补上：
    ///     · FikaHistory.Install()   —— 订阅事件（内部有 _installed 幂等保护）
    ///     · FikaHistory.Tick()      —— 每帧驱动（原本由 FikaAdapterBehaviour 驱动）
    ///     · FikaHistory.Dispose()   —— 退出时清理
    ///
    ///   因为 ShiJiu-Tools.Fika.dll 是【后加载】的（BepInEx 不保证加载顺序），
    ///   所以全部走反射 + 延迟重试，不引用它的类型。
    /// </summary>
    [BepInPlugin(Guid, "Shijiu Fika Fix", "1.0.0")]
    public sealed class ShijiuFikaFixPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.dsh.shijiufikafix";

        internal static ManualLogSource Log;
        private ConfigEntry<bool> _enabled;
        private static ConfigEntry<bool> _verbose;

        private static Type _historyType;
        private static MethodInfo _install;
        private static MethodInfo _tick;
        private static MethodInfo _dispose;

        private bool _installed;
        private bool _givenUp;
        private float _nextRetry;
        private int _retryCount;

        private const int MaxRetries = 60;        // 60 × 2s = 2 分钟
        private const float RetryInterval = 2f;

        private void Awake()
        {
            Log = Logger;
            _enabled = Config.Bind("General", "Enabled", true,
                "绕过拾玖的 Fika 版本门，强制启用战绩适配（修「结果未确认」）。");
            _verbose = Config.Bind("General", "Verbose", true, "打印每次安装/驱动日志。");

            _nextRetry = Time.realtimeSinceStartup + 1f;   // 稍等一下，让 FikaPluginAdapter 先跑完
        }

        private void Update()
        {
            if (!_enabled.Value || _givenUp) return;

            if (!_installed)
            {
                if ((Time.frameCount & 31) != 0) return;
                if (Time.realtimeSinceStartup < _nextRetry) return;
                _nextRetry = Time.realtimeSinceStartup + RetryInterval;

                if (++_retryCount > MaxRetries)
                {
                    _givenUp = true;
                    Log.LogWarning("[ShijiuFikaFix] ★ 重试 " + MaxRetries + " 次后仍找不到 "
                                 + "StashTools.Fika.FikaHistory —— 插件无效。");
                    return;
                }

                TryInstall();
                return;
            }

            // ★ 已装上 —— 每帧驱动（原本由 FikaAdapterBehaviour 驱动，
            //   但版本门让 Awake 提前 return，那个组件根本没被加上）
            try { _tick?.Invoke(null, null); }
            catch (Exception e)
            {
                _installed = false;
                Log.LogError("[ShijiuFikaFix] FikaHistory.Tick 抛异常，已停用：" + e);
            }
        }

        private void TryInstall()
        {
            try
            {
                if (_historyType == null)
                {
                    ResolveTypes();
                    if (_historyType == null) return;   // 还没加载，下次再试
                }

                // ── 先确认拾玖确实因为版本门退出了 ──
                //    如果拾玖自己已经装上（比如将来适配了 2.4.5），就不要重复装。
                if (AdapterAlreadyInstalled())
                {
                    _givenUp = true;
                    Log.LogInfo("[ShijiuFikaFix] 拾玖的 Fika 适配已自行启用，本插件不需要介入。");
                    return;
                }

                _install.Invoke(null, null);            // 内部有 _installed 幂等保护
                _installed = true;

                Log.LogInfo("[ShijiuFikaFix] ★ 已强制启用拾玖战绩适配（绕过 Fika 2.4.4 版本门）"
                          + " —— 新对局不会再变成「结果未确认」。");
            }
            catch (Exception e)
            {
                Log.LogWarning("[ShijiuFikaFix] 安装失败（会继续重试）："
                             + (e.InnerException?.Message ?? e.Message));
            }
        }

        /// <summary>
        /// 拾玖自己有没有成功启用？
        /// 靠 FikaPluginAdapter 的静态字段 _history 判断（它只在版本门通过后才为 true）。
        /// </summary>
        private static bool AdapterAlreadyInstalled()
        {
            try
            {
                var adapter = AccessTools.TypeByName("StashTools.Fika.FikaPluginAdapter");
                if (adapter == null) return false;

                var f = adapter.GetField("_history",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (f == null) return false;

                return f.GetValue(null) is bool b && b;
            }
            catch { return false; }
        }

        /// <summary>反射定位 FikaHistory 及其三个静态方法。</summary>
        private static void ResolveTypes()
        {
            // 只翻 ShiJiu 的程序集，避免全盘扫描
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an;
                try { an = asm.GetName().Name; } catch { continue; }
                if (an == null || an.IndexOf("ShiJiu", StringComparison.OrdinalIgnoreCase) < 0) continue;

                Type t = null;
                try { t = asm.GetType("StashTools.Fika.FikaHistory", false); } catch { }
                if (t == null)
                {
                    try
                    {
                        foreach (var x in asm.GetTypes())
                        {
                            if (x.Name == "FikaHistory") { t = x; break; }
                        }
                    }
                    catch { }
                }
                if (t == null) continue;

                _install = t.GetMethod("Install", BindingFlags.Public | BindingFlags.Static);
                _tick = t.GetMethod("Tick", BindingFlags.Public | BindingFlags.Static);
                _dispose = t.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Static);

                if (_install == null || _tick == null)
                {
                    Log.LogWarning("[ShijiuFikaFix] 找到 FikaHistory 但缺少 Install/Tick —— 拾玖版本可能变了。");
                    return;
                }

                _historyType = t;
                if (_verbose != null && _verbose.Value)
                    Log.LogInfo("[ShijiuFikaFix] 已定位 " + t.FullName
                              + $"（Install={_install != null} Tick={_tick != null} Dispose={_dispose != null}）");
                return;
            }
        }

        private void OnDestroy()
        {
            if (!_installed) return;
            try
            {
                _dispose?.Invoke(null, null);
                Log.LogInfo("[ShijiuFikaFix] 已清理拾玖战绩适配。");
            }
            catch (Exception e)
            {
                Log.LogWarning("[ShijiuFikaFix] Dispose 失败：" + e.Message);
            }
        }
    }
}
