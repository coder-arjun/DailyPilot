using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DailyPilot.Models;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

/// <summary>Lightweight JSON endpoint the layout polls for browser notifications (PRD §11).</summary>
[Authorize]
[Route("api/notifications")]
public class NotificationsController : Controller
{
    private readonly INotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(INotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _notifications = notifications;
        _userManager = userManager;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
    {
        var userId = _userManager.GetUserId(User)!;
        var pending = await _notifications.GetPendingBrowserRemindersAsync(userId);
        return Json(pending.Select(r => new { id = r.Id, message = r.Message, type = r.Type.ToString() }));
    }

    // Called via fetch() from the notification poller; authenticated and only
    // flips the user's own reminder to "sent", so it's exempt from anti-forgery.
    [HttpPost("ack/{id:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Ack(int id)
    {
        await _notifications.MarkSentAsync(id);
        return Ok();
    }
}
