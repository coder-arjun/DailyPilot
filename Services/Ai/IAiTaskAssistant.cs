using DailyPilot.Models;
using DailyPilot.ViewModels;

namespace DailyPilot.Services.Ai;

/// <summary>
/// Phase 2 AI features (PRD §6). Backed either by Semantic Kernel + Azure OpenAI/OpenAI
/// or by a deterministic heuristic engine when no model is configured.
/// </summary>
public interface IAiTaskAssistant
{
    /// <summary>True when a real LLM backs the assistant (vs heuristic rules).</summary>
    bool IsLlmEnabled { get; }

    /// <summary>Natural Language Task Creation.</summary>
    Task<ParsedTaskResult> ParseTaskAsync(string input, IReadOnlyList<string> categories, DateOnly today);

    /// <summary>Break a task into concrete, actionable subtask/checklist steps.</summary>
    Task<BreakdownResult> BreakdownAsync(string title, string? notes, int? estimatedMinutes);

    /// <summary>AI Prioritization Engine.</summary>
    Task<PrioritizationResult> PrioritizeAsync(IReadOnlyList<TaskItem> tasks);

    /// <summary>Daily Schedule Generator.</summary>
    Task<ScheduleResult> GenerateScheduleAsync(IReadOnlyList<TaskItem> tasks, TimeOnly workStart, TimeOnly workEnd);

    /// <summary>Productivity Insights.</summary>
    Task<InsightResult> GenerateInsightsAsync(DashboardViewModel dashboard);

    /// <summary>Task Recommendation Engine.</summary>
    Task<RecommendationResult> RecommendAsync(RecommendationContext context);

    /// <summary>AI Coach — a personalised daily briefing over tasks, habits and metrics (Phase 5).</summary>
    Task<CoachResult> CoachAsync(CoachContext context);
}
