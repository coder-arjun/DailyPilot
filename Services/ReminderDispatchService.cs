using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

public interface IReminderDispatchService
{
    /// <summary>Deliver due task reminders via email + web push (for users who aren't in the app).</summary>
    Task DispatchDueAsync();
}

/// <summary>
/// PRD §11 — closed-app reminder delivery. A per-minute Hangfire job calls this to
/// send email + web push for tasks whose reminder time has just passed. A 60-second
/// grace lets the in-app alarm fire first when the app is open; reminders more than
/// two hours late are left for the in-app "missed" catch-up instead of being emailed.
/// </summary>
public class ReminderDispatchService : IReminderDispatchService
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxLate = TimeSpan.FromHours(2);

    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IAppEmailSender _email;
    private readonly IPushNotificationService _push;

    public ReminderDispatchService(ApplicationDbContext db, IDateTimeProvider clock,
        IAppEmailSender email, IPushNotificationService push)
    {
        _db = db;
        _clock = clock;
        _email = email;
        _push = push;
    }

    public async Task DispatchDueAsync()
    {
        var nowUtc = _clock.UtcNow;
        var users = await _db.Users
            .Select(u => new { u.Id, u.TimeZoneId, u.Email, u.DisplayName, u.EnableEmailNotifications })
            .ToListAsync();

        foreach (var u in users)
        {
            var tz = TimeZoneHelper.Resolve(u.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(localNow);

            var candidates = await _db.Tasks
                .Where(t => t.UserId == u.Id && t.PlannedDate == today
                            && t.ReminderTime != null
                            && t.Status != DailyTaskStatus.Completed
                            && t.ReminderFiredOn != today)
                .ToListAsync();

            var changed = false;
            foreach (var t in candidates)
            {
                var late = localNow.TimeOfDay - t.ReminderTime!.Value.ToTimeSpan();
                if (late < Grace || late > MaxLate) continue;

                var time = t.ReminderTime.Value.ToString("HH:mm");
                await _push.SendToUserAsync(u.Id, "⏰ " + t.Title, "Reminder · " + time, "/Tasks", "dp-task-" + t.Id);

                if (u.EnableEmailNotifications && !string.IsNullOrWhiteSpace(u.Email))
                {
                    await _email.SendAsync(u.Email!,
                        $"DayPilot reminder: {t.Title}",
                        BuildEmailHtml(t.Title, time, t.Notes),
                        u.DisplayName);
                }

                t.ReminderFiredOn = today;
                changed = true;
            }
            if (changed) await _db.SaveChangesAsync();
        }
    }

    private static string BuildEmailHtml(string title, string time, string? notes) => $@"
        <div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto'>
            <h2 style='color:#4f46e5'>⏰ Reminder</h2>
            <p style='font-size:18px;font-weight:600;color:#111'>{System.Net.WebUtility.HtmlEncode(title)}</p>
            <p style='color:#555'>Scheduled for {time}.</p>
            {(string.IsNullOrWhiteSpace(notes) ? "" : $"<p style='color:#555'>{System.Net.WebUtility.HtmlEncode(notes)}</p>")}
            <p><a href='https://dailypilot.runasp.net/Tasks' style='display:inline-block;background:#4f46e5;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none'>Open DayPilot</a></p>
        </div>";
}
