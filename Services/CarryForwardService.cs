using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

public interface ICarryForwardService
{
    /// <summary>Run carry-forward for every user (invoked by the daily Hangfire job).</summary>
    Task RunForAllUsersAsync();

    /// <summary>Migrate a single user's overdue, incomplete tasks up to their "today".</summary>
    Task<int> RunForUserAsync(string userId);
}

/// <summary>
/// FR-005: Automatic Carry Forward. The flagship behaviour — unfinished tasks
/// from past days are rolled forward to the user's current day, and snapshots
/// of the closed-out days are written for analytics. Also materialises the next
/// occurrence of recurring tasks.
/// </summary>
public class CarryForwardService : ICarryForwardService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IAnalyticsService _analytics;

    public CarryForwardService(ApplicationDbContext db, IDateTimeProvider clock, IAnalyticsService analytics)
    {
        _db = db;
        _clock = clock;
        _analytics = analytics;
    }

    public async Task RunForAllUsersAsync()
    {
        var userIds = await _db.Users.Select(u => u.Id).ToListAsync();
        foreach (var id in userIds)
            await RunForUserAsync(id);
    }

    public async Task<int> RunForUserAsync(string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return 0;

        var today = TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow);

        // All incomplete tasks planned for a day before today.
        var overdue = await _db.Tasks
            .Where(t => t.UserId == userId
                        && t.PlannedDate < today
                        && t.Status != DailyTaskStatus.Completed)
            .ToListAsync();

        // Snapshot every closed-out day BEFORE we move tasks off it.
        var affectedDays = overdue.Select(t => t.PlannedDate).Distinct().ToList();
        foreach (var day in affectedDays)
            await _analytics.WriteSnapshotAsync(userId, day);

        var migrated = 0;
        if (user.EnableAutoCarryForward)
        {
            foreach (var task in overdue)
            {
                _db.TaskHistories.Add(new TaskHistory
                {
                    TaskItemId = task.Id,
                    UserId = userId,
                    Action = "CarriedForward",
                    Details = $"{task.PlannedDate:yyyy-MM-dd} → {today:yyyy-MM-dd}",
                    TaskTitle = task.Title,
                    OnDate = today,
                    Timestamp = _clock.UtcNow
                });

                task.Status = DailyTaskStatus.Pending;
                task.PlannedDate = today;
                task.CarryForwardCount++;
                migrated++;
            }
        }

        // Materialise recurring tasks due today that don't yet exist.
        await MaterialiseRecurringAsync(userId, today);

        await _db.SaveChangesAsync();
        return migrated;
    }

    private async Task MaterialiseRecurringAsync(string userId, DateOnly today)
    {
        var templates = await _db.Tasks
            .Where(t => t.UserId == userId && t.Recurrence != RecurrencePattern.None)
            .ToListAsync();

        // Group by title to find the most recent instance of each recurring task.
        foreach (var group in templates.GroupBy(t => t.Title))
        {
            var latest = group.OrderByDescending(t => t.PlannedDate).First();
            if (latest.PlannedDate >= today) continue;
            if (!IsDue(latest.Recurrence, latest.PlannedDate, today)) continue;

            // Don't duplicate if an instance for today already exists.
            bool existsToday = group.Any(t => t.PlannedDate == today);
            if (existsToday) continue;

            _db.Tasks.Add(new TaskItem
            {
                UserId = userId,
                Title = latest.Title,
                Notes = latest.Notes,
                PlannedDate = today,
                OriginalDate = today,
                DueTime = latest.DueTime,
                ReminderTime = latest.ReminderTime,
                Priority = latest.Priority,
                EnergyLevel = latest.EnergyLevel,
                EstimatedMinutes = latest.EstimatedMinutes,
                Recurrence = latest.Recurrence,
                CategoryId = latest.CategoryId,
                Status = DailyTaskStatus.Pending,
                CreatedAt = _clock.UtcNow
            });
        }
    }

    private static bool IsDue(RecurrencePattern pattern, DateOnly last, DateOnly today) => pattern switch
    {
        RecurrencePattern.Daily => today > last,
        RecurrencePattern.Weekly => today >= last.AddDays(7),
        RecurrencePattern.Monthly => today >= last.AddMonths(1),
        RecurrencePattern.Weekdays => today > last
            && today.DayOfWeek != DayOfWeek.Saturday
            && today.DayOfWeek != DayOfWeek.Sunday,
        _ => false
    };
}
