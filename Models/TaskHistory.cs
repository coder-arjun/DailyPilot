namespace DailyPilot.Models;

/// <summary>
/// Audit trail of changes to a task (PRD §13 "TaskHistory", FR-008 Task History).
/// </summary>
public class TaskHistory
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>e.g. Created, Updated, Completed, Reopened, CarriedForward, Deleted.</summary>
    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }

    /// <summary>Snapshot of the task title at the time, so history survives deletes.</summary>
    public string TaskTitle { get; set; } = string.Empty;

    public DateOnly OnDate { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
