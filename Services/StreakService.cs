using DailyPilot.Data;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

public interface IStreakService
{
    /// <summary>Recompute current/longest streak from completion history.</summary>
    Task RecalculateAsync(string userId);
}

/// <summary>
/// Streak tracking (PRD §5 / §10). A day "counts" toward the streak when the
/// user completed at least one task planned for that day.
/// </summary>
public class StreakService : IStreakService
{
    private readonly ApplicationDbContext _db;

    public StreakService(ApplicationDbContext db) => _db = db;

    public async Task RecalculateAsync(string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;

        var completedDays = await _db.Tasks
            .Where(t => t.UserId == userId && t.CompletedAt != null)
            .Select(t => t.PlannedDate)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        if (completedDays.Count == 0)
        {
            user.CurrentStreak = 0;
            user.LongestStreak = Math.Max(user.LongestStreak, 0);
            user.LastCompletionDate = null;
            await _db.SaveChangesAsync();
            return;
        }

        // Longest run of consecutive days anywhere in the history.
        int longest = 1, run = 1;
        for (int i = 1; i < completedDays.Count; i++)
        {
            if (completedDays[i] == completedDays[i - 1].AddDays(-1))
                run++;
            else
                run = 1;
            longest = Math.Max(longest, run);
        }

        // Current streak: consecutive days ending today or yesterday.
        var today = completedDays[0]; // most recent completion day
        int current = 1;
        for (int i = 1; i < completedDays.Count; i++)
        {
            if (completedDays[i] == today.AddDays(-i))
                current++;
            else
                break;
        }

        user.LastCompletionDate = completedDays[0];
        user.CurrentStreak = current;
        user.LongestStreak = Math.Max(user.LongestStreak, longest);
        await _db.SaveChangesAsync();
    }
}
