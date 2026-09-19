using System.Globalization;
using System.Net;
using System.Net.Mail;

namespace Datide.Replenishment.Web.Services;

// Snapshot of a single successful login, captured on the request thread so the
// background send never has to touch HttpContext after the response completes.
public sealed record LoginNotification(
    string Username,
    string Role,
    string? ClientIp,
    string? UserAgent);

/// <summary>
/// Sends a short plain-text email to the app owner whenever someone logs in
/// successfully. Fully config-driven: when the Datide:Notify config is missing
/// or blank this class is a silent no-op, so the demo behaves exactly as before.
/// </summary>
public sealed class LoginNotifier
{
    private const string SmtpUserKey = "Datide:Notify:SmtpUser";
    private const string SmtpPasswordKey = "Datide:Notify:SmtpPassword";
    private const string ToKey = "Datide:Notify:To";
    private const string EnabledKey = "Datide:Notify:Enabled";

    private readonly IConfiguration _config;
    private readonly ILogger<LoginNotifier> _logger;

    public LoginNotifier(IConfiguration config, ILogger<LoginNotifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Notifies the owner of a successful login. Never throws — any failure is
    /// logged and swallowed so a broken SMTP setup can never break the login flow.
    /// </summary>
    public void NotifyLogin(LoginNotification info)
    {
        try
        {
            var smtpUser = _config[SmtpUserKey];
            var smtpPassword = _config[SmtpPasswordKey];
            var to = _config[ToKey];

            // Enabled is optional and defaults to true; only an explicit "false"
            // disables the notifier. An empty value (as shipped in appsettings.json)
            // must not blow up bool parsing.
            var enabled = true;
            if (bool.TryParse(_config[EnabledKey], out var parsed))
            {
                enabled = parsed;
            }

            if (!enabled ||
                string.IsNullOrWhiteSpace(smtpUser) ||
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(to))
            {
                _logger.LogInformation("Login notify disabled (Datide:Notify config missing).");
                return;
            }

            var subject = $"Datide demo login: {info.Username}";
            var body = BuildBody(info);

#pragma warning disable SYSLIB0014
            // SmtpClient is obsolete in modern .NET but is chosen deliberately to
            // avoid adding a NuGet dependency to a portfolio demo; MailKit would be
            // the production choice.
            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
            };
            using var mail = new MailMessage(smtpUser, to, subject, body);
            smtp.Send(mail);
#pragma warning restore SYSLIB0014

            _logger.LogInformation("Login notification sent for user {Username}.", info.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login notification failed.");
        }
    }

    private static string BuildBody(LoginNotification info)
    {
        // UTC+8 wall-clock time (the owner's timezone), rendered as a fixed string
        // so no timezone database is required inside the container.
        var utc8 = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var lines = new[]
        {
            "Datide demo login notification",
            $"Username: {info.Username}",
            $"Role: {info.Role}",
            $"Time (UTC+8): {utc8}",
            $"Client IP: {(string.IsNullOrWhiteSpace(info.ClientIp) ? "(unknown)" : info.ClientIp)}",
            $"User-Agent: {(string.IsNullOrWhiteSpace(info.UserAgent) ? "(unknown)" : info.UserAgent)}",
        };

        return string.Join(Environment.NewLine, lines);
    }
}
