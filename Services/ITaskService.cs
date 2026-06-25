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
}
