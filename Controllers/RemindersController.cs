using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

/// <summary>
/// Drives the in-app reminder/alarm engine. Returns today's due task reminders
/// (with their exact UTC fire time) and records acknowledgement so each reminder
/// fires once per day. This is the backbone of DayPilot's "remind me" experience.
/// </summary>
[Authorize]
[Route("api/reminders")]
public class RemindersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITaskService _tasks;
    private readonly IDateTimeProvider _clock;
    private readonly UserManager<ApplicationUser> _userManager;

    public RemindersController(ApplicationDbContext db, ITaskService tasks,
        IDateTimeProvider clock, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tasks = tasks;
        _clock = clock;
        _userManager = userManager;
    }

    // GET /api/reminders/today — tasks due to remind today that haven't been acknowledged.
    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var user = (await _userManager.GetUserAsync(User))!;
        var tz = TimeZoneHelper.Resolve(user.TimeZoneId);
        var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);

        var due = await _db.Tasks
            .Where(t => t.UserId == user.Id
                        && t.PlannedDate == today
                        && t.ReminderTime != null
                        && t.Status != DailyTaskStatus.Completed
                        && t.ReminderFiredOn != today)
            .Select(t => new { t.Id, t.Title, t.ReminderTime })
            .ToListAsync();

        var result = due.Select(t =>
        {
            var localDt = DateTime.SpecifyKind(today.ToDateTime(t.ReminderTime!.Value), DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(localDt, tz);
            return new
            {
                taskId = t.Id,
                title = t.Title,
                time = t.ReminderTime!.Value.ToString("HH:mm"),
                dueAtUtc = utc.ToString("o")
            };
        });

        return Json(result);
    }

    // POST /api/reminders/ack/{taskId} — mark today's reminder as shown so it won't refire.
    [HttpPost("ack/{taskId:int}")]
    public async Task<IActionResult> Ack(int taskId)
    {
        var user = (await _userManager.GetUserAsync(User))!;
        var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == user.Id);
        if (task is null) return NotFound();
        task.ReminderFiredOn = today;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/reminders/complete/{taskId} — complete the task straight from the alarm.
    [HttpPost("complete/{taskId:int}")]
    public async Task<IActionResult> Complete(int taskId)
    {
        var user = (await _userManager.GetUserAsync(User))!;
        var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == user.Id);
        if (task is null) return NotFound();

        if (task.Status != DailyTaskStatus.Completed)
            await _tasks.ToggleCompleteAsync(user.Id, taskId);

        task.ReminderFiredOn = today;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
