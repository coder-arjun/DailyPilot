using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

[Authorize]
public class CalendarController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITaskService _tasks;
    private readonly UserManager<ApplicationUser> _userManager;

    public CalendarController(ApplicationDbContext db, ITaskService tasks, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tasks = tasks;
        _userManager = userManager;
    }

    // PRD §4 Calendar View
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var user = (await _userManager.GetUserAsync(User))!;
        var today = await _tasks.GetLocalTodayAsync(user);

        var y = year ?? today.Year;
        var m = month ?? today.Month;
        var first = new DateOnly(y, m, 1);
        var last = first.AddMonths(1).AddDays(-1);

        // Grid starts on Monday of the week containing the 1st.
        int offset = ((int)first.DayOfWeek + 6) % 7;
        var gridStart = first.AddDays(-offset);
        var gridEnd = gridStart.AddDays(41); // 6 weeks

        var counts = await _db.Tasks
            .Where(t => t.UserId == user.Id && t.PlannedDate >= gridStart && t.PlannedDate <= gridEnd)
            .GroupBy(t => t.PlannedDate)
            .Select(g => new
            {
                Date = g.Key,
                Planned = g.Count(),
                Completed = g.Count(t => t.Status == DailyTaskStatus.Completed)
            })
            .ToListAsync();

        var lookup = counts.ToDictionary(c => c.Date);

        var vm = new CalendarViewModel { Year = y, Month = m, Today = today };
        for (var d = gridStart; d <= gridEnd; d = d.AddDays(1))
        {
            lookup.TryGetValue(d, out var c);
            vm.Days.Add(new CalendarDay
            {
                Date = d,
                InMonth = d.Month == m,
                IsToday = d == today,
                Planned = c?.Planned ?? 0,
                Completed = c?.Completed ?? 0
            });
        }

        return View(vm);
    }
}
