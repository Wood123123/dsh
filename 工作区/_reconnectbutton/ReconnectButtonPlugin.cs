using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Dsh.ReconnectButton;

/// <summary>
/// 在战局内的 ESC 菜单里，给"断开连接"旁边加一个"重连"按钮。
///
/// 原理（全部基于实测的 EFT/Fika 源码结构）：
///   1) EFT.UI.MenuScreen 里有 `_disconnectButton : DefaultUIButton`，战局内由
///      ChangeScreenInGameStatus(true) 显示出来（ShowInRaid() 调它）。
///      ⇒ 我们【克隆这个按钮对象】，所以 UI 资源/字体/样式与"断开连接"100% 一致。
///
///   2) 点"重连"时：
///        a. 打开 KeepReconnect 标志
///        b. 调用原"断开连接"按钮的 OnClick → 走 EFT 原生退出战局流程（本地清理、回大厅）
///        c. ★ 我们的 RaidLeave 补丁看到标志 → 跳过 FikaRequestHandler.RaidLeave
///           ⇒ 服务端【不会】RemovePlayerFromMatch
///           ⇒ 匹配界面判定 localPlayerInRaid = true → 出现"重新加入"
///        d. 点"重新加入" → Fika 走 IsReconnect 流程 → GetReconnectProfile → 全量重建
///
/// 为什么必须是客户端插件：
///   FikaRequestHandler.RaidLeave 在客户端执行（它发 HTTP 给服务端），
///   ESC 菜单也是客户端的。服务端不需要任何改动。
/// </summary>
[BepInPlugin(Guid, "Reconnect Button", "1.0.0")]
public sealed class ReconnectButtonPlugin : BaseUnityPlugin
{
    public const string Guid = "com.dsh.reconnectbutton";

    internal static ManualLogSource Log;

    /// <summary>★ 开着它时，下一次"离开战局"不会通知服务端（从而保住重连资格）。</summary>
    internal static bool KeepReconnect;

    /// <summary>自动复位：进新局后（或超时）清掉标志，避免影响下一次正常撤离。</summary>
    private float _armedAt = -1f;

    private ConfigEntry<string> _buttonText;
    private ConfigEntry<float> _offsetY;
    private ConfigEntry<float> _autoResetSeconds;

    private static FieldInfo _fDisconnect;
    private static Type _menuScreenType;

    private void Awake()
    {
        Log = Logger;

        _buttonText = Config.Bind("UI", "ButtonText", "重连", "新按钮上显示的文字");
        _offsetY = Config.Bind("UI", "OffsetY", -62f,
            new ConfigDescription("新按钮相对\"断开连接\"的垂直偏移（像素）。正值向上。", new AcceptableValueRange<float>(-400f, 400f)));
        _autoResetSeconds = Config.Bind("Behavior", "KeepReconnectAutoResetSeconds", 180f,
            new ConfigDescription("打开\"保留重连\"后多久自动复位（防止忘记，影响下次正常撤离）。0 = 不自动复位。",
                new AcceptableValueRange<float>(0f, 3600f)));

        try
        {
            var harmony = new Harmony(Guid);

            // ① 定位 EFT.UI.MenuScreen
            _menuScreenType = typeof(MenuScreen);
            _fDisconnect = AccessTools.Field(_menuScreenType, "_disconnectButton");
            if (_fDisconnect == null)
            {
                Log.LogError("[ReconnectButton] 找不到 MenuScreen._disconnectButton —— EFT 版本可能变了，插件不生效。");
                return;
            }

            // ② 挂钩：战局内菜单显示时注入按钮
            var showInRaid = AccessTools.Method(_menuScreenType, "ShowInRaid");
            if (showInRaid != null)
            {
                harmony.Patch(showInRaid, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(Patches), nameof(Patches.ShowInRaid_Postfix))));
                Log.LogInfo("[ReconnectButton] 已挂钩 MenuScreen.ShowInRaid");
            }
            else Log.LogWarning("[ReconnectButton] 找不到 MenuScreen.ShowInRaid");

            // 兜底：ChangeScreenInGameStatus(minimized) 也会被调用
            var changeStatus = AccessTools.Method(_menuScreenType, "ChangeScreenInGameStatus");
            if (changeStatus != null)
            {
                harmony.Patch(changeStatus, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(Patches), nameof(Patches.ChangeScreen_Postfix))));
                Log.LogInfo("[ReconnectButton] 已挂钩 MenuScreen.ChangeScreenInGameStatus");
            }

            // ③ 挂钩 Fika 的 RaidLeave（按标志跳过）
            bool fikaHooked = Patches.HookFikaRaidLeave(harmony);
            if (!fikaHooked)
            {
                Log.LogWarning("[ReconnectButton] ★ 没能挂钩 FikaRequestHandler.RaidLeave —— "
                             + "按钮会显示但\"保留重连\"无效（会正常通知服务端）。");
            }

            // ④ ★★★ 挂钩 SPT 的 RequestHandler.PutJson —— 拦 /raid/profile/scavsave
            //    这是【真正导致物品丢失】的请求：
            //    SendPlayerScavProfileToServerAfterRaidPatch 在结算界面显示时发出它，
            //    服务端据此按 ExitStatus.Left 结算 → 装备/战利品全没了。
            bool scavHooked = Patches.HookScavSave(harmony);
            if (!scavHooked)
            {
                Log.LogWarning("[ReconnectButton] ★ 没能挂钩 scavsave 请求 —— "
                             + "Scav 用\"重连\"会【丢失物品】！");
            }

            Log.LogInfo("[ReconnectButton] 已加载。ESC 菜单里会出现\"" + _buttonText.Value + "\"按钮。");
        }
        catch (Exception e)
        {
            Log.LogError("[ReconnectButton] 初始化异常: " + e);
        }
    }

    private void Update()
    {
        // ★ 回大厅后延迟复位（主要机制）
        TickReturnReset();

        // ★★★ 同步克隆按钮的显示状态（放在这里，因为插件对象常驻激活）
        SyncClone();

        // ★ 超时兜底（防止 ChangeScreen 那条路没被调用）
        if (!KeepReconnect || _armedAt < 0f) return;
        var reset = _autoResetSeconds.Value;
        if (reset > 0f && Time.realtimeSinceStartup - _armedAt > reset)
        {
            KeepReconnect = false;
            _armedAt = -1f;
            Log.LogWarning($"[ReconnectButton] \"保留重连\"已自动复位（超过 {reset:F0} 秒兜底超时）。");
        }
    }

    /// <summary>
    /// ★★★ 关键修复：克隆按钮的显隐同步必须由一个【常驻激活】的对象来做。
    ///
    /// 之前我把同步逻辑放在克隆体自己身上（FollowActive.Update），但它一旦被
    /// SetActive(false)（回大厅时），Unity 就停止调用它的 Update —— 于是它再也
    /// 无法把自己恢复显示，而 Inject() 又因为 Transform.Find 能找到（未激活也算）
    /// 而直接 return，导致按钮永久消失。
    ///
    /// 现在改由插件（挂在 BepInEx 常驻对象上，永远激活）来同步。
    /// </summary>
    private void SyncClone()
    {
        try
        {
            var clone = Patches.CloneButton;
            var target = Patches.CloneTarget;
            if (clone == null || target == null) return;

            bool want = target.activeSelf;
            if (clone.gameObject.activeSelf != want)
            {
                clone.gameObject.SetActive(want);

                // ★★★ 连带修复：从隐藏变回显示时，把交互性也恢复。
                //     点击"重连"时我们把按钮设成了 Interactable=false（防连点），
                //     如果不在这里恢复，重连回来后再进战局按钮就是灰的、点不动。
                if (want)
                {
                    try { clone.Interactable = true; } catch { }
                }
            }
        }
        catch { }
    }

    internal static void Arm(ManualLogSource log)
    {
        KeepReconnect = true;
        log?.LogWarning("[ReconnectButton] ★ 已开启\"保留重连\"：这次离开战局【不会】通知服务端，"
                      + "回大厅后匹配界面应出现\"重新加入\"。");
        var inst = UnityEngine.Object.FindObjectOfType<ReconnectButtonPlugin>();
        if (inst != null) inst._armedAt = Time.realtimeSinceStartup;
    }

    internal static void Disarm(ManualLogSource log)
    {
        if (!KeepReconnect) return;
        KeepReconnect = false;
        Log?.LogWarning("[ReconnectButton] \"保留重连\"已复位。");
    }

    /// <summary>
    /// ★ 回到大厅时复位。延迟 3 秒，给"退出过程中还在飞的结算请求"留出拦截窗口，
    /// 避免刚点重连、请求还没发完就复位了。
    /// </summary>
    internal static void DisarmAfterReturn()
    {
        if (!KeepReconnect) return;
        var inst = UnityEngine.Object.FindObjectOfType<ReconnectButtonPlugin>();
        if (inst != null) inst._returnedAt = Time.realtimeSinceStartup;
    }

    private float _returnedAt = -1f;
    private const float ReturnResetDelay = 3f;

    /// <summary>回大厅后延迟复位（在 Update 里执行）。</summary>
    private void TickReturnReset()
    {
        if (!KeepReconnect || _returnedAt < 0f) return;
        if (Time.realtimeSinceStartup - _returnedAt < ReturnResetDelay) return;
        KeepReconnect = false;
        _returnedAt = -1f;
        _armedAt = -1f;
        Log?.LogWarning("[ReconnectButton] 已回到大厅 —— \"保留重连\"自动复位（之后正常撤离不受影响）。");
    }

    internal static FieldInfo DisconnectField => _fDisconnect;
    internal static Type MenuScreenType => _menuScreenType;
}

/// <summary>
/// ★ 让克隆出来的"重连"按钮【跟随】原"断开连接"按钮的显示/隐藏状态。
///
/// 为什么需要它：MenuScreen.ChangeScreenInGameStatus() 只管理它自己那几个字段
/// （_disconnectButton 等），不认识我们克隆出来的对象。
/// 结果就是：回主菜单后"断开连接"被隐藏了，而我们克隆的按钮还留在界面上。
///
/// ★ 性能：不每帧同步 —— 每 0.2 秒检查一次（5 次/秒）。
///   activeSelf 是一次 native 属性读取（几十纳秒），但没必要每帧做；
///   而这个同步本来就只需要"人眼看不出延迟"就够了。
/// </summary>
internal sealed class FollowActive : MonoBehaviour
{
    public GameObject Target;

    private float _next;

    private void Update()
    {
        try
        {
            if (Target == null) return;

            // ★ 只在需要时同步 + 限频到 5 次/秒
            bool want = Target.activeSelf;
            if (gameObject.activeSelf != want)
            {
                gameObject.SetActive(want);
                return;
            }

            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;

            // 低频兜底：万一别的路径动了显示状态，这里也能跟上
            if (gameObject.activeSelf != want) gameObject.SetActive(want);
        }
        catch { }
    }
}

internal static class Patches
{    // ─────────────────────────────────────────────
    // ① ESC 菜单按钮注入
    // ─────────────────────────────────────────────
    public static void ShowInRaid_Postfix(MenuScreen __instance)
    {
        Inject(__instance);
    }

    public static void ChangeScreen_Postfix(MenuScreen __instance, bool minimized)
    {
        if (minimized)
        {
            Inject(__instance);   // 战局内（minimized=true）才显示"断开连接"，此时注入
        }
        else
        {
            // ★★★ 回到主菜单 → 立刻复位"保留重连"标志。
            //     理由：结算请求是在【退出战局的过程中】发出的；一旦回到大厅，那些请求已经发完
            //     （被拦了），之后再不会有结算请求 —— 除非你又打了一局并结束。
            //     所以标志只需要在"退出那一瞬间"有效。一回大厅就复位，风险最小。
            ReconnectButtonPlugin.DisarmAfterReturn();
        }
    }

    private const string CloneName = "DSH_ReconnectButton";

    /// <summary>克隆出来的按钮（由插件的 Update 同步显隐）。</summary>
    internal static DefaultUIButton CloneButton;

    /// <summary>克隆按钮要跟随的目标（原"断开连接"按钮）。</summary>
    internal static GameObject CloneTarget;

    private static void Inject(MenuScreen screen)
    {
        try
        {
            var f = ReconnectButtonPlugin.DisconnectField;
            if (f == null || screen == null) return;

            var disconnect = f.GetValue(screen) as DefaultUIButton;
            if (disconnect == null) return;

            var parent = disconnect.transform.parent;
            if (parent == null) return;

            // ★★★ 关键：Transform.Find 能找到【未激活】的子对象。
            //     所以这里不能直接 return —— 必须确保它被激活。
            var existing = parent.Find(CloneName);
            if (existing != null)
            {
                var eb = existing.GetComponent<DefaultUIButton>();
                CloneButton = eb;
                CloneTarget = disconnect.gameObject;
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                return;
            }

            // ★★★ 克隆"断开连接"按钮 —— UI 资源/字体/样式完全一致
            var clone = UnityEngine.Object.Instantiate(disconnect.gameObject, parent);
            clone.name = CloneName;
            clone.SetActive(true);

            // 位置：复制 RectTransform，按配置偏移
            var srcRt = disconnect.GetComponent<RectTransform>();
            var dstRt = clone.GetComponent<RectTransform>();
            if (srcRt != null && dstRt != null)
            {
                dstRt.anchorMin = srcRt.anchorMin;
                dstRt.anchorMax = srcRt.anchorMax;
                dstRt.pivot = srcRt.pivot;
                dstRt.sizeDelta = srcRt.sizeDelta;
                dstRt.localScale = srcRt.localScale;
                dstRt.anchoredPosition = srcRt.anchoredPosition
                                       + new Vector2(0f, Plugin_OffsetY());
            }

            // 文字
            var btn = clone.GetComponent<DefaultUIButton>();
            if (btn != null)
            {
                // ★★★ 登记给插件，由插件的 Update 同步显隐。
                //     注意：不能在克隆体自己身上挂同步组件 —— 它被 SetActive(false) 后
                //     Update 就不跑了，永远无法自己恢复（这就是之前按钮消失的原因）。
                CloneButton = btn;
                CloneTarget = disconnect.gameObject;

                try { btn.SetHeaderText(Plugin_ButtonText()); }
                catch (Exception e) { ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] 设置按钮文字失败: " + e.Message); }

                // ★ 清掉克隆过来的监听（原来指向"断开连接"的处理），换成我们的
                try { btn.OnClick.RemoveAllListeners(); } catch { }
                btn.OnClick.AddListener(() =>
                {
                    try
                    {
                        // ★ 防重复点击：已经开过就忽略（实测点两次会重复触发退出流程）
                        if (ReconnectButtonPlugin.KeepReconnect)
                        {
                            ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] 已经在\"保留重连\"模式，忽略重复点击。");
                            return;
                        }

                        try { btn.Interactable = false; } catch { }   // 视觉上禁用，防连点

                        ReconnectButtonPlugin.Arm(ReconnectButtonPlugin.Log);

                        // ★ 触发原"断开连接"的点击 → 复用 EFT 原生退出战局流程
                        var d = ReconnectButtonPlugin.DisconnectField?.GetValue(screen) as DefaultUIButton;
                        if (d != null) d.OnClick.Invoke();
                        else ReconnectButtonPlugin.Log?.LogError("[ReconnectButton] 找不到 _disconnectButton，无法退出");
                    }
                    catch (Exception e)
                    {
                        ReconnectButtonPlugin.Log?.LogError("[ReconnectButton] 按钮点击异常: " + e);
                    }
                });
            }

            ReconnectButtonPlugin.Log?.LogInfo($"[ReconnectButton] 已在 ESC 菜单注入\"{Plugin_ButtonText()}\"按钮");
        }
        catch (Exception e)
        {
            ReconnectButtonPlugin.Log?.LogError("[ReconnectButton] 注入异常: " + e.Message);
        }
    }

    // 从插件实例读配置（避免静态持有 ConfigEntry）
    private static ReconnectButtonPlugin _cached;
    private static ReconnectButtonPlugin Inst()
    {
        if (_cached == null) _cached = UnityEngine.Object.FindObjectOfType<ReconnectButtonPlugin>();
        return _cached;
    }
    private static string Plugin_ButtonText()
    {
        var i = Inst();
        if (i == null) return "重连";
        var f = AccessTools.Field(typeof(ReconnectButtonPlugin), "_buttonText");
        return (f?.GetValue(i) as ConfigEntry<string>)?.Value ?? "重连";
    }
    private static float Plugin_OffsetY()
    {
        var i = Inst();
        if (i == null) return -62f;
        var f = AccessTools.Field(typeof(ReconnectButtonPlugin), "_offsetY");
        return (f?.GetValue(i) as ConfigEntry<float>)?.Value ?? -62f;
    }

    // ─────────────────────────────────────────────
    // ② Fika RaidLeave 拦截
    // ─────────────────────────────────────────────
    private static MethodBase _raidLeave;

    public static bool HookFikaRaidLeave(Harmony harmony)
    {
        try
        {
            // 在已加载程序集里找 FikaRequestHandler（不依赖精确命名空间）
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an;
                try { an = asm.GetName().Name; } catch { continue; }
                if (an == null || an.IndexOf("Fika.Core", StringComparison.OrdinalIgnoreCase) < 0) continue;

                foreach (var ty in asm.GetTypes())
                {
                    if (ty.Name != "FikaRequestHandler") continue;
                    t = ty; break;
                }
                if (t != null) break;
            }

            if (t == null)
            {
                ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] 找不到 FikaRequestHandler 类型");
                return false;
            }

            _raidLeave = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                          .FirstOrDefault(m => m.Name == "RaidLeave");
            if (_raidLeave == null)
            {
                ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] FikaRequestHandler 里找不到 RaidLeave 方法");
                return false;
            }

            harmony.Patch(_raidLeave, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(Patches), nameof(RaidLeave_Prefix))));

            ReconnectButtonPlugin.Log?.LogInfo("[ReconnectButton] 已挂钩 " + t.FullName + ".RaidLeave");
            return true;
        }
        catch (Exception e)
        {
            ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] 挂钩 RaidLeave 异常: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// ★ 核心：开着"保留重连"时跳过 RaidLeave，从而让服务端仍然认为我们在局里。
    /// 返回 false = 不执行原方法 = 不发 /fika/raid/leave。
    /// </summary>
    public static bool RaidLeave_Prefix()
    {
        try
        {
            if (ReconnectButtonPlugin.KeepReconnect)
            {
                ReconnectButtonPlugin.Log?.LogWarning(
                    "[ReconnectButton] ★ 已拦截 RaidLeave —— 服务端仍认为我们在战局里，回大厅后可以\"重新加入\"。");
                return false;
            }
        }
        catch { }
        return true;
    }

    // ─────────────────────────────────────────────
    // ③ ★★★ 拦截 SPT 的 /raid/profile/scavsave（真正导致丢物品的那条）
    // ─────────────────────────────────────────────
    private static MethodBase _putJson;

    public static bool HookScavSave(Harmony harmony)
    {
        try
        {
            // 找 SPT.Common.Http.RequestHandler（在 spt-common.dll 里）
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an;
                try { an = asm.GetName().Name; } catch { continue; }
                if (an == null) continue;
                if (an.IndexOf("spt-common", StringComparison.OrdinalIgnoreCase) < 0 &&
                    an.IndexOf("SPT.Common", StringComparison.OrdinalIgnoreCase) < 0) continue;

                foreach (var ty in asm.GetTypes())
                {
                    if (ty.Name != "RequestHandler") continue;
                    t = ty; break;
                }
                if (t != null) break;
            }

            if (t == null)
            {
                ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] 找不到 SPT.Common.Http.RequestHandler");
                return false;
            }

            // PutJson(string url, string json) —— 取第一个名为 PutJson 的
            _putJson = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "PutJson");
            if (_putJson == null)
            {
                ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] RequestHandler 里找不到 PutJson");
                return false;
            }

            harmony.Patch(_putJson, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(Patches), nameof(PutJson_Prefix))));

            ReconnectButtonPlugin.Log?.LogInfo("[ReconnectButton] 已挂钩 " + t.FullName + ".PutJson（拦截 scavsave）");
            return true;
        }
        catch (Exception e)
        {
            ReconnectButtonPlugin.Log?.LogWarning("[ReconnectButton] 挂钩 PutJson 异常: " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// ★★★ 核心：开着"保留重连"时，拦下 /raid/profile/scavsave。
    /// 不拦的话，服务端会按 ExitStatus.Left 结算 → 装备和战利品全部丢失（已实测确认）。
    /// 只拦这一个端点，其它 HTTP 请求照常。
    /// </summary>
    public static bool PutJson_Prefix(string __0)
    {
        try
        {
            if (!ReconnectButtonPlugin.KeepReconnect) return true;
            if (string.IsNullOrEmpty(__0)) return true;

            // ★★★ 要拦的结算端点（实测 + 服务端路由表确认）：
            //   /fika/raid/leave              —— Fika 会让服务端 RemovePlayerFromMatch（已单独挂）
            //   /raid/profile/scavsave        —— ★ Scav 结算（实测确认会丢物品）
            //   /client/match/local/end       —— ★ PMC 结算（MatchStaticRouter 的路由）
            //
            // 只在 KeepReconnect 模式下拦，所以正常撤离完全不受影响。
            // 只挂 PutJson（写入），不动 GetJson（读取）→ 进局读取不受影响。
            string[] blocked =
            {
                "raid/profile",              // Scav: /raid/profile/scavsave
                "/client/match/local/end",   // ★ PMC 结算
            };

            foreach (var b in blocked)
            {
                if (__0.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ReconnectButtonPlugin.Log?.LogWarning(
                        "[ReconnectButton] ★★★ 已拦截结算写入请求 " + __0
                      + " —— 服务端不会结算这一局，装备和战利品保留。");
                    return false;
                }
            }
        }
        catch { }
        return true;
    }
}
