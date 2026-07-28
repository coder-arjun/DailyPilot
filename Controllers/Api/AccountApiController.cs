using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Models;
using DailyPilot.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

/// <summary>Mobile counterpart of <see cref="SettingsController"/> — profile + notification settings.</summary>
[Route("api/v1/account")]
public class AccountApiController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGamificationService _gamification;

    public AccountApiController(UserManager<ApplicationUser> userManager, IGamificationService gamification)
    {
        _userManager = userManager;
        _gamification = gamification;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user is null) return ApiError(401, "Unknown user.");
        return Ok(AccountDto.From(user, _gamification.Describe(user.Xp)));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(AccountSettingsRequest request)
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user is null) return ApiError(401, "Unknown user.");

        if (request.TimeZoneId is not null)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
                return ApiError(400, "Unknown time zone.");
            user.TimeZoneId = request.TimeZoneId;
        }

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName.Trim();

        if (request.EnableMorningReminder is { } morningOn)
            user.EnableMorningReminder = morningOn;

        if (request.MorningReminderTime is not null)
        {
            if (!TimeOnly.TryParseExact(request.MorningReminderTime, "HH:mm", out var morningTime))
                return ApiError(400, "morningReminderTime must be HH:mm.");
            user.MorningReminderTime = morningTime;
        }

        if (request.EnableEndOfDayReminder is { } eodOn)
            user.EnableEndOfDayReminder = eodOn;

        if (request.EndOfDayReminderTime is not null)
        {
            if (!TimeOnly.TryParseExact(request.EndOfDayReminderTime, "HH:mm", out var eodTime))
                return ApiError(400, "endOfDayReminderTime must be HH:mm.");
            user.EndOfDayReminderTime = eodTime;
        }

        if (request.EnableAutoCarryForward is { } carryOn)
            user.EnableAutoCarryForward = carryOn;

        if (request.SpeakReminders is { } speakOn)
            user.SpeakReminders = speakOn;

        await _userManager.UpdateAsync(user);
        return Ok(AccountDto.From(user, _gamification.Describe(user.Xp)));
    }
}
