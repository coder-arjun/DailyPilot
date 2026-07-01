using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>
/// A single daily task (PRD §13 "Task"). Supports priorities, categories,
/// recurrence, carry-forward and reminders per the functional requirements.
/// </summary>
public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>The day this task is planned for (the user's local date).</summary>
    public DateOnly PlannedDate { get; set; }

    /// <summary>Optional time of day the task is due.</summary>
    public TimeOnly? DueTime { get; set; }

    /// <summary>Optional reminder time of day.</summary>
    public TimeOnly? ReminderTime { get; set; }

    /// <summary>The day the reminder for this task was last shown/acknowledged (dedupe so it fires once per day).</summary>
    public DateOnly? ReminderFiredOn { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;
    public EnergyLevel EnergyLevel { get; set; } = EnergyLevel.Any;
    public DailyTaskStatus Status { get; set; } = DailyTaskStatus.Pending;

    /// <summary>Estimated duration in minutes (PRD §8).</summary>
    public int? EstimatedMinutes { get; set; }

    /// <summary>Actual time spent, accumulated from focus/Pomodoro sessions and manual logging.</summary>
    public int? ActualMinutes { get; set; }

    // --- Recurrence (PRD §5) ---
    public RecurrencePattern Recurrence { get; set; } = RecurrencePattern.None;

    /// <summary>Display order within a day.</summary>
    public int SortOrder { get; set; }

    // --- Carry-forward bookkeeping (FR-005) ---
    /// <summary>How many times this task has rolled over to a new day.</summary>
    public int CarryForwardCount { get; set; }

    /// <summary>The date the task was originally created for (before any carry-forward).</summary>
    public DateOnly OriginalDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // --- Relationships ---
    /// <summary>The responsible user (assignee). Drives carry-forward, streaks and reminders.</summary>
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Null = a personal task; otherwise the team workspace it belongs to (PRD Phase 4).</summary>
    public int? WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    /// <summary>Plain column (no FK) — who created the task (may differ from the assignee).</summary>
    public string? CreatedById { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<TaskHistory> History { get; set; } = new List<TaskHistory>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<TaskChecklistItem> ChecklistItems { get; set; } = new List<TaskChecklistItem>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();

    public bool IsCompleted => Status == DailyTaskStatus.Completed;
}
