using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dsh.HeadlessPanel;

/// <summary>
/// One configured headless install. <see cref="ManagerPath"/> is what the panel launches when
/// someone presses "启动"; <see cref="WorkingDirectory"/> is both the process working directory
/// and how a running-but-not-yet-connected instance is recognised.
/// </summary>
public sealed class HeadlessInstance
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("managerPath")]
    public string ManagerPath { get; set; } = "";

    [JsonPropertyName("workingDirectory")]
    public string WorkingDirectory { get; set; } = "";

    /// <summary>Set false to keep an instance visible but not launchable.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public sealed class HeadlessPanelConfig
{
    [JsonPropertyName("headlessInstances")]
    public List<HeadlessInstance> HeadlessInstances { get; set; } = new();

    /// <summary>Seconds the panel waits before offering "启动" again for the same instance.</summary>
    [JsonPropertyName("startCooldownSeconds")]
    public int StartCooldownSeconds { get; set; } = 180;

    /// <summary>When false, the start/restart buttons are hidden for non-configured callers.</summary>
    [JsonPropertyName("allowStart")]
    public bool AllowStart { get; set; } = true;

    /// <summary>
    /// OPTIONAL shared secret for the log-ingest endpoint.
    ///
    /// Leave it EMPTY (the default) and the endpoint is open, so every client reports its logs
    /// with zero configuration — no token to copy, no file to edit. That is acceptable because
    /// ingest can only append log lines; it cannot control the headless clients, and those
    /// endpoints still require SPT login.
    ///
    /// Set a value here AND in each client's com.dsh.logshipper.cfg to require the token.
    /// </summary>
    [JsonPropertyName("logIngestToken")]
    public string LogIngestToken { get; set; } = "";

    /// <summary>
    /// Noise filter for SPT's own server log. The panel tails it directly (it runs inside the
    /// server process), so this is the only knob for how much of it reaches the timeline.
    ///   NoPanel  — drop the panel's own polling (default)
    ///   NoHttp   — drop every "[客户端请求]" line
    ///   WarnPlus — keep only Warning / Error / Fatal
    ///   All      — keep everything
    /// </summary>
    [JsonPropertyName("serverLogFilter")]
    public string ServerLogFilter { get; set; } = "NoPanel";

    /// <summary>
    /// Whether the leading timestamp in SPT's server log is UTC.
    ///
    /// ★ true (default, measured): the log writes [2026-10-14 05:39:27] while the file's last
    ///   write time is local 13:39:27 — so the in-line stamp is already UTC.
    ///   Set false only if a future SPT build switches to local wall-clock.
    /// </summary>
    [JsonPropertyName("serverLogTimestampIsUtc")]
    public bool ServerLogTimestampIsUtc { get; set; } = true;

    /// <summary>
    /// 服务端日志只在【战局窗口】内采集：有战局进行中，或刚结束这么多秒内。
    ///
    /// 后者是必要的缓冲 —— 结算走的是 /raid/profile/scavsave、
    /// /client/match/local/end 这类请求，它们紧跟战局结束发生。
    /// 设为 0 则战局一结束就停止采集。
    /// </summary>
    [JsonPropertyName("serverLogGraceSeconds")]
    public int ServerLogGraceSeconds { get; set; } = 60;
}

/// <summary>
/// Loads config.json from the mod folder. Written back once with defaults if it is missing, so
/// an operator always has a file to edit.
/// </summary>
public static class PanelConfig
{
    private static readonly object Gate = new();
    private static HeadlessPanelConfig _current;
    private static DateTime _stampUtc = DateTime.MinValue;

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "user", "mods", "HeadlessPanel", "config.json");

    /// <summary>Re-read at most once every few seconds so edits apply without a restart.</summary>
    public static HeadlessPanelConfig Current
    {
        get
        {
            lock (Gate)
            {
                if (_current != null && DateTime.UtcNow - _stampUtc < TimeSpan.FromSeconds(5))
                {
                    return _current;
                }

                _current = Load();
                _stampUtc = DateTime.UtcNow;
                return _current;
            }
        }
    }

    private static HeadlessPanelConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                // ★ 不再自动生成 token —— 默认零配置，客户端不需要做任何事。
                var fresh = new HeadlessPanelConfig();
                Write(fresh);
                return fresh;
            }

            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            return JsonSerializer.Deserialize<HeadlessPanelConfig>(json, options) ?? new HeadlessPanelConfig();
        }
        catch
        {
            // A broken config must not take the panel down.
            return new HeadlessPanelConfig();
        }
    }

    private static void Write(HeadlessPanelConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, options), new UTF8Encoding(false));
        }
        catch
        {
            // Ignore: the panel still works with in-memory defaults.
        }
    }
}
