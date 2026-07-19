using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.Services.Ai;
using DailyPilot.Services.Security;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// --- EF Core (PRD §12: SQL Server + EF Core) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- Persist Data Protection keys IN THE DATABASE (FIXES frequent logouts) ---
// The key ring encrypts the auth cookie. Storing it on the filesystem (Storage/dpkeys)
// is fragile on MonsterASP — a redeploy / filesystem reset wipes it, invalidating every
// cookie and silently logging everyone out. Persisting to the DB (dbo.DataProtectionKeys)
// is durable across redeploys and restarts. SetApplicationName must stay "DayPilot"
// forever — changing it invalidates all existing cookies.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("DayPilot");

// --- ASP.NET Identity (PRD §12 Authentication) ---
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        // Hardened password policy (PRD §9 Secure Authentication).
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Brute-force lockout.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Secure the auth cookie. Require HTTPS for the cookie in production; keep it
// relaxed in development so http://localhost testing still works.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".DayPilot.Auth";
    // 30-day sliding lifetime (matches Finoma). An 8-hour window logged users out
    // after any overnight/inactive gap, which also caused missed daily reminders.
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    // Force EVERY sign-in to be persistent (30 days), even without "Remember me".
    // Otherwise a non-persistent login is a session cookie that the PWA drops when
    // it's closed — so tapping a reminder reopens the app logged-out and lands on the
    // login page instead of the task. A persistent cookie keeps the PWA signed in.
    options.Events ??= new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents();
    options.Events.OnSigningIn = context =>
    {
        context.Properties.IsPersistent = true;
        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
        return Task.CompletedTask;
    };
});

// Re-validate the security stamp every 30 min (not every request) so routine use
// doesn't churn the cookie; still catches password/security changes reasonably fast.
builder.Services.Configure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(30);
});

builder.Services.AddMemoryCache();

// --- Application services ---
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddScoped<IGamificationService, GamificationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ICarryForwardService, CarryForwardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IHabitService, HabitService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IWorkspaceContext, WorkspaceContext>();
builder.Services.AddScoped<IUserSeeder, UserSeeder>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<BackgroundJobs>();

// Route Planner: expands maps.app.goo.gl short links and place lookups server-side.
// UseCookies=false so our fixed Cookie header is sent as-is; SOCS/CONSENT skip the
// EU consent interstitial Google serves from EU datacenters (this host is EU-based).
builder.Services.AddHttpClient("maps-resolver", c =>
{
    c.Timeout = TimeSpan.FromSeconds(8);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DayPilot/1.0)");
    c.DefaultRequestHeaders.Add("Accept-Language", "en");
    c.DefaultRequestHeaders.Add("Cookie", "SOCS=CAI; CONSENT=YES+");
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    UseCookies = false,
    AllowAutoRedirect = true,
});

// --- Email (PRD §11) ---
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSender>();

// --- Daily backups (PRD §9) ---
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.AddScoped<IBackupService, SqlBackupService>();

// --- Web Push + reminder dispatch (PRD §11 closed-app reminders) ---
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection("Push"));
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IReminderDispatchService, ReminderDispatchService>();

// --- Phase 2 AI (PRD §6, §12: Azure OpenAI + Semantic Kernel) ---
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.AddSingleton<HeuristicAiTaskAssistant>();
var aiOptions = builder.Configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions();
if (aiOptions.IsConfigured)
{
    builder.Services.AddSingleton<IAiTaskAssistant, SemanticKernelAiTaskAssistant>();
}
else
{
    // No model configured → deterministic heuristic engine (still fully functional).
    builder.Services.AddSingleton<IAiTaskAssistant>(sp => sp.GetRequiredService<HeuristicAiTaskAssistant>());
}

// --- Hangfire background jobs (PRD §12) ---
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        PrepareSchemaIfNecessary = true
    }));
builder.Services.AddHangfireServer();

// Enforce anti-forgery on all unsafe (POST/PUT/DELETE) requests across MVC.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddRazorPages(); // Identity UI

var app = builder.Build();

// Drops every foreign key then every base table, but ONLY in DailyPilot's own
// schemas (dbo + HangFire).
//
// ⚠️ SHARED DATABASE — this prod DB (db56456 on MonsterASP) is shared with the
// Finoma app, which lives entirely in the `finoma` schema. An earlier unscoped
// version of this reset dropped EVERY table in the database and wiped Finoma's
// data. The `TABLE_SCHEMA IN ('dbo','HangFire')` filter below is what keeps this
// reset confined to DailyPilot. Never broaden it to all schemas, and keep
// `Database:ResetOnStartup` = false in production except for a deliberate reset.
const string DropAllObjectsSql = @"
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE [' + TABLE_SCHEMA + N'].[' + TABLE_NAME + N'] DROP CONSTRAINT [' + CONSTRAINT_NAME + N'];'
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_TYPE = 'FOREIGN KEY' AND TABLE_SCHEMA IN ('dbo','HangFire');
SELECT @sql += N'DROP TABLE [' + TABLE_SCHEMA + N'].[' + TABLE_NAME + N'];'
FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA IN ('dbo','HangFire');
IF @sql <> N'' EXEC sp_executesql @sql;";

// --- Apply migrations + seed roles on startup ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // One-time recovery switch: when Database:ResetOnStartup is true, drop everything
    // and migrate fresh. Used to clear a half-initialised DB from a crashed first deploy.
    // Turn it back off afterwards so normal restarts never wipe data.
    if (app.Configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup=true — dropping all tables before migrating.");
        db.Database.ExecuteSqlRaw(DropAllObjectsSql);
    }

    db.Database.Migrate();

    await RoleSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();

// Serve raw files from wwwroot (not the fingerprinted static-asset manifest) so that
// file-by-file FTP deploys of CSS/JS/icons take effect immediately.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        // The PWA manifest and service worker must never be cached, or the browser
        // keeps reading an old manifest that points at old icons — so a redesigned
        // app icon never shows even after reinstall. Always revalidate these two.
        if (name.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("sw.js", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Hangfire dashboard — restricted to authenticated Admin users.
app.UseHangfireDashboard("/jobs", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() }
});

// --- Register recurring jobs (FR-005, FR-006, FR-007, §9 backups) ---
RecurringJob.AddOrUpdate<BackgroundJobs>(
    "carry-forward",
    job => job.CarryForwardAsync(),
    "5 0 * * *"); // 00:05 daily

RecurringJob.AddOrUpdate<BackgroundJobs>(
    "morning-reminders",
    job => job.MorningRemindersAsync(),
    "0 8 * * *"); // 08:00 daily

RecurringJob.AddOrUpdate<BackgroundJobs>(
    "eod-reminders",
    job => job.EndOfDayRemindersAsync(),
    "0 20 * * *"); // 20:00 daily

RecurringJob.AddOrUpdate<BackgroundJobs>(
    "daily-backup",
    job => job.BackupDatabaseAsync(),
    "0 1 * * *"); // 01:00 daily

RecurringJob.AddOrUpdate<BackgroundJobs>(
    "reminder-dispatch",
    job => job.DispatchRemindersAsync(),
    "* * * * *"); // every minute — closed-app reminder delivery

RecurringJob.AddOrUpdate<BackgroundJobs>(
    "weekly-review",
    job => job.WeeklyReviewAsync(),
    "0 8 * * 1"); // Monday 08:00

app.Run();
