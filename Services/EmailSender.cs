using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DailyPilot.Services;

/// <summary>SMTP configuration bound from the "Email" section of appsettings.</summary>
public class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@daypilot.app";
    public string FromName { get; set; } = "DayPilot";
}

/// <summary>Application email abstraction (also adapts ASP.NET Identity's IEmailSender).</summary>
public interface IAppEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, string? toName = null);
}

/// <summary>
/// PRD §11 Email Notifications. Sends via SMTP using MailKit. When email is not
/// configured (Email:Enabled = false) it logs instead, so the app still runs in
/// dev without an SMTP server.
/// </summary>
public class SmtpEmailSender : IAppEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string? toName = null)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogInformation("[EMAIL DISABLED → {To}] {Subject}", toEmail, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var secureOption = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(_options.Host, _options.Port, secureOption);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Don't let a mail failure break the background job / request.
            _logger.LogError(ex, "Failed to send email to {To}", toEmail);
        }
    }
}
