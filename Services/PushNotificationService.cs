using DailyPilot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace DailyPilot.Services;

public class PushOptions
{
    public string Subject { get; set; } = "mailto:no-reply@daypilot.app";
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}

public interface IPushNotificationService
{
    bool IsEnabled { get; }
    Task SendToUserAsync(string userId, string title, string body, string? url = null, string? tag = null);
}

/// <summary>Delivers Web Push notifications to a user's registered browsers (PRD §11 Push).</summary>
public class PushNotificationService : IPushNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly PushOptions _options;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _client = new();

    public PushNotificationService(ApplicationDbContext db, IOptions<PushOptions> options, ILogger<PushNotificationService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.IsConfigured;

    public async Task SendToUserAsync(string userId, string title, string body, string? url = null, string? tag = null)
    {
        if (!IsEnabled) return;

        var subs = await _db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
        if (subs.Count == 0) return;

        var vapid = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body, url, tag });

        foreach (var s in subs)
        {
            try
            {
                var sub = new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                await _client.SendNotificationAsync(sub, payload, vapid);
            }
            catch (WebPushException ex) when ((int)ex.StatusCode is 404 or 410)
            {
                // Subscription expired / unsubscribed — clean it up.
                _db.PushSubscriptions.Remove(s);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web push to {Endpoint} failed.", s.Endpoint);
            }
        }
        await _db.SaveChangesAsync();
    }
}
