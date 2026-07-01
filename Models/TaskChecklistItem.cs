using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>
/// A single subtask / checklist step within a task. Lets a task be broken down
/// into smaller, tickable steps (and is the target of the AI "break it down" feature).
/// </summary>
public class TaskChecklistItem
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    [Required]
    [StringLength(300)]
    public string Text { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    /// <summary>Display order within the task.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
