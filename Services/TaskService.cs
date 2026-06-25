using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

/// <summary>
/// Core task CRUD + completion (FR-001..FR-004). Records history (FR-008)
/// and keeps the user's streak up to date.
/// </summary>
public class TaskService : ITaskService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IStreakService _streaks;

    public TaskService(ApplicationDbContext db, IDateTimeProvider clock, IStreakService streaks)
    {
        _db = db;
        _clock = clock;
        _streaks = streaks;
    }

    public Task<DateOnly> GetLocalTodayAsync(ApplicationUser user)
        => Task.FromResult(TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow));

    public async Task<List<TaskItem>> GetTasksForDateAsync(string userId, DateOnly date, int? workspaceId = null)
    {
        return await _db.Tasks
            .Include(t => t.Category)
            .Include(t => t.Tags)
            .Where(t => t.UserId == userId && t.PlannedDate == date && t.WorkspaceId == workspaceId)
            .OrderBy(t => t.Status == DailyTaskStatus.Completed)
            .ThenBy(t => t.SortOrder)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueTime)
            .ToListAsync();
    }

    public async Task<TaskItem?> GetAsync(string userId, int id)
    {
        return await _db.Tasks
            .Include(t => t.Category)
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    }

    public async Task<TaskItem> CreateAsync(string userId, TaskItem task)
    {
        // Honour a pre-set assignee (workspace tasks); otherwise default to the creator.
        if (string.IsNullOrEmpty(task.UserId)) task.UserId = userId;
        task.CreatedById ??= userId;
        task.CreatedAt = _clock.UtcNow;
        if (task.OriginalDate == default)
            task.OriginalDate = task.PlannedDate;
        task.Status = DailyTaskStatus.Pending;

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        AddHistory(task, "Created", $"Planned for {task.PlannedDate:yyyy-MM-dd}");
        await _db.SaveChangesAsync();
        return task;
    }

    public async Task<bool> UpdateAsync(string userId, TaskItem updated)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == updated.Id && t.UserId == userId);
        if (task is null) return false;

        task.Title = updated.Title;
        task.Notes = updated.Notes;
        task.PlannedDate = updated.PlannedDate;
        task.DueTime = updated.DueTime;
        task.ReminderTime = updated.ReminderTime;
        task.Priority = updated.Priority;
        task.EnergyLevel = updated.EnergyLevel;
        task.EstimatedMinutes = updated.EstimatedMinutes;
        task.Recurrence = updated.Recurrence;
        task.CategoryId = updated.CategoryId;

        AddHistory(task, "Updated", null);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return false;

        AddHistory(task, "Deleted", null);
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCompleteAsync(string userId, int id)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return false;

        if (task.Status == DailyTaskStatus.Completed)
        {
            task.Status = DailyTaskStatus.Pending;
            task.CompletedAt = null;
            AddHistory(task, "Reopened", null);
        }
        else
        {
            task.Status = DailyTaskStatus.Completed;
            task.CompletedAt = _clock.UtcNow;
            AddHistory(task, "Completed", null);
        }

        await _db.SaveChangesAsync();
        await _streaks.RecalculateAsync(userId);
        return true;
    }

    private void AddHistory(TaskItem task, string action, string? details)
    {
        _db.TaskHistories.Add(new TaskHistory
        {
            TaskItemId = task.Id,
            UserId = task.UserId,
            Action = action,
            Details = details,
            TaskTitle = task.Title,
            OnDate = task.PlannedDate,
            Timestamp = _clock.UtcNow
        });
    }
}
