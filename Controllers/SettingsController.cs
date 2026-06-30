using DailyPilot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DailyPilot.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SettingsController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<IActionResult> Index()
    {
        var user = (await _userManager.GetUserAsync(User))!;
        PopulateTimeZones(user.TimeZoneId);
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ApplicationUser form)
    {
        var user = (await _userManager.GetUserAsync(User))!;

        user.DisplayName = form.DisplayName;
        user.TimeZoneId = form.TimeZoneId;
        user.EnableMorningReminder = form.EnableMorningReminder;
        user.EnableEndOfDayReminder = form.EnableEndOfDayReminder;
        user.EnableEmailNotifications = form.EnableEmailNotifications;
        user.MorningReminderTime = form.MorningReminderTime;
        user.EndOfDayReminderTime = form.EndOfDayReminderTime;
        user.EnableAutoCarryForward = form.EnableAutoCarryForward;
        user.DarkMode = form.DarkMode;
        user.SpeakReminders = form.SpeakReminders;

        await _userManager.UpdateAsync(user);
        TempData["Success"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    // Persist the chosen theme to the user's profile (called by the navbar theme picker).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTheme(string theme)
    {
        var allowed = new[] { "obsidian", "nordic", "emerald", "ultraviolet", "titanium" };
        if (!allowed.Contains(theme)) return BadRequest();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        user.Theme = theme;
        await _userManager.UpdateAsync(user);
        return Ok();
    }

    private void PopulateTimeZones(string selected)
    {
        ViewBag.TimeZones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new SelectListItem(tz.DisplayName, tz.Id, tz.Id == selected))
            .ToList();
    }
}
