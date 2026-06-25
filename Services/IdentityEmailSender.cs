using Microsoft.AspNetCore.Identity.UI.Services;

namespace DailyPilot.Services;

/// <summary>
/// Adapts ASP.NET Identity's <see cref="IEmailSender"/> (used for password reset,
/// email confirmation, etc.) onto the application's SMTP sender.
/// </summary>
public class IdentityEmailSender : IEmailSender
{
    private readonly IAppEmailSender _sender;

    public IdentityEmailSender(IAppEmailSender sender) => _sender = sender;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => _sender.SendAsync(email, subject, htmlMessage);
}
