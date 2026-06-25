using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

public interface IWorkspaceService
{
    Task<List<Workspace>> GetUserWorkspacesAsync(string userId);
    Task<Workspace?> GetAsync(int id);
    Task<WorkspaceMember?> GetMembershipAsync(int workspaceId, string userId);
    Task<List<WorkspaceMember>> GetMembersAsync(int workspaceId);

    Task<Workspace> CreateAsync(string userId, string name, string? description, string color);
    Task<bool> UpdateAsync(int workspaceId, string actingUserId, string name, string? description, string color);
    Task<bool> DeleteAsync(int workspaceId, string actingUserId);

    Task<(bool ok, string? error)> InviteAsync(int workspaceId, string actingUserId, string email, WorkspaceRole role);
    Task<List<WorkspaceInvitation>> GetPendingInvitationsForEmailAsync(string email);
    Task<List<WorkspaceInvitation>> GetWorkspaceInvitationsAsync(int workspaceId);
    Task<bool> AcceptInvitationAsync(string token, string userId, string userEmail);
    Task<bool> RevokeInvitationAsync(int invitationId, string actingUserId);

    Task<bool> ChangeRoleAsync(int workspaceId, string actingUserId, string targetUserId, WorkspaceRole role);
    Task<bool> RemoveMemberAsync(int workspaceId, string actingUserId, string targetUserId);
    Task<bool> LeaveAsync(int workspaceId, string userId);

    Task<bool> IsMemberAsync(int workspaceId, string userId);
    Task<bool> CanManageMembersAsync(int workspaceId, string userId);
}

/// <summary>PRD Phase 4 — Team Collaboration. Workspaces, membership, roles and invitations.</summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IAppEmailSender _email;

    public WorkspaceService(ApplicationDbContext db, IDateTimeProvider clock, IAppEmailSender email)
    {
        _db = db;
        _clock = clock;
        _email = email;
    }

    public async Task<List<Workspace>> GetUserWorkspacesAsync(string userId)
    {
        var ids = await _db.WorkspaceMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.WorkspaceId)
            .ToListAsync();
        return await _db.Workspaces
            .Where(w => ids.Contains(w.Id))
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public Task<Workspace?> GetAsync(int id) =>
        _db.Workspaces.FirstOrDefaultAsync(w => w.Id == id);

    public Task<WorkspaceMember?> GetMembershipAsync(int workspaceId, string userId) =>
        _db.WorkspaceMembers.FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);

    public Task<List<WorkspaceMember>> GetMembersAsync(int workspaceId) =>
        _db.WorkspaceMembers
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync();

    public async Task<Workspace> CreateAsync(string userId, string name, string? description, string color)
    {
        var ws = new Workspace
        {
            Name = name.Trim(),
            Description = description,
            Color = string.IsNullOrWhiteSpace(color) ? "#0d6efd" : color,
            OwnerId = userId,
            CreatedAt = _clock.UtcNow
        };
        _db.Workspaces.Add(ws);
        await _db.SaveChangesAsync();

        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = ws.Id,
            UserId = userId,
            Role = WorkspaceRole.Owner,
            JoinedAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync();
        return ws;
    }

    public async Task<bool> UpdateAsync(int workspaceId, string actingUserId, string name, string? description, string color)
    {
        if (!await CanManageMembersAsync(workspaceId, actingUserId)) return false;
        var ws = await _db.Workspaces.FindAsync(workspaceId);
        if (ws is null) return false;
        ws.Name = name.Trim();
        ws.Description = description;
        ws.Color = string.IsNullOrWhiteSpace(color) ? ws.Color : color;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int workspaceId, string actingUserId)
    {
        var ws = await _db.Workspaces.FindAsync(workspaceId);
        if (ws is null || ws.OwnerId != actingUserId) return false; // owner only

        // Detach tasks (keep them as personal tasks for their assignees rather than deleting).
        var tasks = await _db.Tasks.Where(t => t.WorkspaceId == workspaceId).ToListAsync();
        foreach (var t in tasks) t.WorkspaceId = null;

        _db.Workspaces.Remove(ws);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool ok, string? error)> InviteAsync(int workspaceId, string actingUserId, string email, WorkspaceRole role)
    {
        if (!await CanManageMembersAsync(workspaceId, actingUserId))
            return (false, "You don't have permission to invite members.");
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required.");

        email = email.Trim().ToLowerInvariant();

        var alreadyMember = await _db.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.User!.Email!.ToLower() == email);
        if (alreadyMember) return (false, "That person is already a member.");

        var existingInvite = await _db.WorkspaceInvitations
            .AnyAsync(i => i.WorkspaceId == workspaceId && i.Email == email && i.Status == InvitationStatus.Pending);
        if (existingInvite) return (false, "An invitation is already pending for that email.");

        var invite = new WorkspaceInvitation
        {
            WorkspaceId = workspaceId,
            Email = email,
            Role = role,
            Token = Guid.NewGuid().ToString("N"),
            InvitedById = actingUserId,
            Status = InvitationStatus.Pending,
            CreatedAt = _clock.UtcNow
        };
        _db.WorkspaceInvitations.Add(invite);
        await _db.SaveChangesAsync();

        var ws = await _db.Workspaces.FindAsync(workspaceId);
        await _email.SendAsync(email, $"You're invited to join {ws?.Name} on DayPilot",
            $"<p>You've been invited to join the <strong>{ws?.Name}</strong> workspace on DayPilot.</p>" +
            "<p>Log in (or register with this email) and open <em>Workspaces</em> to accept the invitation.</p>");

        return (true, null);
    }

    public Task<List<WorkspaceInvitation>> GetPendingInvitationsForEmailAsync(string email) =>
        _db.WorkspaceInvitations
            .Include(i => i.Workspace)
            .Where(i => i.Email == email.ToLower() && i.Status == InvitationStatus.Pending)
            .ToListAsync();

    public Task<List<WorkspaceInvitation>> GetWorkspaceInvitationsAsync(int workspaceId) =>
        _db.WorkspaceInvitations
            .Where(i => i.WorkspaceId == workspaceId && i.Status == InvitationStatus.Pending)
            .ToListAsync();

    public async Task<bool> AcceptInvitationAsync(string token, string userId, string userEmail)
    {
        var invite = await _db.WorkspaceInvitations
            .FirstOrDefaultAsync(i => i.Token == token && i.Status == InvitationStatus.Pending);
        if (invite is null) return false;
        if (!string.Equals(invite.Email, userEmail, StringComparison.OrdinalIgnoreCase)) return false;

        var already = await _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == invite.WorkspaceId && m.UserId == userId);
        if (!already)
        {
            _db.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = invite.WorkspaceId,
                UserId = userId,
                Role = invite.Role,
                JoinedAt = _clock.UtcNow
            });
        }
        invite.Status = InvitationStatus.Accepted;
        invite.RespondedAt = _clock.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RevokeInvitationAsync(int invitationId, string actingUserId)
    {
        var invite = await _db.WorkspaceInvitations.FindAsync(invitationId);
        if (invite is null) return false;
        if (!await CanManageMembersAsync(invite.WorkspaceId, actingUserId)) return false;
        invite.Status = InvitationStatus.Revoked;
        invite.RespondedAt = _clock.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangeRoleAsync(int workspaceId, string actingUserId, string targetUserId, WorkspaceRole role)
    {
        if (!await CanManageMembersAsync(workspaceId, actingUserId)) return false;
        var ws = await _db.Workspaces.FindAsync(workspaceId);
        if (ws is null) return false;

        var target = await GetMembershipAsync(workspaceId, targetUserId);
        if (target is null) return false;
        if (target.UserId == ws.OwnerId) return false;     // can't change the owner's role
        if (role == WorkspaceRole.Owner) return false;      // ownership transfer is separate

        target.Role = role;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int workspaceId, string actingUserId, string targetUserId)
    {
        if (!await CanManageMembersAsync(workspaceId, actingUserId)) return false;
        var ws = await _db.Workspaces.FindAsync(workspaceId);
        if (ws is null || targetUserId == ws.OwnerId) return false; // can't remove the owner

        var member = await GetMembershipAsync(workspaceId, targetUserId);
        if (member is null) return false;
        _db.WorkspaceMembers.Remove(member);

        // Unassign that user's tasks in this workspace (make them personal to them).
        var tasks = await _db.Tasks.Where(t => t.WorkspaceId == workspaceId && t.UserId == targetUserId).ToListAsync();
        foreach (var t in tasks) t.WorkspaceId = null;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveAsync(int workspaceId, string userId)
    {
        var ws = await _db.Workspaces.FindAsync(workspaceId);
        if (ws is null || ws.OwnerId == userId) return false; // owner must transfer/delete instead
        var member = await GetMembershipAsync(workspaceId, userId);
        if (member is null) return false;
        _db.WorkspaceMembers.Remove(member);

        var tasks = await _db.Tasks.Where(t => t.WorkspaceId == workspaceId && t.UserId == userId).ToListAsync();
        foreach (var t in tasks) t.WorkspaceId = null;

        await _db.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsMemberAsync(int workspaceId, string userId) =>
        _db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);

    public async Task<bool> CanManageMembersAsync(int workspaceId, string userId)
    {
        var m = await GetMembershipAsync(workspaceId, userId);
        return m is not null && (m.Role == WorkspaceRole.Owner || m.Role == WorkspaceRole.Admin);
    }
}
