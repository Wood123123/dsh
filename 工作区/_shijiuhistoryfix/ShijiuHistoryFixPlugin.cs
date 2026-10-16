using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.DI.Annotations;
using SemanticVersioning;
using StashTools.History;

// ★ System.Version / System.Range 与 SemanticVersioning 里的同名类型冲突，用别名消歧
using SemVer = SemanticVersioning.Version;
using SemRange = SemanticVersioning.Range;

namespace Dsh.ShijiuHistoryFix
{
    /// <summary>
    /// 修【拾玖】对局记录在「断线重连」后无限重试的问题。
    ///
    /// ── 问题现象 ──
    ///   客户端日志反复出现：
    ///     [Warning:【拾玖】多项功能优化] 战绩保留在本地待重试：行动编号已用于另一条记录
    ///   每 15 秒一次，永不停止。
    ///
    /// ── 根因（反编译 ShiJiu-Tools.dll / ShiJiu-Tools.Server.dll 确认）──
    ///   客户端：
    ///     · FikaHistorySession.Begin 里 ActionId = "fika-" + 房间GUID
    ///     · RaidEarnings.Begin 里 resume 只在【转图】时为 true
    ///       ⇒ 重连时 resume=false ⇒ HistoryCapture 生成【新的 RecordId + 新的 StartedUtc】
    ///     · HistoryDelivery.Send 先发 "join"，失败就直接返回（不 save）
    ///   服务端 HistoryStore.Join：
    ///     · 按 ActionId 建 TeamManifest，里面为每个玩家记 { RecordId, StartedUtc }
    ///     · 同一 ActionId 再来一次 join 时，若 RecordId/StartedUtc 与登记不符
    ///       ⇒ throw new InvalidOperationException("行动编号已用于另一条记录")
    ///   ⇒ 重连回同一个房间 ⇒ ActionId 不变、但 RecordId 变 ⇒ 永久冲突、永久重试
    ///
    /// ── 本补丁 ──
    ///   给 HistoryStore.Join(string, TeamJoin, bool) 挂一个 Finalizer：
    ///   捕获「行动编号已用于另一条记录」这个异常，把该玩家的登记【更新】为新记录，
    ///   然后吞掉异常（客户端会认为 join 成功，继续 save）。
    ///
    ///   为什么安全：
    ///     · 只针对这一条特定异常，其它异常原样抛出
    ///     · 只改 manifest 里【这一个玩家】的登记项，不动 Context/Members
    ///     · 服务端 Save 的校验是「登记必须等于这条记录」——
    ///       更新登记后，新记录正好通过校验（旧记录如果已保存过，不受影响）
    /// </summary>
    public sealed class Metadata : IModMetadata
    {
        public string ModGuid { get; init; }
        public string Name { get; init; }
        public string Author { get; init; }
        public List<string> Contributors { get; init; }
        public SemVer Version { get; init; }
        public SemRange SptVersion { get; init; }
        public bool HasPrepatcher { get; init; }
        public List<string> Incompatibilities { get; init; }
        public Dictionary<string, SemRange> ModDependencies { get; init; }
        public string Url { get; init; }
        public string License { get; init; }

        public Metadata()
        {
            ModGuid = "com.dsh.shijiuhistoryfix";
            Name = "拾玖战绩重连修复";
            Author = "dsh";
            Version = new SemVer("1.0.0", false);
            SptVersion = new SemRange(">=4.1.6 <4.2.0", false);
            License = "MIT";

            // ★ 不在构造函数里装补丁。
            //
            //   原因：Metadata 的构造函数在【mod 枚举阶段】执行，
            //   那时 ShiJiu-Tools.Server.dll 不一定已经加载，
            //   直接 typeof(HistoryStore) 会抛 TypeLoadException。
            //
            //   （客户端那次 ShijiuReconnectFix 就是栽在这个加载顺序上）
            //
            //   所以改由 Loader（IOnLoad）在所有 DLL 加载完之后装。
        }
    }

    /// <summary>
    /// 真正干活的地方 —— 实现 SPT 的 IOnLoad，在所有 mod 加载完成后执行。
    /// </summary>
    [Injectable]
    public sealed class Loader : IOnLoad
    {
        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                JoinFix.Install();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ShijiuHistoryFix] 装载失败：" + ex);
            }
            return Task.CompletedTask;
        }
    }

    internal static class JoinFix
    {
        private const string HarmonyId = "com.dsh.shijiuhistoryfix";
        private const string ConflictMessage = "行动编号已用于另一条记录";

        internal static bool Installed { get; private set; }

        /// <summary>服务端历史库根目录（与 HistoryRouter 用的完全一致）。</summary>
        private static string StoreRoot
            => Path.Combine(AppContext.BaseDirectory, "user", "stashtools-history");

        internal static void Install()
        {
            if (Installed) return;

            // ★ 用 TypeByName 而不是 typeof(HistoryStore)：
            //   万一拾玖的 DLL 还没被加载，typeof 会直接抛 TypeLoadException；
            //   TypeByName 返回 null，我们可以给出清楚的提示。
            var storeType = AccessTools.TypeByName("StashTools.History.HistoryStore");
            if (storeType == null)
            {
                Console.WriteLine("[ShijiuHistoryFix] ★ 找不到 StashTools.History.HistoryStore"
                                + " —— 拾玖的服务端 mod 没装或版本不符，补丁未装。");
                return;
            }

            var target = AccessTools.Method(storeType, "Join",
                new[] { typeof(string), typeof(TeamJoin), typeof(bool) });

            if (target == null)
            {
                Console.WriteLine("[ShijiuHistoryFix] ★ HistoryStore 里找不到 Join(string, TeamJoin, bool)"
                                + " —— 拾玖版本可能变了，补丁未装。");
                return;
            }

            var finalizer = AccessTools.Method(typeof(JoinFix), nameof(Finalizer));
            new Harmony(HarmonyId).Patch(target, finalizer: new HarmonyMethod(finalizer));

            // 回头核对，避免静默失败
            var info = Harmony.GetPatchInfo(target);
            var hit = false;
            if (info != null)
            {
                foreach (var f in info.Finalizers)
                {
                    if (f.owner == HarmonyId) { hit = true; break; }
                }
            }

            Installed = hit;
            Console.WriteLine(hit
                ? "[ShijiuHistoryFix] ✓ 已装载 —— 重连产生的战绩记录可正常上传（不再无限重试）"
                : "[ShijiuHistoryFix] ✗ 补丁未挂上");
        }

        /// <summary>
        /// 捕获「行动编号已用于另一条记录」，就地修复登记。
        /// 返回 null 表示吞掉异常；否则原样返回让上层处理。
        /// </summary>
        private static Exception Finalizer(string owner, TeamJoin join, Exception __exception)
        {
            if (__exception is not InvalidOperationException ||
                __exception.Message == null ||
                !__exception.Message.Contains(ConflictMessage))
            {
                return __exception;
            }

            try
            {
                if (FixRegistration(owner, join))
                {
                    Console.WriteLine($"[ShijiuHistoryFix] ★ 自动修复：ActionId={join.Context?.ActionId}"
                                    + $" 玩家={owner} 登记更新为 RecordId={join.RecordId}"
                                    + $" StartedUtc={join.StartedUtc:O}（原为「行动编号已用于另一条记录」）");
                    return null;   // ★ 吞掉异常 ⇒ 客户端认为 join 成功
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ShijiuHistoryFix] 自动修复失败：" + ex.Message);
            }

            return __exception;
        }

        /// <summary>把 manifest 里该玩家的登记改成这次 join 带来的记录。</summary>
        private static bool FixRegistration(string owner, TeamJoin join)
        {
            if (join?.Context == null) return false;

            TeamRules.Validate(join.Context);

            var path = Path.Combine(StoreRoot, "_teams", TeamRules.Key(join.Context) + ".json");
            if (!File.Exists(path)) return false;

            var json = File.ReadAllText(path);
            var manifest = JsonSerializer.Deserialize<TeamManifest>(json, HistoryStore.Json);
            if (manifest?.Registrations == null) return false;

            // 只改这一个玩家的登记项；Context / Members / AllowRosterExtension 一律不动
            manifest.Registrations[owner] = new TeamRegistration
            {
                RecordId = join.RecordId,
                StartedUtc = join.StartedUtc
            };

            // 原子写：先写临时文件再替换，避免服务端读到大半截文件
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(manifest, HistoryStore.Json));
                File.Move(tmp, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }

            return true;
        }
    }
}
