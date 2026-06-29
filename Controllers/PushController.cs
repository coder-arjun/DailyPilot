using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

/// <summary>Stores browser Web Push subscriptions so reminders can reach a closed app.</summary>
[Authorize]
[Route("api/push")]
public class PushController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService _push;

    public PushController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPushNotificationService push)
    {
        _db = db;
        _userManager = userManager;
        _push = push;
    }

    public class SubscriptionDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public KeysDto Keys { get; set; } = new();
        public class KeysDto { public string P256dh { get; set; } = string.Empty; public string Auth { get; set; } = string.Empty; }
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscriptionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Endpoint) || string.IsNullOrWhiteSpace(dto.Keys.P256dh)) return BadRequest();
        var userId = _userManager.GetUserId(User)!;

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (existing is null)
        {
            _db.PushSubscriptions.Add(new Models.PushSubscription
            {
                UserId = userId,
                Endpoint = dto.Endpoint,
                P256dh = dto.Keys.P256dh,
                Auth = dto.Keys.Auth,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] SubscriptionDto dto)
    {
        var sub = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (sub is not null) { _db.PushSubscriptions.Remove(sub); await _db.SaveChangesAsync(); }
        return Ok();
    }

    // Sends a test notification to this user's subscribed devices so they can confirm push works.
    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        var userId = _userManager.GetUserId(User)!;
        var count = await _db.PushSubscriptions.CountAsync(s => s.UserId == userId);
        if (!_push.IsEnabled)
            return Json(new { sent = 0, message = "Push is not configured on the server." });
        if (count == 0)
            return Json(new { sent = 0, message = "No subscribed device yet — tap Enable first." });

        await _push.SendToUserAsync(userId,
            "✅ DayPilot notifications are on",
            "This is a test — your task reminders will arrive here.",
            "/Tasks", "dp-test");
        return Json(new { sent = count });
    }
}
