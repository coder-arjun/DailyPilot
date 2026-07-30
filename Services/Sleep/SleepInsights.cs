using System.Globalization;

namespace DailyPilot.Services.Sleep;

/// <summary>
/// Deterministic insight generation per the spec: weekly observations compare the
/// current window to the previous one; personalized patterns need ≥14 entries and
/// each rule has a data-sufficiency guard. Sentences follow the product copy in
/// the requirements file. (When Ai:Provider is configured these facts may be
/// re-phrased by the assistant; the engine itself never needs an LLM.)
/// </summary>
public static class SleepInsights
{
    public static IReadOnlyList<string> Weekly(IReadOnlyList<SleepNight> current, IReadOnlyList<SleepNight>? previous)
    {
        if (current.Count < 3)
            return new[] { "Log more nights to unlock weekly insights — three is enough to start." };

        var lines = new List<string>();
        var avg = (int)current.Average(n => SleepMetrics.DurationMinutes(n));
        lines.Add($"Average sleep: {Fmt(avg)}.");

        if (previous is { Count: >= 3 })
        {
            var prevAvg = (int)previous.Average(n => SleepMetrics.DurationMinutes(n));
            var delta = avg - prevAvg;
            if (Math.Abs(delta) >= 5)
                lines.Add(delta > 0
                    ? $"You slept {Math.Abs(delta)} minutes longer than last week."
                    : $"You slept {Math.Abs(delta)} minutes shorter than last week.");

            var consistencyDelta = (int)Math.Round(SleepMetrics.ConsistencyPct(current) - SleepMetrics.ConsistencyPct(previous));
            if (Math.Abs(consistencyDelta) >= 3)
                lines.Add(consistencyDelta > 0
                    ? $"Sleep consistency improved by {consistencyDelta}%."
                    : $"Sleep consistency dropped by {Math.Abs(consistencyDelta)}%.");
        }

        var best = current.MaxBy(n => n.Quality);
        if (best.Quality > 0)
            lines.Add($"{DayName(best.Date)} had the highest quality sleep.");

        var shortest = current.MinBy(n => SleepMetrics.DurationMinutes(n));
        lines.Add($"{DayName(shortest.Date)} had the shortest sleep ({Fmt(SleepMetrics.DurationMinutes(shortest))}).");

        return lines;
    }

    public static IReadOnlyList<string> Personalized(IReadOnlyList<SleepNight> all)
    {
        if (all.Count < 14) return Array.Empty<string>();
        var lines = new List<string>();

        // Rule: weekday bedtimes past midnight → shift suggestion with computed gain.
        var weekdayNights = all.Where(n => n.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        if (weekdayNights.Count >= 5)
        {
            var lateShare = weekdayNights.Count(n => n.BedTime.ToTimeSpan().TotalMinutes < 720) / (double)weekdayNights.Count;
            if (lateShare >= 0.5)
            {
                var weeklyGainHours = 30 * 5 / 60.0; // 30 min × 5 weekdays
                lines.Add(
                    "You consistently sleep after midnight on weekdays. Shifting your bedtime " +
                    $"30 minutes earlier could increase your weekly sleep by approximately {weeklyGainHours:0.#} hours.");
            }
        }

        // Rule: duration band of the best-rated mornings.
        var great = all.Where(n => n.Quality >= 8).Select(n => SleepMetrics.DurationMinutes(n)).OrderBy(m => m).ToList();
        if (great.Count >= 3)
        {
            var low = great[(int)(great.Count * 0.25)];
            var high = great[Math.Min(great.Count - 1, (int)(great.Count * 0.75))];
            if (high - low <= 120)
                lines.Add($"You report your best mornings after sleeping between {Fmt(low)} and {Fmt(high)}.");
        }

        // Rule: Sunday-night delay making Mondays harder.
        var sundayNights = all.Where(n => n.Date.DayOfWeek == DayOfWeek.Monday).Select(n => n.BedTime).ToList();
        var otherNights = all.Where(n => n.Date.DayOfWeek is not (DayOfWeek.Monday or DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(n => n.BedTime).ToList();
        if (sundayNights.Count >= 2 && otherNights.Count >= 4)
        {
            var d = SleepMetrics.CircularMean(sundayNights).ToTimeSpan().TotalMinutes
                    - SleepMetrics.CircularMean(otherNights).ToTimeSpan().TotalMinutes;
            var shift = ((d % 1440) + 1440) % 1440;
            if (shift is >= 45 and <= 720)
                lines.Add("Sundays appear to delay your sleep schedule, making Monday mornings harder.");
        }

        // Rule: phone-before-bed correlation (needs ≥3 nights on each side).
        var withPhone = all.Where(n => n.PhoneBeforeBed == true).Select(n => (double)n.Quality).ToList();
        var withoutPhone = all.Where(n => n.PhoneBeforeBed == false).Select(n => (double)n.Quality).ToList();
        if (withPhone.Count >= 3 && withoutPhone.Count >= 3 && withoutPhone.Average() - withPhone.Average() >= 1)
            lines.Add("You wake up feeling more refreshed when you avoid screens before bedtime.");

        return lines;
    }

    private static string Fmt(int minutes) => $"{minutes / 60}h {minutes % 60:00}m";

    private static string DayName(DateOnly d) => d.ToString("dddd", CultureInfo.InvariantCulture);
}
