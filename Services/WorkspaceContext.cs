using Microsoft.AspNetCore.Http;

namespace DailyPilot.Services;

/// <summary>
/// Resolves the user's currently-selected workspace from a cookie (PRD Phase 4).
/// Null means the personal context.
/// </summary>
public interface IWorkspaceContext
{
    const string CookieName = "dp_ws";

    /// <summary>The raw workspace id from the cookie, or null for personal context.</summary>
    int? CurrentWorkspaceId { get; }

    void SetCurrent(int? workspaceId);
}

public class WorkspaceContext : IWorkspaceContext
{
    private readonly IHttpContextAccessor _accessor;

    public WorkspaceContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? CurrentWorkspaceId
    {
        get
        {
            var raw = _accessor.HttpContext?.Request.Cookies[IWorkspaceContext.CookieName];
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public void SetCurrent(int? workspaceId)
    {
        var ctx = _accessor.HttpContext;
        if (ctx is null) return;

        if (workspaceId is null)
        {
            ctx.Response.Cookies.Delete(IWorkspaceContext.CookieName);
        }
        else
        {
            ctx.Response.Cookies.Append(IWorkspaceContext.CookieName, workspaceId.Value.ToString(),
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(180),
                    IsEssential = true
                });
        }
    }
}
