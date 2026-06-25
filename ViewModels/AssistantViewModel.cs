using DailyPilot.Services.Ai;

namespace DailyPilot.ViewModels;

/// <summary>Aggregates all Phase 2 AI outputs for the Assistant page.</summary>
public class AssistantViewModel
{
    public DateOnly Date { get; set; }
    public bool LlmEnabled { get; set; }

    public CoachResult Coach { get; set; } = new();
    public PrioritizationResult Prioritization { get; set; } = new();
    public ScheduleResult Schedule { get; set; } = new();
    public InsightResult Insights { get; set; } = new();
    public RecommendationResult Recommendations { get; set; } = new();

    public TimeOnly WorkStart { get; set; } = new(9, 0);
    public TimeOnly WorkEnd { get; set; } = new(17, 0);
}
