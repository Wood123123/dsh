using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Dsh.HeadlessPanel;

/// <summary>
/// Tails SPT's own server log into the LogStore, so the merged raid timeline also has the
/// server side.
///
/// No plugin is needed here — and none could be: SPT.Server.exe is a plain .NET console app,
/// it cannot host a BepInEx plugin. But the panel already runs *inside* that process, so it can
/// simply read the file. That also means this costs no network and no game-process time.
///
/// The server log is far noisier than the game logs: every panel poll writes several
/// "[客户端请求] …/headlesspanel/api/…" lines. Those are filtered by default — otherwise the
/// timeline would be mostly the panel watching itself.
/// </summary>
public static class ServerLogTailer
{
    private static Thread _worker;
    private static volatile bool _running;
    private static long _offset;
    private static string _currentFile;

    private static readonly object Gate = new();

    /// <summary>Pause between polls. The server log grows at well under 1 line/s.</summary>
    private const int PollMs = 3000;

    /// <summary>Give up on a single file once it exceeds this, so a runaway log cannot balloon memory.</summary>
    private const int MaxBatchLines = 5000;

    public static bool IsRunning => _running;

    public static string LogDir =>
        Path.Combine(AppContext.BaseDirectory, "user", "logs", "spt");

    /// <summary>
    /// Remembers how far we have read, so a panel restart does not re-ingest the whole day.
    ///
    /// ★ 没有它的话，每次重启都会把当天的日志重新写一遍 —— 段文件是追加的，
    ///   于是同一批行在合并时间线上出现多次。
    /// </summary>
    private static string StatePath =>
        // ★ 同理放在 user\logs 下：这个文件每批日志都会更新，
//   放在 user\mods 里会让 NarcoNet 每次都以为服务端有更新。
        Path.Combine(AppContext.BaseDirectory, "user", "logs", "HeadlessPanel", "spt-tailer.state");

    /// <summary>Idempotent — safe to call from a controller constructor on every request.</summary>
    public static void Start()
    {
        lock (Gate)
        {
            if (_running) return;
            _running = true;

            LoadState();

            _worker = new Thread(Loop) { IsBackground = true, Name = "SptServerLogTail" };
            _worker.Start();
        }
    }

    public static void Stop()
    {
        _running = false;
        try { _worker?.Join(1500); } catch { }
        _worker = null;
        SaveState();
    }

    private static void LoadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return;

            var parts = File.ReadAllText(StatePath, Encoding.UTF8).Split('|');
            if (parts.Length >= 2 && long.TryParse(parts[1], out var off) && off >= 0)
            {
                _currentFile = parts[0];
                _offset = off;
                Console.WriteLine($"[HeadlessPanel] 服务端日志 tailer 从上次位置继续: "
                                + $"{Path.GetFileName(_currentFile)} @ {_offset} 字节");
            }
        }
        catch { }
    }

    private static void SaveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(StatePath, $"{_currentFile}|{_offset}", new UTF8Encoding(false));
        }
        catch { }
    }

    private static void Loop()
    {
        while (_running)
        {
            try
            {
                // ★ 把"服务端日志时间戳是不是 UTC"同步给解析器。
                //   实测 SPT 的行内时间就是 UTC（日志写 05:39，文件改于本地 13:39）。
                LogStore.ServerLogTimestampIsUtc = PanelConfig.Current.ServerLogTimestampIsUtc;
                // ★ 把"战局结束后还要归属多久"也同步进去，供服务端行找到刚结束的那一局
                LogStore.ServerLogGraceSecondsCached = PanelConfig.Current.ServerLogGraceSeconds;

                var file = LatestFile();
                if (file != null)
                {
                    // Daily rotation: a new file name means start from the top.
                    if (!string.Equals(file, _currentFile, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentFile = file;
                        _offset = 0;
                    }

                    var lines = ReadNew(file);
                    if (lines.Count > 0)
                    {
                        var cfg = PanelConfig.Current;
                        var filtered = ApplyFilter(lines, cfg.ServerLogFilter);

                        // ★ 只在【战局窗口】内采集。
                        //
                        //   SPT 服务端日志绝大部分是启动、加载 44 个模组、以及面板自己的
                        //   HTTP 轮询 —— 与某一局战局无关。全量收进来只会淹没时间线。
                        //   战局内有活跃段（或刚结束 60 秒内，用于捕捉结算请求）才收。
                        //
                        //   不在窗口内时仍然推进 offset（下面 SaveState），这样这些行只是
                        //   被跳过，不会在战局开始后又被重放一遍。
                        if (filtered.Count > 0 && LogStore.IsRaidWindow(cfg.ServerLogGraceSeconds))
                        {
                            LogStore.Ingest("SPT-Server", "server", Environment.MachineName,
                                            DateTime.UtcNow, filtered);
                        }

                        // ★ 每批之后持久化位置，进程被强杀也不会重复读
                        SaveState();
                    }
                }
            }
            catch
            {
                // Tailing is best-effort; never let it take the panel down.
            }

            for (var slept = 0; slept < PollMs && _running; slept += 250)
            {
                Thread.Sleep(250);
            }
        }
    }

    private static string LatestFile()
    {
        try
        {
            if (!Directory.Exists(LogDir)) return null;

            return new DirectoryInfo(LogDir)
                .GetFiles("spt*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    private static List<string> ReadNew(string path)
    {
        var result = new List<string>();

        FileInfo fi;
        try { fi = new FileInfo(path); } catch { return result; }
        if (!fi.Exists) return result;

        if (fi.Length < _offset) _offset = 0;          // truncated
        if (fi.Length == _offset) return result;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete,
                                          64 * 1024, FileOptions.SequentialScan);
            fs.Seek(_offset, SeekOrigin.Begin);
            using var sr = new StreamReader(fs, Encoding.UTF8, true, 64 * 1024);

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                result.Add(line);
                if (result.Count >= MaxBatchLines) break;
            }

            _offset = fs.Position;

            // Don't emit a half-written last line.
            if (result.Count > 0 && !EndsWithNewline(fs))
            {
                var last = result[result.Count - 1];
                result.RemoveAt(result.Count - 1);
                _offset -= Encoding.UTF8.GetByteCount(last) + Environment.NewLine.Length;
                if (_offset < 0) _offset = 0;
            }
        }
        catch { }

        return result;
    }

    private static bool EndsWithNewline(FileStream fs)
    {
        try
        {
            if (fs.Length == 0) return true;
            var pos = fs.Position;
            fs.Seek(-1, SeekOrigin.End);
            var b = fs.ReadByte();
            fs.Seek(pos, SeekOrigin.Begin);
            return b == '\n' || b == '\r';
        }
        catch { return true; }
    }

    /// <summary>
    /// Server-log noise control.
    ///   NoPanel  — drop the panel's own polling (default)
    ///   NoHttp   — drop every "[客户端请求]" line
    ///   WarnPlus — keep only Warning / Error / Fatal
    ///   All      — keep everything
    /// </summary>
    private static List<string> ApplyFilter(List<string> lines, string mode)
    {
        mode = string.IsNullOrWhiteSpace(mode) ? "NoPanel" : mode.Trim();

        if (mode.Equals("All", StringComparison.OrdinalIgnoreCase)) return lines;

        if (mode.Equals("WarnPlus", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Where(l =>
                l.IndexOf("][Error]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                l.IndexOf("][Warning]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                l.IndexOf("][Fatal]", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (mode.Equals("NoHttp", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Where(l => l.IndexOf("[客户端请求]", StringComparison.Ordinal) < 0).ToList();
        }

        // NoPanel (default): keep everything except the panel's own request lines.
        return lines.Where(l =>
            !(l.IndexOf("[客户端请求]", StringComparison.Ordinal) >= 0 &&
              l.IndexOf("/headlesspanel/", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
    }
}
