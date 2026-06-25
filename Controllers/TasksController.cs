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
public class TasksController : Controller
{
    private readonly ITaskService _tasks;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserSeeder _seeder;
    private readonly IWorkspaceContext _wsContext;
    private readonly IWorkspaceService _workspaces;

    public TasksController(ITaskService tasks, ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IUserSeeder seeder, IWorkspaceContext wsContext, IWorkspaceService workspaces)
    {
        _tasks = tasks;
        _db = db;
        _userManager = userManager;
        _seeder = seeder;
        _wsContext = wsContext;
        _workspaces = workspaces;
    }

    private async Task<ApplicationUser> CurrentUserAsync() =>
        (await _userManager.GetUserAsync(User))!;

    /// <summary>Current workspace id if the user is a member of it, else null (personal).</summary>
    private async Task<int?> ResolveWorkspaceIdAsync(string userId)
    {
        var wsId = _wsContext.CurrentWorkspaceId;
        if (wsId is not null && await _workspaces.IsMemberAsync(wsId.Value, userId))
            return wsId;
        return null;
    }

    // GET /Tasks  -> today (or ?date=) view
    public async Task<IActionResult> Index(DateOnly? date, int? categoryId, Priority? priority, int? tagId, bool hideCompleted = false)
    {
        var user = await CurrentUserAsync();
        await _seeder.EnsureDefaultsAsync(user.Id);
        var day = date ?? await _tasks.GetLocalTodayAsync(user);
        var wsId = await ResolveWorkspaceIdAsync(user.Id);

        var tasks = await _tasks.GetTasksForDateAsync(user.Id, day, wsId);

        IEnumerable<TaskItem> filtered = tasks;
        if (categoryId is not null) filtered = filtered.Where(t => t.CategoryId == categoryId);
        if (priority is not null) filtered = filtered.Where(t => t.Priority == priority);
        if (tagId is not null) filtered = filtered.Where(t => t.Tags.Any(tag => tag.Id == tagId));
        if (hideCompleted) filtered = filtered.Where(t => t.Status != DailyTaskStatus.Completed);

        var vm = new TodayViewModel
        {
            Date = day,
            Tasks = filtered.ToList(),
            Categories = await UserCategoriesAsync(user.Id),
            Tags = await UserTagsAsync(user.Id),
            WorkspaceId = wsId,
            WorkspaceName = wsId is null ? null : (await _workspaces.GetAsync(wsId.Value))?.Name,
            FilterCategoryId = categoryId,
            FilterPriority = priority,
            FilterTagId = tagId,
            HideCompleted = hideCompleted
        };
        return View(vm);
    }

    // GET /Tasks/Create
    public async Task<IActionResult> Create(DateOnly? date)
    {
        var user = await CurrentUserAsync();
        var wsId = await ResolveWorkspaceIdAsync(user.Id);
        var vm = new TaskFormViewModel
        {
            PlannedDate = date ?? await _tasks.GetLocalTodayAsync(user),
            Categories = await UserCategoriesAsync(user.Id),
            AllTags = await UserTagNamesAsync(user.Id),
            WorkspaceId = wsId,
            AssigneeId = user.Id
        };
        await PopulateWorkspaceAsync(vm, wsId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskFormViewModel vm)
    {
        var user = await CurrentUserAsync();
        var wsId = await ResolveWorkspaceIdAsync(user.Id);
        if (!ModelState.IsValid)
        {
            vm.Categories = await UserCategoriesAsync(user.Id);
            vm.AllTags = await UserTagNamesAsync(user.Id);
            await PopulateWorkspaceAsync(vm, wsId);
            return View(vm);
        }

        var entity = vm.ToEntity();
        entity.WorkspaceId = wsId;
        entity.CreatedById = user.Id;
        // In a workspace, assign to the chosen (valid) member; otherwise to self.
        entity.UserId = wsId is not null && !string.IsNullOrEmpty(vm.AssigneeId)
                        && await _workspaces.IsMemberAsync(wsId.Value, vm.AssigneeId)
            ? vm.AssigneeId
            : user.Id;

        var created = await _tasks.CreateAsync(user.Id, entity);
        await ApplyTagsAsync(created.UserId, created.Id, vm.TagsCsv);
        TempData["Success"] = "Task added.";
        return RedirectToAction(nameof(Index), new { date = vm.PlannedDate });
    }

    // GET /Tasks/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var user = await CurrentUserAsync();
        var task = await _tasks.GetAsync(user.Id, id);
        if (task is null) return NotFound();

        var vm = TaskFormViewModel.FromEntity(task);
        vm.Categories = await UserCategoriesAsync(user.Id);
        vm.AllTags = await UserTagNamesAsync(user.Id);
        await PopulateWorkspaceAsync(vm, task.WorkspaceId);
        ViewBag.Attachments = await _db.TaskAttachments
            .Where(a => a.TaskItemId == id && a.UserId == user.Id)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TaskFormViewModel vm)
    {
        var user = await CurrentUserAsync();
        if (!ModelState.IsValid)
        {
            vm.Categories = await UserCategoriesAsync(user.Id);
            vm.AllTags = await UserTagNamesAsync(user.Id);
            await PopulateWorkspaceAsync(vm, vm.WorkspaceId);
            return View(vm);
        }

        var ok = await _tasks.UpdateAsync(user.Id, vm.ToEntity());
        if (!ok) return NotFound();

        await ApplyTagsAsync(user.Id, vm.Id, vm.TagsCsv);

        // Re-assign within a workspace (hand-off to another member).
        if (vm.WorkspaceId is not null && !string.IsNullOrEmpty(vm.AssigneeId) && vm.AssigneeId != user.Id
            && await _workspaces.IsMemberAsync(vm.WorkspaceId.Value, vm.AssigneeId))
        {
            var t = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == vm.Id && x.UserId == user.Id);
            if (t is not null) { t.UserId = vm.AssigneeId; await _db.SaveChangesAsync(); }
        }

        TempData["Success"] = "Task updated.";
        return RedirectToAction(nameof(Index), new { date = vm.PlannedDate });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleComplete(int id, DateOnly? date)
    {
        var user = await CurrentUserAsync();
        await _tasks.ToggleCompleteAsync(user.Id, id);
        return RedirectToAction(nameof(Index), new { date });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, DateOnly? date)
    {
        var user = await CurrentUserAsync();
        await _tasks.DeleteAsync(user.Id, id);
        TempData["Success"] = "Task deleted.";
        return RedirectToAction(nameof(Index), new { date });
    }

    private async Task PopulateWorkspaceAsync(TaskFormViewModel vm, int? workspaceId)
    {
        if (workspaceId is null) return;
        vm.WorkspaceId = workspaceId;
        vm.WorkspaceName = (await _workspaces.GetAsync(workspaceId.Value))?.Name;
        vm.Members = await _workspaces.GetMembersAsync(workspaceId.Value);
    }

    private async Task<List<Category>> UserCategoriesAsync(string userId) =>
        await _db.Categories.Where(c => c.UserId == userId).OrderBy(c => c.Name).ToListAsync();

    private async Task<List<Tag>> UserTagsAsync(string userId) =>
        await _db.Tags.Where(t => t.UserId == userId).OrderBy(t => t.Name).ToListAsync();

    private async Task<List<string>> UserTagNamesAsync(string userId) =>
        (await UserTagsAsync(userId)).Select(t => t.Name).ToList();

    /// <summary>Resolve a comma-separated tag string into Tag entities (creating new ones) and set them on the task.</summary>
    private async Task ApplyTagsAsync(string userId, int taskId, string? tagsCsv)
    {
        var names = (tagsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.Length > 40 ? n[..40] : n)
            .GroupBy(n => n.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        var task = await _db.Tasks
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
        if (task is null) return;

        task.Tags.Clear();
        if (names.Count == 0)
        {
            await _db.SaveChangesAsync();
            return;
        }

        var existing = await _db.Tags
            .Where(t => t.UserId == userId)
            .ToListAsync();

        foreach (var name in names)
        {
            var tag = existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                tag = new Tag { Name = name, UserId = userId };
                _db.Tags.Add(tag);
                existing.Add(tag);
            }
            task.Tags.Add(tag);
        }

        await _db.SaveChangesAsync();
    }
}
