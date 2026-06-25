using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.Services.Ai;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

/// <summary>Phase 2 AI features (PRD §6): NL task creation + the AI Assistant page.</summary>
[Authorize]
public class AiController : Controller
{
    private readonly IAiTaskAssistant _ai;
    private readonly ITaskService _tasks;
    private readonly IAnalyticsService _analytics;
    private readonly IHabitService _habits;
    private readonly IWorkspaceContext _wsContext;
    private readonly IWorkspaceService _workspaces;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AiController(IAiTaskAssistant ai, ITaskService tasks, IAnalyticsService analytics,
        IHabitService habits, IWorkspaceContext wsContext, IWorkspaceService workspaces,
        ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _ai = ai;
        _tasks = tasks;
        _analytics = analytics;
        _habits = habits;
        _wsContext = wsContext;
        _workspaces = workspaces;
        _db = db;
        _userManager = userManager;
    }

    private async Task<ApplicationUser> CurrentUserAsync() => (await _userManager.GetUserAsync(User))!;

    private async Task<int?> ResolveWorkspaceIdAsync(string userId)
    {
        var wsId = _wsContext.CurrentWorkspaceId;
        return wsId is not null && await _workspaces.IsMemberAsync(wsId.Value, userId) ? wsId : null;
    }

    // GET /Ai  -> the assistant dashboard
    public async Task<IActionResult> Index(TimeOnly? workStart, TimeOnly? workEnd)
    {
        var user = await CurrentUserAsync();
        var today = await _tasks.GetLocalTodayAsync(user);
        var wsId = await ResolveWorkspaceIdAsync(user.Id);
        var todays = await _tasks.GetTasksForDateAsync(user.Id, today, wsId);

        var vm = new AssistantViewModel
        {
            Date = today,
            LlmEnabled = _ai.IsLlmEnabled,
            WorkStart = workStart ?? new TimeOnly(9, 0),
            WorkEnd = workEnd ?? new TimeOnly(17, 0)
        };

        var dashboard = await _analytics.BuildDashboardAsync(user, today);
        var habitStatuses = await _habits.GetActiveStatusesAsync(user.Id, today);

        vm.Coach = await _ai.CoachAsync(BuildCoachContext(user, today, todays, dashboard, habitStatuses));
        vm.Prioritization = await _ai.PrioritizeAsync(todays);
        vm.Schedule = await _ai.GenerateScheduleAsync(todays, vm.WorkStart, vm.WorkEnd);
        vm.Insights = await _ai.GenerateInsightsAsync(dashboard);
        vm.Recommendations = await _ai.RecommendAsync(await BuildRecommendationContextAsync(user.Id, today, user.CurrentStreak));

        return View(vm);
    }

    private static CoachContext BuildCoachContext(
        ApplicationUser user, DateOnly today,
        IReadOnlyList<TaskItem> todays,
        DailyPilot.ViewModels.DashboardViewModel dashboard,
        IReadOnlyList<DailyPilot.ViewModels.HabitStatus> habits)
    {
        var pending = todays
            .Where(t => t.Status != DailyTaskStatus.Completed)
            .OrderByDescending(t => t.Priority)
            .ThenByDescending(t => t.CarryForwardCount)
            .ThenBy(t => t.DueTime)
            .ToList();

        return new CoachContext
        {
            Today = today,
            DisplayName = user.DisplayName ?? user.UserName,
            PlannedToday = todays.Count,
            CompletedToday = todays.Count(t => t.Status == DailyTaskStatus.Completed),
            PendingToday = pending.Count,
            TopPendingTasks = pending.Take(3).Select(t => t.Title).ToList(),
            WeeklyCompletionRate = dashboard.WeeklyCompletionRate,
            CarryForwardRate = dashboard.CarryForwardRate,
            CurrentStreak = user.CurrentStreak,
            LongestStreak = user.LongestStreak,
            Habits = habits.Select(h => new CoachHabitInfo
            {
                Name = h.Habit.Name,
                Frequency = h.Habit.Frequency.ToString(),
                DoneToday = h.DoneToday,
                TargetMet = h.TargetMet,
                CurrentStreak = h.CurrentStreak
            }).ToList()
        };
    }

    // POST /Ai/QuickAdd -> natural-language task creation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAdd(string text, DateOnly? date)
    {
        var user = await CurrentUserAsync();
        if (string.IsNullOrWhiteSpace(text))
            return RedirectToAction("Index", "Tasks", new { date });

        var categories = await _db.Categories.Where(c => c.UserId == user.Id).ToListAsync();
        var today = await _tasks.GetLocalTodayAsync(user);

        var parsed = await _ai.ParseTaskAsync(text, categories.Select(c => c.Name).ToList(), today);

        var wsId = await ResolveWorkspaceIdAsync(user.Id);
        var task = new TaskItem
        {
            Title = parsed.Title,
            Notes = parsed.Notes,
            PlannedDate = parsed.PlannedDate,
            DueTime = parsed.DueTime,
            Priority = parsed.Priority,
            EnergyLevel = parsed.EnergyLevel,
            EstimatedMinutes = parsed.EstimatedMinutes,
            Recurrence = parsed.Recurrence,
            CategoryId = categories.FirstOrDefault(c =>
                string.Equals(c.Name, parsed.CategoryName, StringComparison.OrdinalIgnoreCase))?.Id,
            WorkspaceId = wsId,
            CreatedById = user.Id
        };

        await _tasks.CreateAsync(user.Id, task);
        TempData["Success"] = $"✨ {parsed.Summary}";
        return RedirectToAction("Index", "Tasks", new { date = parsed.PlannedDate });
    }

    private async Task<RecommendationContext> BuildRecommendationContextAsync(string userId, DateOnly today, int streak)
    {
        var since = today.AddDays(-30);
        var recent = await _db.Tasks
            .Include(t => t.Category)
            .Where(t => t.UserId == userId && t.PlannedDate >= since)
            .ToListAsync();

        var ctx = new RecommendationContext { Today = today, CurrentStreak = streak };

        // Frequently created tasks not already on today's list.
        var todayTitles = recent.Where(t => t.PlannedDate == today)
            .Select(t => t.Title.ToLowerInvariant()).ToHashSet();
        ctx.FrequentTasks = recent
            .Where(t => !todayTitles.Contains(t.Title.ToLowerInvariant()))
            .GroupBy(t => t.Title)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => (g.Key, g.Count(), g.First().Category?.Name))
            .ToList();

        // Tasks that keep slipping.
        ctx.StickyTasks = recent
            .Where(t => t.Status != DailyTaskStatus.Completed && t.CarryForwardCount > 0)
            .OrderByDescending(t => t.CarryForwardCount)
            .Take(5)
            .Select(t => (t.Title, t.CarryForwardCount))
            .ToList();

        // Categories with nothing in the last 14 days.
        var activeCats = recent.Where(t => t.PlannedDate >= today.AddDays(-14) && t.Category != null)
            .Select(t => t.Category!.Name).ToHashSet();
        ctx.NeglectedCategories = (await _db.Categories.Where(c => c.UserId == userId).ToListAsync())
            .Select(c => c.Name)
            .Where(n => !activeCats.Contains(n))
            .ToList();

        return ctx;
    }
}
