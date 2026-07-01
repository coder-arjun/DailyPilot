using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>
/// A comment on a task — the discussion/activity thread for team collaboration.
/// Supports @mentions of workspace members (parsed at post time to notify them).
/// </summary>
public class TaskComment
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    /// <summary>Author id. Plain column (no FK) to avoid a second cascade path to the user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Snapshot of the author's display name so the thread survives membership changes.</summary>
    [StringLength(256)]
    public string AuthorName { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
