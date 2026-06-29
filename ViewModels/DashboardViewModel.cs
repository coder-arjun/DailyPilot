namespace DailyPilot.ViewModels;

public class DashboardViewModel
{
    public DateOnly Date { get; set; }
    public int CompletedToday { get; set; }
    public int PlannedToday { get; set; }
    public int PendingToday { get; set; }
    public double TodayCompletionRate =>
        PlannedToday == 0 ? 0 : Math.Round(CompletedToday * 100.0 / PlannedToday, 1);

    public double WeeklyCompletionRate { get; set; }
    public double CarryForwardRate { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    public List<DailyTrendPoint> Trend { get; set; } = new();
    public List<CategoryPerformance> CategoryPerformance { get; set; } = new();

    /// <summary>Per-day completion data for the GitHub-style activity heatmap (oldest → newest, Monday-aligned).</summary>
    public List<HeatmapDay> Heatmap { get; set; } = new();
}

public class HeatmapDay
{
    public DateOnly Date { get; set; }
    public int Completed { get; set; }
    /// <summary>Intensity bucket 0-4 used to colour the cell.</summary>
    public int Level { get; set; }
}

public class DailyTrendPoint
{
    public DateOnly Date { get; set; }
    public int Planned { get; set; }
    public int Completed { get; set; }
    public double CompletionRate { get; set; }
}

public class CategoryPerformance
{
    public string Category { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Completed { get; set; }
    public double Rate => Total == 0 ? 0 : Math.Round(Completed * 100.0 / Total, 1);
}
