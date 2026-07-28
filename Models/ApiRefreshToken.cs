namespace DailyPilot.Models;

/// <summary>Rotating refresh token for the mobile REST API (stored hashed, never raw).</summary>
public class ApiRefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Base64(SHA256(raw token)) — the raw value exists only on the client.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
}
