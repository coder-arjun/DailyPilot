using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

[Route("api/v1/habits")]
public class HabitsApiController : ApiControllerBase
{
    private readonly IHabitService _habits;
    private readonly ITaskService _tasks;
    private readonly UserManager<ApplicationUser> _userManager;

    public HabitsApiController(IHabitService habits, ITaskService tasks, UserManager<ApplicationUser> userManager)
    {
        _habits = habits;
        _tasks = tasks;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var today = await LocalTodayAsync();
        if (today is null) return ApiError(401, "Unknown user.");

        var statuses = await _habits.GetActiveStatusesAsync(CurrentUserId, today.Value);
        return Ok(statuses.Select(HabitDto.From));
    }

    [HttpPost]
    public async Task<IActionResult> Create(HabitWriteRequest request)
    {
        if (request.Frequency != (int)HabitFrequency.Daily && request.Frequency != (int)HabitFrequency.Weekly)
            return ApiError(400, "frequency must be 0 (Daily) or 1 (Weekly).");

        var habit = new Habit
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#198754" : request.Color,
            Frequency = (HabitFrequency)request.Frequency,
            TargetPerWeek = Math.Clamp(request.TargetPerWeek <= 0 ? 3 : request.TargetPerWeek, 1, 7),
        };
        var created = await _habits.CreateAsync(CurrentUserId, habit);

        var status = new ViewModels.HabitStatus { Habit = created, DoneToday = false, CurrentStreak = 0, LongestStreak = 0, ThisWeekCount = 0 };
        return CreatedAtAction(nameof(List), null, HabitDto.From(status));
    }

    [HttpPost("{id:int}/checkin")]
    public Task<IActionResult> CheckIn(int id) => SetChecked(id, wantChecked: true);

    [HttpPost("{id:int}/uncheck")]
    public Task<IActionResult> Uncheck(int id) => SetChecked(id, wantChecked: false);

    private async Task<IActionResult> SetChecked(int id, bool wantChecked)
    {
        var habit = await _habits.GetAsync(CurrentUserId, id);
        if (habit is null) return ApiError(404, "Habit not found.");

        var today = await LocalTodayAsync();
        if (today is null) return ApiError(401, "Unknown user.");

        var before = await _habits.GetActiveStatusesAsync(CurrentUserId, today.Value);
        var doneToday = before.FirstOrDefault(s => s.Habit.Id == id)?.DoneToday ?? false;
        if (doneToday == wantChecked)
            return ApiError(409, wantChecked ? "Habit is already checked in today." : "Habit is not checked in today.");

        if (!await _habits.ToggleAsync(CurrentUserId, id, today.Value))
            return ApiError(404, "Habit not found.");

        var after = await _habits.GetActiveStatusesAsync(CurrentUserId, today.Value);
        var updated = after.FirstOrDefault(s => s.Habit.Id == id);
        return Ok(updated is null ? new { ok = true } : (object)HabitDto.From(updated));
    }

    private async Task<DateOnly?> LocalTodayAsync()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        return user is null ? null : await _tasks.GetLocalTodayAsync(user);
    }
}
