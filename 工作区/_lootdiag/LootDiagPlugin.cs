using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace Dsh.LootDiag
{
    /// <summary>
    /// 拾取诊断 —— 只读，不修改任何游戏状态。
    ///
    /// 目的：定位「玩家无法拾取地面散落物品」到底卡在哪一步。
    ///
    /// 为什么需要它：现有日志无法回答这个问题。实测 bi8bo 那一局里，
    /// 客户端日志中【没有任何】他自己的拾取失败记录 —— 因为
    ///   · 本地就被拒的操作默认不写日志
    ///   · 只有网络路径的失败才会打 Error
    /// 所以必须主动插桩。
    ///
    /// 追踪链（每一步都打点，一眼看出断在哪）：
    ///   ① InteractionContextHelper.GetAvailableActions(owner, LootItem)
    ///        ★ 最关键：它返回 AvailableInteractionState，
    ///          其中 Actions 是可用动作列表、Error 是拒绝原因。
    ///          如果 Actions 为空或 Error 非空 → 就是"本地不允许拾取"的铁证。
    ///   ② ItemController.CheckItemAction(item, location)
    ///        本地物品操作检查点，被拒时返回带 Error 的 Option
    ///   ③ Player.Interact(IItemOwner, Callback)
    ///        玩家发起交互（拾取入口）
    ///   ④ FikaClient.OnOperationCallbackPacketReceived
    ///        客户端收到主机的执行结果
    ///   ⑤ FikaServer.OnInventoryPacketReceived
    ///        主机收到客户端的操作请求
    ///
    /// 日志量控制：
    ///   · ①③ 对同一个物品只记录一次（去重表 + 定时清理）
    ///   · ② 默认只在失败时记录
    ///   · ④⑤ 全量记录（本来就是低频）
    /// </summary>
    [BepInPlugin(Guid, "Loot Pickup Diag", "1.0.0")]
    public sealed class LootDiagPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.dsh.lootdiag";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> LogAvailableActions;
        internal static ConfigEntry<bool> LogCheckItemAction;
        internal static ConfigEntry<bool> Verbose;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "诊断总开关。关掉后所有挂钩立即失效。");
            LogAvailableActions = Config.Bind("General", "LogAvailableActions", true,
                "记录每个地面物品的可用交互动作与拒绝原因（核心开关，用来回答「为什么拾不起来」）。同一物品只记一次。");
            LogCheckItemAction = Config.Bind("General", "LogCheckItemAction", true,
                "记录本地物品操作检查的结果。默认只在被拒时记录（正常通过的不记，避免刷屏）。");
            Verbose = Config.Bind("General", "Verbose", false,
                "额外记录成功路径的细节（排查时可开，平时关着）。");

            Log.LogInfo("LootDiag v1.0.0 loaded. Enabled=" + Enabled.Value);

            try
            {
                new Harmony(Guid).PatchAll(typeof(LootDiagPlugin).Assembly);
                Log.LogInfo("[LootDiag] PatchAll 完成，开始自检…");
                VerifyPatches();
            }
            catch (Exception e)
            {
                Log.LogError("[LootDiag] PatchAll 失败: " + e);
            }
        }

        /// <summary>
        /// ★ 启动自检。
        ///
        /// 为什么需要：HarmonyX 的 AccessTools.TypeByName 在类型名写错时【只打警告、不报错】，
        /// 补丁会被静默跳过。实测踩过一次（命名空间写成 EFT.InventoryLogic 而实际是 EFT），
        /// 结果整个插件等于没装，排查时却以为它在跑。
        /// 所以装完必须回头核对每个目标上是否真有我们的补丁。
        /// </summary>
        private static void VerifyPatches()
        {
            int ok = 0, bad = 0;

            void Check(string label, System.Reflection.MethodBase m)
            {
                if (m == null) { Log.LogWarning($"[LootDiag] ✗ {label} —— 找不到目标方法"); bad++; return; }
                var info = Harmony.GetPatchInfo(m);
                var hit = false;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) if (p.owner == Guid) hit = true;
                    foreach (var p in info.Postfixes) if (p.owner == Guid) hit = true;
                }
                if (hit) { Log.LogInfo($"[LootDiag] ✓ {label}"); ok++; }
                else { Log.LogWarning($"[LootDiag] ✗ {label} —— 目标存在但补丁未挂上"); bad++; }
            }

            Check("GetAvailableActions(LootItem)",
                  AccessTools.Method(AccessTools.TypeByName("EFT.InteractionContextHelper"), "GetAvailableActions",
                      new[] { AccessTools.TypeByName("EFT.GamePlayerOwner"), typeof(LootItem) }));

            Check("CheckItemAction",
                  AccessTools.Method(typeof(ItemController), "CheckItemAction",
                      new[] { typeof(Item), typeof(ItemAddress) }));

            Check("Player.Interact",
                  AccessTools.Method(typeof(Player), "Interact",
                      new[] { typeof(IItemOwner), typeof(Callback) }));

            Check("FikaClient.OnOperationCallbackPacketReceived",
                  AccessTools.Method(AccessTools.TypeByName("Fika.Core.Networking.FikaClient"), "OnOperationCallbackPacketReceived"));

            Check("FikaServer.OnInventoryPacketReceived",
                  AccessTools.Method(AccessTools.TypeByName("Fika.Core.Networking.FikaServer"), "OnInventoryPacketReceived"));

            Log.LogInfo($"[LootDiag] 自检完成：{ok} 个挂钩生效，{bad} 个失败"
                      + (bad > 0 ? "  ⇒ ★ 失败的挂钩不会产生数据，请把这几行发给开发者" : "  ⇒ 诊断已就绪"));
        }
    }

    // =====================================================================
    // ① 核心：地面物品的可用交互动作 —— 直接回答「为什么拾不起来」
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_GetAvailableActions
    {
        /// <summary>
        /// 精确锁定重载：GetAvailableActions(GamePlayerOwner, LootItem)。
        /// 这个方法有十几个重载，用参数类型区分，避免打错目标。
        /// </summary>
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(
                AccessTools.TypeByName("EFT.InteractionContextHelper"),
                "GetAvailableActions",
                new[] { AccessTools.TypeByName("EFT.GamePlayerOwner"), typeof(LootItem) });
        }

        private static bool Prepare()
        {
            var m = TargetMethod();
            if (m == null)
            {
                LootDiagPlugin.Log.LogWarning("[LootDiag] 找不到 InteractionContextHelper.GetAvailableActions(GamePlayerOwner, LootItem)"
                                            + " —— 拾取动作诊断将不可用");
                return false;
            }
            return true;
        }

        /// <summary>去重：同一个物品只在首次（或状态变化时）记录，避免看着它就刷屏。</summary>
        private static readonly Dictionary<string, string> _lastState = new Dictionary<string, string>();
        private static float _nextCleanup;

        /// <summary>
        /// ★ 注意参数位置：签名是 GetAvailableActions(GamePlayerOwner owner, LootItem lootItem)
        ///     owner   = __0
        ///     lootItem= __1   ← 必须用 __1
        ///   写成 __0 会让 Harmony 按类型注入 GamePlayerOwner 到 LootItem 形参上，
        ///   运行时报 "Specified argument was out of the range of valid values"。
        /// </summary>
        private static void Postfix(LootItem __1, object __result)
        {
            if (!LootDiagPlugin.Enabled.Value || !LootDiagPlugin.LogAvailableActions.Value) return;
            if (__1 == null || __result == null) return;

            try
            {
                // AvailableInteractionState 是类，直接反射读字段（编译期无依赖，避免版本差异）
                var t = __result.GetType();
                var actions = t.GetField("Actions")?.GetValue(__result) as System.Collections.ICollection;
                var error = t.GetField("Error")?.GetValue(__result) as string;

                var itemId = SafeGet(() => __1.ItemId) ?? "?";
                var itemName = SafeGet(() => __1.Name) ?? "?";
                var tpl = SafeGet(() => __1.Item?.TemplateId) ?? "?";

                var actionCount = actions == null ? -1 : actions.Count;
                var hasError = !string.IsNullOrWhiteSpace(error);

                // 关键状态：动作数 + 错误文本
                var state = actionCount + "|" + error;

                // 定时清理去重表（每 60 秒）
                var now = Time.realtimeSinceStartup;
                if (now >= _nextCleanup) { _nextCleanup = now + 60f; _lastState.Clear(); }

                if (_lastState.TryGetValue(itemId, out var prev) && prev == state) return;
                _lastState[itemId] = state;

                // 只有在「没有可用动作」或「有错误」时才用 Warning，其余用 Info
                var line = $"[LootDiag][动作] {itemName} tpl={tpl} id={itemId}"
                         + $" 可用动作={actionCount}"
                         + (hasError ? $" ★错误=\"{error}\"" : "");

                if (actionCount <= 0 || hasError)
                {
                    LootDiagPlugin.Log.LogWarning(line + "   ⇒ ★ 本地不允许拾取这个物品");
                }
                else if (LootDiagPlugin.Verbose.Value)
                {
                    var names = new List<string>();
                    foreach (var a in actions)
                    {
                        names.Add(a == null ? "null" : a.GetType().Name);
                        if (names.Count >= 6) break;
                    }
                    LootDiagPlugin.Log.LogInfo(line + "  [" + string.Join(",", names) + "]");
                }
            }
            catch (Exception e)
            {
                LootDiagPlugin.Log.LogWarning("[LootDiag] GetAvailableActions Postfix 出错: " + e.Message);
            }
        }

        private static T SafeGet<T>(Func<T> f) { try { return f(); } catch { return default; } }
    }

    // =====================================================================
    // ② 本地物品操作检查点 —— 被拒的直接原因
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_CheckItemAction
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ItemController), "CheckItemAction",
                new[] { typeof(Item), typeof(ItemAddress) });
        }

        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        private static void Postfix(Item __0, object __result)
        {
            if (!LootDiagPlugin.Enabled.Value || !LootDiagPlugin.LogCheckItemAction.Value) return;
            if (__result == null) return;

            try
            {
                var t = __result.GetType();
                var failed = (bool)(t.GetProperty("Failed")?.GetValue(__result) ?? false);
                if (!failed)
                {
                    if (LootDiagPlugin.Verbose.Value)
                    {
                        LootDiagPlugin.Log.LogInfo($"[LootDiag][检查] 通过 {Describe(__0)}");
                    }
                    return;
                }

                var err = t.GetProperty("Error")?.GetValue(__result);
                LootDiagPlugin.Log.LogWarning($"[LootDiag][检查] ★ 本地被拒 {Describe(__0)}  原因: {err}");
            }
            catch (Exception e)
            {
                LootDiagPlugin.Log.LogWarning("[LootDiag] CheckItemAction Postfix 出错: " + e.Message);
            }
        }

        internal static string Describe(Item item)
        {
            if (item == null) return "(null)";
            try
            {
                var owner = item.Parent;
                return $"{item.ShortName} tpl={item.TemplateId} id={item.Id}"
                     + (owner != null ? $" 位于 {owner.Container?.ID}" : "");
            }
            catch { return "(读取失败)"; }
        }
    }

    // =====================================================================
    // ③ 玩家发起交互 —— 拾取意图
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_PlayerInteract
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Player), "Interact",
                new[] { typeof(IItemOwner), typeof(Callback) });
        }

        private static bool Prepare() { return TargetMethod() != null; }

        private static readonly Dictionary<string, float> _lastLog = new Dictionary<string, float>();

        private static void Prefix(Player __instance, IItemOwner loot)
        {
            if (!LootDiagPlugin.Enabled.Value) return;
            if (loot == null) return;

            try
            {
                // 只关心地面物品（LootItem），容器/尸体不记
                var lootItem = loot as LootItem;
                if (lootItem == null) return;

                var id = lootItem.ItemId ?? "?";
                var now = Time.realtimeSinceStartup;
                if (_lastLog.TryGetValue(id, out var last) && now - last < 3f) return;
                _lastLog[id] = now;

                LootDiagPlugin.Log.LogInfo($"[LootDiag][拾取意图] 玩家={Nick(__instance)}"
                                         + $" 目标={SafeName(lootItem)} id={id}");
            }
            catch { }
        }

        private static string Nick(Player p)
        {
            try { return p?.Profile?.Nickname ?? "?"; } catch { return "?"; }
        }

        private static string SafeName(LootItem li)
        {
            try { return li.Name ?? "?"; } catch { return "?"; }
        }
    }

    // =====================================================================
    // ④ 客户端收到主机结果
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_OperationCallback
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Fika.Core.Networking.FikaClient");
            return t == null ? null : AccessTools.Method(t, "OnOperationCallbackPacketReceived");
        }

        private static bool Prepare()
        {
            if (TargetMethod() == null)
            {
                LootDiagPlugin.Log.LogWarning("[LootDiag] 找不到 FikaClient.OnOperationCallbackPacketReceived");
                return false;
            }
            return true;
        }

        private static void Prefix(object __0)
        {
            if (!LootDiagPlugin.Enabled.Value || __0 == null) return;
            try
            {
                var t = __0.GetType();
                var netId = t.GetField("NetId")?.GetValue(__0);
                var cbId = t.GetField("CallbackId")?.GetValue(__0);
                var status = t.GetField("Status")?.GetValue(__0);
                var error = t.GetField("Error")?.GetValue(__0) as string;

                var ok = string.Equals(status?.ToString(), "Succeeded", StringComparison.OrdinalIgnoreCase);
                var line = $"[LootDiag][结果] netId={netId} cb={cbId} status={status}"
                         + (string.IsNullOrWhiteSpace(error) ? "" : $" ★error=\"{error}\"");

                if (ok) { if (LootDiagPlugin.Verbose.Value) LootDiagPlugin.Log.LogInfo(line); }
                else LootDiagPlugin.Log.LogWarning(line);
            }
            catch { }
        }
    }

    // =====================================================================
    // ⑤ 主机收到客户端操作请求
    // =====================================================================
    [HarmonyPatch]
    internal static class Patch_HostInventoryPacket
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("Fika.Core.Networking.FikaServer");
            return t == null ? null : AccessTools.Method(t, "OnInventoryPacketReceived");
        }

        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        private static void Prefix(object __0)
        {
            if (!LootDiagPlugin.Enabled.Value || !LootDiagPlugin.Verbose.Value || __0 == null) return;
            try
            {
                var t = __0.GetType();
                var netId = t.GetField("NetId")?.GetValue(__0);
                var cbId = t.GetField("CallbackId")?.GetValue(__0);
                LootDiagPlugin.Log.LogInfo($"[LootDiag][主机收到] netId={netId} cb={cbId}");
            }
            catch { }
        }
    }
}
