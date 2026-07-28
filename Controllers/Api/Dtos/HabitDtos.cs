using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;
using DailyPilot.ViewModels;

namespace DailyPilot.Controllers.Api.Dtos;

/// <summary>Wire shape for habits in the mobile API — includes computed streak/today state.</summary>
public sealed record HabitDto(
    int Id,
    string Name,
    string? Description,
    string Color,
    int Frequency,
    int TargetPerWeek,
    int CurrentStreak,
    int LongestStreak,
    int ThisWeekCount,
    bool CheckedToday)
{
    public static HabitDto From(HabitStatus status) => new(
        status.Habit.Id,
        status.Habit.Name,
        status.Habit.Description,
        status.Habit.Color,
        (int)status.Habit.Frequency,
        status.Habit.TargetPerWeek,
        status.CurrentStreak,
        status.LongestStreak,
        status.ThisWeekCount,
        status.DoneToday);
}

public sealed record HabitWriteRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    int Frequency,
    int TargetPerWeek,
    string? Color);
