using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>How often a habit should be performed (PRD Phase 5: Habit Tracking).</summary>
public enum HabitFrequency
{
    Daily = 0,
    Weekly = 1
}

/// <summary>A recurring behaviour the user wants to build, tracked via daily check-ins.</summary>
public class Habit
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string Color { get; set; } = "#198754";

    public HabitFrequency Frequency { get; set; } = HabitFrequency.Daily;

    /// <summary>For Weekly habits: how many times per week is the goal (ignored for Daily).</summary>
    public int TargetPerWeek { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    // --- Interval reminders (e.g. "drink water every hour" between 09:00–21:00) ---
    public bool ReminderEnabled { get; set; }
    public int ReminderIntervalMinutes { get; set; } = 60;
    public TimeOnly ReminderStart { get; set; } = new(9, 0);
    public TimeOnly ReminderEnd { get; set; } = new(21, 0);
    /// <summary>UTC time of the last interval slot that was pushed (per-slot dedupe).</summary>
    public DateTime? LastReminderSentUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = string.Empty;

    public ICollection<HabitEntry> Entries { get; set; } = new List<HabitEntry>();
}

/// <summary>A single check-in marking a habit done on a given date.</summary>
public class HabitEntry
{
    public int Id { get; set; }

    public int HabitId { get; set; }
    public Habit? Habit { get; set; }

    /// <summary>Denormalised owner id (no FK relationship, to avoid a second cascade path).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
