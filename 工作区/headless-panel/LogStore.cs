using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dsh.HeadlessPanel;

/// <summary>One received log line, already attributed to a segment.</summary>
public sealed class LogLine
{
    public DateTime Utc { get; set; }
    public string Text { get; set; } = "";
    public string Level { get; set; } = "";      // Error / Warning / Info / Debug / ""

    /// <summary>
    /// ★ 这一行【真正的来源实例】。
    ///
    ///   段是按"谁在打这一局"建的，但 SPT 服务端日志没有自己的战局边界标记，
    ///   所以它的行会被并入无头端/客户端建立的那一段。此时段.Instance 是
    ///   "EFT-headless"，而这些行的实际来源是 "SPT-Server" —— 想区分就必须单独记。
    /// </summary>
    public string Source { get; set; } = "";
}

/// <summary>
/// One continuous stretch of a single instance's log that belongs to one raid.
///
/// A reconnect splits one raid into several segments on the client side (and possibly on the
/// headless side too), so segments — not raids — are the unit we detect from log text.
/// Segments are then grouped into a raid by map + time overlap.
/// </summary>
public sealed class RaidSegment
{
    public string Id { get; set; } = "";
    public string Instance { get; set; } = "";
    public string Role { get; set; } = "";        // client / headless / server
    public string Machine { get; set; } = "";
    public string Map { get; set; } = "";
    public string RaidCode { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime? EndedUtc { get; set; }
    public int LineCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    /// <summary>Reconnect / seek markers recorded inside this segment.</summary>
    public List<string> Events { get; set; } = new List<string>();

    /// <summary>
    /// ★ 这个段里每一行【真正的来源】各有多少行。
    ///
    ///   段是按"谁在打这一局"建的（通常是客户端或无头端），但 SPT 服务端日志
    ///   会并入同一段 —— 它的行在时间线上必须能看出来自服务端。
    ///   这里做段级累计，界面就不必为了显示来源而读整个段文件。
    /// </summary>
    public Dictionary<string, int> SourceCounts { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public string FileName => $"{Id}.jsonl";

    public double DurationSec =>
        EndedUtc.HasValue ? Math.Max(0, (EndedUtc.Value - StartedUtc).TotalSeconds) : 0;
}

/// <summary>
/// Receives log lines from the LogShipper plugins, splits them into per-instance raid
/// segments, and persists everything so the panel can show one merged timeline per raid.
///
/// Boundaries are detected from the log text itself, per instance — no cross-machine
/// coordination is needed, and a headless that never connects still produces a segment.
/// </summary>
public static class LogStore
{
    /// <summary>Max lines kept per segment on disk (older lines are dropped from the front).</summary>
    public const int MaxLinesPerSegment = 200000;

    /// <summary>Segments older than this are pruned.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(14);

    /// <summary>Two segments belong to the same raid if their maps match and they overlap within this window.</summary>
    /// <summary>
    /// 同【同一实例】的段之间，多久之内还当成"断线重连"。
    ///
    /// 这是 Raid Code 不可用时的兜底判据：重连是"进程还在、重新载入地图"，
    /// 通常几十秒内完成；而"打完一局再开一局"要经过结算/回大厅/重新匹配/加载，
    /// 一般超过 1 分钟。两者有重叠区，所以 Code 可用时一律以 Code 为准。
    /// </summary>
    private static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 两个段最多相隔多久还算"同一局"（用于 Raid Code 无法判断的场景）。
    ///
    /// ★ 原先是 30 分钟 —— 太宽了。实测：一局在 08:03:14 结束，下一局 08:04:26 开始
    ///   （间隔 72 秒），于是两局被并成一条，来源里出现两个 EFT-headless 段。
    /// </summary>
    private static readonly TimeSpan JoinWindow = TimeSpan.FromSeconds(90);

    private static readonly object Gate = new();

    /// <summary>
    /// ★ 统一的 JSON 选项。
    ///
    /// SaveMeta 用 "id":"…" 这个模式去重（替换同一个段的旧行），所以序列化【必须】输出
    /// camelCase —— 默认的 PascalCase 会让去重匹配不到，于是每次更新都追加一行，
    /// meta.jsonl 里同一个段出现多个版本，界面就会把一局显示成好几段。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>instance -> its currently open segment (null when the instance is not in a raid).</summary>
    private static readonly Dictionary<string, RaidSegment> Open = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, List<LogLine>> Buffers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> LastFlush = new(StringComparer.OrdinalIgnoreCase);

    public static string Root =>
        // ★ 日志数据也放 user\logs 下。虽然 **/logs/** 已经排除了它，
        //   但把易变数据放在 user\mods 里本身就是隐患（NarcoNet 按目录分发）。
        Path.Combine(AppContext.BaseDirectory, "user", "logs", "HeadlessPanel", "raids");

    public static string SegmentDir(string instance) => Path.Combine(Root, Safe(instance));

    public static string SegmentPath(RaidSegment s) => Path.Combine(SegmentDir(s.Instance), s.FileName);

    private static string Safe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_');
        return sb.ToString();
    }

    // ---------------------------------------------------------------- ingestion

    /// <summary>Number of lines accepted from a single ingest call.</summary>
    public sealed class IngestResult
    {
        public int Accepted;
        public int SegmentsOpened;
        public int SegmentsClosed;
        public string Note = "";
    }

    public static IngestResult Ingest(string instance, string role, string machine, DateTime sentUtc, List<string> lines)
    {
        var result = new IngestResult();
        if (string.IsNullOrWhiteSpace(instance) || lines == null || lines.Count == 0) return result;

        // ★★★ 磁盘 IO 必须移出锁。
        //
        //   早期版本在 lock(Gate) 内部直接 File.AppendAllText。只要有一次写入被阻塞
        //   （杀软扫描、文件句柄竞争、慢盘），锁就一直不释放，于是：
        //     · ServerLogTailer 线程卡在锁上
        //     · 所有面板请求（list/telemetry/players…）全部排队等待
        //     → 服务端看起来"进程还在但完全不响应"（实测 CPU 时间冻结、端口不再监听）。
        //
        //   现在锁内只做内存操作并把待写内容收集起来，真正的写盘在锁外执行。
        var writes = new List<KeyValuePair<string, string>>();
        var metaWrites = new List<RaidSegment>();

        lock (Gate)
        {
            // The shipper sends a batch without per-line timestamps, so arrival order is the
            // ordering we have. sentUtc anchors the batch; lines within a batch are spaced by
            // index so the merged timeline stays stable.
            var baseTime = sentUtc == default ? DateTime.UtcNow : sentUtc;

            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i] ?? "";
                // Spread the batch a few ms so ordering survives a merge with other instances.
                var ts = baseTime.AddMilliseconds(i - lines.Count);

                // ★ SPT 服务端日志每行自带【UTC】时间前缀：
                //     [2026-10-14 05:06:39.823][Information][Logger] msg
                //   而插件上报的是 UTC，所以行内时间直接采用，不做时区换算。
                var embedded = TryParseLeadingTimestamp(text);
                if (embedded.HasValue) ts = embedded.Value;

                var boundary = ClassifyBoundary(text);
                var lineMap = ExtractMap(text);

                if (boundary == Boundary.RaidStart)
                {
                    // ★ 一次战局会有【多个】"开始特征"（Starting on location / Match created /
                    //   MatchingType / All players loaded …）。早期版本对每个特征都
                    //   "关旧段+开新段"，于是一局被切成 8 段。
                    //
                    //   正确规则：
                    //     · 没有活跃段           → 开新段
                    //     · 有活跃段且地图不同    → 转图，关旧开新
                    //     · 有活跃段且地图相同/未知 → 同一局的重复特征，不开新段
                    //
                    //   断线重连仍然是两段，因为重连前必然先出现【结束标记】
                    //   （Stop / Session Statistics），那时活跃段已经被关掉了。
                    if (Open.TryGetValue(instance, out var prev) && prev != null)
                    {
                        var mapChanged = lineMap != null
                                      && !string.IsNullOrEmpty(prev.Map)
                                      && !string.Equals(prev.Map, lineMap, StringComparison.OrdinalIgnoreCase);

                        if (mapChanged)
                        {
                            CloseSegmentLocked(instance, ts, writes, metaWrites);
                            result.SegmentsClosed++;
                            Open[instance] = NewSegmentLocked(instance, role, machine, ts, metaWrites);
                            result.SegmentsOpened++;
                        }
                        else if (lineMap != null && string.IsNullOrEmpty(prev.Map))
                        {
                            prev.Map = lineMap;
                        }
                    }
                    else
                    {
                        Open[instance] = NewSegmentLocked(instance, role, machine, ts, metaWrites);
                        result.SegmentsOpened++;
                    }
                }

                if (boundary == Boundary.RaidEnd)
                {
                    AppendLine(instance, role, machine, ts, text, ref result);
                    if (Open.TryGetValue(instance, out var cur) && cur != null)
                    {
                        CloseSegmentLocked(instance, ts, writes, metaWrites);
                        result.SegmentsClosed++;
                    }
                    continue;
                }

                AppendLine(instance, role, machine, ts, text, ref result);

                // A segment may be open already; keep its map / code fresh.
                if (Open.TryGetValue(instance, out var seg) && seg != null)
                {
                    if (lineMap != null && string.IsNullOrEmpty(seg.Map)) seg.Map = lineMap;

                    var code = ExtractRaidCode(text);
                    if (code != null) seg.RaidCode = code;

                    var ev = ExtractEvent(text);
                    if (ev != null && seg.Events.Count < 60) seg.Events.Add($"{ts:HH:mm:ss} {ev}");
                }
            }

            CollectFlush(writes, metaWrites);

            // An instance that has been silent for a long time is no longer in a raid.
            foreach (var inst in Open.Keys.ToList())
            {
                var seg = Open[inst];
                if (seg != null && DateTime.UtcNow - seg.StartedUtc > TimeSpan.FromHours(6))
                {
                    CloseSegmentLocked(inst, seg.StartedUtc.AddHours(6), writes, metaWrites);
                    result.SegmentsClosed++;
                }
            }
        }

        // ★ 锁已释放，现在才真正写盘
        PerformWrites(writes, metaWrites);

        return result;
    }

    /// <summary>写盘（锁外执行）。失败只影响这一次落盘，不会波及请求处理。</summary>
    private static void PerformWrites(List<KeyValuePair<string, string>> writes, List<RaidSegment> metaWrites)
    {
        foreach (var w in writes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(w.Key));
                File.AppendAllText(w.Key, w.Value, new UTF8Encoding(false));
                TrimSegmentFile(w.Key);
            }
            catch { }
        }

        foreach (var seg in metaWrites)
        {
            SaveMeta(seg);
        }
    }

    private static void AppendLine(string instance, string role, string machine, DateTime ts,
                                   string text, ref IngestResult result)
    {
        if (!Open.TryGetValue(instance, out var seg) || seg == null)
        {
            // ★ SPT 服务端日志没有战局边界标记，所以它永远不会有自己的"活跃段"。
            //   但它【本来就是只在战局窗口内才被采集】的（ServerLogTailer 里的
            //   IsRaidWindow 判定），因此此刻若别的实例正在打一局（或刚打完），
            //   这些行就该直接并入那一局 —— 而不是落进按小时切的 idle 段。
            //
            //   否则 idle 段的时间范围是整整一小时，聚合时会把前后两局吸成同一局，
            //   界面上的时长也会变成"63 分钟"这种明显错误的值。
            var isServer = string.Equals(role, "server", StringComparison.OrdinalIgnoreCase);

            seg = isServer
                ? FindRaidSegmentForServerLine(ts, ServerLogGraceSecondsCached)
                : FindActiveRaidSegment(instance);

            if (seg == null)
            {
                // Lines outside a raid still matter (startup errors), so park them in a
                // per-hour "idle" segment so nothing is silently lost.
                seg = GetOrCreateIdleSegmentLocked(instance, role, machine, ts);
            }
        }

        if (!Buffers.TryGetValue(seg.Id, out var buf))
        {
            buf = new List<LogLine>();
            Buffers[seg.Id] = buf;
        }

        var line = new LogLine { Utc = ts, Text = text, Level = ExtractLevel(text), Source = instance };
        buf.Add(line);
        result.Accepted++;
        seg.LineCount++;

        // ★ 累计行来源 —— 服务端行会并入客户端/无头端的段，界面要能看出它们的存在
        if (seg.SourceCounts == null)
        {
            seg.SourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        var srcKey = string.IsNullOrEmpty(line.Source) ? seg.Instance : line.Source;
        seg.SourceCounts.TryGetValue(srcKey, out var sc);
        seg.SourceCounts[srcKey] = sc + 1;

        if (line.Level == "Error") seg.ErrorCount++;
        else if (line.Level == "Warning") seg.WarningCount++;
    }

    /// <summary>
    /// 找出"当前正在进行的那一局"的段（不含调用者自己）。
    ///
    /// 只有 role=server 的实例会走这条路：它在别人的战局期间收到的行，
    /// 事件上属于那一局，应该并进去。
    /// </summary>
    private static RaidSegment FindActiveRaidSegment(string selfInstance)
    {
        RaidSegment best = null;
        foreach (var kv in Open)
        {
            if (string.Equals(kv.Key, selfInstance, StringComparison.OrdinalIgnoreCase)) continue;

            var s = kv.Value;
            if (s == null) continue;
            if (string.IsNullOrEmpty(s.Map)) continue;          // 只认真正在地图里的那一局
            if (s.EndedUtc.HasValue) continue;                   // 已结束的不算

            // 多个同时活跃时取最近开始的那个
            if (best == null || s.StartedUtc > best.StartedUtc) best = s;
        }
        return best;
    }

    /// <summary>
    /// 服务端行该并进哪一段。
    ///
    /// ★ 服务端日志的归属必须跟着【真正的战局起止】，不能掉进按小时切的 idle 段：
    ///
    ///   · ServerLogTailer 每 3 秒才读一次，因此战局刚开始的那批行可能在
    ///     无头端的 "Starting on location" 之前就到达 —— 那时 Open 里还没有活跃段。
    ///   · 战局【结束之后】的结算行（/raid/profile/scavsave、/client/match/local/end）
    ///     到达时，无头端段已经关闭并被移出 Open。
    ///
    ///   两种情况都应当归入那一局，所以这里在活跃段之外，还要看"刚刚结束的段"。
    /// </summary>
    private static RaidSegment FindRaidSegmentForServerLine(DateTime ts, int graceSeconds)
    {
        var active = FindActiveRaidSegment("__server__");
        if (active != null) return active;

        // 刚结束的战局：行时间落在 [段开始 - 容差, 段结束 + 宽限] 内就并入
        var grace = TimeSpan.FromSeconds(Math.Max(graceSeconds, 120));
        var lead = TimeSpan.FromSeconds(120);        // 允许比段开始早一点（时钟/延迟误差）

        RaidSegment best = null;
        lock (Gate)
        {
            foreach (var seg in LastClosed.Values)
            {
                if (seg == null || string.IsNullOrEmpty(seg.Map)) continue;
                if (!seg.EndedUtc.HasValue) continue;

                if (ts < seg.StartedUtc - lead) continue;
                if (ts > seg.EndedUtc.Value + grace) continue;

                // 多个候选时取最近的那个
                if (best == null || seg.EndedUtc.Value > best.EndedUtc.Value) best = seg;
            }
        }
        return best;
    }

    /// <summary>最近关闭的战局段（按实例），供服务端日志在战局结束后继续归属。</summary>
    private static readonly Dictionary<string, RaidSegment> LastClosed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>待写盘的元数据（锁内收集，锁外写）。</summary>
    [ThreadStatic] private static List<RaidSegment> _pendingMeta;

    private static List<RaidSegment> PendingMeta => _pendingMeta ??= new List<RaidSegment>();

    /// <summary>
    /// A segment for lines that arrive outside any detected raid — most notably SPT's own
    /// server log, which carries no "raid started" marker at all.
    ///
    /// ★ 按【小时】切而不是按天：按天的话段的时间范围是 [当天00:00, +∞)，
    ///   与任何战局都"重叠"，聚合时会被错误地并进最早的那一局。
    ///   按小时切之后时间范围是真实的一小时窗口，时间重叠判断才有意义。
    /// </summary>
    private static RaidSegment GetOrCreateIdleSegmentLocked(string instance, string role, string machine, DateTime ts)
    {
        var hourStart = new DateTime(ts.Year, ts.Month, ts.Day, ts.Hour, 0, 0, DateTimeKind.Utc);
        var key = instance + "#idle#" + hourStart.ToString("yyyyMMddHH");

        // 先查缓存（比遍历磁盘快得多）
        if (MetaCache.TryGetValue(key, out var cached) && cached != null) return cached;

        foreach (var s in AllSegments())
        {
            if (s.Id == key) return s;
        }

        var seg = new RaidSegment
        {
            Id = key,
            Instance = instance,
            Role = string.IsNullOrEmpty(role) ? "unknown" : role,
            Machine = machine ?? "",
            Map = "",
            StartedUtc = hourStart,
            // 该小时已经过去就给出确切的结束时间，聚合时不会当成"仍在进行"
            EndedUtc = hourStart.AddHours(1) <= DateTime.UtcNow ? hourStart.AddHours(1) : (DateTime?)null,
        };

        MetaCache[key] = seg;
        InvalidateCache();
        PendingMeta.Add(seg);
        return seg;
    }

    // ------------------------------------------------------------- persistence

    private static RaidSegment NewSegmentLocked(string instance, string role, string machine, DateTime ts,
                                                List<RaidSegment> metaWrites)
    {
        var seg = new RaidSegment
        {
            Id = $"{Safe(instance)}_{ts:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N").Substring(0, 6)}",
            Instance = instance,
            Role = string.IsNullOrEmpty(role) ? "unknown" : role,
            Machine = machine ?? "",
            StartedUtc = ts,
        };
        Buffers[seg.Id] = new List<LogLine>();
        MetaCache[seg.Id] = seg;
        InvalidateCache();
        metaWrites.Add(seg);
        return seg;
    }

    /// <summary>关段：只做内存操作，把待写内容收集起来（锁外才写盘）。</summary>
    private static void CloseSegmentLocked(string instance, DateTime ts,
                                           List<KeyValuePair<string, string>> writes,
                                           List<RaidSegment> metaWrites)
    {
        if (!Open.TryGetValue(instance, out var seg) || seg == null) return;

        seg.EndedUtc = ts;
        CollectSegmentLocked(seg, writes, force: true);
        MetaCache[seg.Id] = seg;
        InvalidateCache();
        metaWrites.Add(seg);

        // ★ 记住刚结束的这一局：服务端日志在战局结束后的结算行
        //   （scavsave / match/local/end）仍应归入它，而不是掉进 idle 段。
        LastClosed[instance] = seg;

        Open.Remove(instance);
        LastFlush.Remove(seg.Id);
    }

    private static void CloseSegment(string instance, DateTime ts)
    {
        var writes = new List<KeyValuePair<string, string>>();
        var meta = new List<RaidSegment>();
        lock (Gate) { CloseSegmentLocked(instance, ts, writes, meta); }
        PerformWrites(writes, meta);

        // ★ 一局刚结束 —— 这是"局数"增长的唯一时刻，顺手按局数清理一次。
        //   Prune 只在手动点「清理」时才会跑，所以以前 20 局的上限永远不生效。
        MaybeAutoPrune();
    }

    /// <summary>自动清理的最小间隔 —— 避免每次关段都把 meta.jsonl 全量扫一遍。</summary>
    private static readonly TimeSpan AutoPruneInterval = TimeSpan.FromMinutes(10);
    private static DateTime _lastAutoPruneUtc = DateTime.MinValue;

    /// <summary>
    /// 关段之后按需触发一次 <see cref="Prune"/>：节流 + 丢到后台线程。
    ///
    /// 为什么不能直接同步调用：Prune 里的 BuildRaids 要读遍所有 meta.jsonl，
    /// 放在关段路径上会把"战局刚结束"那一刻的响应拖慢。
    /// 而清理本身不紧急 —— 晚几秒删掉最老的一局，对它没有任何影响。
    /// </summary>
    private static void MaybeAutoPrune()
    {
        var now = DateTime.UtcNow;
        lock (Gate)
        {
            if (now - _lastAutoPruneUtc < AutoPruneInterval) return;
            _lastAutoPruneUtc = now;
        }

        try
        {
            _ = Task.Run(() =>
            {
                try { Prune(); } catch { }
            });
        }
        catch { }
    }

    /// <summary>把某个段缓冲区里的行收集成一条待写记录（不碰磁盘）。</summary>
    private static void CollectSegmentLocked(RaidSegment seg, List<KeyValuePair<string, string>> writes, bool force)
    {
        if (!Buffers.TryGetValue(seg.Id, out var buf) || buf.Count == 0) return;

        if (!force &&
            LastFlush.TryGetValue(seg.Id, out var last) &&
            DateTime.UtcNow - last < TimeSpan.FromSeconds(3))
        {
            return;
        }

        var sb = new StringBuilder(buf.Count * 200);
        foreach (var l in buf)
        {
            sb.Append("{\"t\":\"").Append(l.Utc.ToString("o")).Append("\",\"l\":\"")
              .Append(l.Level).Append("\",\"m\":\"")
              .Append(JsonEscape(l.Text)).Append("\",\"s\":\"")
              .Append(JsonEscape(string.IsNullOrEmpty(l.Source) ? seg.Instance : l.Source))
              .Append("\"}\n");
        }

        writes.Add(new KeyValuePair<string, string>(SegmentPath(seg), sb.ToString()));
        buf.Clear();
        LastFlush[seg.Id] = DateTime.UtcNow;
    }

    /// <summary>把所有有内容的段收集起来（锁内调用）。</summary>
    private static void CollectFlush(List<KeyValuePair<string, string>> writes, List<RaidSegment> metaWrites)
    {
        foreach (var id in Buffers.Keys.ToList())
        {
            var seg = FindSegment(id);
            if (seg == null) { Buffers.Remove(id); continue; }

            CollectSegmentLocked(seg, writes, force: false);

            // ★ 低频刷新元数据，让面板能看到"进行中"的战局（含实时行数/错误数）
            if (!LastMetaSave.TryGetValue(id, out var lastMeta) ||
                DateTime.UtcNow - lastMeta >= MetaRefreshInterval)
            {
                MetaCache[seg.Id] = seg;
                InvalidateCache();
                metaWrites.Add(seg);
                LastMetaSave[id] = DateTime.UtcNow;
            }
        }

        // 收集本次调用里新建的元数据
        if (_pendingMeta != null && _pendingMeta.Count > 0)
        {
            metaWrites.AddRange(_pendingMeta);
            _pendingMeta.Clear();
        }
    }

    public static void FlushAll(bool force)
    {
        var writes = new List<KeyValuePair<string, string>>();
        var meta = new List<RaidSegment>();

        lock (Gate)
        {
            foreach (var id in Buffers.Keys.ToList())
            {
                var seg = FindSegment(id);
                if (seg == null) { Buffers.Remove(id); continue; }
                CollectSegmentLocked(seg, writes, force);
            }

            if (_pendingMeta != null && _pendingMeta.Count > 0)
            {
                meta.AddRange(_pendingMeta);
                _pendingMeta.Clear();
            }
        }

        PerformWrites(writes, meta);
    }

    private static void Flush(string segmentId, bool force)
    {
        if (!Buffers.TryGetValue(segmentId, out var buf) || buf.Count == 0)
        {
            // ★ 即使没有新行，也要让【进行中】的段元数据保持新鲜，
            //   否则磁盘上的 LineCount 一直是 0，战局列表里就看不到这一局。
            if (force) return;
            if (!LastMetaSave.TryGetValue(segmentId, out var lm) ||
                DateTime.UtcNow - lm >= MetaRefreshInterval)
            {
                var s0 = FindSegment(segmentId);
                if (s0 != null)
                {
                    SaveMeta(s0);
                    LastMetaSave[segmentId] = DateTime.UtcNow;
                }
            }
            return;
        }

        if (!force)
        {
            if (LastFlush.TryGetValue(segmentId, out var last) &&
                DateTime.UtcNow - last < TimeSpan.FromSeconds(3))
            {
                return;
            }
        }

        try
        {
            var seg = FindSegment(segmentId);
            if (seg == null) { Buffers.Remove(segmentId); return; }

            var dir = SegmentDir(seg.Instance);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, seg.FileName);

            var sb = new StringBuilder(buf.Count * 200);
            foreach (var l in buf)
            {
                sb.Append("{\"t\":\"").Append(l.Utc.ToString("o")).Append("\",\"l\":\"")
                  .Append(l.Level).Append("\",\"m\":\"")
                  .Append(JsonEscape(l.Text)).Append("\"}\n");
            }

            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            buf.Clear();
            LastFlush[segmentId] = DateTime.UtcNow;

            TrimSegmentFile(path);

            // ★ 低频刷新元数据，让面板能看到"进行中"的战局（含实时行数/错误数）
            if (!LastMetaSave.TryGetValue(segmentId, out var lastMeta) ||
                DateTime.UtcNow - lastMeta >= MetaRefreshInterval)
            {
                SaveMeta(seg);
                LastMetaSave[segmentId] = DateTime.UtcNow;
            }
        }
        catch
        {
            // Persisting is best-effort; the in-memory buffer keeps working.
        }
    }

    private static readonly Dictionary<string, DateTime> LastMetaSave = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan MetaRefreshInterval = TimeSpan.FromSeconds(20);

    /// <summary>Drop the oldest lines once a segment file grows past the cap.</summary>
    private static void TrimSegmentFile(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length <= MaxLinesPerSegment) return;

            var keep = lines.Skip(lines.Length - MaxLinesPerSegment).ToArray();
            File.WriteAllLines(path, keep, new UTF8Encoding(false));
        }
        catch { }
    }

    private static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // --------------------------------------------------------------- metadata

    private static readonly Dictionary<string, RaidSegment> MetaCache = new(StringComparer.OrdinalIgnoreCase);

    private static RaidSegment FindSegment(string id)
    {
        if (MetaCache.TryGetValue(id, out var s)) return s;
        foreach (var x in AllSegments()) if (x.Id == id) return x;
        return null;
    }

    /// <summary>
    /// Persist a segment's metadata.
    ///
    /// ★ 追加写，不再每次重写整个 meta.jsonl。
    ///
    ///   早期版本每次都是「读全部 + 过滤 + 写全部」—— 而 ServerLogTailer 每 3 秒就灌一次
    ///   服务端日志，每次 Flush 都走到这里。于是一个持锁的请求会做几十毫秒的磁盘 IO，
    ///   面板请求排队等锁，服务端最终完全卡住（实测 CPU 时间冻结、监听消失）。
    ///
    ///   现在只追加一行；读的时候以【最后一条同 Id 记录】为准。文件超过阈值才压缩一次。
    /// </summary>
    private static void SaveMeta(RaidSegment seg)
    {
        MetaCache[seg.Id] = seg;
        InvalidateCache();
        try
        {
            var dir = SegmentDir(seg.Instance);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "meta.jsonl");

            File.AppendAllText(path, JsonSerializer.Serialize(seg, JsonOpts) + "\n",
                               new UTF8Encoding(false));

            CompactMetaIfNeeded(path);
        }
        catch { }
    }

    /// <summary>meta.jsonl 的压缩阈值：超过这个大小才做一次去重重写。</summary>
    private const long MetaCompactBytes = 512 * 1024;

    private static readonly Dictionary<string, DateTime> LastCompact = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rewrite meta.jsonl keeping only the newest record per segment id.
    /// Only runs when the file has grown past the threshold, and at most once a minute.
    /// </summary>
    private static void CompactMetaIfNeeded(string path)
    {
        try
        {
            if (LastCompact.TryGetValue(path, out var last) &&
                DateTime.UtcNow - last < TimeSpan.FromMinutes(1))
            {
                return;
            }

            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length < MetaCompactBytes) return;

            LastCompact[path] = DateTime.UtcNow;

            var newest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var s = JsonSerializer.Deserialize<RaidSegment>(line, JsonOpts);
                    if (s != null) newest[s.Id] = line;
                }
                catch { }
            }

            if (newest.Count == 0) return;

            var tmp = path + ".tmp";
            File.WriteAllLines(tmp, newest.Values, new UTF8Encoding(false));

            // 原子替换，避免读到半个文件
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            InvalidateCache();
        }
        catch { }
    }

    /// <summary>All known segments across all instances, newest first.</summary>
    public static List<RaidSegment> AllSegments()
    {
        // ★ 缓存：Ingest 期间 Flush→FindSegment 会反复走到这里，
        //   每次重扫所有 meta.jsonl 会让上报路径变成 O(段数²)。
        //   2 秒的窗口对"面板显示"完全够用。
        lock (Gate)
        {
            if (_segmentsCache != null && DateTime.UtcNow - _segmentsCacheAt < TimeSpan.FromSeconds(2))
            {
                return _segmentsCache;
            }
        }

        var list = new List<RaidSegment>();
        try
        {
            if (!Directory.Exists(Root))
            {
                lock (Gate) { _segmentsCache = list; _segmentsCacheAt = DateTime.UtcNow; }
                return list;
            }

            foreach (var dir in Directory.GetDirectories(Root))
            {
                var meta = Path.Combine(dir, "meta.jsonl");
                if (!File.Exists(meta)) continue;

                // ★ meta.jsonl 现在是【追加】的，同一个段会有多条记录。
                //   以【最后一条】为准 —— 那正是 SaveMeta 写入最新状态的顺序。
                //   旧版本这里不看重复，界面就会把一局显示成好几段。
                var newest = new Dictionary<string, RaidSegment>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in File.ReadLines(meta, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var s = JsonSerializer.Deserialize<RaidSegment>(line, JsonOpts);
                        if (s != null && !string.IsNullOrEmpty(s.Id)) newest[s.Id] = s;
                    }
                    catch { }
                }

                list.AddRange(newest.Values);
            }
        }
        catch { }

        list = list.OrderByDescending(s => s.StartedUtc).ToList();

        lock (Gate)
        {
            _segmentsCache = list;
            _segmentsCacheAt = DateTime.UtcNow;

            // 让后面 FindSegment 直接命中
            foreach (var s in list) MetaCache[s.Id] = s;
        }

        return list;
    }

    private static List<RaidSegment> _segmentsCache;
    private static DateTime _segmentsCacheAt = DateTime.MinValue;

    /// <summary>写入段元数据后让缓存失效，避免读到旧状态。</summary>
    private static void InvalidateCache()
    {
        lock (Gate) { _segmentsCacheAt = DateTime.MinValue; }
    }

    public static List<LogLine> ReadSegmentLines(RaidSegment seg, int max = 20000)
    {
        var result = new List<LogLine>();
        try
        {
            var path = SegmentPath(seg);
            if (!File.Exists(path)) return result;

            var lines = File.ReadAllLines(path, Encoding.UTF8);
            var start = Math.Max(0, lines.Length - max);
            for (var i = start; i < lines.Length; i++)
            {
                try
                {
                    using var doc = JsonDocument.Parse(lines[i]);
                    var r = doc.RootElement;

                    // ★ 必须用 RoundtripKind：写入的是 "…Z"（UTC），而 DateTime.Parse 默认
                    //   会把它【换算成本地时间】并标 Kind=Local。DateTime 相减又不按 Kind
                    //   换算，于是合并时间线上的 UTC 行会平白偏移一个时区。
                    var tRaw = r.GetProperty("t").GetString();
                    var utc = DateTime.Parse(tRaw, System.Globalization.CultureInfo.InvariantCulture,
                                             System.Globalization.DateTimeStyles.RoundtripKind);

                    result.Add(new LogLine
                    {
                        Utc = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime(),
                        Level = r.TryGetProperty("l", out var l) ? l.GetString() : "",
                        Text = r.TryGetProperty("m", out var m) ? m.GetString() : "",
                        Source = r.TryGetProperty("s", out var s) ? s.GetString() : ""
                    });
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// ★ 战局日志按【局数】保留的上限（0 = 只按时间，不按局数）。
    ///
    /// 原来只有 <see cref="Retention"/>（14 天）这一条规则 —— 一天打 30 局就会攒 30 局，
    /// 时间线的战局下拉框越来越长，每局几十万行的段文件也一直占盘。
    /// 现在额外只保留最新 N 局，更老的【整局】一起删掉。
    ///
    /// 为什么用 BuildRaids 而不是按目录时间：一局包含多个实例的多个段
    /// （无头端 / 每个客户端 / 并入的服务端段），而且断线重连会把同一局拆成好几段。
    /// BuildRaids 才是"什么算一局"的唯一权威，直接复用它，规则不会跑偏。
    /// </summary>
    public const int MaxRaidGroups = 20;

    public static void Prune()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            var cutoff = DateTime.UtcNow - Retention;

            // ① 按时间删（原有规则，保留）
            foreach (var s in AllSegments())
            {
                if (s.StartedUtc >= cutoff) continue;
                try
                {
                    var p = SegmentPath(s);
                    if (File.Exists(p)) File.Delete(p);
                }
                catch { }
            }

            // ② 按局数删：只保留最新 MaxRaidGroups 局
            if (MaxRaidGroups <= 0) return;

            List<RaidGroup> raids;
            try { raids = BuildRaids(int.MaxValue); }   // 已按 StartedUtc 倒序，且只含"真正的一局"
            catch { return; }

            if (raids.Count <= MaxRaidGroups) return;

            // ★ 正在写入的段不能删 —— 删掉后 Flush 会把它重建出来，反而留下残片。
            //   Open 是按【实例名】索引的，所以要取 Values 里的段 id。
            HashSet<string> openIds;
            lock (Gate)
            {
                openIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var seg in Open.Values)
                {
                    if (seg != null && !string.IsNullOrEmpty(seg.Id)) openIds.Add(seg.Id);
                }
            }

            foreach (var oldRaid in raids.Skip(MaxRaidGroups))
            {
                foreach (var seg in oldRaid.Segments)
                {
                    if (seg == null || openIds.Contains(seg.Id)) continue;
                    try
                    {
                        var p = SegmentPath(seg);
                        if (File.Exists(p)) File.Delete(p);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    // -------------------------------------------------------- raid window probe

    /// <summary>上一次观察到"有活跃战局"的 UTC 时刻。</summary>
    private static DateTime _lastRaidSeenUtc = DateTime.MinValue;

    /// <summary>
    /// 现在是否处在【战局窗口】内 —— 有战局正在进行，或刚结束不久。
    ///
    /// 服务端日志 tailer 用它来决定"要不要把这段行交给 LogStore"：SPT 服务端日志里
    /// 绝大部分内容是启动、加载模组、HTTP 轮询，与某一局战局无关；
    /// 只在战局窗口内采集，时间线上的服务端那一列才真正有意义。
    ///
    /// grace 给的是战局【结束之后】的缓冲：结算是靠 /raid/profile/scavsave、
    /// /client/match/local/end 这类请求完成的，它们紧跟战局结束发生。
    /// </summary>
    public static bool IsRaidWindow(int graceSeconds = 60)
    {
        lock (Gate)
        {
            foreach (var seg in Open.Values)
            {
                if (seg != null)
                {
                    _lastRaidSeenUtc = DateTime.UtcNow;
                    return true;
                }
            }

            if (_lastRaidSeenUtc == DateTime.MinValue) return false;

            return DateTime.UtcNow - _lastRaidSeenUtc < TimeSpan.FromSeconds(Math.Max(0, graceSeconds));
        }
    }

    /// <summary>供 tailer 在检测到战局结束的瞬间打点（可选）。</summary>
    public static void MarkRaidActivity()
    {
        lock (Gate) { _lastRaidSeenUtc = DateTime.UtcNow; }
    }

    // ----------------------------------------------------------- line analysis

    /// <summary>
    /// Parse a leading "[yyyy-MM-dd HH:mm:ss.fff]" stamp (SPT server log format) and convert it
    /// to UTC using the server's own time zone.
    ///
    /// Returns null for the BepInEx game logs, which carry no per-line timestamp — those keep
    /// the batch arrival time instead.
    /// </summary>
    private static DateTime? TryParseLeadingTimestamp(string line)
    {
        if (string.IsNullOrEmpty(line) || line[0] != '[') return null;

        // Cheap pre-check before the regex: "[" + 4 digits + "-"
        if (line.Length < 24) return null;
        if (!char.IsDigit(line[1]) || !char.IsDigit(line[2]) ||
            !char.IsDigit(line[3]) || !char.IsDigit(line[4])) return null;

        var m = Regex.Match(line, @"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?\]");
        if (!m.Success) return null;

        if (!DateTime.TryParse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture,
                               System.Globalization.DateTimeStyles.None, out var local))
        {
            return null;
        }

        if (m.Groups[2].Success)
        {
            var frac = m.Groups[2].Value.PadRight(3, '0');
            if (int.TryParse(frac.Substring(0, 3), out var ms))
            {
                local = local.AddMilliseconds(ms);
            }
        }

        // ★ SPT 服务端日志的时间戳是【UTC】，不是本地时间。
        //
        //   实测证据：日志写 [2026-10-14 05:39:27]，而该文件的最后写入时间是本地
        //   2026-10-14 13:39:27 —— 05:39 + 8h = 13:39，即行内时间就是 UTC。
        //
        //   早期版本按"本地时间"转换（再减 8 小时），于是所有服务端行都落到前一天的
        //   小时段里，与战局完全错开。现在直接采用行内时间作为 UTC。
        //
        //   如果某个 SPT 版本改用本地时间，把 serverLogTimestampIsUtc 设为 false 即可。
        if (!ServerLogTimestampIsUtc)
        {
            try
            {
                var offset = TimeZoneInfo.Local.GetUtcOffset(local);
                return new DateTimeOffset(local, offset).UtcDateTime;
            }
            catch
            {
                return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            }
        }

        return DateTime.SpecifyKind(local, DateTimeKind.Utc);
    }

    /// <summary>Whether the leading stamp in the SPT server log is UTC (true, the observed behaviour)
    /// or local wall-clock (false). Mirrors the panel's serverLogTimestampIsUtc config.</summary>
    internal static bool ServerLogTimestampIsUtc = true;

    /// <summary>
    /// 服务端日志在战局结束之后还要继续归属多久（秒）。
    /// 结算走的是 /raid/profile/scavsave、/client/match/local/end，紧跟结束发生。
    /// 由 ServerLogTailer 每轮从配置同步进来。
    /// </summary>
    internal static int ServerLogGraceSecondsCached = 60;

    private enum Boundary { None, RaidStart, RaidEnd }

    private static readonly string[] StartMarkers =
    {
        "Starting on location",
        "Match successfully created",
        "Initialized raid settings",
        "raid location (coop):",
        "Initializing Exfils:",
        "MatchingType: Host",
        "SetupEventsAndExfils 被调用",
        "All players are loaded",
    };

    private static readonly string[] EndMarkers =
    {
        "HeadlessGame] Stop",
        "HeadlessGame] Stop",
        "..:: Fika Server Session Statistics ::..",
        "..:: Fika Client Session Statistics ::..",
        "Time in raid:",
        "对局结算捕获",
        "Destroyed FikaServer",
        "Destroyed FikaClient",
    };

    private static Boundary ClassifyBoundary(string line)
    {
        if (string.IsNullOrEmpty(line)) return Boundary.None;

        // Ends win when a line matches both (shutdown lines mention the raid too).
        foreach (var m in EndMarkers)
        {
            if (line.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return Boundary.RaidEnd;
        }
        foreach (var m in StartMarkers)
        {
            if (line.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return Boundary.RaidStart;
        }
        return Boundary.None;
    }

    /// <summary>
    /// ★ 地图名归一化表。
    ///
    ///   实测踩到的坑：客户端是中文版，日志里写「海关」；无头端是英文版，写「Customs」。
    ///   而分组逻辑有一条「地图不同就拒绝合并」——于是同一局被拆成两条，
    ///   客户端那一栏还显示成单独一局。
    ///
    ///   所以这里把所有别名（中文 / 英文 / 内部 ID）都映射到同一个规范键。
    /// </summary>
    private static readonly Dictionary<string, string> MapAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // 内部 ID → 规范名
        ["bigmap"] = "Customs",
        ["factory4_day"] = "Factory",
        ["factory4_night"] = "Factory",
        // ★ Reserve 在日志里有【三种】写法，每一个都必须收进来：
        //     RezervBase   —— EFT 内部 id（带 z）
        //     ReserveBase  —— 带 e，实测出现 179 次，是日志里最常见的那个
        //     Reserve      —— 规范名
        //   少收任何一个，同一局就会因为"地图名不同"被拆成两条记录。
        ["rezervbase"] = "Reserve",
        ["reservebase"] = "Reserve",
        ["sandbox"] = "GroundZero",
        ["sandbox_high"] = "GroundZero",
        ["tarkovstreets"] = "Streets",
        ["icebreaker"] = "Icebreaker",

        // 英文别名
        ["customs"] = "Customs",
        ["woods"] = "Woods",
        ["interchange"] = "Interchange",
        ["laboratory"] = "Laboratory",
        ["labyrinth"] = "Labyrinth",
        ["lighthouse"] = "Lighthouse",
        ["shoreline"] = "Shoreline",
        ["reserve"] = "Reserve",
        ["streets"] = "Streets",
        ["factory"] = "Factory",
        ["groundzero"] = "GroundZero",
        ["ground_zero"] = "GroundZero",
        ["hideout"] = "Hideout",

        // ★ 中文名（客户端中文版日志）
        ["海关"] = "Customs",
        ["森林"] = "Woods",
        ["立交桥"] = "Interchange",
        ["实验室"] = "Laboratory",
        ["迷宫"] = "Labyrinth",
        ["灯塔"] = "Lighthouse",
        ["海岸线"] = "Shoreline",
        ["储备站"] = "Reserve",
        ["塔科夫街区"] = "Streets",
        ["街区"] = "Streets",
        ["工厂"] = "Factory",
        ["零号区"] = "GroundZero",
        ["藏身处"] = "Hideout",
        ["竞技场"] = "GroundZero",
    };

    /// <summary>把任意写法（中文/英文/内部 ID）折算成规范地图名；认不出就原样返回。</summary>
    private static string NormalizeMap(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var s = raw.Trim();
        if (MapAliases.TryGetValue(s, out var canon)) return canon;

        // ★ 没收录的写法：记一次日志，避免下次又静默地把同一局拆成两条。
        //   同一条只报一次，不会刷屏。
        ReportUnknownMap(s);
        return s;
    }

    /// <summary>已经报告过的不认识的地图名（防止刷屏）。</summary>
    private static readonly HashSet<string> _reportedMaps = new(StringComparer.OrdinalIgnoreCase);

    private static void ReportUnknownMap(string raw)
    {
        try
        {
            lock (Gate)
            {
                if (!_reportedMaps.Add(raw)) return;
            }
            Console.WriteLine("[HeadlessPanel] ★ 未收录的地图名 \"" + raw + "\""
                            + " —— 请在 LogStore.MapAliases 里补上它，否则同一局会因地图名不同被拆成两条。");
        }
        catch { }
    }

    private static readonly string[] KnownMaps =
    {
        "bigmap", "factory4_day", "factory4_night", "interchange", "laboratory", "labyrinth",
        "lighthouse", "rezervbase", "reservebase", "reserve", "sandbox", "sandbox_high",
        "shoreline", "tarkovstreets", "woods", "hideout", "Icebreaker", "Labyrinth",
        // 中文名也参与扫描 —— 客户端中文版日志里只有中文
        "海关", "森林", "立交桥", "实验室", "迷宫", "灯塔", "海岸线", "储备站",
        "塔科夫街区", "街区", "工厂", "零号区", "藏身处",
    };

    private static string ExtractMap(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        // "Starting on location Customs" / "'bigmap'" / "Id factory4_night"
        var m = Regex.Match(line, @"Starting on location\s+([A-Za-z0-9_]+)");
        if (m.Success) return NormalizeMap(m.Groups[1].Value);

        m = Regex.Match(line, @"raid location \(coop\):\s*'([^']+)'");
        if (m.Success) return NormalizeMap(m.Groups[1].Value);

        m = Regex.Match(line, @"Initializing Exfils:\s*Id\s+([A-Za-z0-9_]+)");
        if (m.Success) return NormalizeMap(m.Groups[1].Value);

        // ★ 中文名可能出现在 "Location: 海关" / "地图：海关" 之类的位置，
        //   所以先按已知别名做子串扫描（长名优先，避免「街区」抢「塔科夫街区」）。
        foreach (var map in KnownMaps)
        {
            if (line.IndexOf(map, StringComparison.OrdinalIgnoreCase) >= 0) return NormalizeMap(map);
        }
        return null;
    }

    private static string ExtractRaidCode(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        var m = Regex.Match(line, @"Raid Code:\s*([A-Za-z0-9]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Notable events worth surfacing on the raid card.</summary>
    private static string ExtractEvent(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        // ★ 具体标记必须排在泛化标记前面：一行里同时出现 "Reconnect Button" 和
        //   "已拦截" 时，先匹配宽泛的 "Reconnect" 会把真正有意义的标记吞掉。
        if (line.IndexOf("已拦截", StringComparison.Ordinal) >= 0) return "★ 拦截结算";
        if (line.IndexOf("保留重连", StringComparison.Ordinal) >= 0) return "★ 保留重连";
        if (line.IndexOf("INVENTORY NOT IN SYNC", StringComparison.OrdinalIgnoreCase) >= 0) return "★ 物品不同步";
        if (line.IndexOf("host dropped empty resync", StringComparison.OrdinalIgnoreCase) >= 0) return "★ 空 resync 被弃";
        if (line.IndexOf("client dropped empty resync", StringComparison.OrdinalIgnoreCase) >= 0) return "空 resync 被弃";
        if (line.IndexOf("answering resync request", StringComparison.OrdinalIgnoreCase) >= 0) return "resync 应答";
        if (line.IndexOf("sent resync request", StringComparison.OrdinalIgnoreCase) >= 0) return "resync 请求";
        if (line.IndexOf("Could not find item", StringComparison.OrdinalIgnoreCase) >= 0) return "物品找不到";
        if (line.IndexOf("Not enough space in target", StringComparison.OrdinalIgnoreCase) >= 0) return "★ 主机空间不足";
        if (line.IndexOf("rejected by server", StringComparison.OrdinalIgnoreCase) >= 0) return "★ 操作被服务端拒绝";
        if (line.IndexOf("Peer disconnected", StringComparison.OrdinalIgnoreCase) >= 0) return "玩家断开";
        if (line.IndexOf("Connection established", StringComparison.OrdinalIgnoreCase) >= 0) return "玩家连接";
        if (line.IndexOf("Reconnect", StringComparison.OrdinalIgnoreCase) >= 0) return "★ 重连相关";
        if (line.IndexOf("Tripwire", StringComparison.OrdinalIgnoreCase) >= 0) return "绊雷同步";
        if (line.IndexOf("transit", StringComparison.OrdinalIgnoreCase) >= 0 &&
            line.IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) >= 0) return "转移点触发";
        return null;
    }

    private static string ExtractLevel(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";
        if (line.IndexOf("[Error", StringComparison.OrdinalIgnoreCase) >= 0) return "Error";
        if (line.IndexOf("[Fatal", StringComparison.OrdinalIgnoreCase) >= 0) return "Error";
        if (line.IndexOf("[Warning", StringComparison.OrdinalIgnoreCase) >= 0) return "Warning";
        if (line.IndexOf("[Info", StringComparison.OrdinalIgnoreCase) >= 0) return "Info";
        if (line.IndexOf("[Debug", StringComparison.OrdinalIgnoreCase) >= 0) return "Debug";
        if (line.IndexOf("[Message", StringComparison.OrdinalIgnoreCase) >= 0) return "Message";
        return "";
    }

    // ------------------------------------------------------------------ raids

    /// <summary>A raid = a set of segments from any instance whose map matches and whose time ranges overlap.</summary>
    public sealed class RaidGroup
    {
        public string Key { get; set; } = "";
        public string Map { get; set; } = "";
        public string RaidCode { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public List<RaidSegment> Segments { get; set; } = new();
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public bool HasReconnect { get; set; }
        public int ReconnectCount { get; set; }

        /// <summary>
        /// ★ 这一局里各端各贡献了多少行 —— 按【行的真实来源】统计，而不是段所属实例。
        ///
        ///   服务端日志会并入客户端/无头端的段，只看 Segments[].Instance 就看不到
        ///   SPT-Server 这一端。界面用这个字段显示"本局有哪几端"。
        /// </summary>
        public Dictionary<string, int> SourceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>来源列表（按行数降序），供界面直接渲染。</summary>
        public List<string> Sources => SourceCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// 补齐段的来源统计。
    ///
    /// ★ SourceCounts 是后加的字段，旧段的 meta.jsonl 里没有它 —— 那些段在
    ///   Ingest 时把服务端行并了进来，但没记账，界面就看不到 SPT-Server。
    ///   这里对缺失的段读一次文件统计并回填（段对象由 MetaCache 常驻，只算一次）。
    /// </summary>
    private static void EnsureSourceCounts(RaidSegment seg)
    {
        if (seg == null) return;
        if (seg.SourceCounts != null && seg.SourceCounts.Count > 0) return;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var l in ReadSegmentLines(seg, 300000))
            {
                var key = string.IsNullOrEmpty(l.Source) ? seg.Instance : l.Source;
                if (string.IsNullOrEmpty(key)) continue;
                counts.TryGetValue(key, out var have);
                counts[key] = have + 1;
            }
        }
        catch { }

        if (counts.Count == 0)
        {
            // 读不出来就至少记上段自己的实例，别让界面显示空白
            if (!string.IsNullOrEmpty(seg.Instance) && seg.LineCount > 0)
            {
                counts[seg.Instance] = seg.LineCount;
            }
        }

        seg.SourceCounts = counts;
    }

    /// <summary>
    /// Group segments into raids. Reconnects are handled naturally: a reconnect produces a new
    /// segment on the same map within the join window, so it lands in the same raid and is
    /// counted as a reconnect rather than becoming a separate raid.
    /// </summary>
    public static List<RaidGroup> BuildRaids(int maxRaids = 200)
    {
        var segments = AllSegments()
            // ★ 排除"游离段"（没有战局边界标记的行，例如 BepInEx 启动日志）。
            //   但 SPT 服务端日志【全部】都是游离段 —— 它根本没有 "Starting on location"
            //   这类标记 —— 所以服务端角色的段必须保留，否则服务端侧永远进不了合并时间线。
            //   它们靠下面的时间重叠并入最近的战局。
            .Where(s => !s.Id.Contains("#idle#") ||
                        string.Equals(s.Role, "server", StringComparison.OrdinalIgnoreCase))
            .Where(s => s.LineCount > 0)     // ★ 安全网：0 行的幽灵段不进战局列表
            .OrderBy(s => s.StartedUtc)
            .ToList();

        var groups = new List<RaidGroup>();

        foreach (var seg in segments)
        {
            RaidGroup target = null;

            // ★ 无地图段（典型是 SPT 服务端日志）只能靠时间关联。
            //   早期版本取【第一个】满足重叠/相邻的组，而组的遍历顺序是创建顺序，
            //   于是服务端日志经常被一个更早、只是碰巧还在窗口内的组抢走。
            //   现在对这类段挑【时间上最接近】的组。
            RaidGroup best = null;
            var bestDistance = TimeSpan.MaxValue;
            var mapless = string.IsNullOrEmpty(seg.Map);

            foreach (var g in groups)
            {
                // ★ 一个组如果【一个带地图的段都没有】，它就不是"某一局"，
                //   而是几个游离段碰巧凑在一起（例如按小时切的 SPT 服务端日志）。
                //   这种组绝不能当锚点 —— 它的时间范围是整整一小时，
                //   会把前后两局都吸进来，界面上的时长就会变成"63 分钟"。
                if (string.IsNullOrEmpty(g.Map)) continue;

                // ★★★ Raid Code 是【最高】判据，必须排在地图名之前。
                //
                //   之前的顺序是「先比地图、再比 Code」，结果实测踩坑：
                //   同一局 ReserverBase/Reserve 两种写法 → 地图那一步就 continue 了，
                //   Code 判断根本轮不到执行 → 同一局被拆成两条。
                //   既然 Code 相同就铁定是同一局，地图名、时间都不该有机会否决它。
                if (!string.IsNullOrEmpty(g.RaidCode) && !string.IsNullOrEmpty(seg.RaidCode))
                {
                    if (!string.Equals(g.RaidCode, seg.RaidCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;                 // Code 不同 → 绝不是同一局
                    }

                    // Code 相同 —— 不管隔多久、不管地图名怎么写，都是同一局
                    if (!mapless) { target = g; break; }

                    var sameCodeDist = (seg.StartedUtc - (g.EndedUtc ?? DateTime.MaxValue)).Duration();
                    if (sameCodeDist < bestDistance) { bestDistance = sameCodeDist; best = g; }
                    continue;
                }

                // 双方至少有一边没有 Code —— 这时才退回地图名 + 时间判断
                if (!string.IsNullOrEmpty(seg.Map) &&
                    !string.Equals(g.Map, seg.Map, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // ★ 同一实例已经在这个组里出现过 → 只有"快速重连"才并进来。
                //   否则那多半是【下一局】（该实例上一段已结束，隔了一段时间又开一段）。
                var sameInstance = g.Segments
                    .Where(s => string.Equals(s.Instance, seg.Instance, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (sameInstance.Count > 0 && !mapless)
                {
                    var lastEnd = sameInstance.Max(s => s.EndedUtc ?? DateTime.MaxValue);
                    var gapToSelf = seg.StartedUtc - lastEnd;

                    if (gapToSelf > ReconnectWindow)
                    {
                        continue;                 // 不是重连，是下一局
                    }
                }

                var gEnd = g.EndedUtc ?? DateTime.MaxValue;
                var sEnd = seg.EndedUtc ?? DateTime.MaxValue;
                var overlapStart = seg.StartedUtc < g.StartedUtc ? g.StartedUtc : seg.StartedUtc;
                var overlapEnd = sEnd < gEnd ? sEnd : gEnd;
                var overlap = overlapEnd - overlapStart;

                // Overlapping ranges join; abutting ones join if within the window.
                var gap = seg.StartedUtc - gEnd;
                var near = gap <= JoinWindow && gap > -JoinWindow;

                if (overlap <= TimeSpan.Zero && !near)
                {
                    continue;
                }

                if (!mapless)
                {
                    target = g;
                    break;
                }

                // 有地图的段按上面直接命中；无地图段比较"离得有多近"。
                var distance = gap.Duration();
                if (overlap > TimeSpan.Zero) distance = TimeSpan.Zero;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = g;
                }
            }

            if (mapless) target = best;

            if (target == null)
            {
                target = new RaidGroup
                {
                    Key = seg.Id,
                    Map = seg.Map,
                    StartedUtc = seg.StartedUtc,
                };
                groups.Add(target);
            }

            target.Segments.Add(seg);
            if (string.IsNullOrEmpty(target.Map) && !string.IsNullOrEmpty(seg.Map)) target.Map = seg.Map;
            if (!string.IsNullOrEmpty(seg.RaidCode)) target.RaidCode = seg.RaidCode;

            // ★ 汇总这一局各端的行数（含被并入的服务端行）
            EnsureSourceCounts(seg);
            if (seg.SourceCounts != null)
            {
                foreach (var kv in seg.SourceCounts)
                {
                    target.SourceCounts.TryGetValue(kv.Key, out var have);
                    target.SourceCounts[kv.Key] = have + kv.Value;
                }
            }
            else if (!string.IsNullOrEmpty(seg.Instance))
            {
                target.SourceCounts.TryGetValue(seg.Instance, out var have);
                target.SourceCounts[seg.Instance] = have + seg.LineCount;
            }

            // ★ 组的起止时间只由【有地图的段】决定。
            //
            //   无地图段（SPT 服务端日志）是按小时切的整点段，例如 15:00~16:00。
            //   如果让它参与，界面就会把一局 6 分钟的仗显示成"48 分钟"，
            //   因为它的开始时间是整点而不是真正的开局时刻。
            if (!string.IsNullOrEmpty(seg.Map))
            {
                if (target.StartedUtc == default || seg.StartedUtc < target.StartedUtc) target.StartedUtc = seg.StartedUtc;

                var sEnd2 = seg.EndedUtc;
                if (sEnd2.HasValue && (!target.EndedUtc.HasValue || sEnd2 > target.EndedUtc))
                {
                    target.EndedUtc = sEnd2;
                }
            }
            else if (target.StartedUtc == default)
            {
                // 组里目前只有无地图段 —— 先用它兜底，等有地图的段进来会被覆盖
                target.StartedUtc = seg.StartedUtc;
            }

            target.ErrorCount += seg.ErrorCount;
            target.WarningCount += seg.WarningCount;

            var rcs = seg.Events.Count(e => e.Contains("重连"));
            if (rcs > 0)
            {
                target.HasReconnect = true;
                target.ReconnectCount += rcs;
            }
        }

        // A raid whose own instance produced several segments is itself a reconnect indicator.
        foreach (var g in groups)
        {
            var perInstance = g.Segments.GroupBy(s => s.Instance, StringComparer.OrdinalIgnoreCase);
            foreach (var grp in perInstance)
            {
                if (grp.Count() > 1)
                {
                    g.HasReconnect = true;
                    g.ReconnectCount += grp.Count() - 1;
                }
            }
        }

        // ★ 只返回"真正的一局"：必须至少有一个带地图的段。
        //
        //   否则那些没并进任何战局的游离段（例如 SPT 服务端在战局外收到的 41 行）
        //   会以"地图为空、时长 0 分 0 秒"的形式出现在战局列表里 —— 那是噪音。
        return groups
            .Where(g => !string.IsNullOrEmpty(g.Map))
            .OrderByDescending(g => g.StartedUtc)
            .Take(maxRaids)
            .ToList();
    }
}
