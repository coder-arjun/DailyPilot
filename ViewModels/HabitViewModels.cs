using DailyPilot.Models;

namespace DailyPilot.ViewModels;

/// <summary>A habit plus its computed today/streak/progress state for the Habits page.</summary>
public class HabitStatus
{
    public Habit Habit { get; set; } = null!;
    public bool DoneToday { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int ThisWeekCount { get; set; }

    public string StreakUnit => Habit.Frequency == HabitFrequency.Weekly ? "week" : "day";

    public bool TargetMet => Habit.Frequency == HabitFrequency.Weekly
        ? ThisWeekCount >= Habit.TargetPerWeek
        : DoneToday;

    public double WeekProgress => Habit.Frequency == HabitFrequency.Weekly && Habit.TargetPerWeek > 0
        ? Math.Min(100, Math.Round(ThisWeekCount * 100.0 / Habit.TargetPerWeek, 0))
        : (DoneToday ? 100 : 0);
}

public class HabitsPageViewModel
{
    public DateOnly Date { get; set; }
    public List<HabitStatus> Habits { get; set; } = new();
}
