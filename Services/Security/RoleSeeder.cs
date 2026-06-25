using DailyPilot.Models;
using Microsoft.AspNetCore.Identity;

namespace DailyPilot.Services.Security;

/// <summary>
/// Ensures the Admin role exists and grants it to the email addresses listed in
/// configuration ("Admin:Emails"). Runs once at startup.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(HangfireDashboardAuthFilter.AdminRole))
            await roleManager.CreateAsync(new IdentityRole(HangfireDashboardAuthFilter.AdminRole));

        var adminEmails = config.GetSection("Admin:Emails").Get<string[]>() ?? Array.Empty<string>();
        foreach (var email in adminEmails.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            var user = await userManager.FindByEmailAsync(email.Trim());
            if (user is null)
            {
                logger.LogInformation("Admin email {Email} not registered yet; will be granted on next startup after sign-up.", email);
                continue;
            }
            if (!await userManager.IsInRoleAsync(user, HangfireDashboardAuthFilter.AdminRole))
            {
                await userManager.AddToRoleAsync(user, HangfireDashboardAuthFilter.AdminRole);
                logger.LogInformation("Granted Admin role to {Email}.", email);
            }
        }
    }
}
