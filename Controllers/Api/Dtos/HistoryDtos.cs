using DailyPilot.Models;

namespace DailyPilot.Controllers.Api.Dtos;

public sealed record TaskHistoryDto(
    int Id,
    int TaskItemId,
    string Action,
    string? Details,
    string TaskTitle,
    string OnDate,
    string Timestamp)
{
    public static TaskHistoryDto From(TaskHistory h) => new(
        h.Id,
        h.TaskItemId,
        h.Action,
        h.Details,
        h.TaskTitle,
        h.OnDate.ToString("yyyy-MM-dd"),
        h.Timestamp.ToString("O"));
}

/// <summary>Wire shape for `GET api/v1/history` — mirrors <c>HistoryController.Index</c> (pageSize 50).</summary>
public sealed record HistoryPageDto(List<TaskHistoryDto> Items, int Page, int TotalPages);
