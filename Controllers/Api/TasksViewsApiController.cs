using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

/// <summary>
/// Mobile counterparts of <see cref="CalendarController"/> and the Board action of
/// <see cref="TasksController"/>. Kept separate from <see cref="TasksApiController"/>
/// so the CRUD surface there stays untouched; attribute routes on the same
/// "api/v1/tasks" prefix merge fine as long as templates don't collide.
/// </summary>
[Route("api/v1/tasks")]
public class TasksViewsApiController : ApiControllerBase
{
    private readonly ITaskService _tasks;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TasksViewsApiController(ITaskService tasks, ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _tasks = tasks;
        _db = db;
        _userManager = userManager;
    }

    // GET api/v1/tasks/calendar?month=yyyy-MM -> day counts + all of the month's tasks.
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] string? month)
    {
        if (string.IsNullOrWhiteSpace(month) || !TryParseMonth(month, out var first))
            return ApiError(400, "month must be yyyy-MM.");

        var last = first.AddMonths(1).AddDays(-1);

        var tasks = await _db.Tasks
            .Include(t => t.Category)
            .Where(t => t.UserId == CurrentUserId && t.PlannedDate >= first && t.PlannedDate <= last)
            .OrderBy(t => t.Status == DailyTaskStatus.Completed)
            .ThenBy(t => t.SortOrder)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueTime)
            .ToListAsync();

        var days = tasks
            .GroupBy(t => t.PlannedDate)
            .Select(g => new CalendarDayDto(g.Key.ToString("yyyy-MM-dd"), g.Count(), g.Count(t => t.Status == DailyTaskStatus.Completed)))
            .OrderBy(d => d.Date, StringComparer.Ordinal)
            .ToList();

        return Ok(new CalendarMonthDto(days, tasks.Select(TaskDto.From).ToList()));
    }

    // GET api/v1/tasks/board -> local-today tasks grouped by lifecycle status.
    [HttpGet("board")]
    public async Task<IActionResult> Board()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user is null) return ApiError(401, "Unknown user.");

        var today = await _tasks.GetLocalTodayAsync(user);
        var tasks = await _tasks.GetTasksForDateAsync(CurrentUserId, today);

        var dto = new BoardDto(
            tasks.Where(t => t.Status == DailyTaskStatus.Pending).Select(TaskDto.From).ToList(),
            tasks.Where(t => t.Status == DailyTaskStatus.InProgress).Select(TaskDto.From).ToList(),
            tasks.Where(t => t.Status == DailyTaskStatus.Completed).Select(TaskDto.From).ToList(),
            tasks.Where(t => t.Status == DailyTaskStatus.CarriedForward).Select(TaskDto.From).ToList());

        return Ok(dto);
    }

    // POST api/v1/tasks/{id}/status -> Kanban-style status move.
    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, TaskStatusUpdateRequest request)
    {
        if (!Enum.IsDefined(typeof(DailyTaskStatus), request.Status))
            return ApiError(400, "status must be a valid task status.");

        if (!await _tasks.SetStatusAsync(CurrentUserId, id, (DailyTaskStatus)request.Status))
            return ApiError(404, "Task not found.");

        var loaded = await _tasks.GetAsync(CurrentUserId, id);
        return Ok(loaded is null ? new { ok = true } : (object)TaskDto.From(loaded));
    }

    private static bool TryParseMonth(string month, out DateOnly first) =>
        DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out first);
}
