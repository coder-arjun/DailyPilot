using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

/// <summary>
/// Key-protected endpoint so an external uptime/cron service (e.g. cron-job.org) can
/// drive reminder delivery every minute — this both wakes the free-tier app and pushes
/// due reminders to phones, independent of in-process scheduling. Not for browsers.
/// </summary>
[AllowAnonymous]
[Route("api/jobs")]
public class JobsController : Controller
{
    private readonly IReminderDispatchService _reminders;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;

    public JobsController(IReminderDispatchService reminders, INotificationService notifications, IConfiguration config)
    {
        _reminders = reminders;
        _notifications = notifications;
        _config = config;
    }

    [HttpGet("run")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Run(string key, string job = "reminders")
    {
        var expected = _config["Jobs:TriggerKey"];
        if (string.IsNullOrWhiteSpace(expected) || key != expected)
            return Unauthorized();

        switch (job.ToLowerInvariant())
        {
            case "reminders": await _reminders.DispatchDueAsync(); break;
            case "morning": await _notifications.SendMorningBriefingsAsync(); break;
            case "eod": await _notifications.SendEndOfDaySummariesAsync(); break;
            case "weekly": await _notifications.SendWeeklyReviewsAsync(); break;
            default: return BadRequest("unknown job");
        }
        return Ok($"ran: {job}");
    }
}
