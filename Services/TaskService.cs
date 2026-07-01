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
    private readonly IGamificationService _gamification;

    public TaskService(ApplicationDbContext db, IDateTimeProvider clock, IStreakService streaks,
        IGamificationService gamification)
    {
        _db = db;
        _clock = clock;
        _streaks = streaks;
        _gamification = gamification;
    }

    public Task<DateOnly> GetLocalTodayAsync(ApplicationUser user)
        => Task.FromResult(TimeZoneHelper.LocalToday(user.TimeZoneId, _clock.UtcNow));

    public async Task<List<TaskItem>> GetTasksForDateAsync(string userId, DateOnly date, int? workspaceId = null)
    {
        return await _db.Tasks
            .Include(t => t.Category)
            .Include(t => t.Tags)
            .Include(t => t.ChecklistItems)
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
            .Include(t => t.ChecklistItems.OrderBy(c => c.SortOrder))
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

        // Rescheduling (new day or new reminder time) re-arms the reminder so it can fire again.
        var rescheduled = task.PlannedDate != updated.PlannedDate || task.ReminderTime != updated.ReminderTime;

        task.Title = updated.Title;
        task.Notes = updated.Notes;
        task.PlannedDate = updated.PlannedDate;
        task.DueTime = updated.DueTime;
        task.ReminderTime = updated.ReminderTime;
        if (rescheduled) task.ReminderFiredOn = null;
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
            task.ReminderFiredOn = null;   // re-arm: a reopened task's reminder can fire again
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
        if (task.Status == DailyTaskStatus.Completed)
            await _gamification.AwardAsync(task.UserId, _gamification.XpForTask(task));
        return true;
    }

    public async Task<bool> SetStatusAsync(string userId, int id, DailyTaskStatus status)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return false;
        if (task.Status == status) return true;

        var wasCompleted = task.Status == DailyTaskStatus.Completed;
        task.Status = status;

        if (status == DailyTaskStatus.Completed)
        {
            task.CompletedAt = _clock.UtcNow;
            AddHistory(task, "Completed", null);
        }
        else
        {
            task.CompletedAt = null;
            if (status == DailyTaskStatus.InProgress) { task.ReminderFiredOn = null; AddHistory(task, "Started", null); }
            else if (wasCompleted) { task.ReminderFiredOn = null; AddHistory(task, "Reopened", null); }
        }

        await _db.SaveChangesAsync();
        await _streaks.RecalculateAsync(userId);
        if (status == DailyTaskStatus.Completed && !wasCompleted)
            await _gamification.AwardAsync(task.UserId, _gamification.XpForTask(task));
        return true;
    }

    public async Task<int?> LogTimeAsync(string userId, int id, int minutes)
    {
        if (minutes <= 0) return null;
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return null;
        task.ActualMinutes = (task.ActualMinutes ?? 0) + minutes;
        await _db.SaveChangesAsync();
        return task.ActualMinutes;
    }

    // --- Checklist / subtasks ---

    public async Task<TaskChecklistItem?> AddChecklistItemAsync(string userId, int taskId, string text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0) return null;
        if (text.Length > 300) text = text[..300];

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
        if (task is null) return null;

        var nextOrder = await _db.TaskChecklistItems.Where(c => c.TaskItemId == taskId)
            .Select(c => (int?)c.SortOrder).MaxAsync() ?? -1;

        var item = new TaskChecklistItem
        {
            TaskItemId = taskId,
            Text = text,
            SortOrder = nextOrder + 1,
            CreatedAt = _clock.UtcNow
        };
        _db.TaskChecklistItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<int> AddChecklistItemsAsync(string userId, int taskId, IEnumerable<string> texts)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
        if (task is null) return 0;

        var nextOrder = (await _db.TaskChecklistItems.Where(c => c.TaskItemId == taskId)
            .Select(c => (int?)c.SortOrder).MaxAsync() ?? -1) + 1;

        int added = 0;
        foreach (var raw in texts)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0) continue;
            if (text.Length > 300) text = text[..300];
            _db.TaskChecklistItems.Add(new TaskChecklistItem
            {
                TaskItemId = taskId,
                Text = text,
                SortOrder = nextOrder++,
                CreatedAt = _clock.UtcNow
            });
            added++;
        }
        if (added > 0) await _db.SaveChangesAsync();
        return added;
    }

    public async Task<bool> ToggleChecklistItemAsync(string userId, int itemId)
    {
        var item = await _db.TaskChecklistItems
            .Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == itemId && c.TaskItem!.UserId == userId);
        if (item is null) return false;
        item.IsDone = !item.IsDone;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteChecklistItemAsync(string userId, int itemId)
    {
        var item = await _db.TaskChecklistItems
            .Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == itemId && c.TaskItem!.UserId == userId);
        if (item is null) return false;
        _db.TaskChecklistItems.Remove(item);
        await _db.SaveChangesAsync();
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
