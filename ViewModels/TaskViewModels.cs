using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;

namespace DailyPilot.ViewModels;

/// <summary>Backing model for the create/edit task form.</summary>
public class TaskFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Planned date")]
    public DateOnly PlannedDate { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "Due time")]
    public TimeOnly? DueTime { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "Reminder time")]
    public TimeOnly? ReminderTime { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;

    [Display(Name = "Energy level")]
    public EnergyLevel EnergyLevel { get; set; } = EnergyLevel.Any;

    [Range(0, 1440)]
    [Display(Name = "Estimated minutes")]
    public int? EstimatedMinutes { get; set; }

    public RecurrencePattern Recurrence { get; set; } = RecurrencePattern.None;

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [StringLength(300)]
    [Display(Name = "Tags")]
    public string? TagsCsv { get; set; }

    public List<Category> Categories { get; set; } = new();

    /// <summary>All of the user's existing tag names, for autocomplete suggestions.</summary>
    public List<string> AllTags { get; set; } = new();

    // --- Workspace context (PRD Phase 4) ---
    public int? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }

    [Display(Name = "Assignee")]
    public string? AssigneeId { get; set; }

    /// <summary>Workspace members for the assignee dropdown (empty in personal context).</summary>
    public List<Models.WorkspaceMember> Members { get; set; } = new();

    public TaskItem ToEntity() => new()
    {
        Id = Id,
        Title = Title.Trim(),
        Notes = Notes,
        PlannedDate = PlannedDate,
        DueTime = DueTime,
        ReminderTime = ReminderTime,
        Priority = Priority,
        EnergyLevel = EnergyLevel,
        EstimatedMinutes = EstimatedMinutes,
        Recurrence = Recurrence,
        CategoryId = CategoryId,
        WorkspaceId = WorkspaceId
    };

    public static TaskFormViewModel FromEntity(TaskItem t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Notes = t.Notes,
        PlannedDate = t.PlannedDate,
        DueTime = t.DueTime,
        ReminderTime = t.ReminderTime,
        Priority = t.Priority,
        EnergyLevel = t.EnergyLevel,
        EstimatedMinutes = t.EstimatedMinutes,
        Recurrence = t.Recurrence,
        CategoryId = t.CategoryId,
        TagsCsv = t.Tags.Count == 0 ? null : string.Join(", ", t.Tags.Select(tag => tag.Name)),
        WorkspaceId = t.WorkspaceId,
        AssigneeId = t.UserId
    };
}

/// <summary>Today / day view of tasks with filters (PRD §5 Smart Filters).</summary>
public class TodayViewModel
{
    public DateOnly Date { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
    public List<Category> Categories { get; set; } = new();

    public List<Tag> Tags { get; set; } = new();

    public int? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }

    public int? FilterCategoryId { get; set; }
    public Priority? FilterPriority { get; set; }
    public int? FilterTagId { get; set; }
    public bool HideCompleted { get; set; }

    public int CompletedCount => Tasks.Count(t => t.Status == DailyTaskStatus.Completed);
    public int TotalCount => Tasks.Count;
    public double CompletionRate => TotalCount == 0 ? 0 : Math.Round(CompletedCount * 100.0 / TotalCount, 0);
    public int CarriedForwardCount => Tasks.Count(t => t.CarryForwardCount > 0);
}
