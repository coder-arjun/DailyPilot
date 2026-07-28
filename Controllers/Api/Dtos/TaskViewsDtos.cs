namespace DailyPilot.Controllers.Api.Dtos;

/// <summary>One day's task counts within a calendar month (`GET api/v1/tasks/calendar`).</summary>
public sealed record CalendarDayDto(string Date, int Total, int Completed);

/// <summary>Wire shape for `GET api/v1/tasks/calendar` — day counts + the month's tasks in one payload.</summary>
public sealed record CalendarMonthDto(List<CalendarDayDto> Days, List<TaskDto> Tasks);

/// <summary>Wire shape for `GET api/v1/tasks/board` — today's tasks grouped by <see cref="DailyPilot.Models.DailyTaskStatus"/>.</summary>
public sealed record BoardDto(
    List<TaskDto> Pending,
    List<TaskDto> InProgress,
    List<TaskDto> Completed,
    List<TaskDto> CarriedForward);

/// <summary>Body for `POST api/v1/tasks/{id}/status`.</summary>
public sealed record TaskStatusUpdateRequest(int Status);
