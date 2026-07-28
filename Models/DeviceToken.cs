namespace DailyPilot.Models;

/// <summary>An FCM registration token for one of a user's mobile devices.</summary>
public class DeviceToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
