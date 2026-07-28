using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services.Api;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Tests;

public class RefreshTokenServiceTests
{
    private static ApplicationDbContext Db() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void Hash_IsDeterministicAndInputSensitive()
    {
        Assert.Equal(RefreshTokenService.Hash("abc"), RefreshTokenService.Hash("abc"));
        Assert.NotEqual(RefreshTokenService.Hash("abc"), RefreshTokenService.Hash("abd"));
    }

    [Fact]
    public async Task IssueThenRotate_ReturnsNewTokenAndRevokesOld()
    {
        await using var db = Db();
        db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1" });
        await db.SaveChangesAsync();
        var svc = new RefreshTokenService(db);

        var raw = await svc.IssueAsync("u1");
        var rotated = await svc.RotateAsync(raw);

        Assert.NotNull(rotated);
        Assert.Equal("u1", rotated!.Value.User.Id);
        Assert.NotEqual(raw, rotated.Value.NewRawToken);
        Assert.Null(await svc.RotateAsync(raw));           // old token now dead
        Assert.NotNull(await svc.RotateAsync(rotated.Value.NewRawToken));
    }

    [Fact]
    public async Task Rotate_UnknownToken_ReturnsNull()
    {
        await using var db = Db();
        Assert.Null(await new RefreshTokenService(db).RotateAsync("nope"));
    }

    [Fact]
    public async Task Rotate_ExpiredToken_ReturnsNull()
    {
        await using var db = Db();
        db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1" });
        db.ApiRefreshTokens.Add(new ApiRefreshToken
        {
            UserId = "u1",
            TokenHash = RefreshTokenService.Hash("expired-raw"),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        Assert.Null(await new RefreshTokenService(db).RotateAsync("expired-raw"));
    }

    [Fact]
    public async Task Revoke_MakesTokenUnusable()
    {
        await using var db = Db();
        db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1" });
        await db.SaveChangesAsync();
        var svc = new RefreshTokenService(db);

        var raw = await svc.IssueAsync("u1");
        await svc.RevokeAsync(raw);

        Assert.Null(await svc.RotateAsync(raw));
    }
}
