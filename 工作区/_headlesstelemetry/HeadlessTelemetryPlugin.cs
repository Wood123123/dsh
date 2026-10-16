using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Networking;
using UnityEngine;

namespace Dsh.HeadlessTelemetry;

/// <summary>
/// 无头端遥测采集
///
/// 每 1 秒采集一次，写到 BepInEx\config\headless-telemetry\ 下：
///   live.json                   —— 实时值 + 最近 5 分钟趋势（覆盖写）
///   raids\&lt;实例名&gt;\&lt;时间&gt;_&lt;地图&gt;.json —— 每局一份完整日志（最多保留 N 份，超了删最老）
///
/// HeadlessPanel（SPT 服务端 mod）读这些文件来显示实时数据、趋势图和战局日志。
///
/// 为什么用文件桥接：SPT 服务端与无头端跑在同一台机器上，文件读写最简单可靠，
/// 无需网络、端口或认证。而游戏内的 FPS/AI 数据在 EscapeFromTarkov.exe 进程里，
/// 服务端进程（SPT.Server.exe）无论如何都拿不到。
/// </summary>
[BepInPlugin(Guid, "Headless Telemetry", "1.0.0")]
public sealed class HeadlessTelemetryPlugin : BaseUnityPlugin
{
    public const string Guid = "com.dsh.headlesstelemetry";

    internal static ManualLogSource Log;
    private static HeadlessTelemetryPlugin _instance;

    // ── 配置 ──
    private ConfigEntry<float> _sampleInterval;
    private ConfigEntry<int> _trendWindowSeconds;
    private ConfigEntry<int> _maxRaidLogs;
    private ConfigEntry<int> _maxSamplesPerRaid;
    private ConfigEntry<bool> _verbose;

    // ── 输出 ──
    private string _outDir;        // ...\BepInEx\config\headless-telemetry
    private string _liveFile;      // ...\live.json
    private string _raidDir;       // ...\raids\<实例名>\
    private string _instanceId;    // 取自游戏目录名（无头端0/1 目录不同）

    // ── 采样状态 ──
    private int _frameCount;
    private float _nextSampleAt;
    private readonly List<Sample> _trend = new List<Sample>();
    private int _trendCap = 300;

    // ── 当前战局 ──
    private RaidLog _currentRaid;
    private string _lastMap;

    // 实例状态（给面板判断"无头端是否卡住"用）
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private int _totalSamples;
    private float _lastWriteAt;

    private void Awake()
    {
        Log = Logger;
        _instance = this;

        _sampleInterval = Config.Bind("Sampling", "SampleIntervalSeconds", 1f,
            new ConfigDescription("采样间隔（秒）", new AcceptableValueRange<float>(0.25f, 10f)));
        _trendWindowSeconds = Config.Bind("Sampling", "TrendWindowSeconds", 300,
            new ConfigDescription("实时趋势窗口（秒）@1Hz = 点数。默认 300 = 5 分钟", new AcceptableValueRange<int>(30, 3600)));
        _maxRaidLogs = Config.Bind("Storage", "MaxRaidLogs", 20,
            new ConfigDescription("每个无头端最多保留多少份战局日志（超了删最老的）", new AcceptableValueRange<int>(1, 500)));
        _maxSamplesPerRaid = Config.Bind("Storage", "MaxSamplesPerRaid", 1800,
            new ConfigDescription("单局最多记录多少采样点（超过则 2 倍降采样，控制文件大小）", new AcceptableValueRange<int>(60, 20000)));
        _verbose = Config.Bind("General", "Verbose", false, "详细日志");

        _trendCap = Math.Max(30, _trendWindowSeconds.Value);

        // 输出目录：<游戏目录>\BepInEx\config\headless-telemetry
        try
        {
            // 用 Application.dataPath 推导，避免不同 BepInEx 版本的 Paths API 差异
            //   dataPath = <游戏目录>\EscapeFromTarkov_Data
            var gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot)) gameRoot = AppDomain.CurrentDomain.BaseDirectory;

            _outDir = Path.Combine(gameRoot, "BepInEx", "config", "headless-telemetry");
            _liveFile = Path.Combine(_outDir, "live.json");

            // 实例名取游戏目录名（EFT-headless / EFT-headless1 …）
            _instanceId = Sanitize(Path.GetFileName(gameRoot));
            _raidDir = Path.Combine(_outDir, "raids", _instanceId);

            Directory.CreateDirectory(_outDir);
            Directory.CreateDirectory(_raidDir);
        }
        catch (Exception e)
        {
            Log.LogError("[Telemetry] 初始化输出目录失败: " + e.Message);
        }

        _nextSampleAt = Time.unscaledTime + _sampleInterval.Value;

        Log.LogInfo($"[Telemetry] 已加载。实例={_instanceId} 输出={_outDir} 间隔={_sampleInterval.Value}s 趋势={_trendCap}点 战局上限={_maxRaidLogs.Value}");
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "headless";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.') sb.Append(c);
            else sb.Append('_');
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────

    private void Update()
    {
        _frameCount++;

        var now = Time.unscaledTime;
        if (now < _nextSampleAt) return;
        _nextSampleAt = now + _sampleInterval.Value;

        try
        {
            Tick();
        }
        catch (Exception e)
        {
            if (_verbose.Value) Log.LogWarning("[Telemetry] 采样异常: " + e.Message);
        }
    }

    private void Tick()
    {
        var fps = Mathf.RoundToInt(_frameCount / Math.Max(0.01f, _sampleInterval.Value));
        _frameCount = 0;

        var snap = Collect(fps);
        _totalSamples++;

        // ── 战局边界检测 ──
        bool inRaid = snap.InRaid;
        if (inRaid && _currentRaid == null)
        {
            StartRaid(snap);
        }
        else if (!inRaid && _currentRaid != null)
        {
            EndRaid(snap);
        }
        else if (inRaid && _currentRaid != null && snap.Map != _currentRaid.Map)
        {
            // 转图/换图：结束上一局，开新的一局
            EndRaid(snap);
            StartRaid(snap);
        }

        // ── 趋势环形缓冲 ──
        _trend.Add(snap);
        while (_trend.Count > _trendCap) _trend.RemoveAt(0);

        // ── 当前战局采样 ──
        if (_currentRaid != null)
        {
            // 战局已进行秒数（自己算，不依赖 GameWorld 的内部计时器）
            snap.RaidSeconds = (float)Math.Max(0, (snap.Utc - _currentRaid.StartedUtc).TotalSeconds);
            _currentRaid.Add(snap);
        }

        // ── 写文件 ──
        WriteLive(snap);
    }

    // ─────────────────────────────────────────────

    private Sample Collect(int fps)
    {
        var s = new Sample
        {
            Utc = DateTime.UtcNow,
            Fps = fps,
            Map = "",
            InRaid = false,
        };

        // 地图 / 是否在战局
        try
        {
            var gw = Singleton<GameWorld>.Instance;
            if (gw != null)
            {
                var loc = gw.LocationId;
                if (!string.IsNullOrEmpty(loc))
                {
                    s.Map = loc;
                    s.InRaid = true;
                }
            }
        }
        catch { }

        // Fika 数据
        try
        {
            if (CoopHandler.TryGetCoopHandler(out var coop) && coop != null)
            {
                // 玩家
                var humans = coop.HumanPlayers;
                if (humans != null)
                {
                    s.Humans = humans.Count;
                    s.HumansAlive = humans.Count(p => p != null && IsAlive(p));
                }

                // AI（玩家表里筛 IsAI）
                var all = coop.Players;
                if (all != null)
                {
                    int aiTotal = 0, aiAlive = 0;
                    foreach (var kv in all)
                    {
                        var p = kv.Value;
                        if (p == null) continue;
                        if (!IsAi(p)) continue;
                        aiTotal++;
                        if (IsAlive(p)) aiAlive++;
                    }
                    s.AiTotal = aiTotal;
                    s.AiAlive = aiAlive;
                }
            }
        }
        catch { }

        // 连接数
        try
        {
            var srv = Singleton<FikaServer>.Instance;
            if (srv != null && srv.NetServer != null)
                s.Clients = srv.NetServer.ConnectedPeersCount;
        }
        catch { }

        // 进程内存（MB）
        try { s.MemMb = (int)(GC.GetTotalMemory(false) / 1048576); } catch { }

        return s;
    }

    private static bool IsAi(Player p)
    {
        try { return p.IsAI; } catch { return false; }
    }

    private static bool IsAlive(Player p)
    {
        try
        {
            var hc = p.HealthController;
            return hc == null || hc.IsAlive;
        }
        catch { return true; }
    }

    // ─────────────────────────────────────────────

    private void StartRaid(Sample snap)
    {
        _currentRaid = new RaidLog
        {
            Instance = _instanceId,
            Map = snap.Map,
            StartedUtc = snap.Utc,
            MaxSamples = _maxSamplesPerRaid.Value,
        };
        _lastMap = snap.Map;
        Log.LogInfo($"[Telemetry] ★ 战局开始: {snap.Map}  @{snap.Utc:HH:mm:ss}");
    }

    private void EndRaid(Sample snap)
    {
        var raid = _currentRaid;
        _currentRaid = null;
        if (raid == null) return;

        raid.EndedUtc = snap.Utc;
        raid.Finish();

        var file = Path.Combine(_raidDir,
            $"{raid.StartedUtc:yyyyMMdd_HHmmss}_{Sanitize(raid.Map)}.json");

        try
        {
            File.WriteAllText(file, raid.ToJson(), new UTF8Encoding(false));
            Log.LogInfo($"[Telemetry] ★ 战局结束: {raid.Map}  时长 {raid.DurationSec:F0}s  "
                      + $"平均FPS {raid.FpsAvg:F1}  最低 {raid.FpsMin}  AI峰值 {raid.AiMax}  → {Path.GetFileName(file)}");
        }
        catch (Exception e)
        {
            Log.LogWarning("[Telemetry] 写战局日志失败: " + e.Message);
        }

        PruneRaidLogs();
    }

    /// <summary>★ 只保留最新 N 份战局日志，多余的删掉（按文件名的时间戳排序，即时间顺序）</summary>
    private void PruneRaidLogs()
    {
        try
        {
            var files = new DirectoryInfo(_raidDir).GetFiles("*.json")
                            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                            .ToList();

            int max = _maxRaidLogs.Value;
            if (files.Count <= max) return;

            foreach (var old in files.Skip(max))
            {
                try
                {
                    old.Delete();
                    if (_verbose.Value) Log.LogInfo("[Telemetry] 删除最老的战局日志: " + old.Name);
                }
                catch { }
            }
            Log.LogInfo($"[Telemetry] 战局日志已裁剪到 {max} 份（删了 {files.Count - max} 份）");
        }
        catch (Exception e)
        {
            if (_verbose.Value) Log.LogWarning("[Telemetry] 裁剪战局日志失败: " + e.Message);
        }
    }

    // ─────────────────────────────────────────────

    private void WriteLive(Sample snap)
    {
        if (string.IsNullOrEmpty(_liveFile)) return;
        // 别太频繁（采样间隔 ≥0.25s，这里最多同频）
        try
        {
            var sb = new StringBuilder(4096);
            sb.Append('{');
            sb.Append("\"instance\":\"").Append(Esc(_instanceId)).Append("\",");
            sb.Append("\"updatedUtc\":\"").Append(snap.Utc.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
            sb.Append("\"pluginStartedUtc\":\"").Append(_startedUtc.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
            sb.Append("\"totalSamples\":").Append(_totalSamples).Append(',');
            sb.Append("\"sampleInterval\":").Append(F(_sampleInterval.Value)).Append(',');
            sb.Append("\"current\":").Append(snap.Json()).Append(',');

            // 当前战局的摘要（进行中）
            sb.Append("\"currentRaid\":");
            sb.Append(_currentRaid == null ? "null" : _currentRaid.SummaryJson());
            sb.Append(',');

            // 趋势（紧凑数组，减少体积）
            sb.Append("\"trend\":[");
            for (int i = 0; i < _trend.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(_trend[i].CompactJson());
            }
            sb.Append(']');
            sb.Append('}');

            File.WriteAllText(_liveFile, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            if (_verbose.Value) Log.LogWarning("[Telemetry] 写 live.json 失败: " + e.Message);
        }
    }

    internal static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    internal static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // ─────────────────────────────────────────────
    // 数据结构
    // ─────────────────────────────────────────────

    internal sealed class Sample
    {
        public DateTime Utc;
        public int Fps;
        public int AiAlive;
        public int AiTotal;
        public int Humans;
        public int HumansAlive;
        public int Clients;
        public int MemMb;
        public string Map;
        public bool InRaid;
        public float RaidSeconds;

        public string Json()
        {
            return "{"
                 + "\"utc\":\"" + Utc.ToString("o", CultureInfo.InvariantCulture) + "\","
                 + "\"fps\":" + Fps + ","
                 + "\"aiAlive\":" + AiAlive + ","
                 + "\"aiTotal\":" + AiTotal + ","
                 + "\"humans\":" + Humans + ","
                 + "\"humansAlive\":" + HumansAlive + ","
                 + "\"clients\":" + Clients + ","
                 + "\"memMb\":" + MemMb + ","
                 + "\"map\":\"" + Esc(Map) + "\","
                 + "\"inRaid\":" + (InRaid ? "true" : "false") + ","
                 + "\"raidSeconds\":" + F(RaidSeconds)
                 + "}";
        }

        /// <summary>趋势用的紧凑格式：[unixSec, fps, aiAlive, humansAlive, clients]</summary>
        public string CompactJson()
        {
            var unix = new DateTimeOffset(Utc).ToUnixTimeSeconds();
            return "[" + unix + "," + Fps + "," + AiAlive + "," + HumansAlive + "," + Clients + "]";
        }
    }

    internal sealed class RaidLog
    {
        public string Instance;
        public string Map;
        public DateTime StartedUtc;
        public DateTime EndedUtc;

        public int MaxSamples = 1800;

        public double DurationSec;
        public double FpsAvg, FpsMin = double.MaxValue, FpsMax;
        public int AiMax, AiTotalMax, HumansMax, ClientsMax;
        public int PeakSampleCount;

        private readonly List<Sample> _samples = new List<Sample>();

        /// <summary>加入一个采样点；超过上限就 2 倍降采样（保留偶数索引）以控制文件大小</summary>
        public void Add(Sample s)
        {
            _samples.Add(s);
            if (_samples.Count > MaxSamples)
            {
                for (int i = _samples.Count - 2; i >= 0; i -= 2) _samples.RemoveAt(i);
                // 降采样后放大上限，避免每帧都触发
                MaxSamples = Math.Min(MaxSamples * 2, 200000);
            }
        }

        public void Finish()
        {
            DurationSec = Math.Max(0, (EndedUtc - StartedUtc).TotalSeconds);
            PeakSampleCount = _samples.Count;

            if (_samples.Count == 0) { FpsMin = 0; return; }

            double sum = 0;
            foreach (var s in _samples)
            {
                sum += s.Fps;
                if (s.Fps < FpsMin) FpsMin = s.Fps;
                if (s.Fps > FpsMax) FpsMax = s.Fps;
                if (s.AiAlive > AiMax) AiMax = s.AiAlive;
                if (s.AiTotal > AiTotalMax) AiTotalMax = s.AiTotal;
                if (s.Humans > HumansMax) HumansMax = s.Humans;
                if (s.Clients > ClientsMax) ClientsMax = s.Clients;
            }
            FpsAvg = sum / _samples.Count;
            if (FpsMin == double.MaxValue) FpsMin = 0;
        }

        public string SummaryJson()
        {
            return "{"
                 + "\"map\":\"" + Esc(Map) + "\","
                 + "\"startedUtc\":\"" + StartedUtc.ToString("o", CultureInfo.InvariantCulture) + "\","
                 + "\"durationSec\":" + F((float)DurationSec) + ","
                 + "\"samples\":" + _samples.Count + ","
                 + "\"fpsAvg\":" + F((float)FpsAvg) + ","
                 + "\"fpsMin\":" + F((float)(FpsMin == double.MaxValue ? 0 : FpsMin)) + ","
                 + "\"fpsMax\":" + F((float)FpsMax) + ","
                 + "\"aiMax\":" + AiMax + ","
                 + "\"humansMax\":" + HumansMax
                 + "}";
        }

        public string ToJson()
        {
            var sb = new StringBuilder(65536);
            sb.Append('{');
            sb.Append("\"schema\":1,");
            sb.Append("\"instance\":\"").Append(Esc(Instance)).Append("\",");
            sb.Append("\"map\":\"").Append(Esc(Map)).Append("\",");
            sb.Append("\"startedUtc\":\"").Append(StartedUtc.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
            sb.Append("\"endedUtc\":\"").Append(EndedUtc.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
            sb.Append("\"durationSec\":").Append(F((float)DurationSec)).Append(',');
            sb.Append("\"samples\":").Append(_samples.Count).Append(',');
            sb.Append("\"stats\":{");
            sb.Append("\"fpsAvg\":").Append(F((float)FpsAvg)).Append(',');
            sb.Append("\"fpsMin\":").Append(F((float)(FpsMin == double.MaxValue ? 0 : FpsMin))).Append(',');
            sb.Append("\"fpsMax\":").Append(F((float)FpsMax)).Append(',');
            sb.Append("\"aiMax\":").Append(AiMax).Append(',');
            sb.Append("\"aiTotalMax\":").Append(AiTotalMax).Append(',');
            sb.Append("\"humansMax\":").Append(HumansMax).Append(',');
            sb.Append("\"clientsMax\":").Append(ClientsMax);
            sb.Append("},");
            sb.Append("\"series\":[");
            for (int i = 0; i < _samples.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(_samples[i].CompactJson());
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
