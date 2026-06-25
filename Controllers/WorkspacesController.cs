using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

/// <summary>PRD Phase 4 — Team Collaboration: workspaces, members, invitations, board.</summary>
[Authorize]
public class WorkspacesController : Controller
{
    private readonly IWorkspaceService _workspaces;
    private readonly IWorkspaceContext _wsContext;
    private readonly ITaskService _tasks;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkspacesController(IWorkspaceService workspaces, IWorkspaceContext wsContext,
        ITaskService tasks, ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _workspaces = workspaces;
        _wsContext = wsContext;
        _tasks = tasks;
        _db = db;
        _userManager = userManager;
    }

    private async Task<ApplicationUser> CurrentUserAsync() => (await _userManager.GetUserAsync(User))!;

    public async Task<IActionResult> Index()
    {
        var user = await CurrentUserAsync();
        var vm = new WorkspacesIndexViewModel
        {
            Workspaces = await _workspaces.GetUserWorkspacesAsync(user.Id),
            PendingInvitations = string.IsNullOrEmpty(user.Email)
                ? new()
                : await _workspaces.GetPendingInvitationsForEmailAsync(user.Email),
            CurrentWorkspaceId = _wsContext.CurrentWorkspaceId
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, string color)
    {
        var user = await CurrentUserAsync();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Please enter a workspace name.";
            return RedirectToAction(nameof(Index));
        }
        var ws = await _workspaces.CreateAsync(user.Id, name, description, color);
        _wsContext.SetCurrent(ws.Id);
        TempData["Success"] = $"Workspace “{ws.Name}” created.";
        return RedirectToAction(nameof(Details), new { id = ws.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await CurrentUserAsync();
        var membership = await _workspaces.GetMembershipAsync(id, user.Id);
        if (membership is null) return Forbid();

        var ws = await _workspaces.GetAsync(id);
        if (ws is null) return NotFound();

        var canManage = await _workspaces.CanManageMembersAsync(id, user.Id);
        var vm = new WorkspaceDetailsViewModel
        {
            Workspace = ws,
            Members = await _workspaces.GetMembersAsync(id),
            PendingInvitations = canManage ? await _workspaces.GetWorkspaceInvitationsAsync(id) : new(),
            MyRole = membership.Role,
            CanManage = canManage,
            IsOwner = ws.OwnerId == user.Id
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(int id, string email, WorkspaceRole role)
    {
        var user = await CurrentUserAsync();
        var (ok, error) = await _workspaces.InviteAsync(id, user.Id, email, role);
        TempData[ok ? "Success" : "Error"] = ok ? $"Invitation sent to {email}." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvitation(int id, int invitationId)
    {
        var user = await CurrentUserAsync();
        await _workspaces.RevokeInvitationAsync(invitationId, user.Id);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvitation(string token)
    {
        var user = await CurrentUserAsync();
        var ok = await _workspaces.AcceptInvitationAsync(token, user.Id, user.Email ?? "");
        TempData[ok ? "Success" : "Error"] = ok ? "Invitation accepted." : "Could not accept invitation.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, string targetUserId, WorkspaceRole role)
    {
        var user = await CurrentUserAsync();
        await _workspaces.ChangeRoleAsync(id, user.Id, targetUserId, role);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, string targetUserId)
    {
        var user = await CurrentUserAsync();
        await _workspaces.RemoveMemberAsync(id, user.Id, targetUserId);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id)
    {
        var user = await CurrentUserAsync();
        if (await _workspaces.LeaveAsync(id, user.Id))
        {
            if (_wsContext.CurrentWorkspaceId == id) _wsContext.SetCurrent(null);
            TempData["Success"] = "You left the workspace.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await CurrentUserAsync();
        if (await _workspaces.DeleteAsync(id, user.Id))
        {
            if (_wsContext.CurrentWorkspaceId == id) _wsContext.SetCurrent(null);
            TempData["Success"] = "Workspace deleted.";
        }
        else TempData["Error"] = "Only the owner can delete a workspace.";
        return RedirectToAction(nameof(Index));
    }

    // Workspace switcher — set the active context (null = personal).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(int? id, string? returnUrl)
    {
        var user = await CurrentUserAsync();
        if (id is null)
        {
            _wsContext.SetCurrent(null);
        }
        else if (await _workspaces.IsMemberAsync(id.Value, user.Id))
        {
            _wsContext.SetCurrent(id);
        }
        return LocalRedirect(returnUrl ?? Url.Action("Index", "Tasks")!);
    }

    // Team board — all members' tasks for a date.
    public async Task<IActionResult> Board(int id, DateOnly? date)
    {
        var user = await CurrentUserAsync();
        var membership = await _workspaces.GetMembershipAsync(id, user.Id);
        if (membership is null) return Forbid();

        var ws = await _workspaces.GetAsync(id);
        if (ws is null) return NotFound();

        var day = date ?? await _tasks.GetLocalTodayAsync(user);
        var members = await _workspaces.GetMembersAsync(id);

        var tasks = await _db.Tasks
            .Include(t => t.Category)
            .Where(t => t.WorkspaceId == id && t.PlannedDate == day)
            .ToListAsync();

        var vm = new WorkspaceBoardViewModel { Workspace = ws, Date = day };
        foreach (var m in members)
        {
            vm.Columns.Add(new BoardColumn
            {
                UserId = m.UserId,
                MemberName = m.User?.DisplayName ?? m.User?.UserName ?? m.User?.Email ?? "Member",
                Role = m.Role,
                Tasks = tasks.Where(t => t.UserId == m.UserId)
                    .OrderBy(t => t.Status == DailyTaskStatus.Completed)
                    .ThenByDescending(t => t.Priority)
                    .ToList()
            });
        }
        return View(vm);
    }
}
