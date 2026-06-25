using DailyPilot.Models;

namespace DailyPilot.ViewModels;

public class WorkspacesIndexViewModel
{
    public List<Workspace> Workspaces { get; set; } = new();
    public List<WorkspaceInvitation> PendingInvitations { get; set; } = new();
    public int? CurrentWorkspaceId { get; set; }
}

public class WorkspaceDetailsViewModel
{
    public Workspace Workspace { get; set; } = null!;
    public List<WorkspaceMember> Members { get; set; } = new();
    public List<WorkspaceInvitation> PendingInvitations { get; set; } = new();
    public WorkspaceRole MyRole { get; set; }
    public bool CanManage { get; set; }
    public bool IsOwner { get; set; }
}

public class WorkspaceBoardViewModel
{
    public Workspace Workspace { get; set; } = null!;
    public DateOnly Date { get; set; }
    public List<BoardColumn> Columns { get; set; } = new();
}

public class BoardColumn
{
    public string UserId { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
    public int Completed => Tasks.Count(t => t.Status == DailyTaskStatus.Completed);
}
