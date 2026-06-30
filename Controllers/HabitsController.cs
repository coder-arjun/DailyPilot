using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

/// <summary>PRD Phase 5 — Habit Tracking.</summary>
[Authorize]
public class HabitsController : Controller
{
    private readonly IHabitService _habits;
    private readonly ITaskService _tasks;
    private readonly UserManager<ApplicationUser> _userManager;

    public HabitsController(IHabitService habits, ITaskService tasks, UserManager<ApplicationUser> userManager)
    {
        _habits = habits;
        _tasks = tasks;
        _userManager = userManager;
    }

    private async Task<ApplicationUser> CurrentUserAsync() => (await _userManager.GetUserAsync(User))!;

    public async Task<IActionResult> Index()
    {
        var user = await CurrentUserAsync();
        var today = await _tasks.GetLocalTodayAsync(user);
        var vm = new HabitsPageViewModel
        {
            Date = today,
            Habits = await _habits.GetActiveStatusesAsync(user.Id, today)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, string color, HabitFrequency frequency, int targetPerWeek,
        bool reminderEnabled = false, int reminderIntervalMinutes = 60, TimeOnly? reminderStart = null, TimeOnly? reminderEnd = null)
    {
        var user = await CurrentUserAsync();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Please enter a habit name.";
            return RedirectToAction(nameof(Index));
        }

        await _habits.CreateAsync(user.Id, new Habit
        {
            Name = name.Trim(),
            Description = description,
            Color = string.IsNullOrWhiteSpace(color) ? "#198754" : color,
            Frequency = frequency,
            TargetPerWeek = Math.Clamp(targetPerWeek, 1, 7),
            ReminderEnabled = reminderEnabled,
            ReminderIntervalMinutes = reminderIntervalMinutes < 5 ? 60 : reminderIntervalMinutes,
            ReminderStart = reminderStart ?? new TimeOnly(9, 0),
            ReminderEnd = reminderEnd ?? new TimeOnly(21, 0)
        });
        TempData["Success"] = "Habit added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? description, string color, HabitFrequency frequency, int targetPerWeek,
        bool reminderEnabled = false, int reminderIntervalMinutes = 60, TimeOnly? reminderStart = null, TimeOnly? reminderEnd = null)
    {
        var user = await CurrentUserAsync();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Please enter a habit name.";
            return RedirectToAction(nameof(Index));
        }

        await _habits.UpdateAsync(user.Id, new Habit
        {
            Id = id,
            Name = name.Trim(),
            Description = description,
            Color = string.IsNullOrWhiteSpace(color) ? "#198754" : color,
            Frequency = frequency,
            TargetPerWeek = Math.Clamp(targetPerWeek, 1, 7),
            ReminderEnabled = reminderEnabled,
            ReminderIntervalMinutes = reminderIntervalMinutes < 5 ? 60 : reminderIntervalMinutes,
            ReminderStart = reminderStart ?? new TimeOnly(9, 0),
            ReminderEnd = reminderEnd ?? new TimeOnly(21, 0)
        });
        TempData["Success"] = "Habit updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await CurrentUserAsync();
        var today = await _tasks.GetLocalTodayAsync(user);
        await _habits.ToggleAsync(user.Id, id, today);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var user = await CurrentUserAsync();
        await _habits.ArchiveAsync(user.Id, id);
        TempData["Success"] = "Habit archived.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await CurrentUserAsync();
        await _habits.DeleteAsync(user.Id, id);
        TempData["Success"] = "Habit deleted.";
        return RedirectToAction(nameof(Index));
    }
}
