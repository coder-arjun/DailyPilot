using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;
using DailyPilot.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken] // bearer-token API — CSRF does not apply; the global MVC filter must not 400 JSON posts
[Route("api/v1/auth")]
public class AuthApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenService _refresh;

    public AuthApiController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwt,
        IRefreshTokenService refresh)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwt = jwt;
        _refresh = refresh;
    }

    public sealed record LoginRequest([Required] string Login, [Required] string Password);

    public sealed record RegisterRequest(
        [Required, StringLength(100)] string DisplayName,
        [Required, StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9._@+\-]+$")] string UserName,
        [Required, EmailAddress] string Email,
        [Required] string TimeZoneId,
        [Required, StringLength(100, MinimumLength = 6)] string Password);

    public sealed record RefreshRequest([Required] string RefreshToken);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Login)
                   ?? await _userManager.FindByEmailAsync(request.Login);
        if (user is null)
            return Unauthorized(new { error = "Invalid credentials." });

        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return Unauthorized(new { error = check.IsLockedOut ? "Account is locked — try again later." : "Invalid credentials." });

        return await TokenResponseAsync(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
            return BadRequest(new { error = "Unknown time zone." });

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = request.DisplayName,
            TimeZoneId = request.TimeZoneId,
            CreatedAt = DateTime.UtcNow,
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Errors.FirstOrDefault()?.Description ?? "Registration failed." });

        return await TokenResponseAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var rotated = await _refresh.RotateAsync(request.RefreshToken);
        if (rotated is null)
            return Unauthorized(new { error = "Refresh token is invalid or expired." });

        return TokenResponse(rotated.Value.User, rotated.Value.NewRawToken);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        await _refresh.RevokeAsync(request.RefreshToken);
        return NoContent();
    }

    private async Task<IActionResult> TokenResponseAsync(ApplicationUser user) =>
        TokenResponse(user, await _refresh.IssueAsync(user.Id));

    private IActionResult TokenResponse(ApplicationUser user, string refreshToken)
    {
        var (token, expires) = _jwt.CreateAccessToken(user);
        return Ok(new
        {
            accessToken = token,
            accessTokenExpiresAtUtc = expires,
            refreshToken,
            user = new
            {
                id = user.Id,
                userName = user.UserName,
                displayName = user.DisplayName,
                email = user.Email,
                xp = user.Xp,
            },
        });
    }
}
