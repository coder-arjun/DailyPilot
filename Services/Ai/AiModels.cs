using DailyPilot.Models;

namespace DailyPilot.Services.Ai;

/// <summary>Result of parsing a natural-language task description (PRD §6).</summary>
public class ParsedTaskResult
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateOnly PlannedDate { get; set; }
    public TimeOnly? DueTime { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public EnergyLevel EnergyLevel { get; set; } = EnergyLevel.Any;
    public int? EstimatedMinutes { get; set; }
    public RecurrencePattern Recurrence { get; set; } = RecurrencePattern.None;
    public string? CategoryName { get; set; }

    /// <summary>Human-readable explanation of what was understood.</summary>
    public string Summary { get; set; } = string.Empty;
}

public class PrioritizedTask
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Rank { get; set; }
    public Priority SuggestedPriority { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PrioritizationResult
{
    public List<PrioritizedTask> Tasks { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public enum ScheduleBlockKind { Task, Break, Focus }

public class ScheduleBlock
{
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public int? TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ScheduleBlockKind Kind { get; set; } = ScheduleBlockKind.Task;
}

public class ScheduleResult
{
    public List<ScheduleBlock> Blocks { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class InsightResult
{
    public string Headline { get; set; } = string.Empty;
    public List<string> Insights { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public class TaskRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Priority SuggestedPriority { get; set; } = Priority.Medium;
    public string? CategoryName { get; set; }
}

public class RecommendationResult
{
    public List<TaskRecommendation> Recommendations { get; set; } = new();
}

// ---------- AI Coach (PRD Phase 5) ----------

public class CoachResult
{
    public string Greeting { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty;
    public List<string> Tips { get; set; } = new();
    public List<string> HabitNudges { get; set; } = new();
}

public class CoachHabitInfo
{
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Daily";
    public bool DoneToday { get; set; }
    public bool TargetMet { get; set; }
    public int CurrentStreak { get; set; }
}

/// <summary>Everything the AI Coach reasons over for a personalised daily briefing.</summary>
public class CoachContext
{
    public DateOnly Today { get; set; }
    public string? DisplayName { get; set; }

    public int PlannedToday { get; set; }
    public int CompletedToday { get; set; }
    public int PendingToday { get; set; }
    public List<string> TopPendingTasks { get; set; } = new();

    public double WeeklyCompletionRate { get; set; }
    public double CarryForwardRate { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    public List<CoachHabitInfo> Habits { get; set; } = new();
}

/// <summary>Context the recommendation engine reasons over (built by the controller from the DB).</summary>
public class RecommendationContext
{
    /// <summary>Most frequently created task titles and how often they appear.</summary>
    public List<(string Title, int Count, string? Category)> FrequentTasks { get; set; } = new();

    /// <summary>Tasks currently overdue / repeatedly carried forward.</summary>
    public List<(string Title, int CarryForwardCount)> StickyTasks { get; set; } = new();

    /// <summary>Categories the user owns but hasn't used recently.</summary>
    public List<string> NeglectedCategories { get; set; } = new();

    public int CurrentStreak { get; set; }
    public DateOnly Today { get; set; }
}
