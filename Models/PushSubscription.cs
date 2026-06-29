namespace DailyPilot.Models;

/// <summary>A browser Web Push subscription for delivering reminders when the app is closed.</summary>
public class PushSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
