using DailyPilot.Models;
using DailyPilot.Services;

namespace DailyPilot.Controllers.Api.Dtos;

/// <summary>
/// Notification/reminder settings — the exact fields `PUT account/settings` accepts. A
/// subset of <see cref="ApplicationUser"/> (mirrors <c>SettingsController</c>); excludes
/// web-only fields like Theme/DarkMode/EnableEmailNotifications.
/// </summary>
public sealed record AccountSettingsDto(
    bool EnableMorningReminder,
    string MorningReminderTime,
    bool EnableEndOfDayReminder,
    string EndOfDayReminderTime,
    bool EnableAutoCarryForward,
    bool SpeakReminders)
{
    public static AccountSettingsDto From(ApplicationUser user) => new(
        user.EnableMorningReminder,
        user.MorningReminderTime.ToString("HH:mm"),
        user.EnableEndOfDayReminder,
        user.EndOfDayReminderTime.ToString("HH:mm"),
        user.EnableAutoCarryForward,
        user.SpeakReminders);
}

/// <summary>Wire shape for `GET api/v1/account` — profile + gamification + reminder settings.</summary>
public sealed record AccountDto(
    string Id,
    string? UserName,
    string? DisplayName,
    string? Email,
    string TimeZoneId,
    int Xp,
    LevelInfoDto Level,
    int CurrentStreak,
    int LongestStreak,
    AccountSettingsDto Settings)
{
    public static AccountDto From(ApplicationUser user, LevelInfo level) => new(
        user.Id,
        user.UserName,
        user.DisplayName,
        user.Email,
        user.TimeZoneId,
        user.Xp,
        LevelInfoDto.From(level),
        user.CurrentStreak,
        user.LongestStreak,
        AccountSettingsDto.From(user));
}

/// <summary>
/// Partial update body for `PUT account/settings` — every field is optional; omitted
/// (null) fields are left unchanged on the user.
/// </summary>
public sealed record AccountSettingsRequest(
    string? DisplayName,
    string? TimeZoneId,
    bool? EnableMorningReminder,
    string? MorningReminderTime,
    bool? EnableEndOfDayReminder,
    string? EndOfDayReminderTime,
    bool? EnableAutoCarryForward,
    bool? SpeakReminders);
