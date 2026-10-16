using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Dsh.ShijiuReconnectFix;

/// <summary>
/// 【拾玖】对局记录 + Fika 重连 冲突修复
///
/// ── 问题 ──
/// 【拾玖】的 Fika 战绩适配用【房间 GUID】作为「行动编号」(actionId)：
///     FikaHistorySession.Begin(sharedRoom, transit)
///         → ActionId = "fika-" + sharedRoom.ToString("N")
///
/// 而 Fika 的「重连」会让你重新加入【同一个房间】⇒ sharedRoom 不变 ⇒ actionId 不变。
/// 但客户端把它当成新的一局又记录了一次 ⇒ 同一个 actionId 出现了两条记录。
///
/// 服务端 (ShiJiu-Tools.Server.dll / HistoryStore) 的规则是
/// 「一个 actionId 只能对应一条记录」，于是第二条被拒：
///     "行动编号已用于另一条记录"
/// mod 随即把这条记录「保留在本地待重试」，无限重试刷屏
/// （实测：618 次重试 + 1255 次 /stashtools/history/join，占日志 27%）。
///
/// ── 修复 ──
/// 在【重连中的那一局】跳过对局记录捕获。因为服务端已经有重连前那条记录
/// （同一个 actionId），所以这一局本来也不该再生成一条。
///
/// 拦截点（反射定位，不依赖编译期引用）：
///   ① StashTools.Fika.FikaHistory::Capture(String)             —— 适配器层（主拦截点）
///   ② StashTools.Fika.FikaHistorySession::Capture(String)      —— 第二道保险
///
/// 判定条件：Fika.Core.Main.Utils.FikaBackendUtils.IsReconnect
/// （实测其只在 JoinMatch 时置位、在 CleanUpVariables 时复位，
///   所以整个重连战局期间都是 true）
/// </summary>
[BepInPlugin(Guid, "Shijiu Reconnect Fix", "1.0.0")]
public sealed class ShijiuReconnectFixPlugin : BaseUnityPlugin
{
    public const string Guid = "com.dsh.shijiureconnectfix";

    internal static ManualLogSource Log;

    /// <summary>命中次数（日志用）</summary>
    internal static int SkipCount;

    private ConfigEntry<bool> _enabled;
    private ConfigEntry<bool> _verbose;

    private static PropertyInfo _isReconnectProp;

    /// <summary>
    /// ★ 拦截点是否都已挂上。
    ///
    /// 为什么需要延迟重试：BepInEx 的插件加载顺序【不保证】。
    /// 实测本插件排在【拾玖】之前，于是 Awake 执行时
    /// ShiJiu-Tools.Fika.dll 还没被加载，TypeByName 自然找不到
    /// StashTools.Fika.FikaHistory —— 补丁静默失败，插件等于没装。
    /// （日志表现：[ShijiuFix] 找不到类型 StashTools.Fika.FikaHistory
    ///            [ShijiuFix] ★ 两个拦截点都没挂上）
    ///
    /// 所以：Awake 先试一次，没成功就每 2 秒重试，直到挂上或超时放弃。
    /// </summary>
    private static bool _hookA;
    private static bool _hookB;
    private static bool _givenUp;
    private float _nextRetry;
    private int _retryCount;

    private const int MaxRetries = 60;      // 60 × 2s = 2 分钟，足够任何加载顺序
    private const float RetryInterval = 2f;

    private void Awake()
    {
        Log = Logger;

        _enabled = Config.Bind("General", "Enabled", true,
            "重连时跳过【拾玖】对局记录（避免 actionId 冲突导致服务端拒绝 + 无限重试）");
        _verbose = Config.Bind("General", "Verbose", true, "每次拦截都打日志（排查用，稳定后可关）");

        TryHookAll();
    }

    /// <summary>尝试装好所有拦截点。可能被 Awake 与 Update 反复调用。</summary>
    private void TryHookAll()
    {
        try
        {
            var harmony = new Harmony(Guid);

            // ── 定位 FikaBackendUtils.IsReconnect（也受加载顺序影响，一并重试）──
            if (_isReconnectProp == null)
            {
                var backendUtils = FindType("FikaBackendUtils");
                if (backendUtils != null)
                {
                    _isReconnectProp = backendUtils.GetProperty("IsReconnect",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (_isReconnectProp != null)
                        Log.LogInfo("[ShijiuFix] 已定位 FikaBackendUtils.IsReconnect");
                }
            }

            // ── ① 主拦截点：StashTools.Fika.FikaHistory::Capture(String) ──
            //    silent=true：重试期间不刷屏（类型还没加载是正常现象）
            if (!_hookA)
                _hookA = Hook(harmony, "StashTools.Fika.FikaHistory", "Capture",
                              nameof(FikaHistory_Capture_Prefix), "FikaHistory.Capture", silent: true);

            // ── ② 第二道保险：StashTools.Fika.FikaHistorySession::Capture(String) ──
            if (!_hookB)
                _hookB = Hook(harmony, "StashTools.Fika.FikaHistorySession", "Capture",
                              nameof(FikaHistorySession_Capture_Prefix), "FikaHistorySession.Capture", silent: true);

            if ((_hookA || _hookB) && !_givenUp)
            {
                Log.LogInfo("[ShijiuFix] ★ 已加载 —— 重连的战局将不再生成重复的对局记录。"
                          + $"（主拦截={(_hookA ? "✓" : "✗")} 保险={(_hookB ? "✓" : "✗")}）");
                _givenUp = true;      // 挂上就够了，不再重试
            }
        }
        catch (Exception e)
        {
            Log.LogError("[ShijiuFix] 初始化异常: " + e);
        }
    }

    /// <summary>
    /// ★ 延迟重试。
    ///
    /// BepInEx 不保证插件加载顺序，而本插件依赖【后加载】的 ShiJiu-Tools.Fika.dll。
    /// 所以这里每 2 秒重试一次，最多 60 次（2 分钟）。
    /// 挂上之后永久停止（_givenUp），零开销。
    /// </summary>
    private void Update()
    {
        if (_givenUp) return;                        // ★ 完成后立即返回
        if ((Time.frameCount & 31) != 0) return;     // 每 32 帧才判断一次时间

        var now = Time.realtimeSinceStartup;
        if (now < _nextRetry) return;
        _nextRetry = now + RetryInterval;

        if (++_retryCount > MaxRetries)
        {
            _givenUp = true;
            Log.LogWarning("[ShijiuFix] ★ 重试 " + MaxRetries + " 次后仍未找到拾玖的 Fika 类型"
                         + " —— 插件无效。（检查 ShiJiu-Tools.Fika.dll 是否已安装）");
            return;
        }

        TryHookAll();

        if (_hookA || _hookB)
        {
            _givenUp = true;
        }
        else if (_retryCount == 1)
        {
            Log.LogInfo("[ShijiuFix] 拾玖的 Fika 类型尚未加载，开始延迟重试（每 2 秒一次）…");
        }
    }

    // ─────────────────────────────────────────────

    private static Type FindType(string simpleName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string an;
            try { an = asm.GetName().Name; } catch { continue; }
            if (an == null) continue;
            if (an.IndexOf("ShiJiu", StringComparison.OrdinalIgnoreCase) < 0 &&
                an.IndexOf("Fika", StringComparison.OrdinalIgnoreCase) < 0) continue;

            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == simpleName) return t;
                }
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// 把前缀挂到目标方法上。
    ///
    /// ★ silent：延迟重试期间为 true，不打印"找不到"的警告。
    ///   否则 60 次重试会刷出上百条重复警告（前几次尝试时类型本来就还没加载）。
    ///   真正的失败会在重试用尽时统一报告一次。
    /// </summary>
    private static bool Hook(Harmony harmony, string typeName, string methodName,
                             string prefixName, string label, bool silent = false)
    {
        try
        {
            var t = FindType(typeName);
            if (t == null)
            {
                if (!silent) Log.LogWarning("[ShijiuFix] 找不到类型 " + typeName);
                return false;
            }

            // Capture(String) —— 取唯一那个带单个 string 参数的
            var m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                 BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .FirstOrDefault(x => x.Name == methodName &&
                                          x.GetParameters().Length == 1 &&
                                          x.GetParameters()[0].ParameterType == typeof(string));

            if (m == null)
            {
                if (!silent) Log.LogWarning($"[ShijiuFix] {typeName} 里找不到 {methodName}(String)");
                return false;
            }

            harmony.Patch(m, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(ShijiuReconnectFixPlugin), prefixName)));

            Log.LogInfo($"[ShijiuFix] 已挂钩 {label}({m.GetParameters()[0].ParameterType.Name})");
            return true;
        }
        catch (Exception e)
        {
            if (!silent) Log.LogWarning($"[ShijiuFix] 挂钩 {label} 失败: {e.Message}");
            return false;
        }
    }

    /// <summary>重连中吗？（FikaBackendUtils.IsReconnect）</summary>
    internal static bool InReconnect()
    {
        try
        {
            if (_isReconnectProp == null) return false;
            var v = _isReconnectProp.GetValue(null, null);
            return v is bool b && b;
        }
        catch { return false; }
    }

    /// <summary>
    /// ★ 主拦截：重连中 → 跳过捕获（返回引用类型的默认值 null）。
    /// 服务端已有重连前那条记录（同 actionId），所以这一局不该再记一条。
    /// </summary>
    public static bool FikaHistory_Capture_Prefix()
    {
        try
        {
            var inst = FindPlugin();
            if (inst == null || !inst._enabled.Value) return true;
            if (!InReconnect()) return true;

            SkipCount++;
            if (inst._verbose.Value)
                Log?.LogWarning($"[ShijiuFix] ★ 重连中 → 跳过 FikaHistory.Capture（第 {SkipCount} 次）"
                              + " —— 避免同一行动编号被重复提交导致服务端拒绝。");
            return false;
        }
        catch { return true; }
    }

    /// <summary>★ 第二道保险：会话层同样跳过。</summary>
    public static bool FikaHistorySession_Capture_Prefix()
    {
        try
        {
            var inst = FindPlugin();
            if (inst == null || !inst._enabled.Value) return true;
            if (!InReconnect()) return true;

            if (inst._verbose.Value)
                Log?.LogWarning("[ShijiuFix] ★ 重连中 → 跳过 FikaHistorySession.Capture");
            return false;
        }
        catch { return true; }
    }

    private static ShijiuReconnectFixPlugin _cached;
    private static ShijiuReconnectFixPlugin FindPlugin()
    {
        if (_cached == null)
        {
            try { _cached = UnityEngine.Object.FindObjectOfType<ShijiuReconnectFixPlugin>(); }
            catch { }
        }
        return _cached;
    }
}
