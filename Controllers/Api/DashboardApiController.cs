using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

/// <summary>Mobile counterpart of <see cref="DashboardController"/> (FR-009 Analytics Dashboard).</summary>
[Route("api/v1/dashboard")]
public class DashboardApiController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly ITaskService _tasks;
    private readonly IGamificationService _gamification;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardApiController(IAnalyticsService analytics, ITaskService tasks,
        IGamificationService gamification, UserManager<ApplicationUser> userManager)
    {
        _analytics = analytics;
        _tasks = tasks;
        _gamification = gamification;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user is null) return ApiError(401, "Unknown user.");

        var today = await _tasks.GetLocalTodayAsync(user);
        var vm = await _analytics.BuildDashboardAsync(user, today);
        var level = _gamification.Describe(user.Xp);

        var dto = new DashboardDto(
            user.DisplayName ?? user.UserName ?? "there",
            LevelInfoDto.From(level),
            vm.CompletedToday,
            vm.PlannedToday,
            user.CurrentStreak,
            user.LongestStreak,
            vm.Trend.Select(DashboardTrendPointDto.From).ToList());

        return Ok(dto);
    }
}
