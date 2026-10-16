using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Dsh.HeadlessPanel;

/// <summary>
/// One recorded control-panel action.
/// </summary>
public sealed class AuditEntry
{
    [JsonPropertyName("utc")]
    public DateTime Utc { get; set; }

    /// <summary>Client address the request came from (X-Forwarded-For aware).</summary>
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    /// <summary>SPT web-UI account name, when the host exposes one.</summary>
    [JsonPropertyName("account")]
    public string Account { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("instance")]
    public string Instance { get; set; } = "";

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "";

    [JsonPropertyName("agent")]
    public string Agent { get; set; } = "";
}

/// <summary>
/// Append-only audit trail of who operated the headless clients from which address.
///
/// Stored as JSON Lines (<c>audit.jsonl</c>) so a record is one atomic append — a crash
/// mid-write can at worst lose the last line, never the whole history. The file is trimmed
/// to <see cref="MaxEntries"/> by rewriting it, which only happens once per overflow.
///
/// The log survives restarts by design: it is the point of the feature. The panel reads it
/// back on every page load.
/// </summary>
public static class AuditLog
{
    /// <summary>How many entries to keep on disk.</summary>
    public const int MaxEntries = 2000;

    /// <summary>How many entries a single page request may return.</summary>
    public const int MaxReadEntries = 500;

    private static readonly object Gate = new();

    public static string FilePath =>
        // ★ 放在 user\logs 下，不是 user\mods —— NarcoNet 会把整个 user\mods 分发给客户端，
        //   而审计文件每做一次操作就变，那会让客户端每次启动都收到"更新"。
        //   user\logs 已被 NarcoNet 的 **/logs/** 规则排除。
        Path.Combine(AppContext.BaseDirectory, "user", "logs", "HeadlessPanel", "audit.jsonl");

    // ------------------------------------------------------------------ write

    /// <summary>
    /// Record one action. Never throws: an audit failure must not break the operation itself,
    /// so the error is surfaced in the returned detail instead.
    /// </summary>
    public static void Record(HttpContext context, string action, string profileId,
                              string instance, bool ok, string detail)
    {
        var entry = new AuditEntry
        {
            Utc = DateTime.UtcNow,
            Ip = ClientIp(context),
            Account = AccountName(context),
            Action = action,
            ProfileId = profileId ?? "",
            Instance = instance ?? "",
            Ok = ok,
            Detail = Trim(detail, 400),
            Agent = Trim(context?.Request?.Headers["User-Agent"].FirstOrDefault(), 200)
        };

        Append(entry);
    }

    /// <summary>Best-effort client address; honours a leading X-Forwarded-For hop.</summary>
    public static string ClientIp(HttpContext context)
    {
        try
        {
            var forwarded = context?.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',')[0].Trim();
                if (first.Length > 0)
                {
                    return Normalize(first);
                }
            }

            var remote = context?.Connection?.RemoteIpAddress;
            return remote == null ? "" : Normalize(remote.ToString());
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Map loopback forms to a friendly label, and strip the IPv6 scope id.</summary>
    private static string Normalize(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return "";
        }

        var v = ip.Trim();
        var pct = v.IndexOf('%');
        if (pct > 0)
        {
            v = v.Substring(0, pct);   // "fe80::1%eth0" -> "fe80::1"
        }

        if (v == "::1" || v == "127.0.0.1" || v == "0.0.0.0" || v == "::ffff:127.0.0.1")
        {
            return "本机 (127.0.0.1)";
        }

        // Normalise the IPv4-mapped IPv6 form so one client does not appear as two.
        if (v.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
        {
            v = v.Substring(7);
        }

        return v;
    }

    private static string AccountName(HttpContext context)
    {
        try
        {
            var name = context?.User?.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? "" : name;
        }
        catch
        {
            return "";
        }
    }

    private static string Trim(string s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "…");

    private static void Append(AuditEntry entry)
    {
        lock (Gate)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var line = JsonSerializer.Serialize(entry);
                System.IO.File.AppendAllText(FilePath, line + Environment.NewLine, new UTF8Encoding(false));

                TrimIfNeeded();
            }
            catch
            {
                // Auditing is best-effort; the control action itself already succeeded or failed
                // on its own merits and the caller reports that separately.
            }
        }
    }

    /// <summary>
    /// Keep only the newest <see cref="MaxEntries"/> lines. Rewriting the file is the one
    /// non-append operation, and it only runs when the log has actually overflowed.
    /// </summary>
    private static void TrimIfNeeded()
    {
        try
        {
            var lines = System.IO.File.ReadAllLines(FilePath, Encoding.UTF8)
                                     .Where(l => !string.IsNullOrWhiteSpace(l))
                                     .ToList();

            if (lines.Count <= MaxEntries)
            {
                return;
            }

            var keep = lines.Skip(lines.Count - MaxEntries);
            System.IO.File.WriteAllLines(FilePath, keep, new UTF8Encoding(false));
        }
        catch
        {
            // Trim failure just means the file grows for now; the next append tries again.
        }
    }

    // ------------------------------------------------------------------- read

    /// <summary>Newest-first slice of the trail, capped by <see cref="MaxReadEntries"/>.</summary>
    public static List<AuditEntry> Read(int max = 200)
    {
        var result = new List<AuditEntry>();

        lock (Gate)
        {
            try
            {
                if (!System.IO.File.Exists(FilePath))
                {
                    return result;
                }

                var take = Math.Clamp(max, 1, MaxReadEntries);

                var lines = System.IO.File.ReadAllLines(FilePath, Encoding.UTF8)
                                         .Where(l => !string.IsNullOrWhiteSpace(l))
                                         .ToList();

                for (var i = lines.Count - 1; i >= 0 && result.Count < take; i--)
                {
                    try
                    {
                        var e = JsonSerializer.Deserialize<AuditEntry>(lines[i]);
                        if (e != null)
                        {
                            result.Add(e);
                        }
                    }
                    catch
                    {
                        // One corrupt line must not hide the rest of the history.
                    }
                }
            }
            catch
            {
                // Unreadable file behaves like an empty trail.
            }
        }

        return result;
    }

    public static int Count()
    {
        lock (Gate)
        {
            try
            {
                return System.IO.File.Exists(FilePath)
                    ? System.IO.File.ReadAllLines(FilePath, Encoding.UTF8).Count(l => !string.IsNullOrWhiteSpace(l))
                    : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
