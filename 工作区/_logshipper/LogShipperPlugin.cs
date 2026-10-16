using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace Dsh.LogShipper;

/// <summary>
/// 把本端的 BepInEx 日志增量上报给无头端管理面板，让面板能按战局合并
/// 客户端 / 无头端 / 服务端三方的日志并排出一条统一时间线。
///
/// 为什么需要插件：客户端（WOODSPC）与无头端/服务端（Xtzj-20200101vo）在不同机器上，
/// 没有共享目录，服务端读不到客户端日志。插件主动上报是唯一不依赖 SMB 的做法。
///
/// 认证：面板的既有端点都走 SPT 的 cookie 认证，插件拿不到 cookie，
/// 所以上报端点用 [AllowAnonymous] + 一个共享 token（面板配置里生成，插件配置里填）。
///
/// 断线重连兼容：本插件只负责"把行送到面板"，不判断战局边界。
/// 战局归属由面板侧的解析器决定 —— 它用 Fika Raid Code / actionId 做主键，
/// 因此重连前后产生的多段日志会自然归到同一个战局里。
/// </summary>
[BepInPlugin(Guid, "Log Shipper", "1.0.0")]
public sealed class LogShipperPlugin : BaseUnityPlugin
{
    public const string Guid = "com.dsh.logshipper";

    internal static ManualLogSource Log;

    // ── 配置 ──
    private ConfigEntry<string> _panelUrl;
    private ConfigEntry<string> _token;
    private ConfigEntry<string> _instanceName;
    private ConfigEntry<float> _interval;
    private ConfigEntry<int> _maxLinesPerBatch;
    private ConfigEntry<int> LagSkipBytes;
    private ConfigEntry<int> CatchUpTailBytes;
    private ConfigEntry<string> _minLevel;
    private ConfigEntry<bool> _enabled;
    private ConfigEntry<bool> _verbose;

    // ── 运行状态 ──
    private string _logPath;
    private long _offset;
    private long _inode;               // 用于检测日志轮转（文件被替换）
    private readonly List<string> _pending = new List<string>();
    private readonly object _gate = new object();

    /// <summary>
    /// 实际上报用的实例名。
    ///
    /// ★ 单独一个运行时字段，不写回 cfg —— 识别到的玩家名如果落盘，
    ///   下次启动就会读到非空值，于是再也不会重新识别（换角色/换人都不更新）。
    /// </summary>
    private static string _instanceNameRuntime;

    /// <summary>是否已经不用再找玩家名了（识别成功，或用户手工指定过）。</summary>
    private static bool _nameResolveDone;

    private float _nextTick;
    private Thread _worker;
    private volatile bool _running;
    /// <summary>
    /// ★ SPT 4.x 的面板跑在【HTTPS】上，用的是自签证书。
    ///
    ///   实测：http://host:6969 会被直接掐断（"连接被意外关闭"），
    ///         https://host:6969 则因证书不受信任而失败。
    ///   浏览器能打开只是因为操作者点过"接受风险"。
    ///
    ///   所以这里显式接受自签证书 —— 目标是自己的游戏服务端，不是公网。
    /// </summary>
    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        try
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                // 允许 http 与 https 混用（探测阶段两种都会试）
                AllowAutoRedirect = true,
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        }
        catch
        {
            // 极老的运行时不支持自定义回调时退化成普通客户端
            return new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        }
    }

    private long _sentLines;
    private long _droppedLines;
    private long _failedBatches;

    private void Awake()
    {
        Log = Logger;

        _enabled = Config.Bind("General", "Enabled", true, "总开关");
        _panelUrl = Config.Bind("General", "PanelUrl", "",
            "无头端管理面板的地址，留空则【自动发现】：从 SPT 的 LauncherSettings.json 读出你连的服务端地址。"
          + "示例 http://26.216.11.222:6969");
        _token = Config.Bind("General", "Token", "",
            "★ 可选。留空（默认）即可，插件会直接上报并由面板接收。"
          + "只有当面板 config.json 里显式设置了 logIngestToken 时才需要填同样的值。");
        _instanceName = Config.Bind("General", "InstanceName", "",
            "本端在面板上的显示名。★ 留空（推荐）则由插件自动取名：client-<玩家名>。"
          + "运行时从游戏内存识别玩家名，只改内存不写回本文件，所以每次启动都会重新识别。"
          + "手工填了则完全以你填的为准。");
        _interval = Config.Bind("General", "BatchIntervalSeconds", 2f,
            new ConfigDescription("批量上报间隔（秒）", new AcceptableValueRange<float>(0.5f, 30f)));
        _maxLinesPerBatch = Config.Bind("General", "MaxLinesPerBatch", 3000,
            new ConfigDescription("单批最多发送多少行（超出会截断并在面板上标注）", new AcceptableValueRange<int>(100, 50000)));
        LagSkipBytes = Config.Bind("Performance", "LagSkipBytes", 8 * 1024 * 1024,
            new ConfigDescription("★ 落后保护：未上报字节超过此值时，直接跳过并只读末尾一段（防止落后无界、拖慢战局）",
                new AcceptableValueRange<int>(256 * 1024, 256 * 1024 * 1024)));
        CatchUpTailBytes = Config.Bind("Performance", "CatchUpTailBytes", 1 * 1024 * 1024,
            new ConfigDescription("★ 追赶时保留末尾多少字节（这些行仍会正常上报）",
                new AcceptableValueRange<int>(64 * 1024, 64 * 1024 * 1024)));
        _minLevel = Config.Bind("Filter", "MinLevel", "All",
            "只上报不低于此级别的行：All / Info / Warning / Error / None(=只发 Error)");
        _verbose = Config.Bind("General", "Verbose", false, "打详细日志（排查用）");

        if (!_enabled.Value)
        {
            Log.LogInfo("[LogShipper] 已禁用（General/Enabled=false）");
            return;
        }

        // ★ 令牌是可选的：留空就直接上报（面板默认不校验）。
        //   旧版本在这里 return，导致"没配 token 就不工作"—— 那正是要避免的手工步骤。
        if (string.IsNullOrWhiteSpace(_token.Value))
        {
            Log.LogInfo("[LogShipper] 未配置 Token —— 直接上报（面板默认不需要令牌）。"
                      + "仅当面板启用了 logIngestToken 时才需要在配置里填同样的值。");
        }

        _logPath = ResolveLogPath();
        if (_logPath == null)
        {
            Log.LogWarning("[LogShipper] 找不到 BepInEx\\LogOutput.log —— 不上报");
            return;
        }

        // ★ 实例名解析。
        //
        //   客户端的玩家名只能从【运行时内存】取
        //   （GameWorld.MainPlayer.Profile.Nickname）：
        //   客户端的 SPT_Runtime\user\profiles 里并【没有】自己的 profile，
        //   那些文件只存在于服务端。
        //
        //   而运行时取必须走主线程、且要等玩家加载完，所以：
        //     · 这里只准备一个【回退名】，不写进 cfg
        //     · 真名由 Update 识别后放入 _instanceNameRuntime
        //     · ★ 不写回 cfg：否则下次启动读到非空，就再也不会重新识别
        //
        //   ★ 无头端不参与取名：它的 Nickname 是 "headless_<profileId>"，
        //     拿来当显示名很啰嗦；而它的游戏目录名（EFT-headless / EFT-headless1）
        //     本身就已经唯一且可读。
        var gameRoot = Path.GetDirectoryName(Application.dataPath);
        var gameDir = Safe(Path.GetFileName(gameRoot));

        if (!string.IsNullOrWhiteSpace(_instanceName.Value))
        {
            // 用户在 cfg 里显式写了名字 → 尊重它，不再自动覆盖
            _instanceNameRuntime = _instanceName.Value.Trim();
            _nameResolveDone = true;
        }
        else if (IsHeadless())
        {
            _instanceNameRuntime = gameDir;         // EFT-headless / EFT-headless1
            _nameResolveDone = true;
        }
        else
        {
            // 客户端：先用回退名顶着，识别到玩家名后换成 client-<玩家名>
            _instanceNameRuntime = Safe(Environment.MachineName) + "-" + gameDir;
        }

        // ★ 面板地址解析。
        //
        //   配置里写了地址也要【验证】再相信：实测踩过的坑是配置里存着
        //   http://host:6969 而 SPT 实际是 HTTPS —— 直接用就永远连不上，
        //   而且失败信息只有一句 "An error occurred while sending the request"。
        //
        //   规则：
        //     · 配置为空        → 生成候选列表并探测
        //     · 配置非空且可用   → 直接用（并写回规范化后的地址）
        //     · 配置非空但不可用 → 探测；探测成功则采用并写回配置
        var configured = (_panelUrl.Value ?? "").Trim();

        if (!string.IsNullOrEmpty(configured))
        {
            var normalized = Normalize(configured);

            if (ProbePanel(new List<string> { normalized }) != null)
            {
                _panelUrl.Value = normalized;
                Log.LogInfo($"[LogShipper] 使用配置里的面板地址（已验证）: {normalized}");
            }
            else
            {
                Log.LogWarning($"[LogShipper] 配置的面板地址 {normalized} 连不上，改用自动探测…");
                configured = "";
            }
        }

        if (string.IsNullOrEmpty(configured))
        {
            var candidates = BuildCandidates();
            Log.LogInfo($"[LogShipper] 正在探测面板地址（{candidates.Count} 个候选）…");

            var picked = ProbePanel(candidates);
            if (picked != null)
            {
                _panelUrl.Value = picked;
                Log.LogInfo($"[LogShipper] ★ 面板地址已确定: {picked}（已写回配置）");
            }
            else
            {
                _panelUrl.Value = candidates.Count > 0 ? candidates[0] : "https://127.0.0.1:6969";
                Log.LogWarning($"[LogShipper] ★ 所有候选地址都不通，暂时使用 {_panelUrl.Value}；"
                             + "运行时会继续重试。");
            }
        }

        _panelUrl.Value = _panelUrl.Value.TrimEnd('/');

        // ★ 从文件末尾开始（只上报启动之后的新行）
        try
        {
            var fi = new FileInfo(_logPath);
            _offset = fi.Exists ? fi.Length : 0;
        }
        catch { _offset = 0; }

        _running = true;
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "LogShipper" };
        _worker.Start();

        _nextTick = Time.unscaledTime + _interval.Value;

        Log.LogInfo($"[LogShipper] 已加载。实例={EffectiveInstanceName()} 面板={_panelUrl.Value} "
                  + $"间隔={_interval.Value}s 过滤={_minLevel.Value} 起始偏移={_offset}"
                  + (_nameResolveDone ? "" : "（等待识别玩家名…）"));
    }

    /// <summary>
    /// 上报时真正使用的实例名。
    ///
    /// 顺序：运行时识别到的（client-玩家名 / 无头端目录名） → 机器名。
    /// </summary>
    private static string EffectiveInstanceName()
    {
        if (!string.IsNullOrEmpty(_instanceNameRuntime)) return _instanceNameRuntime;
        return Safe(Environment.MachineName);
    }

    /// <summary>
    /// 本端是不是无头端（Fika.Headless 在场）。
    ///
    /// 用它来区分"该不该按玩家名取名"：无头端的 Nickname 是
    /// "headless_&lt;profileId&gt;"，拿来当显示名不合适，直接用目录名更好。
    /// </summary>
    private static bool IsHeadless()
    {
        try
        {
            return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.fika.headless");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ★ 玩家名识别（极轻量）。
    ///
    /// 为什么要 Update：玩家名只存在于运行时内存（GameWorld.MainPlayer.Profile.Nickname），
    /// 而 Unity 对象只能在主线程访问 —— 后台 worker 碰不得。
    ///
    /// 开销控制：
    ///   · 识别成功后 _nameResolveDone = true，此后每帧只做一次 bool 判断，永久零开销
    ///   · 识别前每 32 帧才真正查一次
    /// </summary>
    private void Update()
    {
        if (_nameResolveDone) return;                       // ★ 识别后立即返回
        if ((Time.frameCount & 31) != 0) return;            // 每 32 帧查一次

        try
        {
            var gw = Singleton<GameWorld>.Instance;
            var player = gw == null ? null : gw.MainPlayer;
            var profile = player == null ? null : player.Profile;
            if (profile == null) return;

            // ★★★ 必须优先用 MainProfileNickname（主档案昵称），而不是 Nickname。
            //
            //   原因：Scav 战局里 Profile.Nickname 是【系统随机生成的俄文名】
            //   （实测见过 "Володарчик_Почетный"、"Прохор_Винторез"、"Никита_Клим"），
            //   每局都不一样，于是同一个玩家会被记成好几个不同的实例目录。
            //   而 MainProfileNickname 是玩家自己的固定名（PMC 名，如 "Bi8bo"）。
            //
            //   这也是 Fika 自己的取法（ObservedPlayer.cs 第 1514 行）：
            //     var nickname = !string.IsNullOrEmpty(Profile.Info.MainProfileNickname)
            //                  ? Profile.Info.MainProfileNickname : Profile.Nickname;
            var info = profile.Info;
            var main = info == null ? null : info.MainProfileNickname;
            var nick = !string.IsNullOrWhiteSpace(main) ? main : profile.Nickname;

            if (string.IsNullOrWhiteSpace(nick)) return;    // 还没加载完，下次再说

            var name = "client-" + Safe(nick);
            _instanceNameRuntime = name;
            _nameResolveDone = true;

            Log.LogInfo($"[LogShipper] 已识别玩家: {nick} ({profile.ProfileId})"
                      + $" 侧={profile.Side}"
                      + (string.IsNullOrWhiteSpace(main) ? "" : $" [当前角色名={profile.Nickname}]")
                      + $" → 实例名 = {name}（只改内存，不写回 cfg）");
        }
        catch
        {
            // 拿不到就保持回退名，绝不因为取名失败而影响上报
        }
    }

    private static string ResolveLogPath()
    {
        try
        {
            // 1) BepInEx 自己知道日志在哪
            var p = Path.Combine(BepInEx.Paths.BepInExRootPath ?? "", "LogOutput.log");
            if (File.Exists(p)) return p;

            // 2) 回退：用游戏目录推导
            var gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (!string.IsNullOrEmpty(gameRoot))
            {
                foreach (var name in new[] { "LogOutput.log", "LogOutput.prev.log" })
                {
                    var c = Path.Combine(gameRoot, "BepInEx", name);
                    if (File.Exists(c) && name == "LogOutput.log") return c;
                }
                var c2 = Path.Combine(gameRoot, "BepInEx", "LogOutput.log");
                if (File.Exists(c2)) return c2;
            }
        }
        catch { }
        return null;
    }

    private static string Safe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Work out the panel address without asking the operator.
    ///
    /// Two sources, in order of trust:
    ///
    ///   1. SPT's launcher file (&lt;SPT_Runtime&gt;\user\Launcher\LauncherSettings.json) —
    ///      every server this install has connected to, appended on each connect. Only entries
    ///      that actually carry a port are usable. This is what a *client* should use.
    ///
    ///   2. The local SPT HTTP config (&lt;SPT_Runtime&gt;\SPT_Data\configs\http.json) — for a
    ///      headless install, which runs on the same box as the panel, this is
    ///      "127.0.0.1:6969", exactly right. (Headless launcher files sometimes hold a stale
    ///      entry pointing at a *client*, with no port — hence the port requirement above.)
    /// </summary>
    private static string DiscoverPanelUrl()
    {
        try
        {
            var gameRoot = Path.GetDirectoryName(Application.dataPath);   // …\EFT
            if (!string.IsNullOrEmpty(gameRoot))
            {
                var launcher = FirstExisting(
                    Path.Combine(gameRoot, "SPT_Runtime", "user", "Launcher", "LauncherSettings.json"),
                    Path.Combine(gameRoot, "user", "Launcher", "LauncherSettings.json"));

                if (launcher != null)
                {
                    var json = File.ReadAllText(launcher, Encoding.UTF8);
                    var matches = System.Text.RegularExpressions.Regex.Matches(
                        json, "\"IpAddress\"\\s*:\\s*\"([^\"]+)\"");

                    for (var i = matches.Count - 1; i >= 0; i--)
                    {
                        var addr = matches[i].Groups[1].Value.Trim();
                        if (addr.Length == 0) continue;
                        if (addr.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return addr.TrimEnd('/');

                        // ★ 必须有端口才可用：无头端的 launcher 文件里可能残留一个
                        //   "26.154.77.60"（某个客户端，没端口）—— 那不是服务端。
                        if (!addr.Contains(":")) continue;

                        return "http://" + addr;
                    }
                }
            }

            // 回退：本机 SPT 的 HTTP 配置（无头端场景）
            if (!string.IsNullOrEmpty(gameRoot))
            {
                var http = FirstExisting(
                    Path.Combine(gameRoot, "SPT_Runtime", "SPT_Data", "configs", "http.json"),
                    Path.Combine(gameRoot, "SPT_Data", "configs", "http.json"));

                if (http != null)
                {
                    var cfg = File.ReadAllText(http, Encoding.UTF8);
                    var ip = System.Text.RegularExpressions.Regex.Match(cfg, "\"ip\"\\s*:\\s*\"([^\"]+)\"");
                    var port = System.Text.RegularExpressions.Regex.Match(cfg, "\"port\"\\s*:\\s*(\\d+)");

                    if (port.Success)
                    {
                        var host = ip.Success && ip.Groups[1].Value.Length > 0 ? ip.Groups[1].Value : "127.0.0.1";
                        return $"http://{host}:{port.Groups[1].Value}";
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static string FirstExisting(params string[] paths)
    {
        foreach (var p in paths)
        {
            try { if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    /// <summary>Ports the panel might be listening on, when the recorded one is stale.</summary>
    private static readonly int[] CandidatePorts = { 6969, 80, 443, 8080, 44300 };

    /// <summary>
    /// Build the list of addresses worth trying, most likely first:
    ///
    /// ★ HTTPS 排在 HTTP 前面：SPT 4.x 在 6969 上是 HTTPS（自签），
    ///   用 http 去连会被直接掐断，所以 https 才是主路径。
    ///   实际使用的端口/协议由 ProbePanel 用真实请求定出来。
    /// </summary>
    private static List<string> BuildCandidates()
    {
        var list = new List<string>();
        var hosts = new List<string>();

        var discovered = DiscoverPanelUrl();
        if (!string.IsNullOrEmpty(discovered))
        {
            // 记录的地址如果是 http，同时补一个 https 变体并让它优先
            if (discovered.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                list.Add("https://" + discovered.Substring(7));
                list.Add(discovered);
            }
            else
            {
                list.Add(discovered);
            }

            try { hosts.Add(new Uri(discovered).Host); } catch { }
        }

        var hostSnapshot = new List<string>(hosts);
        foreach (var h in hostSnapshot)
        {
            Add(list, $"https://{h}");
            foreach (var port in CandidatePorts)
            {
                if (port == 443) continue;
                Add(list, $"https://{h}:{port}");
            }
            Add(list, $"http://{h}");
            foreach (var port in CandidatePorts)
            {
                if (port == 80) continue;
                Add(list, $"http://{h}:{port}");
            }
        }

        // 无头端与服务端同机
        Add(list, "https://127.0.0.1:6969");
        Add(list, "http://127.0.0.1:6969");
        Add(list, "https://127.0.0.1");
        Add(list, "http://127.0.0.1");

        return list;
    }

    private static void Add(List<string> list, string url)
    {
        var u = url.TrimEnd('/');
        foreach (var existing in list)
        {
            if (string.Equals(existing, u, StringComparison.OrdinalIgnoreCase)) return;
        }
        list.Add(u);
    }

    /// <summary>补上缺失的协议前缀，并去掉结尾斜杠。</summary>
    private static string Normalize(string url)
    {
        var u = (url ?? "").Trim().TrimEnd('/');
        if (u.Length == 0) return u;
        if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return u;
        }
        return "https://" + u;   // ★ 默认 https —— SPT 4.x 的面板就是 https
    }

    /// <summary>
    /// Try each candidate with a real (tiny) ingest request. A successful response means the
    /// panel is there AND accepting our data — a plain reachability check would also pass on
    /// unrelated web servers that happen to share the port.
    /// </summary>
    private static string ProbePanel(List<string> candidates)
    {
        var probe = BuildProbeJson();

        foreach (var url in candidates)
        {
            try
            {
                using var content = new StringContent(probe, Encoding.UTF8, "application/json");

                using var req = new HttpRequestMessage(HttpMethod.Post,
                    url + "/headlesspanel/api/logs/ingest") { Content = content };
                req.Headers.TryAddWithoutValidation("X-LogShipper-Token", "");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using var resp = Http.SendAsync(req, cts.Token).GetAwaiter().GetResult();

                if (resp.IsSuccessStatusCode)
                {
                    return url;
                }
            }
            catch
            {
                // 这个候选不通，试下一个
            }
        }

        return null;
    }

    private static string BuildProbeJson()
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"instance\":\"LogShipper-Probe\",");
        sb.Append("\"role\":\"probe\",");
        sb.Append("\"machine\":\"").Append(J(Environment.MachineName)).Append("\",");
        sb.Append("\"sentUtc\":\"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
        sb.Append("\"seq\":0,");
        sb.Append("\"lines\":[\"[LogShipper] 面板地址探测\"]");
        sb.Append('}');
        return sb.ToString();
    }

    // ─────────────────────────────────────────────
    // ★ 主线程零开销
    //
    // 早期版本用 Update() 定时唤醒 worker，那意味着每帧一次 Time 属性读取 ——
    // 微不足道，但战局里没必要付这个钱。现在 worker 线程自己 Sleep 定时，
    // 主线程完全不参与上报流程：不读文件、不组 JSON、不发网络。
    //
    // 唯一的 Unity 调用（Application.dataPath / MachineName）都在 Awake 里做完了。
    // ─────────────────────────────────────────────

    // ─────────────────────────────────────────────
    // 后台线程：读文件 → 过滤 → 批量 POST（自带定时，无需主线程驱动）
    // ─────────────────────────────────────────────

    private void WorkerLoop()
    {
        while (_running)
        {
            try
            {
                var lines = ReadNewLines();
                if (lines != null && lines.Count > 0)
                {
                    Send(lines);
                }
            }
            catch (Exception e)
            {
                if (_verbose.Value) Log?.LogWarning("[LogShipper] 工作线程异常: " + e.Message);
            }

            // 在自己的线程里睡，主线程完全不受影响
            var ms = (int)Math.Max(250, _interval.Value * 1000);
            var slept = 0;
            while (_running && slept < ms)
            {
                Thread.Sleep(100);
                slept += 100;
            }
        }
    }

    /// <summary>读取日志文件自 offset 之后的新行；处理轮转/截断/落后保护。</summary>
    private List<string> ReadNewLines()
    {
        if (_logPath == null) return null;

        FileInfo fi;
        try { fi = new FileInfo(_logPath); }
        catch { return null; }
        if (!fi.Exists) return null;

        // ★ 日志轮转：文件变小了，说明被重建过 → 从头发
        if (fi.Length < _offset)
        {
            if (_verbose.Value) Log?.LogInfo($"[LogShipper] 检测到日志轮转（{_offset} → {fi.Length}），从头读取");
            _offset = 0;
        }

        // ★★★ 落后保护：如果还差很多字节没读，直接跳到"接近末尾"。
        //     否则一旦日志产生的速度超过上报速度，offset 会永远追不上，
        //     内存和 IO 都会无界增长 —— 那会真的影响战局性能。
        var lag = fi.Length - _offset;
        if (lag > LagSkipBytes.Value)
        {
            var skipped = lag - CatchUpTailBytes.Value;
            _offset = fi.Length - CatchUpTailBytes.Value;
            _lagSkips++;
            _lagSkippedBytes += skipped;
            // 跳过的位置若落在行中间，丢弃第一个不完整的行
            _skipPartialFirstLine = true;
        }

        if (fi.Length == _offset) return null;

        var result = new List<string>();
        var hardCap = Math.Max(_maxLinesPerBatch.Value * 2, 2000);

        try
        {
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            fs.Seek(_offset, SeekOrigin.Begin);
            using var sr = new StreamReader(fs, Encoding.UTF8, true, 64 * 1024);

            if (_skipPartialFirstLine)
            {
                sr.ReadLine();          // 丢掉被截断的那半行
                _skipPartialFirstLine = false;
            }

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (PassesFilter(line)) result.Add(line);
                if (result.Count >= hardCap) break;   // ★ 硬上限，防止单次读爆内存
            }
            _offset = fs.Position;

            // 文件末尾可能正写到一半：ReadLine 会把不完整的最后一行也读出来，
            // 这里回退一行，等下次再读（避免把半行发出去）。
            if (result.Count > 0 && !EndsWithNewline(fs))
            {
                var last = result[result.Count - 1];
                result.RemoveAt(result.Count - 1);
                _offset -= Encoding.UTF8.GetByteCount(last) + Environment.NewLine.Length;
                if (_offset < 0) _offset = 0;
            }
        }
        catch (Exception e)
        {
            if (_verbose.Value) Log?.LogWarning("[LogShipper] 读取日志失败: " + e.Message);
            return null;
        }

        // 有跳过时，在批首标注，面板上能直接看到漏了多少
        if (_lagSkips > _lastReportedSkips)
        {
            var delta = _lagSkips - _lastReportedSkips;
            _lastReportedSkips = _lagSkips;
            result.Insert(0, $"[LogShipper] ★ 上报落后，已跳过 {delta} 次（累计跳过约 "
                           + $"{_lagSkippedBytes / 1024 / 1024} MB）。如需完整日志请提高 MaxLinesPerBatch 或降低 MinLevel。");
        }

        if (result.Count > _maxLinesPerBatch.Value)
        {
            _droppedLines += result.Count - _maxLinesPerBatch.Value;
            var skip = result.Count - _maxLinesPerBatch.Value;
            result.RemoveRange(0, skip);
            result.Insert(0, $"[LogShipper] ★ 本批丢弃了 {skip} 行（超过 MaxLinesPerBatch）");
        }

        return result;
    }

    private long _lagSkips;
    private long _lagSkippedBytes;
    private long _lastReportedSkips;
    private bool _skipPartialFirstLine;

    private static bool EndsWithNewline(FileStream fs)
    {
        try
        {
            if (fs.Length == 0) return true;
            var pos = fs.Position;
            fs.Seek(-1, SeekOrigin.End);
            int b = fs.ReadByte();
            fs.Seek(pos, SeekOrigin.Begin);
            return b == '\n' || b == '\r';
        }
        catch { return true; }
    }

    private bool PassesFilter(string line)
    {
        var lvl = _minLevel.Value ?? "All";
        if (lvl.Equals("All", StringComparison.OrdinalIgnoreCase)) return true;

        var isErr = line.IndexOf("[Error", StringComparison.OrdinalIgnoreCase) >= 0
                 || line.IndexOf("[Fatal", StringComparison.OrdinalIgnoreCase) >= 0;
        if (lvl.Equals("None", StringComparison.OrdinalIgnoreCase)) return isErr;
        if (lvl.Equals("Error", StringComparison.OrdinalIgnoreCase)) return isErr;

        var isWarn = line.IndexOf("[Warning", StringComparison.OrdinalIgnoreCase) >= 0;
        if (lvl.Equals("Warning", StringComparison.OrdinalIgnoreCase)) return isErr || isWarn;

        return true;   // Info / 其它 → 全发
    }

    /// <summary>把一批行 JSON 化后 POST 给面板。</summary>
    private void Send(List<string> lines)
    {
        var url = _panelUrl.Value.TrimEnd('/') + "/headlesspanel/api/logs/ingest";
        var payload = BuildJson(lines);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                content.Headers.TryAddWithoutValidation("X-LogShipper-Token", _token.Value);

                using var resp = Http.PostAsync(url, content).GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                {
                    _sentLines += lines.Count;
                    _consecutiveFailures = 0;
                    if (_verbose.Value)
                        Log?.LogInfo($"[LogShipper] 已上报 {lines.Count} 行（累计 {_sentLines}）");
                    return;
                }

                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // ★ 认证/参数错误：重试没意义，但必须让操作者看到 —— 否则就是"日志没上来"
                //   而没有任何线索。这类错误只报一次，避免刷屏。
                if (!_reportedRejection)
                {
                    _reportedRejection = true;
                    Log?.LogError($"[LogShipper] ★ 上报被拒 HTTP {(int)resp.StatusCode}  ({url})  {Trunc(body, 200)}");
                }
                return;
            }
            catch (Exception e)
            {
                if (attempt == 2)
                {
                    _failedBatches++;
                    _consecutiveFailures++;

                    // ★ 第一次失败、以及之后每 30 次报一次 —— 既能看到问题又不刷日志。
                    if (_consecutiveFailures == 1 || _consecutiveFailures % 30 == 0)
                    {
                        Log?.LogWarning($"[LogShipper] ★ 上报失败（连续 {_consecutiveFailures} 次）"
                                      + $" {url} —— {e.Message}");
                        if (_consecutiveFailures == 1)
                        {
                            Log?.LogWarning("[LogShipper] 面板地址可能不对。"
                                          + "请在 com.dsh.logshipper.cfg 里显式填写 PanelUrl（浏览器能打开面板的那个地址）。");
                        }
                    }
                }
                else Thread.Sleep(500);
            }
        }
    }

    private int _consecutiveFailures;
    private bool _reportedRejection;

    private string BuildJson(List<string> lines)
    {
        var sb = new StringBuilder(lines.Count * 160 + 512);
        sb.Append('{');
        sb.Append("\"instance\":\"").Append(J(EffectiveInstanceName())).Append("\",");
        sb.Append("\"role\":\"").Append(J(DetectRole())).Append("\",");
        sb.Append("\"machine\":\"").Append(J(Environment.MachineName)).Append("\",");
        sb.Append("\"sentUtc\":\"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
        sb.Append("\"seq\":").Append(Interlocked.Increment(ref _seq)).Append(',');
        sb.Append("\"lines\":[");
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(J(lines[i])).Append('"');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private long _seq;

    /// <summary>无头端还是客户端 —— 通过 Fika 的类型是否存在来判断，避免硬依赖。</summary>
    private static string DetectRole()
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an;
                try { an = asm.GetName().Name; } catch { continue; }
                if (an == null || an.IndexOf("Fika.Headless", StringComparison.OrdinalIgnoreCase) < 0) continue;
                return "headless";
            }
        }
        catch { }
        return "client";
    }

    private static string J(string s)
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

    private static string Trunc(string s, int n)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

    private void OnDestroy()
    {
        _running = false;
        try { _worker?.Join(1500); } catch { }
    }
}
