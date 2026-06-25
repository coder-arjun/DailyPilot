using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

public interface IAnalyticsService
{
    Task WriteSnapshotAsync(string userId, DateOnly date);
    Task<DashboardViewModel> BuildDashboardAsync(ApplicationUser user, DateOnly today);
}

/// <summary>
/// FR-009 Analytics Dashboard / §10 Reporting: completion rates, carry-forward
/// rate, streaks and category performance.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AnalyticsService(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task WriteSnapshotAsync(string userId, DateOnly date)
    {
        var dayTasks = await _db.Tasks
            .Where(t => t.UserId == userId && t.PlannedDate == date)
            .ToListAsync();

        var planned = dayTasks.Count;
        var completed = dayTasks.Count(t => t.Status == DailyTaskStatus.Completed);
        var carried = dayTasks.Count(t => t.Status != DailyTaskStatus.Completed);
        var rate = planned == 0 ? 0 : Math.Round(completed * 100.0 / planned, 1);

        var snapshot = await _db.AnalyticsSnapshots
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date);

        if (snapshot is null)
        {
            snapshot = new AnalyticsSnapshot { UserId = userId, Date = date };
            _db.AnalyticsSnapshots.Add(snapshot);
        }

        snapshot.TasksPlanned = planned;
        snapshot.TasksCompleted = completed;
        snapshot.TasksCarriedForward = carried;
        snapshot.CompletionRate = rate;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        snapshot.StreakOnDate = user?.CurrentStreak ?? 0;

        await _db.SaveChangesAsync();
    }

    public async Task<DashboardViewModel> BuildDashboardAsync(ApplicationUser user, DateOnly today)
    {
        var userId = user.Id;
        var weekStart = today.AddDays(-6);

        var todayTasks = await _db.Tasks
            .Where(t => t.UserId == userId && t.PlannedDate == today)
            .ToListAsync();

        var weekTasks = await _db.Tasks
            .Include(t => t.Category)
            .Where(t => t.UserId == userId && t.PlannedDate >= weekStart && t.PlannedDate <= today)
            .ToListAsync();

        var weekPlanned = weekTasks.Count;
        var weekCompleted = weekTasks.Count(t => t.Status == DailyTaskStatus.Completed);
        var weekCarried = weekTasks.Count(t => t.CarryForwardCount > 0);

        var vm = new DashboardViewModel
        {
            Date = today,
            CompletedToday = todayTasks.Count(t => t.Status == DailyTaskStatus.Completed),
            PlannedToday = todayTasks.Count,
            PendingToday = todayTasks.Count(t => t.Status != DailyTaskStatus.Completed),
            WeeklyCompletionRate = weekPlanned == 0 ? 0 : Math.Round(weekCompleted * 100.0 / weekPlanned, 1),
            CarryForwardRate = weekPlanned == 0 ? 0 : Math.Round(weekCarried * 100.0 / weekPlanned, 1),
            CurrentStreak = user.CurrentStreak,
            LongestStreak = user.LongestStreak
        };

        // Last 7 days completion trend.
        for (int i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var dayTasks = weekTasks.Where(t => t.PlannedDate == d).ToList();
            var planned = dayTasks.Count;
            var completed = dayTasks.Count(t => t.Status == DailyTaskStatus.Completed);
            vm.Trend.Add(new DailyTrendPoint
            {
                Date = d,
                Planned = planned,
                Completed = completed,
                CompletionRate = planned == 0 ? 0 : Math.Round(completed * 100.0 / planned, 1)
            });
        }

        // Category performance.
        vm.CategoryPerformance = weekTasks
            .GroupBy(t => t.Category?.Name ?? "Uncategorised")
            .Select(g => new CategoryPerformance
            {
                Category = g.Key,
                Total = g.Count(),
                Completed = g.Count(t => t.Status == DailyTaskStatus.Completed)
            })
            .OrderByDescending(c => c.Total)
            .ToList();

        return vm;
    }
}
