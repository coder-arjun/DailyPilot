using DailyPilot.Services.Sleep;

namespace DailyPilot.Tests;

public class SleepInsightsTests
{
    private static SleepNight Night(string date, string sleep = "23:00", string wake = "07:00",
        int quality = 7, bool? phone = null) =>
        new(DateOnly.Parse(date), TimeOnly.Parse(sleep), TimeOnly.Parse(sleep),
            TimeOnly.Parse(wake), TimeOnly.Parse(wake), quality, phone);

    private static List<SleepNight> Week(string mondayWakeDate, string sleep = "23:00", string wake = "07:00", int quality = 7)
    {
        var start = DateOnly.Parse(mondayWakeDate);
        return Enumerable.Range(0, 7)
            .Select(i => Night(start.AddDays(i).ToString("yyyy-MM-dd"), sleep, wake, quality))
            .ToList();
    }

    // ---------- weekly observations ----------

    [Fact]
    public void Weekly_ReportsDurationDeltaVsPreviousWeek()
    {
        var previous = Week("2026-07-13");                       // 8h
        var current = Week("2026-07-20", sleep: "23:18");        // 7h42 → 18 min shorter
        var lines = SleepInsights.Weekly(current, previous);
        Assert.Contains(lines, l => l.Contains("18 minutes shorter"));
    }

    [Fact]
    public void Weekly_NamesBestQualityAndShortestDays()
    {
        var current = Week("2026-07-20");
        current[2] = current[2] with { Quality = 10 };                                  // Wednesday wake-date
        current[4] = current[4] with { EstimatedSleepTime = TimeOnly.Parse("01:30") };  // Friday shortest
        var lines = SleepInsights.Weekly(current, previous: null);
        Assert.Contains(lines, l => l.Contains("Wednesday") && l.Contains("highest quality"));
        Assert.Contains(lines, l => l.Contains("Friday") && l.Contains("shortest"));
    }

    [Fact]
    public void Weekly_TooFewNights_YieldsLogMoreMessage()
    {
        var lines = SleepInsights.Weekly(new List<SleepNight> { Night("2026-07-20") }, null);
        Assert.Contains(lines, l => l.Contains("Log more nights"));
    }

    // ---------- personalized patterns ----------

    private static List<SleepNight> TwoWeeks(Func<DateOnly, SleepNight> factory)
    {
        var start = new DateOnly(2026, 7, 13); // a Monday
        return Enumerable.Range(0, 14).Select(i => factory(start.AddDays(i))).ToList();
    }

    [Fact]
    public void Personalized_MidnightWeekdays_SuggestsShift()
    {
        var nights = TwoWeeks(d => Night(d.ToString("yyyy-MM-dd"),
            sleep: d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "23:00" : "00:40"));
        var lines = SleepInsights.Personalized(nights);
        Assert.Contains(lines, l => l.Contains("after midnight on weekdays") && l.Contains("30 minutes earlier"));
    }

    [Fact]
    public void Personalized_BestMorningsBand_FromHighQualityNights()
    {
        var nights = TwoWeeks(d =>
        {
            var idx = d.DayNumber % 3;
            return idx == 0
                ? Night(d.ToString("yyyy-MM-dd"), sleep: "23:00", wake: "07:00", quality: 9)  // 8h nights feel best
                : Night(d.ToString("yyyy-MM-dd"), sleep: "01:00", wake: "07:00", quality: 5);
        });
        var lines = SleepInsights.Personalized(nights);
        Assert.Contains(lines, l => l.Contains("best mornings"));
    }

    [Fact]
    public void Personalized_PhoneBeforeBed_CorrelationReported()
    {
        var nights = TwoWeeks(d =>
        {
            var phone = d.DayNumber % 2 == 0;
            return Night(d.ToString("yyyy-MM-dd"), quality: phone ? 5 : 8, phone: phone);
        });
        var lines = SleepInsights.Personalized(nights);
        Assert.Contains(lines, l => l.Contains("screens") || l.Contains("phone"));
    }

    [Fact]
    public void Personalized_TooFewEntries_IsEmpty() =>
        Assert.Empty(SleepInsights.Personalized(Week("2026-07-20")));
}
