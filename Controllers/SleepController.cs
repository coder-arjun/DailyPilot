using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.Services.Sleep;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

/// <summary>
/// Manual sleep journal (dashboard + entry forms). Evening (bed time) and morning
/// (wake time, quality) fields are saved separately, each via
/// <see cref="ISleepService.UpsertAsync"/>, and are keyed by the WAKE-UP date — see
/// docs/superpowers/specs/2026-07-30-sleep-tracker-design.md. Score/insights math
/// lives in Services/Sleep/SleepMetrics.cs and SleepInsights.cs.
/// </summary>
[Authorize]
public class SleepController : Controller
{
    private readonly ISleepService _sleep;
    private readonly ITaskService _tasks;
    private readonly IDateTimeProvider _clock;
    private readonly UserManager<ApplicationUser> _userManager;

    public SleepController(ISleepService sleep, ITaskService tasks, IDateTimeProvider clock, UserManager<ApplicationUser> userManager)
    {
        _sleep = sleep;
        _tasks = tasks;
        _clock = clock;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var today = await _tasks.GetLocalTodayAsync(user);
        var eveningDate = EveningTargetDate(user, today);

        var dashboard = await _sleep.BuildDashboardAsync(user.Id, today);
        var (weekly, personalized) = await _sleep.InsightsAsync(user.Id, today);

        var eveningEntry = eveningDate == today ? dashboard.Today : await _sleep.GetAsync(user.Id, eveningDate);

        return View(new SleepPageViewModel
        {
            Dashboard = dashboard,
            Weekly = weekly,
            Personalized = personalized,
            EveningEntry = eveningEntry,
            EveningTargetDate = eveningDate,
            Today = today,
            MorningEntry = dashboard.Today,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEvening(TimeOnly bedTime, TimeOnly? estimatedSleepTime, bool phoneBeforeBed = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var today = await _tasks.GetLocalTodayAsync(user);
        var date = EveningTargetDate(user, today);

        await _sleep.UpsertAsync(user.Id, new SleepEntry
        {
            Date = date,
            BedTime = bedTime,
            EstimatedSleepTime = estimatedSleepTime ?? bedTime.AddMinutes(15),
            PhoneBeforeBed = phoneBeforeBed,
            BedTimeEstimated = false, // the user just typed this in — it's no longer a guess
        });

        TempData["Success"] = "Evening entry saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMorning(TimeOnly wakeTime, TimeOnly? timeOutOfBed, int quality, bool dreamRemembered = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var today = await _tasks.GetLocalTodayAsync(user);

        // BedTime/EstimatedSleepTime are unconditionally (re)written by UpsertAsync, so the
        // existing evening values must be carried forward explicitly or they'd be clobbered
        // to default(TimeOnly). If no evening entry exists yet, synthesize a sane bed time —
        // and honestly flag it as estimated (BedTimeEstimated) so the UI never presents a
        // guess as if the user had logged it. If an evening entry (or an earlier estimate)
        // already exists, its BedTime/estimated-flag are carried forward unchanged.
        var existing = await _sleep.GetAsync(user.Id, today);
        var bedTimeEstimated = existing is null ? true : existing.BedTimeEstimated ?? false;

        await _sleep.UpsertAsync(user.Id, new SleepEntry
        {
            Date = today,
            BedTime = existing?.BedTime ?? wakeTime.AddHours(-8),
            EstimatedSleepTime = existing?.EstimatedSleepTime ?? wakeTime.AddHours(-8).AddMinutes(15),
            PhoneBeforeBed = existing?.PhoneBeforeBed,
            BedTimeEstimated = bedTimeEstimated,
            WakeTime = wakeTime,
            TimeOutOfBed = timeOutOfBed ?? wakeTime,
            Quality = Math.Clamp(quality, 1, 10),
            DreamRemembered = dreamRemembered,
        });

        TempData["Success"] = "Morning entry saved.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// An evening entry is keyed by the WAKE date. Logged at/after 12:00 local (a normal
    /// bedtime) it belongs to tomorrow's wake-up; logged before noon (an already-past-
    /// midnight bedtime) it belongs to today's — the entry is keyed by wake date.
    /// </summary>
    private DateOnly EveningTargetDate(ApplicationUser user, DateOnly today)
    {
        var tz = TimeZoneHelper.Resolve(user.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, tz);
        return localNow.TimeOfDay >= TimeSpan.FromHours(12) ? today.AddDays(1) : today;
    }
}
