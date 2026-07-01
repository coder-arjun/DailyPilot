using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IAnalyticsService _analytics;
    private readonly ITaskService _tasks;
    private readonly IGamificationService _gamification;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(IAnalyticsService analytics, ITaskService tasks,
        IGamificationService gamification, UserManager<ApplicationUser> userManager)
    {
        _analytics = analytics;
        _tasks = tasks;
        _gamification = gamification;
        _userManager = userManager;
    }

    // FR-009 Analytics Dashboard
    public async Task<IActionResult> Index()
    {
        var user = (await _userManager.GetUserAsync(User))!;
        var today = await _tasks.GetLocalTodayAsync(user);
        var vm = await _analytics.BuildDashboardAsync(user, today);
        ViewBag.Gamification = await _gamification.GetSummaryAsync(user);
        return View(vm);
    }
}
