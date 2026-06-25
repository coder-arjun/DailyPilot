using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>Free-form label that can be applied to many tasks (PRD §5 "Categories &amp; Tags").</summary>
public class Tag
{
    public int Id { get; set; }

    [Required]
    [StringLength(40)]
    public string Name { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
