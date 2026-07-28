using DailyPilot.Models;

namespace DailyPilot.Controllers.Api.Dtos;

public sealed record SearchTaskDto(int Id, string Title, string PlannedDate, bool IsCompleted)
{
    public static SearchTaskDto From(TaskItem t) => new(
        t.Id, t.Title, t.PlannedDate.ToString("yyyy-MM-dd"), t.IsCompleted);
}

public sealed record SearchHabitDto(int Id, string Name)
{
    public static SearchHabitDto From(Habit h) => new(h.Id, h.Name);
}

public sealed record SearchCategoryDto(int Id, string Name, string Color)
{
    public static SearchCategoryDto From(Category c) => new(c.Id, c.Name, c.Color);
}

public sealed record SearchTagDto(int Id, string Name)
{
    public static SearchTagDto From(Tag t) => new(t.Id, t.Name);
}

public sealed record SearchWorkspaceDto(int Id, string Name)
{
    public static SearchWorkspaceDto From(Workspace w) => new(w.Id, w.Name);
}

/// <summary>Wire shape for `GET api/v1/search` — mirrors the entity kinds <c>SearchController</c> covers.</summary>
public sealed record SearchResultsDto(
    List<SearchTaskDto> Tasks,
    List<SearchHabitDto> Habits,
    List<SearchCategoryDto> Categories,
    List<SearchTagDto> Tags,
    List<SearchWorkspaceDto> Workspaces);
