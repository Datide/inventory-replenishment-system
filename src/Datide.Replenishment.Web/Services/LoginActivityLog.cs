using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Datide.Replenishment.Web.Services;

/// <summary>
/// Snapshot of a single successful login, captured on the request thread so the
/// background write never has to touch HttpContext after the response completes.
/// The IP is already masked by <see cref="LoginActivityLog.MaskIp"/> before this
/// record is constructed, so no full IP ever lives in memory beyond this point.
/// </summary>
public sealed record LoginActivity(
    string Username,
    string Role,
    string ClientIpMasked,
    string UserAgent);

/// <summary>
/// Records successful login events to a local JSON-lines file. This is the
/// credential-free replacement for the withdrawn SMTP notifier: the container
/// never sends email and holds no credentials. An external poller on the
/// owner's machine reads the low-information summary via /demo-activity.
/// </summary>
public sealed class LoginActivityLog
{
    private const int MaxLines = 500;
    private const string EmptySummary = "Datide demo activity\nTotal logins: 0";

    // Local log file only (never embedded in HTML), so we can relax the default
    // HTML-sensitive escaping and write literal '+' / '<' / '>' etc. This keeps
    // the JSON line byte-for-byte identical to the spec's shape (e.g. "+08").
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly object SyncLock = new();

    private readonly string _logPath;
    private readonly ILogger<LoginActivityLog> _logger;

    public LoginActivityLog(IWebHostEnvironment env, ILogger<LoginActivityLog> logger)
    {
        _logger = logger;
        _logPath = Path.Combine(env.ContentRootPath, "App_Data", "logins.log");
    }

    /// <summary>
    /// Appends one JSON line for a successful login. Thread-safe, bounded to the
    /// most recent 500 lines, and never throws — any failure is logged and
    /// swallowed so a broken filesystem can never break the login flow.
    /// </summary>
    public void Record(LoginActivity a)
    {
        try
        {
            // UTC+8 wall-clock time, rendered as a fixed string so no timezone
            // database is required inside the container.
            var timestamp = DateTime.UtcNow.AddHours(8)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " +08";

            var line = JsonSerializer.Serialize(new
            {
                t = timestamp,
                u = a.Username,
                r = a.Role,
                ip = a.ClientIpMasked,
                ua = a.UserAgent,
            }, JsonOptions);

            lock (SyncLock)
            {
                var dir = Path.GetDirectoryName(_logPath)!;
                Directory.CreateDirectory(dir);

                var lines = File.Exists(_logPath) ? File.ReadAllLines(_logPath).ToList() : new List<string>();
                lines.Add(line);
                if (lines.Count > MaxLines)
                {
                    lines = lines.Skip(lines.Count - MaxLines).ToList();
                }

                File.WriteAllLines(_logPath, lines);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record login activity.");
        }
    }

    /// <summary>
    /// Masks an IP address to a low-information form so no full IP is ever
    /// persisted: IPv4 is masked to /24, IPv6 to /48. Null or unparseable
    /// values map to "unknown".
    /// </summary>
    public static string MaskIp(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        try
        {
            var bytes = address.GetAddressBytes();

            if (bytes.Length == 4)
            {
                // IPv4 -> mask to /24, e.g. 203.145.94.0/24
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.0/24";
            }

            if (bytes.Length == 16)
            {
                // IPv6 -> keep the first 3 groups then ::/48, e.g. 2001:db8:1234::/48
                var g1 = (bytes[0] << 8) | bytes[1];
                var g2 = (bytes[2] << 8) | bytes[3];
                var g3 = (bytes[4] << 8) | bytes[5];
                return $"{g1:x}:{g2:x}:{g3:x}::/48";
            }

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Returns a plain-text summary of login activity. Never throws — any error
    /// (including a corrupt line) is swallowed and a safe fallback is returned.
    /// </summary>
    public string GetActivitySummary()
    {
        try
        {
            string[] lines;
            lock (SyncLock)
            {
                if (!File.Exists(_logPath))
                {
                    return EmptySummary;
                }

                lines = File.ReadAllLines(_logPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();
            }

            if (lines.Length == 0)
            {
                return EmptySummary;
            }

            var entries = new List<(string T, string U, string R, string Ip)>();
            foreach (var line in lines)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var t = ReadString(root, "t");
                    var u = ReadString(root, "u");
                    var r = ReadString(root, "r");
                    var ip = ReadString(root, "ip");
                    if (t is null || u is null || r is null || ip is null)
                    {
                        continue; // corrupt / incomplete line — skip silently
                    }

                    entries.Add((t, u, r, ip));
                }
                catch
                {
                    // corrupt line — skip silently
                }
            }

            if (entries.Count == 0)
            {
                return EmptySummary;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Datide demo activity");
            sb.AppendLine($"Total logins: {lines.Length}");
            sb.AppendLine("---");

            foreach (var e in entries.AsEnumerable().Reverse().Take(20))
            {
                sb.AppendLine($"{e.T} | {e.U} | {e.R} | {e.Ip}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read login activity summary.");
            return EmptySummary;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }
}
