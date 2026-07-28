# DayPilot Android — Phase 1: API Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** JWT-authenticated REST API (`/api/v1`) over the existing ASP.NET services: auth with rotating refresh tokens, tasks, and categories — deployed to production.

**Architecture:** Thin `[ApiController]`s in `Controllers/Api/` call the existing `ITaskService` / `ApplicationDbContext`; a JWT bearer scheme ("Api") coexists with the untouched Identity cookie scheme. Refresh tokens are stored hashed in a new `dbo.ApiRefreshTokens` table (additive migration; shared-DB contract: `dbo` only).

**Tech Stack:** ASP.NET Core 10, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x, EF Core, xunit (existing `tests/DailyPilot.Tests`).

## Global Constraints

- Shared prod DB `db56456`: migrations additive, `dbo` schema only, never touch `finoma` schema (CLAUDE.md).
- Web app auth (Identity cookies) and all MVC pages must keep working unchanged.
- `Jwt:Secret` never committed — dev value via `dotnet user-secrets`, prod value added to the server's `appsettings.Production.json` by hand during deploy (never overwrite server appsettings wholesale — FTP memory).
- EF migrations: build BEFORE `dotnet ef migrations add`; never `--no-build` (memory). Migrations auto-run on prod at startup.
- Deploy: new NuGet package ⇒ upload its DLLs + `DailyPilot.deps.json`, not just `DailyPilot.dll` (FTP memory).
- API error bodies: `{ "error": "<message>" }`, status codes 400/401/404.

---

### Task 1: JwtTokenService

**Files:**
- Modify: `DailyPilot.csproj` (add JwtBearer package)
- Create: `Services/Api/JwtTokenService.cs`
- Test: `tests/DailyPilot.Tests/JwtTokenServiceTests.cs`

**Interfaces:**
- Produces: `IJwtTokenService.CreateAccessToken(ApplicationUser user) → (string Token, DateTime ExpiresAtUtc)`; claims: `sub` = user.Id, `name` = user.UserName, `dpn` = user.DisplayName. Reads config `Jwt:Secret`, `Jwt:Issuer` (default "DayPilot"), `Jwt:AccessTokenMinutes` (default 15).

- [ ] **Step 1:** `dotnet add DailyPilot.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.*`
- [ ] **Step 2:** Write failing tests:

```csharp
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
}
```

- [ ] **Step 3:** Run `dotnet test tests/DailyPilot.Tests/DailyPilot.Tests.csproj` — expect FAIL (JwtTokenService missing).
- [ ] **Step 4:** Implement:

```csharp
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
```

- [ ] **Step 5:** Tests pass; note: `JwtSecurityTokenHandler` maps `sub`→ClaimTypes-style types on VALIDATION, so Task 3's wiring sets `MapInboundClaims = false`.
- [ ] **Step 6:** Commit `feat(api): JwtTokenService`.

### Task 2: ApiRefreshToken entity + migration + RefreshTokenService

**Files:**
- Create: `Models/ApiRefreshToken.cs`, `Services/Api/RefreshTokenService.cs`
- Modify: `Data/ApplicationDbContext.cs` (add `DbSet<ApiRefreshToken> ApiRefreshTokens`)
- Test: `tests/DailyPilot.Tests/RefreshTokenServiceTests.cs`
- Migration: `AddApiRefreshTokens`

**Interfaces:**
- Produces: `RefreshTokenService.Hash(string raw) → string` (Base64 SHA-256, static);
  `IRefreshTokenService.IssueAsync(string userId) → Task<string>` (returns RAW token, stores hash, 60-day expiry);
  `IRefreshTokenService.RotateAsync(string rawToken) → Task<(ApplicationUser User, string NewRawToken)?>` (null when invalid/expired/revoked; revokes old, issues new);
  `IRefreshTokenService.RevokeAsync(string rawToken) → Task`.

- [ ] **Step 1:** Entity:

```csharp
namespace DailyPilot.Models;

/// <summary>Rotating refresh token for the mobile REST API (stored hashed).</summary>
public class ApiRefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;   // Base64(SHA256(raw))
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
}
```

- [ ] **Step 2:** Add DbSet + index in `OnModelCreating`: `builder.Entity<ApiRefreshToken>().HasIndex(t => t.TokenHash).IsUnique();`
- [ ] **Step 3:** Failing tests for `Hash` (deterministic, differs per input) and rotation logic. Rotation tests use EF InMemory (`dotnet add tests/DailyPilot.Tests package Microsoft.EntityFrameworkCore.InMemory --version 10.0.*`):

```csharp
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
}
```

- [ ] **Step 4:** Implement (raw token = 64 random bytes Base64Url; expiry 60 days; `RotateAsync` loads by hash incl. `User`, checks `RevokedAtUtc == null && ExpiresAtUtc > UtcNow`, sets RevokedAtUtc, issues new row):

```csharp
using System.Security.Cryptography;
using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services.Api;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(string userId);
    Task<(ApplicationUser User, string NewRawToken)?> RotateAsync(string rawToken);
    Task RevokeAsync(string rawToken);
}

public class RefreshTokenService : IRefreshTokenService
{
    private const int LifetimeDays = 60;
    private readonly ApplicationDbContext _db;
    public RefreshTokenService(ApplicationDbContext db) => _db = db;

    public static string Hash(string raw) =>
        Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

    public async Task<string> IssueAsync(string userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.ApiRefreshTokens.Add(new ApiRefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(LifetimeDays),
        });
        await _db.SaveChangesAsync();
        return raw;
    }

    public async Task<(ApplicationUser User, string NewRawToken)?> RotateAsync(string rawToken)
    {
        var row = await _db.ApiRefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken));
        if (row?.User is null || row.RevokedAtUtc is not null || row.ExpiresAtUtc <= DateTime.UtcNow)
            return null;
        row.RevokedAtUtc = DateTime.UtcNow;
        var newRaw = await IssueAsync(row.UserId);
        return (row.User, newRaw);
    }

    public async Task RevokeAsync(string rawToken)
    {
        var row = await _db.ApiRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken));
        if (row is not null && row.RevokedAtUtc is null)
        {
            row.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 5:** Tests green. Then migration: `dotnet build DailyPilot.csproj` FIRST, then `dotnet ef migrations add AddApiRefreshTokens --project DailyPilot.csproj`. Inspect the migration: must ONLY create `ApiRefreshTokens` (+index/FK) in default (dbo) schema — nothing else.
- [ ] **Step 6:** Commit `feat(api): rotating refresh tokens (hashed, dbo.ApiRefreshTokens)`.

### Task 3: Auth endpoints + JWT wiring + smoke script

**Files:**
- Create: `Controllers/Api/AuthApiController.cs`, `Controllers/Api/ApiControllerBase.cs`, `scripts/api-smoke.ps1`
- Modify: `Program.cs`

**Interfaces:**
- Produces: `ApiControllerBase` (`[ApiController]`, `[Authorize(AuthenticationSchemes = "Api")]`, `protected string CurrentUserId` from `User.FindFirstValue(JwtRegisteredClaimNames.Sub)`); auth response shape consumed by mobile phase 2: `{ accessToken, accessTokenExpiresAtUtc, refreshToken, user: { id, userName, displayName, email, xp } }`.

- [ ] **Step 1:** Program.cs — after existing Identity setup add (do NOT change default schemes; cookies stay default):

```csharp
builder.Services.AddAuthentication()
    .AddJwtBearer("Api", o =>
    {
        var secret = builder.Configuration["Jwt:Secret"];
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DayPilot",
            ValidAudience = builder.Configuration["Jwt:Issuer"] ?? "DayPilot",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(secret ?? "unconfigured-jwt-secret-placeholder-value")),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddScoped<DailyPilot.Services.Api.IJwtTokenService, DailyPilot.Services.Api.JwtTokenService>();
builder.Services.AddScoped<DailyPilot.Services.Api.IRefreshTokenService, DailyPilot.Services.Api.RefreshTokenService>();
```

  Dev secret: `dotnet user-secrets set "Jwt:Secret" "<64 random chars>"`.
- [ ] **Step 2:** `AuthApiController` (`[Route("api/v1/auth")]`, anonymous): `POST login` — find user by UserName OR Email (`_userManager.FindByNameAsync` then `FindByEmailAsync`), `CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)`, on success issue access+refresh and return the shape above; 401 `{error:"Invalid credentials."}` otherwise. `POST register` — validate DisplayName/UserName/Email/TimeZoneId/Password (mirror web rules: username regex `^[a-zA-Z0-9._@+\-]+$`, len 3–50), `new ApplicationUser { UserName, Email, DisplayName, TimeZoneId, CreatedAt = DateTime.UtcNow }`, `CreateAsync(user, password)`, then same token response; 400 with first Identity error. `POST refresh` — body `{refreshToken}` → `RotateAsync`; 401 when null. `POST logout` — `RevokeAsync`, 204.
- [ ] **Step 3:** `scripts/api-smoke.ps1`: boots nothing (assumes app running on :5245) — registers random user, logs in, refreshes (old refresh must then 401), calls `GET /api/v1/tasks?date=` with bearer (Task 4), prints PASS/FAIL per step.
- [ ] **Step 4:** Run app + script; all steps PASS (Task 4 step will fail until Task 4 — acceptable interim: script prints SKIP if endpoint 404s).
- [ ] **Step 5:** Commit `feat(api): JWT auth endpoints (login/register/refresh/logout)`.

### Task 4: Tasks + Categories API

**Files:**
- Create: `Controllers/Api/TasksApiController.cs`, `Controllers/Api/CategoriesApiController.cs`, `Controllers/Api/Dtos/TaskDtos.cs`

**Interfaces:**
- Consumes: `ITaskService` — `GetLocalTodayAsync(user)`, `GetTasksForDateAsync(userId, date, workspaceId=null)`, `GetAsync`, `CreateAsync(userId, TaskItem)`, `UpdateAsync(userId, TaskItem)`, `DeleteAsync`, `ToggleCompleteAsync`, `SetStatusAsync`.
- Produces (mobile contract): `TaskDto { id, title, notes, plannedDate ("yyyy-MM-dd"), dueTime ("HH:mm"|null), reminderTime, priority (int), status (int), categoryId, categoryName, categoryColor, carryForwardCount, estimatedMinutes, actualMinutes, isCompleted }`; `CategoryDto { id, name, color }`.

- [ ] **Step 1:** DTOs + mapper (`TaskDto.From(TaskItem)`); requests: `TaskWriteRequest { title (required, ≤200), notes, plannedDate, dueTime?, reminderTime?, priority, categoryId? }`.
- [ ] **Step 2:** `TasksApiController : ApiControllerBase`, `[Route("api/v1/tasks")]`:
  - `GET ?date=yyyy-MM-dd` (omitted ⇒ user's local today via `GetLocalTodayAsync(await _userManager.GetUserAsync(User))` — note `GetUserAsync` works because sub claim maps through `_userManager.GetUserId`; if null, load by `CurrentUserId`). Returns `List<TaskDto>`.
  - `GET {id}` → 404 when not found/foreign.
  - `POST` → maps request to `TaskItem { UserId = CurrentUserId, OriginalDate = plannedDate }`, `CreateAsync`, 201 with dto.
  - `PUT {id}` → `GetAsync` then apply request fields, `UpdateAsync`, 404 on false.
  - `DELETE {id}` → 404 on false, else 204.
  - `POST {id}/complete` and `POST {id}/reopen` → `ToggleCompleteAsync` (verify current state first via `GetAsync`; complete when pending, reopen when completed; 409 `{error}` when already in target state).
- [ ] **Step 3:** `CategoriesApiController`: `GET api/v1/categories` (user's categories from `ApplicationDbContext.Categories.Where(c => c.UserId == CurrentUserId)`), `POST` `{name, color}`.
- [ ] **Step 4:** Extend `scripts/api-smoke.ps1`: create task → list contains it → complete → dto.isCompleted true → delete → 404 on get. Run: all PASS.
- [ ] **Step 5:** Full `dotnet test` green; commit `feat(api): tasks + categories endpoints`.

### Task 5: Deploy Phase 1 to production

- [ ] **Step 1:** `dotnet publish -c Release -o publish`; generate a 64-char `Jwt:Secret`.
- [ ] **Step 2:** Download server `appsettings.Production.json` via FTP, ADD `"Jwt": { "Secret": "<generated>" }` locally, re-upload (never overwrite with local dev file).
- [ ] **Step 3:** app_offline.htm up → upload `DailyPilot.dll`, `DailyPilot.deps.json`, and NEW package DLLs (`Microsoft.AspNetCore.Authentication.JwtBearer.dll`, `Microsoft.IdentityModel.*.dll`, `System.IdentityModel.Tokens.Jwt.dll` — diff `publish/` against server listing) → remove app_offline.
- [ ] **Step 4:** Startup runs the `AddApiRefreshTokens` migration automatically; verify site 200, then run `scripts/api-smoke.ps1 -BaseUrl https://dailypilot.runasp.net` (register throwaway → full pass).
- [ ] **Step 5:** Commit any fixes; update `docs/superpowers/plans/…phase1…md` checkboxes; note deployed state in the plan footer.

---

**Self-review done:** spec's Phase-1 scope fully covered (auth+refresh+tasks+categories+deploy); no placeholders; interfaces consistent across tasks (RefreshTokenService names, dto shapes, "Api" scheme string).
