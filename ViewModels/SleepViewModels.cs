using DailyPilot.Models;
using DailyPilot.Services.Sleep;

namespace DailyPilot.ViewModels;

/// <summary>View model for the Sleep dashboard + journal page (Controllers/SleepController.cs).</summary>
public class SleepPageViewModel
{
    public required SleepDashboard Dashboard { get; init; }
    public required IReadOnlyList<string> Weekly { get; init; }
    public required IReadOnlyList<string> Personalized { get; init; }

    /// <summary>Prefill only — the entry the evening form will upsert into if submitted right now.</summary>
    public SleepEntry? EveningEntry { get; init; }

    /// <summary>Wake date the evening form saves to if submitted right now (today or tomorrow).</summary>
    public required DateOnly EveningTargetDate { get; init; }

    /// <summary>User's local today (the date the morning form always saves to).</summary>
    public required DateOnly Today { get; init; }

    /// <summary>Prefill only — the entry the morning form will upsert into (always today).</summary>
    public SleepEntry? MorningEntry { get; init; }

    /// <summary>Stars = round(score/20), per the spec.</summary>
    public int StarCount => Dashboard.TodayScore is int s ? Math.Clamp((int)Math.Round(s / 20.0), 0, 5) : 0;

    public static string Fmt(int? minutes) =>
        minutes is null ? "—" : $"{minutes.Value / 60}h {minutes.Value % 60:00}m";

    public static string FmtTime(TimeOnly? t) => t?.ToString("h:mm tt") ?? "—";
}
