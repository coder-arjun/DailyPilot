using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

[Route("api/v1/tasks")]
public class TasksApiController : ApiControllerBase
{
    private readonly ITaskService _tasks;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICarryForwardService _carryForward;

    public TasksApiController(ITaskService tasks, UserManager<ApplicationUser> userManager,
        ICarryForwardService carryForward)
    {
        _tasks = tasks;
        _userManager = userManager;
        _carryForward = carryForward;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? date)
    {
        // Roll unfinished tasks from previous days into today — the web Today page
        // does this on load (TasksController.Index); the app must match or
        // carried-forward tasks only appear after visiting the website.
        try { await _carryForward.RunForUserAsync(CurrentUserId); } catch { /* never block the list */ }

        DateOnly day;
        if (string.IsNullOrWhiteSpace(date))
        {
            var user = await _userManager.FindByIdAsync(CurrentUserId);
            if (user is null) return ApiError(401, "Unknown user.");
            day = await _tasks.GetLocalTodayAsync(user);
        }
        else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out day))
        {
            return ApiError(400, "date must be yyyy-MM-dd.");
        }

        var items = await _tasks.GetTasksForDateAsync(CurrentUserId, day);
        return Ok(items.Select(TaskDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var task = await _tasks.GetAsync(CurrentUserId, id);
        return task is null ? ApiError(404, "Task not found.") : Ok(TaskDto.From(task));
    }

    [HttpPost]
    public async Task<IActionResult> Create(TaskWriteRequest request)
    {
        if (!TryParseParts(request, out var planned, out var due, out var reminder, out var error))
            return ApiError(400, error!);

        var task = new TaskItem
        {
            UserId = CurrentUserId,
            Title = request.Title,
            Notes = request.Notes,
            PlannedDate = planned,
            OriginalDate = planned,
            DueTime = due,
            ReminderTime = reminder,
            Priority = (Priority)request.Priority,
            CategoryId = request.CategoryId,
            EstimatedMinutes = request.EstimatedMinutes,
        };
        var created = await _tasks.CreateAsync(CurrentUserId, task);
        var loaded = await _tasks.GetAsync(CurrentUserId, created.Id) ?? created;
        return CreatedAtAction(nameof(Get), new { id = created.Id }, TaskDto.From(loaded));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TaskWriteRequest request)
    {
        if (!TryParseParts(request, out var planned, out var due, out var reminder, out var error))
            return ApiError(400, error!);

        var task = await _tasks.GetAsync(CurrentUserId, id);
        if (task is null) return ApiError(404, "Task not found.");

        task.Title = request.Title;
        task.Notes = request.Notes;
        task.PlannedDate = planned;
        task.DueTime = due;
        task.ReminderTime = reminder;
        task.Priority = (Priority)request.Priority;
        task.CategoryId = request.CategoryId;
        task.EstimatedMinutes = request.EstimatedMinutes;

        if (!await _tasks.UpdateAsync(CurrentUserId, task))
            return ApiError(404, "Task not found.");
        var loaded = await _tasks.GetAsync(CurrentUserId, id) ?? task;
        return Ok(TaskDto.From(loaded));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await _tasks.DeleteAsync(CurrentUserId, id) ? NoContent() : ApiError(404, "Task not found.");

    [HttpPost("{id:int}/complete")]
    public Task<IActionResult> Complete(int id) => SetCompleted(id, wantCompleted: true);

    [HttpPost("{id:int}/reopen")]
    public Task<IActionResult> Reopen(int id) => SetCompleted(id, wantCompleted: false);

    private async Task<IActionResult> SetCompleted(int id, bool wantCompleted)
    {
        var task = await _tasks.GetAsync(CurrentUserId, id);
        if (task is null) return ApiError(404, "Task not found.");
        if (task.IsCompleted == wantCompleted)
            return ApiError(409, wantCompleted ? "Task is already completed." : "Task is not completed.");

        if (!await _tasks.ToggleCompleteAsync(CurrentUserId, id))
            return ApiError(404, "Task not found.");
        var loaded = await _tasks.GetAsync(CurrentUserId, id);
        return Ok(loaded is null ? new { ok = true } : (object)TaskDto.From(loaded));
    }

    private static bool TryParseParts(
        TaskWriteRequest request, out DateOnly planned, out TimeOnly? due, out TimeOnly? reminder, out string? error)
    {
        planned = default; due = null; reminder = null; error = null;
        if (!DateOnly.TryParseExact(request.PlannedDate, "yyyy-MM-dd", out planned))
        { error = "plannedDate must be yyyy-MM-dd."; return false; }

        if (!string.IsNullOrWhiteSpace(request.DueTime))
        {
            if (!TimeOnly.TryParseExact(request.DueTime, "HH:mm", out var d))
            { error = "dueTime must be HH:mm."; return false; }
            due = d;
        }
        if (!string.IsNullOrWhiteSpace(request.ReminderTime))
        {
            if (!TimeOnly.TryParseExact(request.ReminderTime, "HH:mm", out var r))
            { error = "reminderTime must be HH:mm."; return false; }
            reminder = r;
        }
        return true;
    }
}
