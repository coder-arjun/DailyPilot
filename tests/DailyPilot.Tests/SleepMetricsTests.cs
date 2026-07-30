using DailyPilot.Services.Sleep;

namespace DailyPilot.Tests;

public class SleepMetricsTests
{
    private static SleepNight Night(
        string date = "2026-07-30", string bed = "23:00", string sleep = "23:15",
        string wake = "07:00", string outOfBed = "07:10", int quality = 8, bool? phone = null) =>
        new(DateOnly.Parse(date), TimeOnly.Parse(bed), TimeOnly.Parse(sleep),
            TimeOnly.Parse(wake), TimeOnly.Parse(outOfBed), quality, phone);

    // ---------- spans across midnight ----------

    [Theory]
    [InlineData("23:00", "07:00", 480)]
    [InlineData("01:00", "07:00", 360)]
    [InlineData("22:30", "22:30", 0)]
    [InlineData("23:45", "00:15", 30)]
    public void SpanMinutes_WrapsMidnight(string from, string to, int expected) =>
        Assert.Equal(expected, SleepMetrics.SpanMinutes(TimeOnly.Parse(from), TimeOnly.Parse(to)));

    [Fact]
    public void NightDerivedValues_MatchHandComputation()
    {
        var n = Night(); // bed 23:00, sleep 23:15, wake 07:00, out 07:10
        Assert.Equal(490, SleepMetrics.TimeInBedMinutes(n));
        Assert.Equal(465, SleepMetrics.DurationMinutes(n));
        Assert.Equal(465.0 / 490.0 * 100, SleepMetrics.EfficiencyPct(n), 1);
    }

    [Fact]
    public void EfficiencyPct_IsCappedAt100()
    {
        // Degenerate input: claims sleeping longer than time in bed.
        var n = Night(bed: "23:00", sleep: "22:00", outOfBed: "07:00", wake: "07:00");
        Assert.Equal(100, SleepMetrics.EfficiencyPct(n));
    }

    // ---------- circular statistics ----------

    [Fact]
    public void CircularMean_HandlesMidnightStraddle()
    {
        var mean = SleepMetrics.CircularMean(new[] { TimeOnly.Parse("23:30"), TimeOnly.Parse("00:30") });
        Assert.Equal(TimeOnly.Parse("00:00"), mean);
    }

    [Fact]
    public void CircularStdDev_MidnightStraddlePair_Is30()
    {
        var sd = SleepMetrics.CircularStdDevMinutes(new[] { TimeOnly.Parse("23:30"), TimeOnly.Parse("00:30") });
        Assert.Equal(30, sd, 1);
    }

    [Fact]
    public void CircularStdDev_IdenticalTimes_IsZero() =>
        Assert.Equal(0, SleepMetrics.CircularStdDevMinutes(new[] { TimeOnly.Parse("23:00"), TimeOnly.Parse("23:00") }), 3);

    // ---------- window metrics ----------

    private static List<SleepNight> UniformWeek()
    {
        var list = new List<SleepNight>();
        for (var i = 0; i < 7; i++)
            list.Add(Night(date: $"2026-07-{20 + i:00}", sleep: "23:00", wake: "07:00", bed: "23:00", outOfBed: "07:00"));
        return list;
    }

    [Fact]
    public void SleepDebt_SumsShortfallOnly()
    {
        var nights = new List<SleepNight>
        {
            Night(sleep: "00:00", wake: "07:00"), // 420 → 60 short
            Night(sleep: "23:00", wake: "07:00"), // 480 → 0
            Night(sleep: "23:30", wake: "07:00"), // 450 → 30 short
            Night(sleep: "22:00", wake: "07:00"), // 540 → surplus ignored
        };
        Assert.Equal(90, SleepMetrics.SleepDebtMinutes(nights));
    }

    [Fact]
    public void Consistency_UniformWeek_Is100() =>
        Assert.Equal(100, SleepMetrics.ConsistencyPct(UniformWeek()), 1);

    [Fact]
    public void Consistency_AlternatingBedTimes_Is75()
    {
        // Bed alternates 22:00/23:30 (stddev 45), wake constant (stddev 0) → avg 22.5 → 75%.
        var nights = new List<SleepNight>();
        for (var i = 0; i < 6; i++)
            nights.Add(Night(date: $"2026-07-{20 + i:00}", bed: i % 2 == 0 ? "22:00" : "23:30",
                sleep: i % 2 == 0 ? "22:00" : "23:30", wake: "07:00", outOfBed: "07:00"));
        Assert.Equal(75, SleepMetrics.ConsistencyPct(nights), 1);
    }

    [Fact]
    public void WeekendShift_MeasuresFriSatNights()
    {
        var nights = new List<SleepNight>();
        // Wake dates 2026-07-20 (Mon) … 2026-07-26 (Sun). Sat+Sun wake-dates are the Fri/Sat nights.
        for (var i = 0; i < 7; i++)
        {
            var date = new DateOnly(2026, 7, 20 + i);
            var weekendNight = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            nights.Add(Night(date: date.ToString("yyyy-MM-dd"),
                bed: weekendNight ? "00:00" : "23:00",
                sleep: weekendNight ? "00:00" : "23:00",
                wake: "07:00", outOfBed: "07:00"));
        }
        Assert.Equal(60, SleepMetrics.WeekendShiftMinutes(nights));
    }

    [Fact]
    public void LateAndEarlyDayCounts()
    {
        var nights = new List<SleepNight>
        {
            Night(bed: "00:30", sleep: "00:30"),                    // late sleep
            Night(bed: "23:50", sleep: "23:50"),                    // not late
            Night(wake: "05:45", outOfBed: "05:45"),                // early wake
            Night(wake: "06:00", outOfBed: "06:00"),                // not early (boundary)
        };
        Assert.Equal(1, SleepMetrics.LateSleepDays(nights));
        Assert.Equal(1, SleepMetrics.EarlyWakeDays(nights));
    }

    // ---------- score ----------

    [Fact]
    public void Score_PerfectNightQuality8_Is98Excellent()
    {
        var window = UniformWeek(); // consistency 100, wake regularity 100
        var night = Night(bed: "23:00", sleep: "23:00", wake: "07:00", outOfBed: "07:00", quality: 8);
        var (score, band) = SleepMetrics.Score(night, window);
        // 0.30*100 + 0.25*100 + 0.20*100 + 0.15*100 + 0.10*80 = 98
        Assert.Equal(98, score);
        Assert.Equal("Excellent", band);
    }

    [Theory]
    [InlineData(85, "Excellent")]
    [InlineData(84, "Good")]
    [InlineData(70, "Good")]
    [InlineData(69, "Fair")]
    [InlineData(55, "Fair")]
    [InlineData(54, "Poor")]
    public void Band_Edges(int score, string expected) => Assert.Equal(expected, SleepMetrics.Band(score));

    [Fact]
    public void Score_DurationComponent_DegradesLinearlyOver3Hours()
    {
        // 6h30m sleep = 90min short → durationPts = 100 − 90/180*100 = 50.
        var window = UniformWeek();
        var night = Night(bed: "00:30", sleep: "00:30", wake: "07:00", outOfBed: "07:00", quality: 10);
        var (score, _) = SleepMetrics.Score(night, window);
        // 0.30*50 + 0.25*100 + 0.20*100 + 0.15*100 + 0.10*100 = 15+25+20+15+10 = 85
        Assert.Equal(85, score);
    }

    [Fact]
    public void IdealBedTime_IsMeanWakeMinus8h15()
    {
        var ideal = SleepMetrics.IdealBedTime(UniformWeek()); // wake 07:00 → 22:45
        Assert.Equal(TimeOnly.Parse("22:45"), ideal);
    }
}
