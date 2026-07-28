using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

/// <summary>
/// Base for the mobile REST API: JWT-authenticated ("Api" scheme), JSON errors as
/// {"error": "..."}. The web app's cookie-authenticated MVC controllers are unrelated.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "Api")]
[IgnoreAntiforgeryToken] // bearer-token API — CSRF does not apply; the global MVC filter must not 400 JSON posts
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>User id from the JWT `sub` claim (MapInboundClaims is off).</summary>
    protected string CurrentUserId =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("Authenticated API request without sub claim.");

    protected IActionResult ApiError(int statusCode, string message) =>
        StatusCode(statusCode, new { error = message });
}
