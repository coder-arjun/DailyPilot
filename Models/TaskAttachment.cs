namespace DailyPilot.Models;

/// <summary>A file attached to a task (PRD §5 "Notes & Attachments", §8).</summary>
public class TaskAttachment
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded by the user.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Randomised name the file is stored under on disk.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
