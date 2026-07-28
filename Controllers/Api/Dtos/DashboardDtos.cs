using DailyPilot.Services;
using DailyPilot.ViewModels;

namespace DailyPilot.Controllers.Api.Dtos;

/// <summary>Pure XP/level maths, mirrored from <see cref="LevelInfo"/> for the mobile dashboard.</summary>
public sealed record LevelInfoDto(
    int Level,
    string Title,
    int Xp,
    int XpIntoLevel,
    int XpSpan,
    int XpToNext,
    int ProgressPct)
{
    public static LevelInfoDto From(LevelInfo l) => new(
        l.Level, l.Title, l.Xp, l.XpIntoLevel, l.XpSpan, l.XpToNext, l.ProgressPct);
}

/// <summary>One day of the 7-day completion trend.</summary>
public sealed record DashboardTrendPointDto(string Date, int Completed, int Total)
{
    public static DashboardTrendPointDto From(DailyTrendPoint p) => new(
        p.Date.ToString("yyyy-MM-dd"), p.Completed, p.Planned);
}

/// <summary>Wire shape for `GET api/v1/dashboard` — mirrors <c>DashboardController.Index</c> + gamification summary.</summary>
public sealed record DashboardDto(
    string DisplayName,
    LevelInfoDto Level,
    int TodayCompleted,
    int TodayTotal,
    int CurrentStreak,
    int LongestStreak,
    List<DashboardTrendPointDto> Trend);
