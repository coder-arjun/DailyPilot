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
}
