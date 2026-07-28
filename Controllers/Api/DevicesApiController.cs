using System.ComponentModel.DataAnnotations;
using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

[Route("api/v1/devices")]
public class DevicesApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    public DevicesApiController(ApplicationDbContext db) => _db = db;

    public sealed record DeviceRequest([Required, StringLength(512)] string Token, string? Platform);

    /// <summary>Registers (or refreshes) an FCM device token for the current user.
    /// A token that re-appears under a different account moves to that account.</summary>
    [HttpPost]
    public async Task<IActionResult> Register(DeviceRequest request)
    {
        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == request.Token);
        if (existing is null)
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                UserId = CurrentUserId,
                Token = request.Token,
                Platform = string.IsNullOrWhiteSpace(request.Platform) ? "android" : request.Platform,
            });
        }
        else
        {
            existing.UserId = CurrentUserId;
            existing.LastSeenAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete]
    public async Task<IActionResult> Unregister(DeviceRequest request)
    {
        var row = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token && t.UserId == CurrentUserId);
        if (row is not null)
        {
            _db.DeviceTokens.Remove(row);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}
