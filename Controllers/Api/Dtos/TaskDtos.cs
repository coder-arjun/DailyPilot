using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;

namespace DailyPilot.Controllers.Api.Dtos;

/// <summary>Wire shape for tasks in the mobile API (dates/times as ISO strings).</summary>
public sealed record TaskDto(
    int Id,
    string Title,
    string? Notes,
    string PlannedDate,
    string? DueTime,
    string? ReminderTime,
    int Priority,
    int Status,
    int? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    int CarryForwardCount,
    int? EstimatedMinutes,
    int? ActualMinutes,
    bool IsCompleted)
{
    public static TaskDto From(TaskItem t) => new(
        t.Id,
        t.Title,
        t.Notes,
        t.PlannedDate.ToString("yyyy-MM-dd"),
        t.DueTime?.ToString("HH:mm"),
        t.ReminderTime?.ToString("HH:mm"),
        (int)t.Priority,
        (int)t.Status,
        t.CategoryId,
        t.Category?.Name,
        t.Category?.Color,
        t.CarryForwardCount,
        t.EstimatedMinutes,
        t.ActualMinutes,
        t.IsCompleted);
}

public sealed record TaskWriteRequest(
    [Required, StringLength(200)] string Title,
    [StringLength(2000)] string? Notes,
    [Required] string PlannedDate,
    string? DueTime,
    string? ReminderTime,
    int Priority,
    int? CategoryId,
    int? EstimatedMinutes);

public sealed record CategoryDto(int Id, string Name, string Color)
{
    public static CategoryDto From(Category c) => new(c.Id, c.Name, c.Color);
}

public sealed record CategoryWriteRequest(
    [Required, StringLength(60)] string Name,
    [StringLength(7)] string? Color);
