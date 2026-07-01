using DailyPilot.Models;

namespace DailyPilot.Services;

public interface ITaskService
{
    /// <summary>Tasks for a user on a date within a context: null workspaceId = personal.</summary>
    Task<List<TaskItem>> GetTasksForDateAsync(string userId, DateOnly date, int? workspaceId = null);
    Task<TaskItem?> GetAsync(string userId, int id);
    Task<TaskItem> CreateAsync(string userId, TaskItem task);
    Task<bool> UpdateAsync(string userId, TaskItem task);
    Task<bool> DeleteAsync(string userId, int id);
    Task<bool> ToggleCompleteAsync(string userId, int id);
    Task<DateOnly> GetLocalTodayAsync(ApplicationUser user);

    /// <summary>Set a task's lifecycle status (used by the Kanban board).</summary>
    Task<bool> SetStatusAsync(string userId, int id, DailyTaskStatus status);

    /// <summary>Add elapsed focus/work minutes to a task's actual-time total.</summary>
    Task<int?> LogTimeAsync(string userId, int id, int minutes);

    // --- Checklist / subtasks ---
    Task<TaskChecklistItem?> AddChecklistItemAsync(string userId, int taskId, string text);
    Task<bool> ToggleChecklistItemAsync(string userId, int itemId);
    Task<bool> DeleteChecklistItemAsync(string userId, int itemId);
    /// <summary>Bulk-add checklist items (used by AI break-down). Returns count added.</summary>
    Task<int> AddChecklistItemsAsync(string userId, int taskId, IEnumerable<string> texts);
}
