using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>
/// One night of manually journaled sleep, keyed by the WAKE-UP date (unique per
/// user+date). Evening fields are saved before bed; morning fields may arrive
/// later — an entry is "complete" (usable for metrics) once WakeTime and Quality
/// exist.
/// </summary>
public class SleepEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>The morning this night ended (user-local wake date).</summary>
    public DateOnly Date { get; set; }

    // Evening
    public TimeOnly BedTime { get; set; }
    public TimeOnly EstimatedSleepTime { get; set; }
    public bool? PhoneBeforeBed { get; set; }

    // Morning (nullable until logged)
    public TimeOnly? WakeTime { get; set; }
    public TimeOnly? TimeOutOfBed { get; set; }

    /// <summary>1–10 subjective rating; null until the morning entry.</summary>
    [Range(1, 10)]
    public int? Quality { get; set; }

    public bool? DreamRemembered { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsComplete => WakeTime is not null && Quality is not null;
}
