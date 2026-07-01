using System.Text.RegularExpressions;
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

[Authorize]
public class TasksController : Controller
{
    private readonly ITaskService _tasks;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserSeeder _seeder;
    private readonly IWorkspaceContext _wsContext;
    private readonly IWorkspaceService _workspaces;
    private readonly IAiTaskAssistant _ai;
    private readonly IPushNotificationService _push;
    private readonly IAppEmailSender _email;
    private readonly ICarryForwardService _carryForward;

    public TasksController(ITaskService tasks, ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IUserSeeder seeder, IWorkspaceContext wsContext, IWorkspaceService workspaces,
        IAiTaskAssistant ai, IPushNotificationService push, IAppEmailSender email,
        ICarryForwardService carryForward)
    {
        _tasks = tasks;
        _db = db;
        _userManager = userManager;
        _seeder = seeder;
        _wsContext = wsContext;
        _workspaces = workspaces;
        _ai = ai;
        _push = push;
        _email = email;
        _carryForward = carryForward;
    }

    /// <summary>
    /// Runs carry-forward + recurring-task materialisation for the user whenever they
    /// view their tasks. On the free tier the app sleeps, so the nightly Hangfire job
    /// often never fires — without this, overdue tasks don't roll over and recurring
    /// tasks (daily/weekly/weekdays) never generate their next occurrence. The operation
    /// is idempotent and cheap (a couple of indexed queries), so running it per view is
    /// safe and also catches tasks back-dated later in the day.
    /// </summary>
    private async Task EnsureCarriedForwardAsync(string userId)
    {
        try { await _carryForward.RunForUserAsync(userId); } catch { /* never block the page */ }
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
        var todayDate = await _tasks.GetLocalTodayAsync(user);
        await EnsureCarriedForwardAsync(user.Id);
        var day = date ?? todayDate;
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
            Today = todayDate,
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
        ViewBag.Comments = await _db.TaskComments
            .Where(c => c.TaskItemId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        ViewBag.Checklist = task.ChecklistItems.OrderBy(c => c.SortOrder).ToList();
        ViewBag.ActualMinutes = task.ActualMinutes;
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

    // GET /Tasks/Board -> Kanban (To Do / In Progress / Done) for the day + context.
    public async Task<IActionResult> Board(DateOnly? date)
    {
        var user = await CurrentUserAsync();
        await _seeder.EnsureDefaultsAsync(user.Id);
        var todayDate = await _tasks.GetLocalTodayAsync(user);
        await EnsureCarriedForwardAsync(user.Id);
        var day = date ?? todayDate;
        var wsId = await ResolveWorkspaceIdAsync(user.Id);

        var tasks = await _tasks.GetTasksForDateAsync(user.Id, day, wsId);
        var vm = new KanbanViewModel
        {
            Date = day,
            Today = todayDate,
            WorkspaceId = wsId,
            WorkspaceName = wsId is null ? null : (await _workspaces.GetAsync(wsId.Value))?.Name,
            Todo = tasks.Where(t => t.Status is DailyTaskStatus.Pending or DailyTaskStatus.CarriedForward).ToList(),
            InProgress = tasks.Where(t => t.Status == DailyTaskStatus.InProgress).ToList(),
            Done = tasks.Where(t => t.Status == DailyTaskStatus.Completed).ToList()
        };
        return View(vm);
    }

    // GET /Tasks/Matrix -> Eisenhower matrix for the day + context.
    public async Task<IActionResult> Matrix(DateOnly? date)
    {
        var user = await CurrentUserAsync();
        await _seeder.EnsureDefaultsAsync(user.Id);
        var todayDate = await _tasks.GetLocalTodayAsync(user);
        await EnsureCarriedForwardAsync(user.Id);
        var day = date ?? todayDate;
        var wsId = await ResolveWorkspaceIdAsync(user.Id);

        var tasks = (await _tasks.GetTasksForDateAsync(user.Id, day, wsId))
            .Where(t => t.Status != DailyTaskStatus.Completed)
            .ToList();

        static bool Important(TaskItem t) => t.Priority is Priority.High or Priority.Medium;
        static bool Urgent(TaskItem t) => t.DueTime is not null || t.CarryForwardCount > 0;

        var vm = new MatrixViewModel
        {
            Date = day,
            Today = todayDate,
            WorkspaceId = wsId,
            WorkspaceName = wsId is null ? null : (await _workspaces.GetAsync(wsId.Value))?.Name,
            DoFirst = tasks.Where(t => Important(t) && Urgent(t)).ToList(),
            Schedule = tasks.Where(t => Important(t) && !Urgent(t)).ToList(),
            Delegate = tasks.Where(t => !Important(t) && Urgent(t)).ToList(),
            Eliminate = tasks.Where(t => !Important(t) && !Urgent(t)).ToList()
        };
        return View(vm);
    }

    // POST /Tasks/SetStatus -> Kanban drag-drop (AJAX) or fallback redirect.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, DailyTaskStatus status, DateOnly? date)
    {
        var user = await CurrentUserAsync();
        var ok = await _tasks.SetStatusAsync(user.Id, id, status);
        if (IsAjax()) return ok ? Json(new { ok = true }) : NotFound();
        return RedirectToAction(nameof(Board), new { date });
    }

    // POST /Tasks/LogTime -> add focus/Pomodoro minutes to a task (AJAX).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTime(int id, int minutes)
    {
        var user = await CurrentUserAsync();
        var total = await _tasks.LogTimeAsync(user.Id, id, minutes);
        if (total is null) return NotFound();
        return Json(new { ok = true, actualMinutes = total });
    }

    // POST /Tasks/AddChecklistItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChecklistItem(int taskId, string text)
    {
        var user = await CurrentUserAsync();
        await _tasks.AddChecklistItemAsync(user.Id, taskId, text);
        return RedirectToAction(nameof(Edit), new { id = taskId });
    }

    // POST /Tasks/ToggleChecklistItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleChecklistItem(int itemId, int taskId)
    {
        var user = await CurrentUserAsync();
        var ok = await _tasks.ToggleChecklistItemAsync(user.Id, itemId);
        if (IsAjax()) return ok ? Json(new { ok = true }) : NotFound();
        return RedirectToAction(nameof(Edit), new { id = taskId });
    }

    // POST /Tasks/DeleteChecklistItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChecklistItem(int itemId, int taskId)
    {
        var user = await CurrentUserAsync();
        await _tasks.DeleteChecklistItemAsync(user.Id, itemId);
        return RedirectToAction(nameof(Edit), new { id = taskId });
    }

    // POST /Tasks/Breakdown -> AI generates checklist steps for a task.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Breakdown(int id)
    {
        var user = await CurrentUserAsync();
        var task = await _tasks.GetAsync(user.Id, id);
        if (task is null) return NotFound();

        var result = await _ai.BreakdownAsync(task.Title, task.Notes, task.EstimatedMinutes);
        var added = await _tasks.AddChecklistItemsAsync(user.Id, id, result.Steps);
        TempData["Success"] = added > 0 ? $"✨ Added {added} subtasks." : "No subtasks were generated.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // POST /Tasks/AddComment -> post a comment; notify @mentioned workspace members.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int taskId, string body)
    {
        var user = await CurrentUserAsync();
        body = (body ?? string.Empty).Trim();
        if (body.Length == 0) return RedirectToAction(nameof(Edit), new { id = taskId });
        if (body.Length > 2000) body = body[..2000];

        // Only allow commenting on tasks the user can see (own or same workspace).
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId
            && (t.UserId == user.Id || (t.WorkspaceId != null)));
        if (task is null) return NotFound();
        if (task.WorkspaceId is not null && !await _workspaces.IsMemberAsync(task.WorkspaceId.Value, user.Id))
            return Forbid();

        var authorName = user.DisplayName ?? user.UserName ?? user.Email ?? "Someone";
        _db.TaskComments.Add(new TaskComment
        {
            TaskItemId = taskId,
            UserId = user.Id,
            AuthorName = authorName,
            Body = body
        });
        await _db.SaveChangesAsync();

        await NotifyMentionsAsync(task, body, authorName, user.Id);
        return RedirectToAction(nameof(Edit), new { id = taskId });
    }

    /// <summary>Parse @mentions and notify matched workspace members via push + email.</summary>
    private async Task NotifyMentionsAsync(TaskItem task, string body, string authorName, string authorId)
    {
        if (task.WorkspaceId is null) return;
        var handles = Regex.Matches(body, @"@([\w.\-]{2,40})")
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .ToHashSet();
        if (handles.Count == 0) return;

        var members = await _workspaces.GetMembersAsync(task.WorkspaceId.Value);
        foreach (var m in members)
        {
            if (m.UserId == authorId || m.User is null) continue;
            var uname = (m.User.UserName ?? "").ToLowerInvariant();
            var dname = (m.User.DisplayName ?? "").ToLowerInvariant().Replace(" ", "");
            var mailLocal = (m.User.Email ?? "").Split('@')[0].ToLowerInvariant();
            if (!handles.Any(h => h == uname || h == dname || h == mailLocal)) continue;

            await _push.SendToUserAsync(m.UserId, $"💬 {authorName} mentioned you",
                Trim(body, 120), $"/Tasks/Edit/{task.Id}", $"dp-comment-{task.Id}");

            if (!string.IsNullOrWhiteSpace(m.User.Email))
            {
                await _email.SendAsync(m.User.Email!,
                    $"{authorName} mentioned you on “{task.Title}”",
                    $@"<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto'>
                        <p><strong>{System.Net.WebUtility.HtmlEncode(authorName)}</strong> mentioned you on the task
                        “<strong>{System.Net.WebUtility.HtmlEncode(task.Title)}</strong>”:</p>
                        <blockquote style='border-left:3px solid #4f46e5;margin:0;padding:.25rem .75rem;color:#444'>
                        {System.Net.WebUtility.HtmlEncode(body)}</blockquote>
                        <p><a href='https://dailypilot.runasp.net/Tasks/Edit/{task.Id}'
                        style='display:inline-block;background:#4f46e5;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none'>Open task</a></p>
                    </div>",
                    m.User.DisplayName);
            }
        }
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private bool IsAjax() =>
        Request.Headers["X-Requested-With"] == "XMLHttpRequest";

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
