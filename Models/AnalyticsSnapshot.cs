namespace DailyPilot.Models;

/// <summary>
/// Daily roll-up of productivity metrics per user (PRD §13 "AnalyticsSnapshot",
/// FR-009 Analytics Dashboard, §10 Reporting).
/// </summary>
public class AnalyticsSnapshot
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateOnly Date { get; set; }

    public int TasksPlanned { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksCarriedForward { get; set; }

    /// <summary>0-100 completion rate for the day.</summary>
    public double CompletionRate { get; set; }

    public int StreakOnDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
