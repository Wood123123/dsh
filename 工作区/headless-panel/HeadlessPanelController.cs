using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsh.HeadlessPanel;

/// <summary>
/// Serves the headless-client management panel.
///
/// Live state (who is connected, which map a raid is on) is read in-process from Fika via
/// <see cref="FikaBridge"/> — no HTTP, no apiKey, and no touch of Fika's profile cache, which
/// logs a warning on refresh whenever two profiles share a PMC nickname.
///
/// Only "restart" still goes over Fika's HTTP API, because that path owns the shutdown message
/// the headless client listens for. The API key stays on the server.
///
/// [Authorize] rides on SPT's own cookie auth (the /login page of the SPT web UI), so only
/// people who can already log into the web interface can touch this panel.
/// </summary>
[ApiController]
[Authorize]
public sealed class HeadlessPanelController : ControllerBase
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, DateTime> StartCooldowns = new();
    private static readonly object CooldownGate = new();

    /// <summary>When each instance was first seen as "process up but not connected yet".</summary>
    private static readonly Dictionary<string, DateTime> StartingSince = new();

    /// <summary>After this long in "starting", the instance is reported as stuck rather than starting.</summary>
    private const int StuckAfterSeconds = 300;

    private readonly IServiceProvider _services;

    public HeadlessPanelController(IServiceProvider services)
    {
        _services = services;

        // ★ 面板跑在 SPT 服务端进程里，所以能直接 tail 服务端自己的日志 ——
        //   客户端/无头端要插件上报（跨机器），服务端不用。幂等，重复调用无副作用。
        ServerLogTailer.Start();
    }

    // ---------------------------------------------------------------- endpoints

    [HttpGet("/headlesspanel")]
    [Produces("text/html")]
    public IActionResult Index()
    {
        return Content(PageHtml.Value, "text/html; charset=utf-8");
    }

    /// <summary>
    /// One row per configured headless install, merged with what Fika reports and what the
    /// process table says, so the UI can distinguish connected / starting / stopped.
    /// </summary>
    [HttpGet("/headlesspanel/api/list")]
    public IActionResult List()
    {
        var config = PanelConfig.Current;
        var connected = FikaBridge.GetHeadlessStates(_services)
            .ToDictionary(h => h.ProfileId, h => h, StringComparer.OrdinalIgnoreCase);

        var liveProcesses = SnapshotHeadlessProcesses();

        var rows = new List<object>();
        foreach (var instance in config.HeadlessInstances)
        {
            connected.TryGetValue(instance.ProfileId ?? "", out var state);

            var live = liveProcesses
                .Where(p => PathsOverlap(p.ExecutablePath, instance.WorkingDirectory))
                .ToList();
            var running = live.Count > 0;

            var startingFor = TrackStarting(instance.ProfileId, state == null && running);

            // "starting" = the process is up but Fika has not accepted its socket yet. Once that
            // has gone on too long it is almost always an orphaned game process whose manager
            // died, so report it as stuck instead of pretending it is still coming up.
            string status;
            if (state != null)
            {
                status = state.Players > 0 ? "inraid" : "ready";
            }
            else if (running)
            {
                status = startingFor >= StuckAfterSeconds ? "stuck" : "starting";
            }
            else
            {
                status = "stopped";
            }

            rows.Add(new
            {
                name = instance.Name,
                profileId = instance.ProfileId,
                enabled = instance.Enabled,
                status,
                state = state?.State ?? -1,
                players = state?.Players ?? 0,
                level = state?.Level ?? 0,
                socketOpen = state?.SocketOpen ?? false,
                cooldownSeconds = RemainingCooldown(instance.ProfileId),
                startingSeconds = startingFor,
                processes = live.Select(p => new { name = p.Name, pid = p.Pid })
            });
        }

        // Anything Fika knows about that is not in the config still deserves a row.
        foreach (var orphan in connected.Values.Where(c =>
                     !config.HeadlessInstances.Any(i =>
                         string.Equals(i.ProfileId, c.ProfileId, StringComparison.OrdinalIgnoreCase))))
        {
            rows.Add(new
            {
                name = "(未配置)",
                profileId = orphan.ProfileId,
                enabled = false,
                status = orphan.Players > 0 ? "inraid" : "ready",
                state = orphan.State,
                players = orphan.Players,
                level = orphan.Level,
                socketOpen = orphan.SocketOpen,
                cooldownSeconds = 0,
                startingSeconds = 0,
                processes = Array.Empty<object>()
            });
        }

        return Content(JsonSerializer.Serialize(new
        {
            fikaLoaded = FikaBridge.FikaLoaded,
            allowStart = config.AllowStart,
            headlessClients = rows
        }), "application/json; charset=utf-8");
    }

    /// <summary>Online players plus the map each one is in, straight from Fika's presence service.</summary>
    [HttpGet("/headlesspanel/api/players")]
    public IActionResult Players()
    {
        var players = FikaBridge.GetPlayerStates(_services);

        return Content(JsonSerializer.Serialize(new
        {
            fikaLoaded = FikaBridge.FikaLoaded,
            players = players.Select(p => new
            {
                nickname = p.Nickname,
                level = p.Level,
                activity = p.Activity,
                activityText = ActivityText(p.Activity),
                location = p.Location,
                locationText = MapName(p.Location),
                raidStarted = p.RaidStarted,
                matchId = p.MatchId
            })
        }), "application/json; charset=utf-8");
    }

    /// <summary>Launch a configured headless install's manager.</summary>
    [HttpPost("/headlesspanel/api/start")]
    public IActionResult Start([FromBody] JsonElement body)
    {
        var config = PanelConfig.Current;
        if (!config.AllowStart)
        {
            Audit("start", body, "", false, "配置里已禁止启动");
            return StatusCode(403, new { ok = false, error = "配置里已禁止启动" });
        }

        if (!TryProfileId(body, out var profileId, out var error))
        {
            Audit("start", body, "", false, error);
            return BadRequest(new { ok = false, error });
        }

        var instance = config.HeadlessInstances.FirstOrDefault(i =>
            string.Equals(i.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

        if (instance == null)
        {
            Audit("start", null, profileId, false, "配置里没有这个 profileId");
            return NotFound(new { ok = false, error = "配置里没有这个 profileId" });
        }

        if (!instance.Enabled)
        {
            Audit("start", null, profileId, false, "该实例已禁用", instance.Name);
            return StatusCode(403, new { ok = false, error = "该实例已禁用" });
        }

        if (RemainingCooldown(profileId) > 0)
        {
            var left = RemainingCooldown(profileId);
            Audit("start", null, profileId, false, $"冷却中，还需 {left} 秒", instance.Name);
            return Content(JsonSerializer.Serialize(new
            {
                ok = false,
                error = $"冷却中，还需 {left} 秒",
                cooldownSeconds = left
            }), "application/json; charset=utf-8");
        }

        try
        {
            if (!System.IO.File.Exists(instance.ManagerPath))
            {
                Audit("start", null, profileId, false, $"找不到启动器：{instance.ManagerPath}", instance.Name);
                return Content(JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = $"找不到启动器：{instance.ManagerPath}"
                }), "application/json; charset=utf-8");
            }

            var psi = new ProcessStartInfo
            {
                FileName = instance.ManagerPath,
                WorkingDirectory = string.IsNullOrWhiteSpace(instance.WorkingDirectory)
                    ? Path.GetDirectoryName(instance.ManagerPath)
                    : instance.WorkingDirectory,
                UseShellExecute = true
            };

            var proc = Process.Start(psi);
            SetCooldown(profileId, config.StartCooldownSeconds);

            Audit("start", null, profileId, true, $"已拉起管理器 PID={proc?.Id ?? 0}", instance.Name);

            return Content(JsonSerializer.Serialize(new
            {
                ok = true,
                profileId,
                pid = proc?.Id ?? 0,
                message = "已发起启动；无头端完成初始化并连上服务端后状态会自动变为就绪"
            }), "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            Audit("start", null, profileId, false, "启动失败：" + ex.Message, instance.Name);
            return Content(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "启动失败：" + ex.Message
            }), "application/json; charset=utf-8");
        }
    }

    /// <summary>
    /// Force-stop a headless install by killing the processes rooted in its directory.
    ///
    /// The manager is killed first: it is a supervisor that relaunches the game whenever the
    /// game exits, so killing the game first would simply make it spawn a new one. This is also
    /// the only way to clear an orphaned game process whose manager already died — Fika's
    /// restart endpoint cannot help there, because Fika never saw the client connect.
    /// </summary>
    [HttpPost("/headlesspanel/api/stop")]
    public IActionResult Stop([FromBody] JsonElement body)
    {
        if (!TryProfileId(body, out var profileId, out var error))
        {
            Audit("stop", body, "", false, error);
            return BadRequest(new { ok = false, error });
        }

        var instance = PanelConfig.Current.HeadlessInstances.FirstOrDefault(i =>
            string.Equals(i.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

        if (instance == null)
        {
            Audit("stop", null, profileId, false, "配置里没有这个 profileId");
            return NotFound(new { ok = false, error = "配置里没有这个 profileId" });
        }

        // Manager first so it cannot relaunch the game, then the game itself.
        var killed = new List<object>();

        foreach (var name in new[] { "FikaHeadlessManager", "EscapeFromTarkov" })
        {
            foreach (var target in FindProcessesIn(name, instance.WorkingDirectory))
            {
                var ok = TryKill(target.Pid, out var detail);
                killed.Add(new { name = target.Name, pid = target.Pid, ok, detail });
            }
        }

        var failed = killed.Count(k => !(bool)k.GetType().GetProperty("ok").GetValue(k));
        var summary = killed.Count == 0
            ? "该实例本来就没有进程在跑"
            : $"已结束 {killed.Count} 个进程" + (failed > 0 ? $"（{failed} 个失败）" : "");

        Audit("stop", null, profileId, failed == 0, summary, instance.Name);

        // Nothing to kill is a success too: the desired end state is "not running".
        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            profileId,
            killed,
            message = summary
        }), "application/json; charset=utf-8");
    }

    /// <summary>Restart a connected headless via Fika's own shutdown-message API.</summary>
    [HttpPost("/headlesspanel/api/restart")]
    public async Task<IActionResult> Restart([FromBody] JsonElement body)
    {
        if (!TryProfileId(body, out var profileId, out var error))
        {
            Audit("restart", body, "", false, error);
            return BadRequest(new { ok = false, error });
        }

        var instance = PanelConfig.Current.HeadlessInstances.FirstOrDefault(i =>
            string.Equals(i.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

        var payload = JsonSerializer.Serialize(new { profileId });
        var (ok, detail) = await CallFikaAsync(HttpMethod.Post, "/fika/api/restartheadless", payload);

        Audit("restart", null, profileId, ok, ok ? "已发送重启指令" : ("重启失败：" + detail), instance?.Name ?? "");

        return Content(JsonSerializer.Serialize(new
        {
            ok,
            profileId,
            detail
        }), "application/json; charset=utf-8");
    }

    /// <summary>
    /// Persistent audit trail: which address operated which headless, when, and with what result.
    /// Read fresh from disk on every call, so the history is visible to everyone on every login.
    /// </summary>
    [HttpGet("/headlesspanel/api/audit")]
    public IActionResult AuditTrail([FromQuery] int? limit)
    {
        var entries = AuditLog.Read(limit ?? 200);

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            total = AuditLog.Count(),
            returned = entries.Count,
            file = AuditLog.FilePath,
            entries = entries.Select(e => new
            {
                utc = e.Utc,
                ip = e.Ip,
                account = e.Account,
                action = e.Action,
                actionText = ActionText(e.Action),
                profileId = e.ProfileId,
                instance = e.Instance,
                ok = e.Ok,
                detail = e.Detail,
                agent = e.Agent
            })
        }), "application/json; charset=utf-8");
    }

    internal static string ActionText(string action)
    {
        switch ((action ?? "").ToLowerInvariant())
        {
            case "start": return "启动";
            case "stop": return "关闭";
            case "restart": return "重启";
            default: return action ?? "";
        }
    }

    /// <summary>Record an audit entry, pulling profileId out of the body when the caller had one.</summary>
    private void Audit(string action, JsonElement? body, string profileId, bool ok,
                       string detail, string instance = "")
    {
        try
        {
            if (string.IsNullOrEmpty(profileId) && body.HasValue && body.Value.ValueKind == JsonValueKind.Object &&
                body.Value.TryGetProperty("profileId", out var p) && p.ValueKind == JsonValueKind.String)
            {
                profileId = p.GetString() ?? "";
            }
        }
        catch { /* fall through with whatever we have */ }

        AuditLog.Record(HttpContext, action, profileId, instance, ok, detail);
    }

    // ------------------------------------------------------- telemetry (headless-side)

    /// <summary>
    /// Live FPS / AI counts / clients per headless, plus the recent trend.
    ///
    /// The data does not exist in this process: it lives inside each EscapeFromTarkov.exe,
    /// written every second by the HeadlessTelemetry BepInEx plugin to
    /// &lt;instance&gt;\BepInEx\config\headless-telemetry\live.json. We just read those files.
    /// </summary>
    [HttpGet("/headlesspanel/api/telemetry")]
    public IActionResult Telemetry()
    {
        var config = PanelConfig.Current;
        var list = new List<object>();

        foreach (var instance in config.HeadlessInstances)
        {
            var live = ReadJsonSafe(LiveFile(instance), 4 * 1024 * 1024);
            double age = -1;
            try
            {
                var fi = new FileInfo(LiveFile(instance));
                if (fi.Exists)
                {
                    age = (DateTime.UtcNow - fi.LastWriteTimeUtc).TotalSeconds;
                }
            }
            catch { }

            list.Add(new
            {
                name = instance.Name,
                profileId = instance.ProfileId,
                instanceDir = InstanceKey(instance),
                // 面板据此区分"没装采集插件 / 无头端没跑 / 数据陈旧"
                available = live != null,
                stale = live != null && age > 10,
                ageSeconds = age < 0 ? (double?)null : Math.Round(age, 1),
                payload = live
            });
        }

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            serverUtc = DateTime.UtcNow,
            instances = list
        }), "application/json; charset=utf-8");
    }

    /// <summary>List the stored raid logs (newest first), up to the plugin's retention cap.</summary>
    [HttpGet("/headlesspanel/api/raids")]
    public IActionResult Raids([FromQuery] string profileId)
    {
        var instance = FindInstance(profileId, out var error);
        if (instance == null)
        {
            return BadRequest(new { ok = false, error });
        }

        var dir = RaidDir(instance);
        var rows = new List<object>();

        try
        {
            if (Directory.Exists(dir))
            {
                foreach (var f in new DirectoryInfo(dir).GetFiles("*.json")
                             .OrderByDescending(x => x.Name, StringComparer.Ordinal))
                {
                    // Read only the header of each file: the series can be large.
                    var summary = ReadRaidSummary(f.FullName);
                    rows.Add(new
                    {
                        file = f.Name,
                        sizeBytes = f.Length,
                        modifiedUtc = f.LastWriteTimeUtc,
                        summary
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return Content(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "读取战局日志目录失败：" + ex.Message
            }), "application/json; charset=utf-8");
        }

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            profileId,
            instanceDir = InstanceKey(instance),
            directory = dir,
            count = rows.Count,
            raids = rows
        }), "application/json; charset=utf-8");
    }

    /// <summary>Full contents of one stored raid log, including its sample series.</summary>
    [HttpGet("/headlesspanel/api/raid")]
    public IActionResult Raid([FromQuery] string profileId, [FromQuery] string file)
    {
        var instance = FindInstance(profileId, out var error);
        if (instance == null)
        {
            return BadRequest(new { ok = false, error });
        }

        // ★ Never trust the caller with a path: only a bare *.json file name is accepted.
        if (string.IsNullOrWhiteSpace(file) ||
            !Regex.IsMatch(file, @"^[A-Za-z0-9_.\-]{1,120}\.json$") ||
            file.Contains(".."))
        {
            return BadRequest(new { ok = false, error = "文件名不合法" });
        }

        var path = Path.Combine(RaidDir(instance), file);
        var json = ReadJsonSafe(path, 32 * 1024 * 1024);
        if (json == null)
        {
            return NotFound(new { ok = false, error = "战局日志不存在或无法读取" });
        }

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            profileId,
            file,
            payload = json
        }), "application/json; charset=utf-8");
    }

    // ------------------------------------------------------- telemetry helpers

    /// <summary>Game-directory name, matching what the telemetry plugin uses as its instance key.</summary>
    private static string InstanceKey(HeadlessInstance instance)
    {
        try
        {
            var dir = (instance.WorkingDirectory ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(dir);
        }
        catch
        {
            return instance.ProfileId ?? "";
        }
    }

    internal static string TelemetryDir(HeadlessInstance instance)
        => Path.Combine(instance.WorkingDirectory ?? "", "BepInEx", "config", "headless-telemetry");

    internal static string LiveFile(HeadlessInstance instance)
        => Path.Combine(TelemetryDir(instance), "live.json");

    internal static string RaidDir(HeadlessInstance instance)
        => Path.Combine(TelemetryDir(instance), "raids", InstanceKey(instance));

    private HeadlessInstance FindInstance(string profileId, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(profileId) || !Regex.IsMatch(profileId, "^[0-9a-fA-F]{24}$"))
        {
            error = "profileId 必须是 24 位十六进制";
            return null;
        }

        var instance = PanelConfig.Current.HeadlessInstances.FirstOrDefault(i =>
            string.Equals(i.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

        if (instance == null)
        {
            error = "配置里没有这个 profileId";
        }

        return instance;
    }

    /// <summary>Parse a JSON file into a JsonElement, or null when missing/unreadable.</summary>
    private static JsonElement? ReadJsonSafe(string path, long maxBytes)
    {
        try
        {
            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > maxBytes)
            {
                return null;
            }

            // The plugin rewrites this file every second, so a read can land mid-write.
            // A failed parse just means "no fresh data this poll" — the next one succeeds.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Read just the stats header of a raid log without materialising its series.</summary>
    private static JsonElement? ReadRaidSummary(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > 32 * 1024 * 1024)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var o = new Dictionary<string, object?>
            {
                ["map"] = root.TryGetProperty("map", out var m) ? m.GetString() : null,
                ["startedUtc"] = root.TryGetProperty("startedUtc", out var s) ? s.GetString() : null,
                ["endedUtc"] = root.TryGetProperty("endedUtc", out var e) ? e.GetString() : null,
                ["durationSec"] = root.TryGetProperty("durationSec", out var d) ? d.GetDouble() : (double?)null,
                ["samples"] = root.TryGetProperty("samples", out var c) ? c.GetInt32() : (int?)null
            };

            if (root.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in stats.EnumerateObject())
                {
                    o[prop.Name] = prop.Value.ValueKind == JsonValueKind.Number
                        ? prop.Value.GetDouble()
                        : (object?)prop.Value.ToString();
                }
            }

            return JsonSerializer.SerializeToElement(o);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------- log aggregation (route B)

    /// <summary>Body posted by the LogShipper plugins.</summary>
    public sealed class LogIngestRequest
    {
        public string Instance { get; set; } = "";
        public string Role { get; set; } = "";
        public string Machine { get; set; } = "";
        public DateTime SentUtc { get; set; }
        public long Seq { get; set; }
        public List<string> Lines { get; set; } = new();
    }

    /// <summary>
    /// Receive a batch of log lines from an in-game LogShipper.
    ///
    /// [AllowAnonymous] is deliberate, and the token is OPTIONAL:
    ///
    ///   · The shipper runs inside EscapeFromTarkov.exe and has no SPT browser session, so
    ///     cookie auth is impossible.
    ///   · Requiring a shared secret would mean configuring every client by hand — which is
    ///     exactly the friction we want to avoid.
    ///
    /// So by default the endpoint is open. That is acceptable here because it can only append
    /// log lines to a file: it cannot start, stop, restart or reconfigure anything (those all
    /// still require SPT login), and the server is only reachable from the VPN/LAN the game
    /// already uses.
    ///
    /// If you ever want to lock it down, set <see cref="HeadlessPanelConfig.LogIngestToken"/>
    /// and put the same value in each shipper's config — the check below then applies.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("/headlesspanel/api/logs/ingest")]
    public IActionResult LogsIngest([FromBody] LogIngestRequest body)
    {
        var expected = PanelConfig.Current.LogIngestToken ?? "";

        // 只有面板【显式配置】了令牌才校验；留空 = 不需要客户端做任何配置。
        if (!string.IsNullOrEmpty(expected))
        {
            var presented = Request.Headers["X-LogShipper-Token"].FirstOrDefault() ?? "";
            if (!FixedTimeEquals(expected, presented))
            {
                return Unauthorized(new { ok = false, error = "token 不匹配" });
            }
        }

        if (body == null || body.Lines == null || body.Lines.Count == 0)
        {
            return Content("{\"ok\":true,\"accepted\":0}", "application/json; charset=utf-8");
        }

        // 匿名端点必须自带资源保护：单批行数上限，避免一次请求把内存/磁盘打满。
        const int maxLinesPerRequest = 20000;
        if (body.Lines.Count > maxLinesPerRequest)
        {
            body.Lines = body.Lines.GetRange(body.Lines.Count - maxLinesPerRequest, maxLinesPerRequest);
        }

        var sent = body.SentUtc == default ? DateTime.UtcNow : DateTime.SpecifyKind(body.SentUtc, DateTimeKind.Utc);

        var r = LogStore.Ingest(body.Instance, body.Role, body.Machine, sent, body.Lines);

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            accepted = r.Accepted,
            segmentsOpened = r.SegmentsOpened,
            segmentsClosed = r.SegmentsClosed
        }), "application/json; charset=utf-8");
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    /// <summary>Raids rebuilt from every instance's segments, newest first.</summary>
    [HttpGet("/headlesspanel/api/logs/raids")]
    public IActionResult LogsRaids([FromQuery] int? limit)
    {
        var groups = LogStore.BuildRaids(limit ?? 100);

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            root = LogStore.Root,
            count = groups.Count,
            raids = groups.Select(g => new
            {
                key = g.Key,
                map = g.Map,
                mapText = MapName(g.Map),
                raidCode = g.RaidCode,
                startedUtc = g.StartedUtc,
                endedUtc = g.EndedUtc,
                durationSec = g.EndedUtc.HasValue
                    ? Math.Round((g.EndedUtc.Value - g.StartedUtc).TotalSeconds, 1)
                    : (double?)null,
                errors = g.ErrorCount,
                warnings = g.WarningCount,
                hasReconnect = g.HasReconnect,
                reconnectCount = g.ReconnectCount,

                // ★ 本局各端实际贡献的行数。
                //
                //   SPT 服务端日志会并入客户端/无头端建立的段，只看 segments[].instance
                //   就完全看不到服务端那一端。界面用这个字段显示"本局有哪几端"。
                sources = g.SourceCounts
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => new { instance = kv.Key, lines = kv.Value }),

                segments = g.Segments
                    .OrderBy(s => s.StartedUtc)
                    .Select(s => new
                    {
                        id = s.Id,
                        instance = s.Instance,
                        role = s.Role,
                        machine = s.Machine,
                        startedUtc = s.StartedUtc,
                        endedUtc = s.EndedUtc,
                        lines = s.LineCount,
                        errors = s.ErrorCount,
                        warnings = s.WarningCount,
                        events = s.Events,
                        sourceCounts = s.SourceCounts,
                        roleLabel = RoleLabel(s)
                    })
            })
        }), "application/json; charset=utf-8");
    }

    /// <summary>段的显示角色：段所属实例，或"它主要承载了谁的行"。</summary>
    private static string RoleLabel(RaidSegment s)
    {
        if (s.SourceCounts != null && s.SourceCounts.Count > 1) return "mixed";
        return s.Role;
    }

    /// <summary>
    /// One merged, time-ordered timeline for a raid, with every line tagged by instance.
    /// This is the view that makes a desync/reconnect visible: you can see the host's
    /// "Not enough space in target" line next to the client's "Could not find item" line.
    /// </summary>
    [HttpGet("/headlesspanel/api/logs/timeline")]
    public IActionResult LogsTimeline([FromQuery] string key, [FromQuery] int? max)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { ok = false, error = "缺少 key" });
        }

        var groups = LogStore.BuildRaids(500);
        var raid = groups.FirstOrDefault(g => string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase));
        if (raid == null)
        {
            return NotFound(new { ok = false, error = "找不到这个战局（可能已被清理）" });
        }

        var cap = Math.Clamp(max ?? 5000, 100, 50000);

        var merged = new List<object>();
        foreach (var seg in raid.Segments)
        {
            foreach (var line in LogStore.ReadSegmentLines(seg, cap))
            {
                // ★ 用【行的真实来源】而不是段所属实例。
                //   服务端行并入了无头端的段，若用 seg.Instance 就会全部标成
                //   "EFT-headless"，时间线上根本看不出服务端参与了。
                var source = string.IsNullOrEmpty(line.Source) ? seg.Instance : line.Source;
                merged.Add(new
                {
                    utc = line.Utc,
                    instance = source,
                    segmentInstance = seg.Instance,
                    role = seg.Role,
                    level = line.Level,
                    text = line.Text
                });
            }
        }

        var ordered = merged
            .OrderBy(x => (DateTime)x.GetType().GetProperty("utc").GetValue(x))
            .Take(cap)
            .ToList();

        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            key = raid.Key,
            map = raid.Map,
            mapText = MapName(raid.Map),
            raidCode = raid.RaidCode,
            startedUtc = raid.StartedUtc,
            endedUtc = raid.EndedUtc,
            hasReconnect = raid.HasReconnect,
            reconnectCount = raid.ReconnectCount,
            errors = raid.ErrorCount,
            warnings = raid.WarningCount,
            // ★ 下拉列表用【实际出现的行来源】，不是段所属实例 ——
            //   这样 SPT-Server 会作为一个独立选项出现，可以单独过滤它的行。
            instances = merged
                .Select(x => (string)x.GetType().GetProperty("instance").GetValue(x))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Select(s => new
                {
                    instance = s,
                    role = raid.Segments
                        .FirstOrDefault(seg => string.Equals(seg.Instance, s, StringComparison.OrdinalIgnoreCase))?.Role
                        ?? (string.Equals(s, "SPT-Server", StringComparison.OrdinalIgnoreCase) ? "server" : "")
                })
                .ToList(),
            truncated = merged.Count > cap,
            totalLines = merged.Count,
            lines = ordered
        }), "application/json; charset=utf-8");
    }

    /// <summary>
    /// The token the shippers must use. Authenticated on purpose: it is shown in the panel so
    /// the operator can paste it into each client's config, but it must not be world-readable.
    /// </summary>
    [HttpGet("/headlesspanel/api/logs/token")]
    public IActionResult LogsToken()
    {
        var cfg = PanelConfig.Current;
        return Content(JsonSerializer.Serialize(new
        {
            ok = true,
            token = cfg.LogIngestToken,
            root = LogStore.Root,
            hint = "把 token 填到每个客户端/无头端的 BepInEx\\config\\com.dsh.logshipper.cfg 的 Token 项"
        }), "application/json; charset=utf-8");
    }

    /// <summary>Discard segments past the retention window.</summary>
    [HttpPost("/headlesspanel/api/logs/prune")]
    public IActionResult LogsPrune()
    {
        LogStore.Prune();
        Audit("logs-prune", null, "", true, "已清理过期日志段", "");
        return Content("{\"ok\":true}", "application/json; charset=utf-8");
    }

    // ------------------------------------------------------------------ helpers

    private sealed class ProcessInfo
    {
        public int Pid { get; init; }
        public string Name { get; init; } = "";
        public string ExecutablePath { get; init; } = "";
    }

    /// <summary>
    /// Snapshot of the processes that make up a headless install. MainModule can throw for
    /// processes owned by other users, so each read is guarded individually.
    /// </summary>
    private static List<ProcessInfo> SnapshotHeadlessProcesses()
    {
        var result = new List<ProcessInfo>();

        foreach (var name in new[] { "FikaHeadlessManager", "EscapeFromTarkov" })
        {
            Process[] found;
            try
            {
                found = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            foreach (var p in found)
            {
                try
                {
                    result.Add(new ProcessInfo
                    {
                        Pid = p.Id,
                        Name = p.ProcessName,
                        ExecutablePath = p.MainModule?.FileName ?? ""
                    });
                }
                catch
                {
                    // Access denied or the process exited between enumeration and read.
                }
                finally
                {
                    p.Dispose();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Seconds an instance has continuously looked like "process up, not connected". Returns 0
    /// and forgets the timestamp as soon as the condition no longer holds.
    /// </summary>
    private static int TrackStarting(string profileId, bool starting)
    {
        if (string.IsNullOrEmpty(profileId))
        {
            return 0;
        }

        lock (CooldownGate)
        {
            if (!starting)
            {
                StartingSince.Remove(profileId);
                return 0;
            }

            if (!StartingSince.TryGetValue(profileId, out var since))
            {
                since = DateTime.UtcNow;
                StartingSince[profileId] = since;
            }

            return (int)(DateTime.UtcNow - since).TotalSeconds;
        }
    }

    /// <summary>Processes of the given name whose executable lives under the instance directory.</summary>
    private static List<ProcessInfo> FindProcessesIn(string processName, string directory)
    {
        var result = new List<ProcessInfo>();
        if (string.IsNullOrWhiteSpace(directory))
        {
            return result;
        }

        foreach (var p in SnapshotHeadlessProcesses())
        {
            if (!string.Equals(p.Name, processName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (PathsOverlap(p.ExecutablePath, directory))
            {
                result.Add(p);
            }
        }

        return result;
    }

    private static bool TryKill(int pid, out string detail)
    {
        detail = "";
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
            return true;
        }
        catch (ArgumentException)
        {
            detail = "进程已不存在";
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static bool PathsOverlap(string executablePath, string directory)    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            var dir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var exe = Path.GetFullPath(executablePath);
            return exe.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int RemainingCooldown(string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
        {
            return 0;
        }

        lock (CooldownGate)
        {
            if (!StartCooldowns.TryGetValue(profileId, out var until))
            {
                return 0;
            }

            var left = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds);
            return left > 0 ? left : 0;
        }
    }

    private static void SetCooldown(string profileId, int seconds)
    {
        if (string.IsNullOrEmpty(profileId) || seconds <= 0)
        {
            return;
        }

        lock (CooldownGate)
        {
            StartCooldowns[profileId] = DateTime.UtcNow.AddSeconds(seconds);
        }
    }

    private static bool TryProfileId(JsonElement body, out string profileId, out string error)
    {
        profileId = null;
        error = null;

        if (body.ValueKind != JsonValueKind.Object ||
            !body.TryGetProperty("profileId", out var prop) ||
            prop.ValueKind != JsonValueKind.String)
        {
            error = "缺少 profileId";
            return false;
        }

        profileId = prop.GetString();
        if (string.IsNullOrWhiteSpace(profileId) || !Regex.IsMatch(profileId, "^[0-9a-fA-F]{24}$"))
        {
            error = "profileId 必须是 24 位十六进制";
            return false;
        }

        return true;
    }

    /// <summary>Fika's map slugs to the names players see.</summary>
    internal static string MapName(string slug)
    {
        switch ((slug ?? "").ToLowerInvariant())
        {
            case "bigmap": return "海关";
            case "factory4_day": return "工厂（白天）";
            case "factory4_night": return "工厂（夜间）";
            case "interchange": return "立交桥";
            case "laboratory": return "实验室";
            case "labyrinth": return "迷宫";
            case "lighthouse": return "灯塔";
            case "rezervbase": return "储备站";
            case "sandbox": return "中心区";
            case "sandbox_high": return "中心区（高层）";
            case "shoreline": return "海岸线";
            case "tarkovstreets": return "塔科夫街区";
            case "woods": return "森林";
            case "hideout": return "藏身处";
            case "": return "—";
            default: return slug;
        }
    }

    internal static string ActivityText(int activity)
    {
        switch (activity)
        {
            case 0: return "菜单中";
            case 1: return "战局中";
            case 2: return "仓库中";
            case 3: return "藏身处";
            case 4: return "跳蚤市场";
            default: return "未知";
        }
    }

    private static async Task<(bool Ok, string Body)> CallFikaAsync(HttpMethod method, string path, string json)
    {
        var settings = PanelSettings.Current;
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            return (false, "fika.jsonc 里没有可用的 apiKey");
        }

        using var request = new HttpRequestMessage(method, settings.FikaBaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.TryAddWithoutValidation("requestcompressed", "0");

        if (json != null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (response.IsSuccessStatusCode, body);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            // SPT serves HTTPS with its own self-signed certificate.
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static readonly Lazy<string> PageHtml = new(LoadPage);

    private static string LoadPage()
    {
        var dir = AppContext.BaseDirectory;
        foreach (var candidate in new[]
                 {
                     Path.Combine(dir, "user", "mods", "HeadlessPanel", "wwwroot", "index.html"),
                     Path.Combine(dir, "wwwroot", "index.html")
                 })
        {
            if (System.IO.File.Exists(candidate))
            {
                return System.IO.File.ReadAllText(candidate, Encoding.UTF8);
            }
        }

        return "<!doctype html><meta charset=\"utf-8\"><h1>HeadlessPanel</h1>"
             + "<p>找不到 wwwroot/index.html。</p>";
    }
}

/// <summary>
/// Reads the bits of server-side configuration the panel still needs. Everything here stays on
/// the server; only the derived results are ever returned to the browser.
/// </summary>
public sealed class PanelSettings
{
    public string ApiKey { get; private init; } = "";

    public string FikaBaseUrl { get; private init; } = "https://127.0.0.1:6969";

    private static PanelSettings _current;

    public static PanelSettings Current => _current ??= Load();

    private static PanelSettings Load()
    {
        var root = AppContext.BaseDirectory;
        return new PanelSettings
        {
            ApiKey = ReadApiKey(root),
            FikaBaseUrl = $"https://127.0.0.1:{ReadHttpPort(root)}"
        };
    }

    private static string ReadApiKey(string root)
    {
        var path = Path.Combine(root, "user", "mods", "fika-server", "assets", "configs", "fika.jsonc");
        if (!System.IO.File.Exists(path))
        {
            return "";
        }

        // fika.jsonc is JSONC (it carries // comments), so a tolerant regex beats a parser here.
        var text = System.IO.File.ReadAllText(path, Encoding.UTF8);
        var match = Regex.Match(text, "\"apiKey\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static int ReadHttpPort(string root)
    {
        foreach (var name in new[] { "http.json", "core.json" })
        {
            var path = Path.Combine(root, "SPT_Data", "configs", name);
            if (!System.IO.File.Exists(path))
            {
                continue;
            }

            var text = System.IO.File.ReadAllText(path, Encoding.UTF8);
            var match = Regex.Match(text, "\"port\"\\s*:\\s*(\\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var port) && port > 0)
            {
                return port;
            }
        }

        return 6969;
    }
}
