using System.Security.Cryptography;
using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services.Api;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(string userId);
    Task<(ApplicationUser User, string NewRawToken)?> RotateAsync(string rawToken);
    Task RevokeAsync(string rawToken);
}

/// <summary>
/// Rotating refresh tokens for the mobile API. Raw tokens are returned to the
/// client once and stored only as SHA-256 hashes; every use revokes and replaces.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private const int LifetimeDays = 60;
    private readonly ApplicationDbContext _db;
    public RefreshTokenService(ApplicationDbContext db) => _db = db;

    public static string Hash(string raw) =>
        Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

    public async Task<string> IssueAsync(string userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.ApiRefreshTokens.Add(new ApiRefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(LifetimeDays),
        });
        await _db.SaveChangesAsync();
        return raw;
    }

    public async Task<(ApplicationUser User, string NewRawToken)?> RotateAsync(string rawToken)
    {
        var row = await _db.ApiRefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken));
        if (row?.User is null || row.RevokedAtUtc is not null || row.ExpiresAtUtc <= DateTime.UtcNow)
            return null;
        row.RevokedAtUtc = DateTime.UtcNow;
        var newRaw = await IssueAsync(row.UserId);
        return (row.User, newRaw);
    }

    public async Task RevokeAsync(string rawToken)
    {
        var row = await _db.ApiRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken));
        if (row is not null && row.RevokedAtUtc is null)
        {
            row.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
