using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DailyPilot.Models;
using Microsoft.IdentityModel.Tokens;

namespace DailyPilot.Services.Api;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user);
}

/// <summary>
/// Issues short-lived HS256 access tokens for the mobile REST API. The web app's
/// cookie auth is untouched — this scheme exists only for /api/v1.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user)
    {
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var minutes = _config.GetValue("Jwt:AccessTokenMinutes", 15);
        var issuer = _config["Jwt:Issuer"] ?? "DayPilot";
        var expires = DateTime.UtcNow.AddMinutes(minutes);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, issuer, new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName ?? ""),
            new Claim("dpn", user.DisplayName ?? ""),
        }, notBefore: null, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
