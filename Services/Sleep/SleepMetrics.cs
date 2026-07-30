namespace DailyPilot.Services.Sleep;

/// <summary>A complete recorded night, keyed by the wake-up date.</summary>
public readonly record struct SleepNight(
    DateOnly Date,
    TimeOnly BedTime,
    TimeOnly EstimatedSleepTime,
    TimeOnly WakeTime,
    TimeOnly TimeOutOfBed,
    int Quality,
    bool? PhoneBeforeBed = null);

/// <summary>
/// Pure sleep math per the spec (docs/superpowers/specs/2026-07-30-sleep-tracker-design.md).
/// All spans wrap midnight (mod 24 h); time averages use circular statistics so
/// 23:30 and 00:30 average to 00:00, not 12:00.
/// </summary>
public static class SleepMetrics
{
    public const int TargetMinutes = 480;         // fixed 8h target (v1)
    private const int LatencyMinutes = 15;        // assumed fall-asleep latency for ideal bedtime
    private const double MaxStdDevMinutes = 90;   // stddev ≥ this → 0% consistency

    public static int SpanMinutes(TimeOnly from, TimeOnly to)
    {
        var minutes = (to.ToTimeSpan() - from.ToTimeSpan()).TotalMinutes;
        return (int)((minutes % 1440 + 1440) % 1440);
    }

    public static int TimeInBedMinutes(in SleepNight n) => SpanMinutes(n.BedTime, n.TimeOutOfBed);

    public static int DurationMinutes(in SleepNight n) => SpanMinutes(n.EstimatedSleepTime, n.WakeTime);

    public static double EfficiencyPct(in SleepNight n)
    {
        var tib = TimeInBedMinutes(n);
        if (tib <= 0) return 0;
        return Math.Min(100.0, DurationMinutes(n) * 100.0 / tib);
    }

    public static TimeOnly CircularMean(IEnumerable<TimeOnly> times)
    {
        var (sin, cos, count) = SumVectors(times);
        if (count == 0) return TimeOnly.MinValue;
        var angle = Math.Atan2(sin / count, cos / count);
        var minutes = (angle / (2 * Math.PI) * 1440 + 1440) % 1440;
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(minutes)));
    }

    public static double CircularStdDevMinutes(IEnumerable<TimeOnly> times)
    {
        var list = times.ToList();
        if (list.Count < 2) return 0;
        var mean = CircularMean(list);
        // Mean of squared shortest angular distances to the circular mean.
        var sumSq = list
            .Select(t =>
            {
                var d = Math.Abs(t.ToTimeSpan().TotalMinutes - mean.ToTimeSpan().TotalMinutes);
                var shortest = Math.Min(d, 1440 - d);
                return shortest * shortest;
            })
            .Sum();
        return Math.Sqrt(sumSq / list.Count);
    }

    public static int SleepDebtMinutes(IReadOnlyList<SleepNight> window) =>
        window.Sum(n => Math.Max(0, TargetMinutes - DurationMinutes(n)));

    public static double ConsistencyPct(IReadOnlyList<SleepNight> window)
    {
        if (window.Count < 2) return 100;
        var bed = CircularStdDevMinutes(window.Select(n => n.BedTime));
        var wake = CircularStdDevMinutes(window.Select(n => n.WakeTime));
        return StdDevToPct((bed + wake) / 2);
    }

    public static double WakeRegularityPct(IReadOnlyList<SleepNight> window) =>
        window.Count < 2 ? 100 : StdDevToPct(CircularStdDevMinutes(window.Select(n => n.WakeTime)));

    /// <summary>Fri/Sat nights = entries whose wake date is Sat/Sun.</summary>
    public static int WeekendShiftMinutes(IReadOnlyList<SleepNight> window)
    {
        var weekend = window.Where(n => n.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToList();
        var weekday = window.Where(n => n.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        if (weekend.Count == 0 || weekday.Count == 0) return 0;
        var a = CircularMean(weekend.Select(n => n.BedTime)).ToTimeSpan().TotalMinutes;
        var b = CircularMean(weekday.Select(n => n.BedTime)).ToTimeSpan().TotalMinutes;
        var d = Math.Abs(a - b);
        return (int)Math.Round(Math.Min(d, 1440 - d));
    }

    public static int LateSleepDays(IReadOnlyList<SleepNight> window) =>
        window.Count(n => n.BedTime.ToTimeSpan().TotalMinutes < 720); // 00:00–11:59 = after midnight

    public static int EarlyWakeDays(IReadOnlyList<SleepNight> window) =>
        window.Count(n => n.WakeTime.ToTimeSpan().TotalMinutes < 360); // before 06:00

    /// <summary>Weighted score: duration 30, consistency 25, efficiency 20, wake regularity 15, quality 10.</summary>
    public static (int Score, string Band) Score(in SleepNight night, IReadOnlyList<SleepNight> window)
    {
        var durationPts = Math.Max(0.0, 100 - Math.Abs(DurationMinutes(night) - TargetMinutes) / 180.0 * 100);
        var score = (int)Math.Round(
            0.30 * durationPts +
            0.25 * ConsistencyPct(window) +
            0.20 * EfficiencyPct(night) +
            0.15 * WakeRegularityPct(window) +
            0.10 * night.Quality * 10);
        return (score, Band(score));
    }

    public static string Band(int score) => score switch
    {
        >= 85 => "Excellent",
        >= 70 => "Good",
        >= 55 => "Fair",
        _ => "Poor",
    };

    public static TimeOnly IdealBedTime(IReadOnlyList<SleepNight> window)
    {
        var meanWake = CircularMean(window.Select(n => n.WakeTime));
        var minutes = (meanWake.ToTimeSpan().TotalMinutes - TargetMinutes - LatencyMinutes + 1440 * 2) % 1440;
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(minutes)));
    }

    private static double StdDevToPct(double stdDevMinutes) =>
        Math.Max(0, 100 - stdDevMinutes / MaxStdDevMinutes * 100);

    private static (double Sin, double Cos, int Count) SumVectors(IEnumerable<TimeOnly> times)
    {
        double sin = 0, cos = 0;
        var count = 0;
        foreach (var t in times)
        {
            var angle = t.ToTimeSpan().TotalMinutes / 1440 * 2 * Math.PI;
            sin += Math.Sin(angle);
            cos += Math.Cos(angle);
            count++;
        }
        return (sin, cos, count);
    }
}
