using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services.Sleep;

public sealed record SleepDashboard(
    SleepEntry? Today,
    int? TodayDurationMinutes,
    int? TodayScore,
    string? TodayBand,
    int? AverageDurationMinutes,
    TimeOnly? AverageBedTime,
    TimeOnly? AverageWakeTime,
    int SleepDebtMinutes,
    int? ConsistencyPct,
    TimeOnly? IdealBedTime,
    int WeekendShiftMinutes,
    int LateSleepDays,
    int EarlyWakeDays,
    int CompleteNightCount,
    IReadOnlyList<(DateOnly Date, int DurationMinutes, int Quality)> Recent);

public interface ISleepService
{
    Task<SleepEntry> UpsertAsync(string userId, SleepEntry values);
    Task<SleepEntry?> GetAsync(string userId, DateOnly date);
    Task<List<SleepEntry>> GetRangeAsync(string userId, DateOnly from, DateOnly to);
    Task<bool> DeleteAsync(string userId, DateOnly date);
    Task<SleepDashboard> BuildDashboardAsync(string userId, DateOnly today);
    Task<(IReadOnlyList<string> Weekly, IReadOnlyList<string> Personalized)> InsightsAsync(string userId, DateOnly today);
}

public class SleepService : ISleepService
{
    private readonly ApplicationDbContext _db;
    public SleepService(ApplicationDbContext db) => _db = db;

    public async Task<SleepEntry> UpsertAsync(string userId, SleepEntry values)
    {
        var entry = await _db.SleepEntries.FirstOrDefaultAsync(s => s.UserId == userId && s.Date == values.Date);
        if (entry is null)
        {
            entry = new SleepEntry { UserId = userId, Date = values.Date };
            _db.SleepEntries.Add(entry);
        }

        entry.BedTime = values.BedTime;
        entry.EstimatedSleepTime = values.EstimatedSleepTime;
        if (values.PhoneBeforeBed is not null) entry.PhoneBeforeBed = values.PhoneBeforeBed;
        if (values.WakeTime is not null) entry.WakeTime = values.WakeTime;
        if (values.TimeOutOfBed is not null) entry.TimeOutOfBed = values.TimeOutOfBed;
        if (values.Quality is not null) entry.Quality = values.Quality;
        if (values.DreamRemembered is not null) entry.DreamRemembered = values.DreamRemembered;
        if (values.Notes is not null) entry.Notes = values.Notes;

        await _db.SaveChangesAsync();
        return entry;
    }

    public Task<SleepEntry?> GetAsync(string userId, DateOnly date) =>
        _db.SleepEntries.FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date);

    public Task<List<SleepEntry>> GetRangeAsync(string userId, DateOnly from, DateOnly to) =>
        _db.SleepEntries
            .Where(s => s.UserId == userId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync();

    public async Task<bool> DeleteAsync(string userId, DateOnly date)
    {
        var entry = await _db.SleepEntries.FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date);
        if (entry is null) return false;
        _db.SleepEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<SleepDashboard> BuildDashboardAsync(string userId, DateOnly today)
    {
        var complete = await CompleteNightsAsync(userId, today, take: 7);
        var todayEntry = await GetAsync(userId, today);
        var todayNight = todayEntry is { IsComplete: true } ? ToNight(todayEntry) : (SleepNight?)null;

        int? score = null;
        string? band = null;
        if (todayNight is { } n && complete.Count > 0)
            (score, band) = SleepMetrics.Score(n, complete);

        var enough = complete.Count >= 2;
        return new SleepDashboard(
            todayEntry,
            todayNight is { } tn ? SleepMetrics.DurationMinutes(tn) : null,
            score,
            band,
            enough ? (int)complete.Average(x => SleepMetrics.DurationMinutes(x)) : null,
            enough ? SleepMetrics.CircularMean(complete.Select(x => x.BedTime)) : null,
            enough ? SleepMetrics.CircularMean(complete.Select(x => x.WakeTime)) : null,
            SleepMetrics.SleepDebtMinutes(complete),
            complete.Count >= 3 ? (int)Math.Round(SleepMetrics.ConsistencyPct(complete)) : null,
            enough ? SleepMetrics.IdealBedTime(complete) : null,
            SleepMetrics.WeekendShiftMinutes(complete),
            SleepMetrics.LateSleepDays(complete),
            SleepMetrics.EarlyWakeDays(complete),
            complete.Count,
            (await CompleteNightsAsync(userId, today, take: 14))
                .Select(x => (x.Date, SleepMetrics.DurationMinutes(x), x.Quality))
                .OrderBy(t => t.Date)
                .ToList());
    }

    public async Task<(IReadOnlyList<string> Weekly, IReadOnlyList<string> Personalized)> InsightsAsync(string userId, DateOnly today)
    {
        var recent = await CompleteNightsAsync(userId, today, take: 14);
        var ordered = recent.OrderByDescending(n => n.Date).ToList();
        var current = ordered.Take(7).ToList();
        var previous = ordered.Skip(7).Take(7).ToList();
        var all = await CompleteNightsAsync(userId, today, take: 60);
        return (SleepInsights.Weekly(current, previous.Count >= 3 ? previous : null),
                SleepInsights.Personalized(all));
    }

    private async Task<List<SleepNight>> CompleteNightsAsync(string userId, DateOnly upTo, int take)
    {
        var rows = await _db.SleepEntries
            .Where(s => s.UserId == userId && s.Date <= upTo && s.WakeTime != null && s.Quality != null)
            .OrderByDescending(s => s.Date)
            .Take(take)
            .ToListAsync();
        return rows.Select(ToNight).ToList();
    }

    private static SleepNight ToNight(SleepEntry e) => new(
        e.Date, e.BedTime, e.EstimatedSleepTime,
        e.WakeTime!.Value, e.TimeOutOfBed ?? e.WakeTime!.Value,
        e.Quality!.Value, e.PhoneBeforeBed);
}
