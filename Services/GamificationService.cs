using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

/// <summary>A level summary derived purely from an XP total.</summary>
public class LevelInfo
{
    public int Level { get; set; }
    public string Title { get; set; } = "Novice";
    public int Xp { get; set; }
    public int XpAtLevelStart { get; set; }
    public int XpAtNextLevel { get; set; }
    public int XpIntoLevel => Xp - XpAtLevelStart;
    public int XpSpan => Math.Max(1, XpAtNextLevel - XpAtLevelStart);
    public int XpToNext => Math.Max(0, XpAtNextLevel - Xp);
    public int ProgressPct => (int)Math.Round(Math.Min(100.0, XpIntoLevel * 100.0 / XpSpan));
}

public class Badge
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-award";      // Bootstrap icon
    public string Description { get; set; } = string.Empty;
    public bool Earned { get; set; }
}

public class GamificationSummary
{
    public LevelInfo Level { get; set; } = new();
    public List<Badge> Badges { get; set; } = new();
    public int EarnedBadgeCount => Badges.Count(b => b.Earned);
    public int TasksCompleted { get; set; }
}

public interface IGamificationService
{
    /// <summary>Award XP to a user (persisted). No-op for null/empty ids.</summary>
    Task AwardAsync(string userId, int xp);

    /// <summary>XP earned for completing a task, weighted by priority.</summary>
    int XpForTask(TaskItem task);

    /// <summary>Pure level maths from an XP total.</summary>
    LevelInfo Describe(int xp);

    /// <summary>Full summary (level + badges) for the profile/dashboard.</summary>
    Task<GamificationSummary> GetSummaryAsync(ApplicationUser user);
}

/// <summary>
/// Lightweight gamification layer: XP, levels and badges. Retention research on
/// habit apps shows gamified feedback materially lifts follow-through, so tasks and
/// habit check-ins grant XP that rolls up into a level and unlockable badges.
/// </summary>
public class GamificationService : IGamificationService
{
    private readonly ApplicationDbContext _db;

    public GamificationService(ApplicationDbContext db) => _db = db;

    // Total XP required to *reach* level L is 50·L·(L-1): L1=0, L2=100, L3=300, L4=600, L5=1000…
    private static int XpForLevel(int level) => 50 * level * (level - 1);

    private static readonly string[] Titles =
    {
        "Novice", "Apprentice", "Planner", "Achiever", "Strategist",
        "Expert", "Master", "Grandmaster", "Legend", "Productivity Sage"
    };

    public int XpForTask(TaskItem task) => task.Priority switch
    {
        Priority.High => 20,
        Priority.Medium => 12,
        _ => 8
    };

    public async Task AwardAsync(string userId, int xp)
    {
        if (string.IsNullOrEmpty(userId) || xp == 0) return;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;
        user.Xp = Math.Max(0, user.Xp + xp);
        await _db.SaveChangesAsync();
    }

    public LevelInfo Describe(int xp)
    {
        xp = Math.Max(0, xp);
        int level = 1;
        while (XpForLevel(level + 1) <= xp) level++;
        return new LevelInfo
        {
            Level = level,
            Title = Titles[Math.Min(level - 1, Titles.Length - 1)],
            Xp = xp,
            XpAtLevelStart = XpForLevel(level),
            XpAtNextLevel = XpForLevel(level + 1)
        };
    }

    public async Task<GamificationSummary> GetSummaryAsync(ApplicationUser user)
    {
        var completed = await _db.Tasks
            .CountAsync(t => t.UserId == user.Id && t.Status == DailyTaskStatus.Completed);

        var bestHabitStreak = 0;
        var habitEntryDates = await _db.HabitEntries
            .Where(e => e.UserId == user.Id)
            .Select(e => new { e.HabitId, e.Date })
            .ToListAsync();
        foreach (var g in habitEntryDates.GroupBy(x => x.HabitId))
        {
            var dates = g.Select(x => x.Date).OrderBy(d => d).ToList();
            int run = dates.Count == 0 ? 0 : 1, best = run;
            for (int i = 1; i < dates.Count; i++)
            {
                run = dates[i] == dates[i - 1].AddDays(1) ? run + 1 : 1;
                best = Math.Max(best, run);
            }
            bestHabitStreak = Math.Max(bestHabitStreak, best);
        }

        var level = Describe(user.Xp);

        var badges = new List<Badge>
        {
            Badge("first-task", "First Step", "bi-flag", "Complete your first task", completed >= 1),
            Badge("ten-tasks", "Getting Going", "bi-check2-circle", "Complete 10 tasks", completed >= 10),
            Badge("century", "Centurion", "bi-trophy", "Complete 100 tasks", completed >= 100),
            Badge("streak-3", "Streak Starter", "bi-fire", "Reach a 3-day streak", user.LongestStreak >= 3),
            Badge("streak-7", "On Fire", "bi-fire", "Reach a 7-day streak", user.LongestStreak >= 7),
            Badge("streak-30", "Unstoppable", "bi-lightning-charge", "Reach a 30-day streak", user.LongestStreak >= 30),
            Badge("habit-7", "Habit Builder", "bi-arrow-repeat", "Keep a habit for 7 days straight", bestHabitStreak >= 7),
            Badge("level-5", "Strategist", "bi-star", "Reach level 5", level.Level >= 5),
            Badge("level-10", "Sage", "bi-gem", "Reach level 10", level.Level >= 10),
        };

        return new GamificationSummary { Level = level, Badges = badges, TasksCompleted = completed };
    }

    private static Badge Badge(string key, string name, string icon, string desc, bool earned) =>
        new() { Key = key, Name = name, Icon = icon, Description = desc, Earned = earned };
}
