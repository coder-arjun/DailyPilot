using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>Role of a member within a workspace (PRD Phase 4: Team Collaboration).</summary>
public enum WorkspaceRole
{
    Owner = 0,
    Admin = 1,
    Member = 2
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2
}

/// <summary>A shared team workspace that groups members and assignable tasks.</summary>
public class Workspace
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string Color { get; set; } = "#0d6efd";

    public string OwnerId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<WorkspaceInvitation> Invitations { get; set; } = new List<WorkspaceInvitation>();
}

/// <summary>Membership linking a user to a workspace with a role.</summary>
public class WorkspaceMember
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An email invitation to join a workspace, accepted by the recipient.</summary>
public class WorkspaceInvitation
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;

    [StringLength(64)]
    public string Token { get; set; } = string.Empty;

    /// <summary>Plain column (no FK) — who sent the invite.</summary>
    public string InvitedById { get; set; } = string.Empty;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
