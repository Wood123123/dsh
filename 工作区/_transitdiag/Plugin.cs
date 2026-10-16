using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using Fika.Core.Networking;
using HarmonyLib;

namespace TransitDiag
{
    /// <summary>
    /// 中转全链路探针。
    ///
    /// 目的：定位「到达转移区域但没有互动键」到底断在链条的哪一环。
    /// 每一环都打日志，包括 Fika 里**原本静默返回**的分支。
    ///
    /// 客户端链路：
    ///   ① OnInitEvent(TransitInitEvent)      收到房主下发的点
    ///   ② IsTargetPlayer(playerId)           ★ 核心：ID 是否匹配（原版静默失败）
    ///   ③ GetTransitPoints(points, side)     阵营/保护地点过滤
    ///   ④ IsVisibleTransitPoint(point)       hideIfNoKey / TryGetAccessToLocation
    ///   ⑤ SetTimers(list)                    ★ 这一步才让互动键生效
    ///   ⑥ TransitInteraction()               玩家踩进区域触发交互
    ///   ⑦ HandleClientExtract(id)            收到房主的 Extract 指令
    ///
    /// 房主链路：
    ///   ⑧ SetupEventsAndExfils(player)       为每个玩家发 TransitInitEvent
    ///   ⑨ InteractWithTransit(...)           房主收到交互
    ///   ⑩ OnTransitInteractPacketReceived     服务端收到交互包
    ///   ⑪ OnTransitEventPacketReceived        客户端收到事件包
    /// </summary>
    [BepInPlugin("com.local.transitdiag", "Transit Diagnostics", "1.0.0")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.fika.headless", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static Harmony _harmony;
        private const string Tag = "[TransitDiag]";

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony("com.local.transitdiag");
            BuildTypeIndex();

            Log.LogWarning($"{Tag} ===== 探针启动 =====");
            Log.LogWarning($"{Tag} 进程判定: headless={IsHeadless()} isServer={IsServer()}");

            int ok = 0, fail = 0;

            // ─────────── 客户端链路 ───────────
            ok += P("① OnInitEvent", "OnInitEvent", prefix: nameof(Pre_OnInitEvent), typeFilter: "ClientTransitController");
            ok += P("② IsTargetPlayer", "IsTargetPlayer", postfix: nameof(Post_IsTargetPlayer), typeFilter: "TransitController");
            ok += P("③ GetTransitPoints", "GetTransitPoints", postfix: nameof(Post_GetTransitPoints), typeFilter: "TransitController");
            ok += P("④ IsVisibleTransitPoint", "IsVisibleTransitPoint", postfix: nameof(Post_IsVisibleTransitPoint), typeFilter: "TransitController");
            ok += P("⑤ SetTimers", "SetTimers", prefix: nameof(Pre_SetTimers), typeFilter: "TransitController");
            ok += P("⑤b UpdateTimers", "UpdateTimers", prefix: nameof(Pre_UpdateTimers), typeFilter: "TransitController");
            ok += P("⑥ TransitInteraction", "TransitInteraction", prefix: nameof(Pre_TransitInteraction));
            // ★ ⑥b/⑥c：玩家进出转移点的时刻 —— 这是"互动按钮会不会出现"的起点
            ok += P("⑥b OnClientPlayerEnter", "OnClientPlayerEnter", prefix: nameof(Pre_OnClientPlayerEnter), typeFilter: "TransitController");
            ok += P("⑥c OnClientPlayerExit", "OnClientPlayerExit", prefix: nameof(Pre_OnClientPlayerExit), typeFilter: "TransitController");
            ok += P("⑦ HandleClientExtract", "HandleClientExtract", prefix: nameof(Pre_HandleClientExtract), typeFilter: "TransitController");

            // ─────────── 房主 / 无头端链路（含所有 override）───────────
            ok += P("⑧ SetupEventsAndExfils", "SetupEventsAndExfils", prefix: nameof(Pre_SetupEventsAndExfils));
            ok += P("⑨ InteractWithTransit", "InteractWithTransit", prefix: nameof(Pre_HostInteract), typeFilter: "TransitController");
            ok += P("⑩ OnTransitInteractPacketReceived", "OnTransitInteractPacketReceived", prefix: nameof(Pre_ServerGotInteract));
            ok += P("⑪ OnTransitEventPacketReceived", "OnTransitEventPacketReceived", prefix: nameof(Pre_ClientGotEvent));
            // ★ Init 这个名字太通用，必须限定到 TransitController，否则会挂到一堆每帧调用的方法上
            ok += P("⑫ TransitController.Init", "Init", prefix: nameof(Pre_HostInit), typeFilter: "TransitController");

            // ─────────── ★ 无头端特有 ───────────
            ok += P("⑬ InitializeTransitSystem", "InitializeTransitSystem", prefix: nameof(Pre_InitTransitSystem), typeFilter: "GameController");
            ok += P("⑭ Transit()", "Transit", prefix: nameof(Pre_Transit), typeFilter: "TransitController");
            ok += P("⑮ ExtractHeadlessClient", "ExtractHeadlessClient", prefix: nameof(Pre_ExtractHeadless), typeFilter: "HeadlessTransitController");
            ok += P("⑯ DelayHeadlessExtract", "DelayHeadlessExtract", prefix: nameof(Pre_DelayHeadlessExtract), typeFilter: "HeadlessTransitController");
            ok += P("⑰ StopHeadlessGameFromTransit", "StopHeadlessGameFromTransit", prefix: nameof(Pre_StopHeadlessGame), typeFilter: "HeadlessTransitController");
            ok += P("⑱ get_AliveTransitPlayers", "get_AliveTransitPlayers", postfix: nameof(Post_AliveTransitPlayers), typeFilter: "TransitController");
            ok += P("⑲ WaitForHeadlessInit", "WaitForHeadlessInit", prefix: nameof(Pre_WaitForHeadlessInit), typeFilter: "Headless");

            Log.LogWarning($"{Tag} ===== 探针装载完成: 成功 {ok} 个, 失败 {fail} ===== ");
        }

        // ─────────────────────────────────────────────
        // 类型索引：把相关程序集的所有类型按"方法名"建索引
        // 这样能同时命中基类与所有 override（无头端关键）
        // ─────────────────────────────────────────────
        private static readonly Dictionary<string, List<MethodInfo>> _byMethod = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);

        private static void BuildTypeIndex()
        {
            string[] wanted =
            {
                "Fika.Core", "Fika.Headless", "Assembly-CSharp"
            };
            int types = 0;
            var foundAsm = new List<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an;
                try { an = asm.GetName().Name; } catch { continue; }
                if (an == null) continue;
                if (!wanted.Any(w => an.Equals(w, StringComparison.OrdinalIgnoreCase))) continue;
                foundAsm.Add(an);
                Type[] all;
                try { all = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { all = e.Types.Where(x => x != null).ToArray(); } catch { continue; }
                foreach (var t in all)
                {
                    if (t == null) continue;
                    types++;
                    MethodInfo[] ms;
                    try { ms = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); } catch { continue; }
                    foreach (var m in ms)
                    {
                        if (!_byMethod.TryGetValue(m.Name, out var list)) { list = new List<MethodInfo>(); _byMethod[m.Name] = list; }
                        list.Add(m);
                    }
                }
            }
            Log.LogWarning($"{Tag} 类型索引建立完成: {types} 个类型, {_byMethod.Count} 个方法名");
            Log.LogWarning($"{Tag} 已索引程序集: {string.Join(", ", foundAsm)}");
            if (!foundAsm.Any(x => x.Equals("Fika.Headless", StringComparison.OrdinalIgnoreCase)))
            {
                Log.LogWarning($"{Tag} ⚠ 未找到 Fika.Headless 程序集 —— 无头端专用探针不会生效（若本进程不是无头端则正常）");
            }
            // 自检：关键方法名是否都已索引
            foreach (var mn in new[] { "StopHeadlessGameFromTransit", "SetupEventsAndExfils", "InteractWithTransit", "IsTargetPlayer", "SetTimers", "InitializeTransitSystem" })
            {
                int c = _byMethod.TryGetValue(mn, out var l) ? l.Count : 0;
                Log.LogWarning($"{Tag} 自检 {mn} ⇒ 索引到 {c} 个声明 {string.Join(", ", (l ?? new List<MethodInfo>()).Select(x => x.DeclaringType?.Name))}");
            }
        }

        // ─────────────────────────────────────────────
        // 按方法名给"所有"声明类型打补丁（基类 + 每个 override）
        // ─────────────────────────────────────────────
        private int _failCount;
        private int P(string label, string methodName, string prefix = null, string postfix = null, string typeFilter = null)
        {
            if (!_byMethod.TryGetValue(methodName, out var list) || list.Count == 0)
            {
                _failCount++;
                Log.LogError($"{Tag} {label}: 索引里没有名为 {methodName} 的方法");
                return 0;
            }

            // ★ 类型名过滤：方法名太通用时（如 Init / Transit）必须限定声明类型，
            //   否则会挂到一堆无关类型上，每帧刷屏。
            var targets = list;
            if (!string.IsNullOrEmpty(typeFilter))
            {
                targets = list.Where(m =>
                {
                    string fn = m.DeclaringType?.FullName ?? "";
                    return fn.IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                }).ToList();
                if (targets.Count == 0)
                {
                    _failCount++;
                    Log.LogError($"{Tag} {label}: 按类型过滤 '{typeFilter}' 后没有匹配（原候选 {list.Count} 个）");
                    return 0;
                }
            }

            HarmonyMethod pre = prefix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Plugin), prefix));
            HarmonyMethod post = postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(Plugin), postfix));
            int n = 0;
            foreach (var m in targets)
            {
                try
                {
                    _harmony.Patch(m, pre, post);
                    n++;
                    Log.LogWarning($"{Tag} {label}: ✅ {m.DeclaringType.FullName}::{m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))})");
                }
                catch (Exception e)
                {
                    Log.LogError($"{Tag} {label}: 挂在 {m.DeclaringType?.Name}::{m.Name} 失败 {e.GetType().Name} {e.Message}");
                }
            }
            return n;
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
        }

        // ─────────────────────────────────────────────
        // 工具
        // ─────────────────────────────────────────────
        private static bool IsHeadless()
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => (a.GetName().Name ?? "").IndexOf("Fika.Headless", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch { return false; }
        }

        private static bool IsServer()
        {
            try
            {
                object g = Singleton<IFikaNetworkManager>.Instance;
                return g != null && g.GetType().Name.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static Player Me()
        {
            try { return GamePlayerOwner.MyPlayer; } catch { return null; }
        }

        private static string MeDesc()
        {
            Player me = Me();
            if (me == null) return "MyPlayer=NULL";
            int pid = -999, netid = -999;
            try { pid = me.PlayerId; } catch { }
            try { netid = (int)AccessTools.Field(me.GetType(), "NetId").GetValue(me); } catch { }
            return $"MyPlayer.PlayerId={pid} NetId={netid} ProfileId={me.ProfileId}";
        }

        private static string PtDesc(object point)
        {
            if (point == null) return "<null>";
            try
            {
                object tp = AccessTools.Field(point.GetType(), "parameters")?.GetValue(point);
                if (tp == null) return point.GetType().Name;
                object id = AccessTools.Field(tp.GetType(), "id")?.GetValue(tp);
                object loc = AccessTools.Field(tp.GetType(), "location")?.GetValue(tp);
                object hide = AccessTools.Field(tp.GetType(), "hideIfNoKey")?.GetValue(tp);
                object act = AccessTools.Field(tp.GetType(), "activateAfterSec")?.GetValue(tp);
                object active = AccessTools.Field(tp.GetType(), "active")?.GetValue(tp);
                return $"id={id} →{loc} hideIfNoKey={hide} active={active} activateAfterSec={act}";
            }
            catch (Exception e) { return "PtDesc异常:" + e.Message; }
        }

        /// <summary>
        /// 节流器：同一个 key 在 minSeconds 内只允许输出一次。
        /// ④/⑤ 这类方法是周期性调用的（一局几百次），不加节流会把日志刷爆，
        /// 而且 DistDesc 里的 GetComponentInParent + 集合遍历本身就有开销。
        /// </summary>
        private static readonly Dictionary<string, DateTime> _lastLogAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        private static bool Allow(string key, double minSeconds)
        {
            DateTime now = DateTime.UtcNow;
            if (_lastLogAt.TryGetValue(key, out DateTime prev) && (now - prev).TotalSeconds < minSeconds)
            {
                return false;
            }
            _lastLogAt[key] = now;
            return true;
        }

        /// <summary>转移点 id（用于节流键）。</summary>
        private static string PtId(object point)
        {
            try
            {
                if (point == null) return "?";
                object tp = AccessTools.Field(point.GetType(), "parameters")?.GetValue(point);
                object id = tp == null ? null : AccessTools.Field(tp.GetType(), "id")?.GetValue(tp);
                return id?.ToString() ?? "?";
            }
            catch { return "?"; }
        }

        /// <summary>
        /// 距离诊断：玩家坐标 / 转移点坐标 / 两者距离 / 该点是否还在「等待变可见」集合里。
        /// 这几项能直接区分两种情况：
        ///   距离很远（而画面上你就在点旁边） → 位置数据不对（漂移）
        ///   距离很近但点在等待集合里          → 可见性/激活逻辑没跑完
        /// </summary>
        private static string DistDesc(object point)
        {
            if (point == null) return "dist=<null point>";
            try
            {
                Player me = Me();
                if (me == null) return "dist=MyPlayer=NULL";

                UnityEngine.Vector3 myPos;
                try { myPos = me.Position; } catch { return "dist=读玩家坐标失败"; }

                UnityEngine.Vector3 ptPos;
                var comp = point as UnityEngine.Component;
                if (comp == null) return "dist=点不是Component";
                try { ptPos = comp.transform.position; } catch { return "dist=读点坐标失败"; }

                float d = UnityEngine.Vector3.Distance(myPos, ptPos);

                // 该点是否还在「等待变可见」集合里
                // ★ 缓存 controller，避免每次都 GetComponentInParent（那是父级链遍历）
                string waiting = "?";
                try
                {
                    string key = PtId(point);
                    if (!_ctrlCache.TryGetValue(key, out object ctrl) || ctrl == null)
                    {
                        // ★ 三种取法依次尝试（TransitPoint 不在 controller 的子节点下，
                        //   所以 GetComponentInParent 通常拿不到）
                        ctrl = AccessTools.Field(point.GetType(), "_controller")?.GetValue(point);
                        if (ctrl == null) ctrl = Singleton<EFT.TransitController>.Instance;
                        if (ctrl == null) ctrl = comp.GetComponentInParent<EFT.TransitController>();
                        _ctrlCache[key] = ctrl;
                    }

                    if (ctrl != null)
                    {
                        var f = AccessTools.Field(ctrl.GetType(), "_waitForVisibleTransitPoints");
                        if (f != null && f.GetValue(ctrl) is System.Collections.IEnumerable en)
                        {
                            bool found = false;
                            foreach (var x in en) { if (ReferenceEquals(x, point)) { found = true; break; } }
                            waiting = found ? "★ 是（还没可见）" : "否";
                        }
                    }
                }
                catch { }

                return $"玩家坐标=({myPos.x:F1},{myPos.y:F1},{myPos.z:F1}) " +
                       $"点坐标=({ptPos.x:F1},{ptPos.y:F1},{ptPos.z:F1}) " +
                       $"★ 距离={d:F1}m  等待可见集合={waiting}";
            }
            catch (Exception e) { return "dist异常:" + e.Message; }
        }

        // ─────────────────────────────────────────────
        // ① OnInitEvent
        // ─────────────────────────────────────────────
        private static void Pre_OnInitEvent(object __0)
        {
            try
            {
                int n = -1; int pid = -1;
                if (__0 != null)
                {
                    object p = AccessTools.Property(__0.GetType(), "Points")?.GetValue(__0);
                    if (p is IDictionary d) n = d.Count;
                    object idv = AccessTools.Property(__0.GetType(), "PlayerId")?.GetValue(__0);
                    if (idv is int i) pid = i;
                }
                Log.LogWarning($"{Tag} ① OnInitEvent 收到事件: 包内 PlayerId={pid} 点数={n}   {MeDesc()}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ① 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ② IsTargetPlayer  ★ 核心探针
        // ─────────────────────────────────────────────
        private static void Post_IsTargetPlayer(int __0, bool __result)
        {
            try
            {
                Player me = Me();
                int mine = me == null ? -999 : me.PlayerId;
                Log.LogWarning($"{Tag} ② IsTargetPlayer: 包内={__0}  我={mine}  {(me == null ? "MyPlayer=NULL" : "")}  ⇒ 匹配={__result}" +
                               (__result ? "" : "   ★★★ 不匹配！这里失败会导致后续全不执行、没有互动键"));
            }
            catch (Exception e) { Log.LogError($"{Tag} ② 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ③ GetTransitPoints
        // ─────────────────────────────────────────────
        private static void Post_GetTransitPoints(object __result, object __1)
        {
            try
            {
                int n = -1; string detail = "";
                if (__result is IEnumerable en)
                {
                    var list = en.Cast<object>().ToList();
                    n = list.Count;
                    detail = string.Join(" | ", list.Select(PtDesc));
                }
                Log.LogWarning($"{Tag} ③ GetTransitPoints: side={__1} ⇒ 通过过滤的点数={n}");
                if (detail.Length > 0) Log.LogWarning($"{Tag} ③   明细: {detail}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ③ 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ④ IsVisibleTransitPoint  ★ 互动键的闸门
        // ─────────────────────────────────────────────
        private static void Post_IsVisibleTransitPoint(object __0, bool __result)
        {
            try
            {
                string id = PtId(__0);
                // ★ 可见性【变化】时必须立刻记录（这是关键事件），不变时每 5 秒最多一次
                bool changed = !_lastVisible.TryGetValue(id, out bool prev) || prev != __result;
                _lastVisible[id] = __result;

                // ★ 靠近时（<30m）不节流 —— 这样"走到点上"的过程会被完整记录，
                //   能直接看出你是走近了还是根本没到
                bool near = false;
                try
                {
                    Player me = Me();
                    var comp = __0 as UnityEngine.Component;
                    if (me != null && comp != null)
                    {
                        near = UnityEngine.Vector3.Distance(me.Position, comp.transform.position) < 30f;
                    }
                }
                catch { }

                if (!changed && !near && !Allow("④" + id, 5.0))
                {
                    return;
                }

                Log.LogWarning($"{Tag} ④ IsVisibleTransitPoint ⇒ {__result}{(changed ? "  ★变化" : "")}   {PtDesc(__0)}   {MeDesc()}");
                Log.LogWarning($"{Tag} ④   {DistDesc(__0)}");
                if (!__result)
                    Log.LogWarning($"{Tag} ④   ★★★ 该点不可见 → 没有互动键");
            }
            catch (Exception e) { Log.LogError($"{Tag} ④ 探针异常 {e.Message}"); }
        }

        /// <summary>每个转移点上一次的可见性，用来识别「变化」。</summary>
        private static readonly Dictionary<string, bool> _lastVisible = new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>转移点 id → 它的 TransitController 缓存（避免热路径上反复 GetComponentInParent）。</summary>
        private static readonly Dictionary<string, object> _ctrlCache = new Dictionary<string, object>(StringComparer.Ordinal);

        // ─────────────────────────────────────────────
        // ⑤ SetTimers / UpdateTimers
        // ─────────────────────────────────────────────
        private static void Pre_SetTimers(object __0, object __1, bool __2)
        {
            try
            {
                int n = -1; string detail = "";
                if (__0 is IEnumerable en)
                {
                    var list = en.Cast<object>().ToList();
                    n = list.Count;
                    detail = string.Join(" | ", list.Select(PtDesc));
                }
                Log.LogWarning($"{Tag} ⑤ SetTimers 被调用: 点数={n} player={(__1 == null ? "NULL" : "有")} arg3={__2}  ★ 这一步决定互动键是否生效");
                // ★ 明细行内容重复度高，节流到每 10 秒一次
                if (detail.Length > 0 && Allow("⑤明细", 10.0)) Log.LogWarning($"{Tag} ⑤   明细: {detail}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑤ 探针异常 {e.Message}"); }
        }

        private static void Pre_UpdateTimers(object __instance)
        {
            try
            {
                // ★ UpdateTimers 是周期性调用（一局 80+ 次），全部输出纯属刷屏 → 节流到每 5 秒一次
                if (!Allow("⑤b", 5.0)) return;
                Log.LogWarning($"{Tag} ⑤b UpdateTimers 被调用   {MeDesc()}");
                // ★ __instance 就是 ClientTransitController 本身 —— 比 Singleton 可靠得多
                ScanPoints(__instance);
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑤b 探针异常 {e.Message}"); }
        }

        /// <summary>
        /// ★ 每 5 秒把【所有】转移点的状态打一遍（每点一行）。
        /// 这是回答"某张图的某个点为什么没有互动键"的关键：
        ///   距离       → 你到底离它多远（判断是不是没走到）
        ///   active     → 服务端配置里这个点启用了吗
        ///   _enabled   → EFT 运行时是否启用
        ///   _isActive  → 是否已激活（配合 activateAfterSec）
        ///   go         → 该点的 GameObject 是否处于激活状态（ReEnablePoints 是否生效）
        ///   waiting    → 是否卡在「等待变可见」集合里
        /// </summary>
        private static void ScanPoints(object ctrl)
        {
            try
            {
                // ★ 优先用调用方传进来的实例；拿不到才退回单例
                if (ctrl == null)
                {
                    try { ctrl = Singleton<EFT.TransitController>.Instance; } catch { }
                }
                if (ctrl == null)
                {
                    Log.LogWarning($"{Tag} ⑤b   ★ 拿不到 TransitController（既没有 __instance 也没有单例）");
                    return;
                }

                var f = AccessTools.Field(ctrl.GetType(), "pointsById")
                        ?? AccessTools.Field(typeof(EFT.TransitController), "pointsById");
                if (f == null) { Log.LogWarning($"{Tag} ⑤b   ★ 找不到 pointsById 字段"); return; }

                if (!(f.GetValue(ctrl) is System.Collections.IDictionary dict))
                {
                    Log.LogWarning($"{Tag} ⑤b   ★ pointsById 不是字典");
                    return;
                }

                Player me = Me();
                UnityEngine.Vector3 myPos = UnityEngine.Vector3.zero;
                bool haveMe = false;
                try { if (me != null) { myPos = me.Position; haveMe = true; } } catch { }

                // 等待可见集合（一次取出，避免逐点反射）
                System.Collections.IEnumerable waitSet = null;
                try
                {
                    var wf = AccessTools.Field(ctrl.GetType(), "_waitForVisibleTransitPoints");
                    waitSet = wf?.GetValue(ctrl) as System.Collections.IEnumerable;
                }
                catch { }

                int n = 0;
                foreach (System.Collections.DictionaryEntry de in dict)
                {
                    object pt = de.Value;
                    var comp = pt as UnityEngine.Component;
                    if (comp == null || comp.Equals(null)) continue;
                    n++;

                    string id = "?", loc = "?", act = "?";
                    try
                    {
                        object pars = AccessTools.Field(comp.GetType(), "parameters")?.GetValue(comp);
                        if (pars != null)
                        {
                            id = AccessTools.Field(pars.GetType(), "id")?.GetValue(pars)?.ToString() ?? "?";
                            loc = AccessTools.Field(pars.GetType(), "location")?.GetValue(pars)?.ToString() ?? "?";
                            act = AccessTools.Field(pars.GetType(), "active")?.GetValue(pars)?.ToString() ?? "?";
                        }
                    }
                    catch { }

                    string en = "?", isact = "?";
                    try { en = AccessTools.Field(comp.GetType(), "_enabled")?.GetValue(comp)?.ToString() ?? "?"; } catch { }
                    try { isact = AccessTools.Field(comp.GetType(), "_isActive")?.GetValue(comp)?.ToString() ?? "?"; } catch { }

                    string go = "?";
                    UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
                    string scene = "?";
                    try
                    {
                        go = comp.gameObject.activeSelf.ToString();
                        pos = comp.transform.position;
                        scene = comp.gameObject.scene.name;
                    }
                    catch { }

                    float d = -1f;
                    if (haveMe) { try { d = UnityEngine.Vector3.Distance(myPos, pos); } catch { } }

                    bool w = false;
                    if (waitSet != null)
                    {
                        try { foreach (var x in waitSet) { if (ReferenceEquals(x, pt)) { w = true; break; } } } catch { }
                    }

                    Log.LogWarning($"{Tag} ⑤b   id={id,-3} →{loc,-14} 距离={d,7:F1}m active={act,-5} _enabled={en,-5} _isActive={isact,-5} GO={go,-5} 等待集合={(w ? "★是" : "否")} pos=({pos.x:F0},{pos.y:F0},{pos.z:F0})");
                    Log.LogWarning($"{Tag} ⑤b       场景={scene}");
                }

                Log.LogWarning($"{Tag} ⑤b   —— 共 {n} 个转移点 ——");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑤b ScanPoints 异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ⑥ TransitInteraction
        // ─────────────────────────────────────────────
        private static void Pre_TransitInteraction(object __instance, int transitPointId, string keyId)
        {
            try
            {
                int netid = -999;
                try { netid = (int)AccessTools.Field(__instance.GetType(), "NetId").GetValue(__instance); } catch { }
                Log.LogWarning($"{Tag} ⑥ TransitInteraction 触发！transitPointId={transitPointId} keyId={keyId} NetId={netid}");
                // 触发时的玩家坐标 + 该 id 对应的转移点坐标/距离
                try
                {
                    var pl = __instance as Player;
                    if (pl != null)
                    {
                        var pos = pl.Position;
                        Log.LogWarning($"{Tag} ⑥   玩家坐标=({pos.x:F1},{pos.y:F1},{pos.z:F1})");
                    }
                    var ctrl = Singleton<EFT.TransitController>.Instance;
                    var f = AccessTools.Field(typeof(EFT.TransitController), "pointsById");
                    if (ctrl != null && f != null && f.GetValue(ctrl) is System.Collections.IDictionary dict)
                    {
                        object pt = null;
                        foreach (System.Collections.DictionaryEntry de in dict)
                        {
                            object v = de.Value;
                            if (v == null) continue;
                            var idf = AccessTools.Field(v.GetType(), "parameters");
                            object pars = idf?.GetValue(v);
                            object idv = pars == null ? null : AccessTools.Field(pars.GetType(), "id")?.GetValue(pars);
                            if (idv != null && Convert.ToInt32(idv) == transitPointId) { pt = v; break; }
                        }
                        if (pt != null)
                        {
                            Log.LogWarning($"{Tag} ⑥   目标点: {PtDesc(pt)}");
                            Log.LogWarning($"{Tag} ⑥   {DistDesc(pt)}");
                        }
                        else { Log.LogWarning($"{Tag} ⑥   未在 pointsById 里找到 id={transitPointId} 的点"); }
                    }
                }
                catch (Exception e2) { Log.LogWarning($"{Tag} ⑥   坐标诊断异常: {e2.Message}"); }
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑥ 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ⑥b / ⑥c 玩家进出转移点 ★ 互动按钮出现的起点
        // ─────────────────────────────────────────────
        private static void Pre_OnClientPlayerEnter(object __0, object __1)
        {
            try
            {
                string who = "?";
                try
                {
                    if (__1 is Player pl)
                    {
                        int pid = -999;
                        try { pid = pl.PlayerId; } catch { }
                        who = $"PlayerId={pid} ProfileId={pl.ProfileId}";
                    }
                    else who = (__1 == null ? "Player=NULL" : __1.GetType().Name);
                }
                catch { }

                Log.LogWarning($"{Tag} ⑥b ★★★ OnClientPlayerEnter 触发！点: {PtDesc(__0)}  玩家: {who}   {MeDesc()}");
                Log.LogWarning($"{Tag} ⑥b   {DistDesc(__0)}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑥b 探针异常 {e.Message}"); }
        }

        private static void Pre_OnClientPlayerExit(object __0, object __1)
        {
            try
            {
                Log.LogWarning($"{Tag} ⑥c OnClientPlayerExit 触发！点: {PtDesc(__0)}   {MeDesc()}");
                Log.LogWarning($"{Tag} ⑥c   {DistDesc(__0)}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑥c 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ⑦ HandleClientExtract
        // ─────────────────────────────────────────────
        private static void Pre_HandleClientExtract(int transitId, int playerId)
        {
            try
            {
                Player me = Me();
                Log.LogWarning($"{Tag} ⑦ HandleClientExtract: transitId={transitId} playerId={playerId}  我={ (me==null?-999:me.PlayerId) }");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑦ 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ⑧ SetupEventsAndExfils（房主为每个玩家发事件）
        // ─────────────────────────────────────────────
        private static void Pre_SetupEventsAndExfils(object __0)
        {
            try
            {
                string d = "<null>";
                if (__0 != null)
                {
                    int pid = -999, netid = -999;
                    try { pid = (int)AccessTools.Property(__0.GetType(), "PlayerId").GetValue(__0); } catch { }
                    try { netid = (int)AccessTools.Field(__0.GetType(), "NetId").GetValue(__0); } catch { }
                    d = $"PlayerId={pid} NetId={netid} Side={AccessTools.Property(__0.GetType(), "Side")?.GetValue(__0)}";
                }
                Log.LogWarning($"{Tag} ⑧ SetupEventsAndExfils 被调用: {d}   ★ 这里会为该玩家下发 TransitInitEvent");

                // ★★★ 关键：主机侧这个方法【忽略传入的 player】，改为遍历 CoopHandler.HumanPlayers 逐个发包。
                //     所以真正决定"发给谁"的是 HumanPlayers 的内容 —— 把它整个打出来。
                DumpHumanPlayers();
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑧ 探针异常 {e.Message}"); }
        }

        /// <summary>
        /// 打印 CoopHandler.HumanPlayers 的完整名单。
        /// 主机就是按这个列表 foreach 发 TransitInitEvent 的，
        /// 所以「列表里有几个、分别是谁」= 「发了几个包、发给谁」。
        /// </summary>
        private static void DumpHumanPlayers()
        {
            try
            {
                Type chType = AccessTools.TypeByName("Fika.Core.Main.Components.CoopHandler");
                if (chType == null)
                {
                    Log.LogWarning($"{Tag} ⑧   ★ 找不到 CoopHandler 类型（客户端上可能没有？）");
                    return;
                }

                object coop = null;
                var tryGet = chType.GetMethod("TryGetCoopHandler",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (tryGet != null)
                {
                    var args = new object[] { null };
                    try
                    {
                        if (tryGet.Invoke(null, args) is bool ok && ok) coop = args[0];
                    }
                    catch { }
                }

                if (coop == null)
                {
                    Log.LogWarning($"{Tag} ⑧   ★ TryGetCoopHandler 没拿到实例 → 无法列出名单");
                    return;
                }

                // HumanPlayers（真正被遍历发包的那个列表）
                var hpProp = chType.GetProperty("HumanPlayers");
                var humans = hpProp?.GetValue(coop) as System.Collections.IEnumerable;
                int n = 0;
                if (humans != null)
                {
                    foreach (var p in humans)
                    {
                        n++;
                        Log.LogWarning($"{Tag} ⑧   └─ HumanPlayers[{n - 1}] {PlayerBrief(p)}");
                    }
                }

                // Players（NetId → FikaPlayer 全表）
                var pProp = chType.GetProperty("Players");
                var all = pProp?.GetValue(coop) as System.Collections.IDictionary;
                if (all != null)
                {
                    Log.LogWarning($"{Tag} ⑧   Players 表共 {all.Count} 项：");
                    foreach (System.Collections.DictionaryEntry de in all)
                    {
                        Log.LogWarning($"{Tag} ⑧      [{de.Key}] {PlayerBrief(de.Value)}");
                    }
                }

                // ExtractedPlayers
                var epProp = chType.GetProperty("ExtractedPlayers");
                var ex = epProp?.GetValue(coop) as System.Collections.IEnumerable;
                if (ex != null)
                {
                    var list = new List<string>();
                    foreach (var x in ex) list.Add(x?.ToString() ?? "?");
                    Log.LogWarning($"{Tag} ⑧   ExtractedPlayers = [{string.Join(",", list)}]");
                }

                Log.LogWarning($"{Tag} ⑧   ★★ HumanPlayers 数量 = {n} → 本局将发出 {n} 个 TransitInitEvent（每个带各自的 PlayerId）");
            }
            catch (Exception e) { Log.LogWarning($"{Tag} ⑧   DumpHumanPlayers 异常: {e.Message}"); }
        }

        /// <summary>把一个玩家对象压成一行短描述（PlayerId / NetId / Side / 昵称）。</summary>
        private static string PlayerBrief(object p)
        {
            if (p == null) return "<null>";
            try
            {
                string pid = "?", netid = "?", side = "?", nick = "?";
                try { pid = AccessTools.Property(p.GetType(), "PlayerId")?.GetValue(p)?.ToString() ?? "?"; } catch { }
                try { netid = AccessTools.Field(p.GetType(), "NetId")?.GetValue(p)?.ToString() ?? "?"; } catch { }
                try { side = AccessTools.Property(p.GetType(), "Side")?.GetValue(p)?.ToString() ?? "?"; } catch { }
                try
                {
                    object prof = AccessTools.Property(p.GetType(), "Profile")?.GetValue(p);
                    if (prof != null) nick = AccessTools.Property(prof.GetType(), "Nickname")?.GetValue(prof)?.ToString() ?? "?";
                }
                catch { }
                try
                {
                    if (nick == "?")
                    {
                        object profId = AccessTools.Property(p.GetType(), "ProfileId")?.GetValue(p);
                        if (profId != null) nick = profId.ToString();
                    }
                }
                catch { }
                return $"{p.GetType().Name}  PlayerId={pid} NetId={netid} Side={side} 昵称={nick}";
            }
            catch (Exception e) { return "PlayerBrief异常:" + e.Message; }
        }

        private static void Pre_HostInit()
        {
            try { Log.LogWarning($"{Tag} ⑫ FikaHostTransitController.Init() 被调用"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑫ 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ⑨⑩⑪ 网络往返
        // ─────────────────────────────────────────────
        private static void Pre_HostInteract(object __0, object __1)
        {
            try { Log.LogWarning($"{Tag} ⑨ 房主 InteractWithTransit: player={__0} data={__1}"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑨ 探针异常 {e.Message}"); }
        }

        private static void Pre_ServerGotInteract(object __0, object __1)
        {
            try { Log.LogWarning($"{Tag} ⑩ FikaServer 收到 TransitInteractPacket: packet={__0} peer={__1}"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑩ 探针异常 {e.Message}"); }
        }

        private static void Pre_ClientGotEvent(object __0)
        {
            try
            {
                object type = AccessTools.Field(__0?.GetType(), "EventType")?.GetValue(__0);
                object tid = AccessTools.Field(__0?.GetType(), "TransitId")?.GetValue(__0);
                Log.LogWarning($"{Tag} ⑪ FikaClient 收到 TransitEventPacket: EventType={type} TransitId={tid}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑪ 探针异常 {e.Message}"); }
        }

        // ─────────────────────────────────────────────
        // ★ 无头端专用探针
        // ─────────────────────────────────────────────
        private static void Pre_InitTransitSystem(object __0, object __3, object __4)
        {
            try
            {
                string loc = "<null>";
                if (__4 != null) { try { loc = AccessTools.Property(__4.GetType(), "Id")?.GetValue(__4)?.ToString(); } catch { } }
                Log.LogWarning($"{Tag} ⑬ InitializeTransitSystem 被调用！(无头端/房主) location={loc}  headless={IsHeadless()}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑬ 探针异常 {e.Message}"); }
        }

        private static void Pre_Transit(object __0, int __1, string __2, object __4)
        {
            try
            {
                int pid = -999, netid = -999;
                if (__4 != null)
                {
                    try { pid = (int)AccessTools.Property(__4.GetType(), "PlayerId").GetValue(__4); } catch { }
                    try { netid = (int)AccessTools.Field(__4.GetType(), "NetId").GetValue(__4); } catch { }
                }
                Log.LogWarning($"{Tag} ⑭ Transit() 被调用！ transitPoint={PtDesc(__0)} id={__1} keyId={__2}  玩家 PlayerId={pid} NetId={netid}");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑭ 探针异常 {e.Message}"); }
        }

        private static void Pre_ExtractHeadless(bool __runOriginal)
        {
            try { Log.LogWarning($"{Tag} ⑮ ExtractHeadlessClient 被调用！(无头端准备退出战局)"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑮ 探针异常 {e.Message}"); }
        }

        private static void Pre_DelayHeadlessExtract(bool __runOriginal)
        {
            try { Log.LogWarning($"{Tag} ⑯ DelayHeadlessExtract 被调用！"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑯ 探针异常 {e.Message}"); }
        }

        private static void Pre_StopHeadlessGame(bool __runOriginal)
        {
            try { Log.LogWarning($"{Tag} ⑰ ★★★ StopHeadlessGameFromTransit 被调用！无头端即将结束本局"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑰ 探针异常 {e.Message}"); }
        }

        private static void Post_AliveTransitPlayers(object __result)
        {
            try
            {
                int n = -1;
                if (__result is System.Collections.IEnumerable en) n = en.Cast<object>().Count();
                Log.LogWarning($"{Tag} ⑱ AliveTransitPlayers ⇒ 数量={n}   ★ 中转需要足够人数");
            }
            catch (Exception e) { Log.LogError($"{Tag} ⑱ 探针异常 {e.Message}"); }
        }

        private static void Pre_WaitForHeadlessInit(bool __runOriginal)
        {
            try { Log.LogWarning($"{Tag} ⑲ WaitForHeadlessInit 被调用"); }
            catch (Exception e) { Log.LogError($"{Tag} ⑲ 探针异常 {e.Message}"); }
        }
    }
}
