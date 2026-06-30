using Microsoft.AspNetCore.Identity;

namespace DailyPilot.Models;

/// <summary>
/// Application user (PRD §13 "User"). Extends ASP.NET Identity with
/// productivity-specific profile and settings (FR-010).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    /// <summary>IANA/Windows timezone id used to compute the user's "today".</summary>
    public string TimeZoneId { get; set; } = "UTC";

    // --- Notification settings (PRD §11) ---
    public bool EnableMorningReminder { get; set; } = true;
    public bool EnableEndOfDayReminder { get; set; } = true;
    public bool EnableEmailNotifications { get; set; } = false;

    /// <summary>Time of day for the morning briefing.</summary>
    public TimeOnly MorningReminderTime { get; set; } = new(8, 0);

    /// <summary>Time of day for the end-of-day summary.</summary>
    public TimeOnly EndOfDayReminderTime { get; set; } = new(20, 0);

    /// <summary>Auto-migrate unfinished tasks to the next day (FR-005).</summary>
    public bool EnableAutoCarryForward { get; set; } = true;

    public bool DarkMode { get; set; } = false;

    /// <summary>Selected UI theme key (e.g. obsidian, nordic, emerald, ultraviolet, titanium).</summary>
    public string Theme { get; set; } = "obsidian";

    /// <summary>Read reminders aloud (text-to-speech) when the app is open.</summary>
    public bool SpeakReminders { get; set; } = false;

    // --- Streak tracking (PRD §5 / §10) ---
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastCompletionDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
