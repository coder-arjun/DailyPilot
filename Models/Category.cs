using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>User-defined category/tag grouping for tasks (PRD §5, §13).</summary>
public class Category
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex colour used for badges in the UI, e.g. #0d6efd.</summary>
    [StringLength(7)]
    public string Color { get; set; } = "#0d6efd";

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
