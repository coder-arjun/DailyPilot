namespace DailyPilot.Models;

/// <summary>
/// A scheduled reminder/notification (PRD §13 "Reminder", FR-006/FR-007).
/// </summary>
public class Reminder
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int? TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public ReminderType Type { get; set; } = ReminderType.Custom;
    public ReminderChannel Channel { get; set; } = ReminderChannel.Browser;

    public string Message { get; set; } = string.Empty;

    public DateTime ScheduledAtUtc { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
