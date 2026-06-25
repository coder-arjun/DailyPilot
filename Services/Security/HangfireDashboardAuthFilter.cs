using Hangfire.Dashboard;

namespace DailyPilot.Services.Security;

/// <summary>
/// Restricts the Hangfire dashboard (/jobs) to authenticated users in the Admin role.
/// Without this, the dashboard is reachable by anyone who can hit the URL.
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public const string AdminRole = "Admin";

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true
               && http.User.IsInRole(AdminRole);
    }
}
