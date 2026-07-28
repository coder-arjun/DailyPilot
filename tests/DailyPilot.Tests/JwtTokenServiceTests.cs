using DailyPilot.Models;
using DailyPilot.Services.Api;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace DailyPilot.Tests;

public class JwtTokenServiceTests
{
    private static JwtTokenService Make(int minutes = 15) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = new string('k', 64),
            ["Jwt:AccessTokenMinutes"] = minutes.ToString(),
        }).Build());

    private static readonly ApplicationUser User = new()
    { Id = "u1", UserName = "arjun", DisplayName = "Arjun" };

    [Fact]
    public void CreateAccessToken_ContainsSubNameAndDisplayClaims()
    {
        var (token, _) = Make().CreateAccessToken(User);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("u1", jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("arjun", jwt.Claims.First(c => c.Type == "name").Value);
        Assert.Equal("Arjun", jwt.Claims.First(c => c.Type == "dpn").Value);
        Assert.Equal("DayPilot", jwt.Issuer);
    }

    [Fact]
    public void CreateAccessToken_ExpiryMatchesConfig()
    {
        var (_, expires) = Make(minutes: 30).CreateAccessToken(User);
        Assert.InRange(expires, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void CreateAccessToken_MissingSecret_Throws()
    {
        var svc = new JwtTokenService(new ConfigurationBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => svc.CreateAccessToken(User));
    }
}
