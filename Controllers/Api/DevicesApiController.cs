using System.ComponentModel.DataAnnotations;
using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

[Route("api/v1/devices")]
public class DevicesApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFcmPushSender _fcm;

    public DevicesApiController(ApplicationDbContext db, IFcmPushSender fcm)
    {
        _db = db;
        _fcm = fcm;
    }

    /// <summary>Diagnostic: sends a native-only (FCM) test notification to the
    /// caller's registered devices and reports what the server knows.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest()
    {
        var deviceCount = await _db.DeviceTokens.CountAsync(t => t.UserId == CurrentUserId);
        if (_fcm.IsEnabled && deviceCount > 0)
            await _fcm.SendToUserAsync(CurrentUserId, "DayPilot app test",
                "Native push works! You should hear this too if Speak reminders is on. 🎉", "/Tasks");
        return Ok(new { ok = true, fcmEnabled = _fcm.IsEnabled, deviceCount });
    }

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
