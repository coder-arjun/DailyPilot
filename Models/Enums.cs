namespace DailyPilot.Models;

/// <summary>Task priority levels (PRD §8 Additional Inputs).</summary>
public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>Optional energy level required for a task (PRD §8).</summary>
public enum EnergyLevel
{
    Any = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>Lifecycle state of a task.</summary>
public enum DailyTaskStatus
{
    Pending = 0,
    Completed = 1,
    CarriedForward = 2,
    InProgress = 3
}

/// <summary>Recurrence cadence for recurring tasks (PRD §5).</summary>
public enum RecurrencePattern
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Weekdays = 4
}

/// <summary>Type of reminder/notification (PRD §11).</summary>
public enum ReminderType
{
    Custom = 0,
    MorningBriefing = 1,
    EndOfDaySummary = 2
}

/// <summary>How a reminder should be delivered (PRD §11).</summary>
public enum ReminderChannel
{
    Browser = 0,
    Email = 1,
    Push = 2
}
