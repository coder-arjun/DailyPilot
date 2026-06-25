using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

public interface IHabitService
{
    Task<List<HabitStatus>> GetActiveStatusesAsync(string userId, DateOnly today);
    Task<Habit?> GetAsync(string userId, int id);
    Task<Habit> CreateAsync(string userId, Habit habit);
    Task<bool> UpdateAsync(string userId, Habit habit);
    Task<bool> ArchiveAsync(string userId, int id);
    Task<bool> DeleteAsync(string userId, int id);
    Task<bool> ToggleAsync(string userId, int habitId, DateOnly date);
}

/// <summary>
/// PRD Phase 5 — Habit Tracking. Manages habits and daily check-ins and computes
/// per-habit streaks (consecutive days for Daily habits, consecutive on-target
/// weeks for Weekly habits).
/// </summary>
public class HabitService : IHabitService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public HabitService(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<HabitStatus>> GetActiveStatusesAsync(string userId, DateOnly today)
    {
        var habits = await _db.Habits
            .Include(h => h.Entries)
            .Where(h => h.UserId == userId && h.IsActive)
            .OrderBy(h => h.Name)
            .ToListAsync();

        return habits.Select(h => BuildStatus(h, today)).ToList();
    }

    public Task<Habit?> GetAsync(string userId, int id) =>
        _db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

    public async Task<Habit> CreateAsync(string userId, Habit habit)
    {
        habit.UserId = userId;
        habit.CreatedAt = _clock.UtcNow;
        habit.IsActive = true;
        _db.Habits.Add(habit);
        await _db.SaveChangesAsync();
        return habit;
    }

    public async Task<bool> UpdateAsync(string userId, Habit updated)
    {
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == updated.Id && h.UserId == userId);
        if (habit is null) return false;

        habit.Name = updated.Name;
        habit.Description = updated.Description;
        habit.Color = updated.Color;
        habit.Frequency = updated.Frequency;
        habit.TargetPerWeek = updated.TargetPerWeek;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveAsync(string userId, int id)
    {
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
        if (habit is null) return false;
        habit.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
        if (habit is null) return false;
        _db.Habits.Remove(habit);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleAsync(string userId, int habitId, DateOnly date)
    {
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit is null) return false;

        var entry = await _db.HabitEntries.FirstOrDefaultAsync(e => e.HabitId == habitId && e.Date == date);
        if (entry is null)
        {
            _db.HabitEntries.Add(new HabitEntry
            {
                HabitId = habitId,
                UserId = userId,
                Date = date,
                CompletedAt = _clock.UtcNow
            });
        }
        else
        {
            _db.HabitEntries.Remove(entry);
        }
        await _db.SaveChangesAsync();
        return true;
    }

    private static HabitStatus BuildStatus(Habit habit, DateOnly today)
    {
        var dates = habit.Entries.Select(e => e.Date).ToHashSet();
        var status = new HabitStatus { Habit = habit, DoneToday = dates.Contains(today) };

        if (habit.Frequency == HabitFrequency.Weekly)
        {
            var weekCounts = habit.Entries
                .GroupBy(e => WeekStart(e.Date))
                .ToDictionary(g => g.Key, g => g.Count());

            bool Met(DateOnly ws) => weekCounts.TryGetValue(ws, out var c) && c >= habit.TargetPerWeek;

            status.ThisWeekCount = weekCounts.TryGetValue(WeekStart(today), out var tc) ? tc : 0;

            // Current streak of consecutive on-target weeks (ending this or last week).
            var w = WeekStart(today);
            if (!Met(w)) w = w.AddDays(-7);
            int current = 0;
            while (Met(w)) { current++; w = w.AddDays(-7); }
            status.CurrentStreak = current;

            // Longest run of on-target weeks anywhere in history.
            status.LongestStreak = LongestRun(weekCounts.Keys
                .Where(Met).OrderBy(k => k).ToList(), 7);
        }
        else
        {
            status.ThisWeekCount = habit.Entries.Count(e => e.Date >= WeekStart(today) && e.Date <= WeekStart(today).AddDays(6));

            // Current streak of consecutive days (ending today or yesterday).
            var d = today;
            if (!dates.Contains(d)) d = today.AddDays(-1);
            int current = 0;
            while (dates.Contains(d)) { current++; d = d.AddDays(-1); }
            status.CurrentStreak = current;

            status.LongestStreak = LongestRun(dates.OrderBy(x => x).ToList(), 1);
        }

        return status;
    }

    /// <summary>Longest run of consecutive dates spaced exactly <paramref name="stepDays"/> apart.</summary>
    private static int LongestRun(List<DateOnly> sortedDates, int stepDays)
    {
        if (sortedDates.Count == 0) return 0;
        int longest = 1, run = 1;
        for (int i = 1; i < sortedDates.Count; i++)
        {
            if (sortedDates[i] == sortedDates[i - 1].AddDays(stepDays)) run++;
            else run = 1;
            longest = Math.Max(longest, run);
        }
        return longest;
    }

    private static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); // Monday
}
