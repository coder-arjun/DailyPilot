using DailyPilot.Models;

namespace DailyPilot.ViewModels;

public class SearchViewModel
{
    public string Query { get; set; } = string.Empty;

    public List<TaskItem> Tasks { get; set; } = new();
    public int TaskPage { get; set; } = 1;
    public int TaskTotalPages { get; set; }

    public List<Habit> Habits { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<Workspace> Workspaces { get; set; } = new();

    public int TotalResults { get; set; }
}
