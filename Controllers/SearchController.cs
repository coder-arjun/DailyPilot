using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

/// <summary>Global search across the user's tasks, habits, categories, tags and workspaces.</summary>
[Authorize]
public class SearchController : Controller
{
    private const int TaskPageSize = 10;

    private readonly ApplicationDbContext _db;
    private readonly IWorkspaceService _workspaces;
    private readonly UserManager<ApplicationUser> _userManager;

    public SearchController(ApplicationDbContext db, IWorkspaceService workspaces, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _workspaces = workspaces;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var userId = _userManager.GetUserId(User)!;
        var vm = new SearchViewModel { Query = q?.Trim() ?? string.Empty, TaskPage = page };

        if (string.IsNullOrWhiteSpace(vm.Query))
            return View(vm);

        var term = vm.Query;

        // Tasks (paginated)
        var taskQuery = _db.Tasks
            .Include(t => t.Category)
            .Where(t => t.UserId == userId && (t.Title.Contains(term) || (t.Notes != null && t.Notes.Contains(term))))
            .OrderByDescending(t => t.PlannedDate);

        var taskTotal = await taskQuery.CountAsync();
        vm.TaskTotalPages = (int)Math.Ceiling(taskTotal / (double)TaskPageSize);
        vm.Tasks = await taskQuery.Skip((page - 1) * TaskPageSize).Take(TaskPageSize).ToListAsync();

        vm.Habits = await _db.Habits
            .Where(h => h.UserId == userId && (h.Name.Contains(term) || (h.Description != null && h.Description.Contains(term))))
            .OrderBy(h => h.Name).Take(20).ToListAsync();

        vm.Categories = await _db.Categories
            .Where(c => c.UserId == userId && c.Name.Contains(term))
            .OrderBy(c => c.Name).Take(20).ToListAsync();

        vm.Tags = await _db.Tags
            .Where(t => t.UserId == userId && t.Name.Contains(term))
            .OrderBy(t => t.Name).Take(20).ToListAsync();

        var myWsIds = (await _workspaces.GetUserWorkspacesAsync(userId)).Select(w => w.Id).ToList();
        vm.Workspaces = await _db.Workspaces
            .Where(w => myWsIds.Contains(w.Id) && (w.Name.Contains(term) || (w.Description != null && w.Description.Contains(term))))
            .OrderBy(w => w.Name).ToListAsync();

        vm.TotalResults = taskTotal + vm.Habits.Count + vm.Categories.Count + vm.Tags.Count + vm.Workspaces.Count;
        return View(vm);
    }
}
