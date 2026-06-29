using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DailyPilot.Services;

public interface INotificationService
{
    /// <summary>FR-007: queue morning briefings for all opted-in users.</summary>
    Task SendMorningBriefingsAsync();

    /// <summary>FR-006: queue end-of-day summaries for all opted-in users.</summary>
    Task SendEndOfDaySummariesAsync();

    /// <summary>Unsent browser reminders for the in-app notification poller.</summary>
    Task<List<Reminder>> GetPendingBrowserRemindersAsync(string userId);

    Task MarkSentAsync(int reminderId);

    /// <summary>Weekly productivity digest email for opted-in users.</summary>
    Task SendWeeklyReviewsAsync();
}

/// <summary>
/// PRD §11 Notifications. Generates morning briefings (FR-007) and EOD summaries
/// (FR-006) as Reminder rows. Browser reminders are surfaced via an in-app poller;
/// email delivery is logged (pluggable with a real provider later).
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NotificationService> _logger;
    private readonly IAppEmailSender _email;

    public NotificationService(ApplicationDbContext db, IDateTimeProvider clock,
        ILogger<NotificationService> logger, IAppEmailSender email)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
        _email = email;
    }

    public async Task SendMorningBriefingsAsync()
    {
        var users = await _db.Users.Where(u => u.EnableMorningReminder).ToListAsync();
        foreach (var user in users)
        {
            var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);
            var count = await _db.Tasks.CountAsync(t =>
                t.UserId == user.Id && t.PlannedDate == today && t.Status != DailyTaskStatus.Completed);

            var message = count == 0
                ? "Good morning! You have a clear slate today. Plan your day in DayPilot."
                : $"Good morning! You have {count} task(s) planned for today. Let's make progress!";

            await QueueAsync(user, ReminderType.MorningBriefing, "Your morning briefing", message);
        }
        await _db.SaveChangesAsync();
    }

    public async Task SendEndOfDaySummariesAsync()
    {
        var users = await _db.Users.Where(u => u.EnableEndOfDayReminder).ToListAsync();
        foreach (var user in users)
        {
            var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);
            var tasks = await _db.Tasks
                .Where(t => t.UserId == user.Id && t.PlannedDate == today)
                .ToListAsync();

            var completed = tasks.Count(t => t.Status == DailyTaskStatus.Completed);
            var pending = tasks.Count - completed;
            var message = tasks.Count == 0
                ? "No tasks were planned today. Set yourself up for tomorrow in DayPilot."
                : $"Today's summary: {completed}/{tasks.Count} completed." +
                  (pending > 0 ? $" {pending} task(s) will carry forward to tomorrow." : " Great job clearing your list!");

            await QueueAsync(user, ReminderType.EndOfDaySummary, "Your end-of-day summary", message);
        }
        await _db.SaveChangesAsync();
    }

    private async Task QueueAsync(ApplicationUser user, ReminderType type, string subject, string message)
    {
        _db.Reminders.Add(new Reminder
        {
            UserId = user.Id,
            Type = type,
            Channel = ReminderChannel.Browser,
            Message = message,
            ScheduledAtUtc = _clock.UtcNow,
            CreatedAt = _clock.UtcNow
        });

        if (user.EnableEmailNotifications && !string.IsNullOrWhiteSpace(user.Email))
        {
            var html = BuildEmailHtml(subject, message);
            await _email.SendAsync(user.Email, $"DayPilot — {subject}", html, user.DisplayName);

            _db.Reminders.Add(new Reminder
            {
                UserId = user.Id,
                Type = type,
                Channel = ReminderChannel.Email,
                Message = message,
                ScheduledAtUtc = _clock.UtcNow,
                CreatedAt = _clock.UtcNow,
                IsSent = true,
                SentAtUtc = _clock.UtcNow
            });
        }
    }

    private static string BuildEmailHtml(string heading, string message) => $@"
        <div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto'>
            <h2 style='color:#0d6efd'>DayPilot</h2>
            <h3>{heading}</h3>
            <p style='font-size:15px;color:#333'>{message}</p>
            <hr/>
            <p style='font-size:12px;color:#888'>Plan. Complete. Move Forward.</p>
        </div>";

    public async Task<List<Reminder>> GetPendingBrowserRemindersAsync(string userId)
    {
        return await _db.Reminders
            .Where(r => r.UserId == userId && !r.IsSent && r.Channel == ReminderChannel.Browser)
            .OrderBy(r => r.ScheduledAtUtc)
            .ToListAsync();
    }

    public async Task MarkSentAsync(int reminderId)
    {
        var reminder = await _db.Reminders.FindAsync(reminderId);
        if (reminder is null) return;
        reminder.IsSent = true;
        reminder.SentAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task SendWeeklyReviewsAsync()
    {
        var users = await _db.Users.Where(u => u.EnableEmailNotifications).ToListAsync();
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) continue;

            var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);
            var start = today.AddDays(-7);
            var end = today.AddDays(-1);

            var tasks = await _db.Tasks
                .Include(t => t.Category)
                .Where(t => t.UserId == user.Id && t.PlannedDate >= start && t.PlannedDate <= end)
                .ToListAsync();

            var planned = tasks.Count;
            var completed = tasks.Count(t => t.Status == DailyTaskStatus.Completed);
            var rate = planned == 0 ? 0 : (int)Math.Round(completed * 100.0 / planned);

            var bestDay = tasks.Where(t => t.Status == DailyTaskStatus.Completed)
                .GroupBy(t => t.PlannedDate)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            var topCategory = tasks.Where(t => t.Status == DailyTaskStatus.Completed && t.Category != null)
                .GroupBy(t => t.Category!.Name)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            var name = string.IsNullOrWhiteSpace(user.DisplayName) ? "there" : user.DisplayName;
            var html = $@"
                <div style='font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:auto'>
                    <h2 style='color:#4f46e5'>Your week in review</h2>
                    <p>Hi {System.Net.WebUtility.HtmlEncode(name)}, here's how {start:dd MMM}–{end:dd MMM} went:</p>
                    <ul style='font-size:15px;line-height:1.8;color:#333'>
                        <li><strong>{completed}/{planned}</strong> tasks completed (<strong>{rate}%</strong>)</li>
                        <li>Current streak: <strong>{user.CurrentStreak}</strong> day(s) · best ever: {user.LongestStreak}</li>
                        {(bestDay != null ? $"<li>Most productive day: <strong>{bestDay.Key:dddd}</strong> ({bestDay.Count()} done)</li>" : "")}
                        {(topCategory != null ? $"<li>Top focus area: <strong>{System.Net.WebUtility.HtmlEncode(topCategory.Key)}</strong></li>" : "")}
                    </ul>
                    <p style='color:#555'>{(rate >= 70 ? "Fantastic consistency — keep it going! 🔥" : "A fresh week is a fresh start. Plan your top 3 today. 💪")}</p>
                    <p><a href='https://dailypilot.runasp.net/Dashboard' style='display:inline-block;background:#4f46e5;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none'>View your dashboard</a></p>
                    <hr/><p style='font-size:12px;color:#888'>DayPilot · Plan. Complete. Move Forward.</p>
                </div>";

            await _email.SendAsync(user.Email!, "DayPilot — your week in review", html, user.DisplayName);
        }
    }
}
