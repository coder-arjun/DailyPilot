using System.Text.Json;
using System.Text.Json.Serialization;
using DailyPilot.Models;
using DailyPilot.ViewModels;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Options;

namespace DailyPilot.Services.Ai;

/// <summary>
/// LLM-backed assistant using Semantic Kernel over Azure OpenAI / OpenAI (PRD §12).
/// Every method degrades gracefully to the heuristic engine if the model call or
/// JSON parsing fails, so the feature never hard-breaks.
/// </summary>
public class SemanticKernelAiTaskAssistant : IAiTaskAssistant
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;
    private readonly HeuristicAiTaskAssistant _fallback;
    private readonly ILogger<SemanticKernelAiTaskAssistant> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsLlmEnabled => true;

    public SemanticKernelAiTaskAssistant(
        IOptions<AiOptions> options,
        HeuristicAiTaskAssistant fallback,
        ILogger<SemanticKernelAiTaskAssistant> logger)
    {
        var o = options.Value;
        _fallback = fallback;
        _logger = logger;

        var builder = Kernel.CreateBuilder();
        if (o.Provider == AiProvider.AzureOpenAI)
            builder.AddAzureOpenAIChatCompletion(o.Model!, o.Endpoint!, o.ApiKey!);
        else
            builder.AddOpenAIChatCompletion(o.Model!, o.ApiKey!);

        _kernel = builder.Build();
        _chat = _kernel.GetRequiredService<IChatCompletionService>();
    }

    private async Task<string> CompleteJsonAsync(string system, string user)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(system + " Respond ONLY with valid minified JSON, no prose or code fences.");
        history.AddUserMessage(user);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.2,
            MaxTokens = 1200,
            ResponseFormat = "json_object"
        };

        var response = await _chat.GetChatMessageContentAsync(history, settings, _kernel);
        return response.Content ?? "{}";
    }

    // ---------- Natural Language Task Creation ----------

    public async Task<ParsedTaskResult> ParseTaskAsync(string input, IReadOnlyList<string> categories, DateOnly today)
    {
        try
        {
            var system =
                $"You convert a natural-language to-do into structured fields. Today is {today:yyyy-MM-dd} ({today:dddd}). " +
                $"Known categories: {(categories.Count == 0 ? "none" : string.Join(", ", categories))}. " +
                "Return JSON with keys: title (string), notes (string|null), date (yyyy-MM-dd), time (HH:mm|null), " +
                "priority (Low|Medium|High), energy (Any|Low|Medium|High), estimatedMinutes (int|null), " +
                "recurrence (None|Daily|Weekly|Monthly|Weekdays), category (one of the known categories or null), summary (short confirmation).";

            var json = await CompleteJsonAsync(system, input);
            var dto = JsonSerializer.Deserialize<ParsedTaskDto>(json, JsonOpts) ?? throw new JsonException("null");

            return new ParsedTaskResult
            {
                Title = string.IsNullOrWhiteSpace(dto.Title) ? input : dto.Title!,
                Notes = dto.Notes,
                PlannedDate = ParseDateOnly(dto.Date, today),
                DueTime = ParseTimeOnly(dto.Time),
                Priority = ParseEnum(dto.Priority, Priority.Medium),
                EnergyLevel = ParseEnum(dto.Energy, EnergyLevel.Any),
                EstimatedMinutes = dto.EstimatedMinutes,
                Recurrence = ParseEnum(dto.Recurrence, RecurrencePattern.None),
                CategoryName = categories.FirstOrDefault(c => string.Equals(c, dto.Category, StringComparison.OrdinalIgnoreCase)),
                Summary = string.IsNullOrWhiteSpace(dto.Summary) ? "Task understood." : dto.Summary!
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM ParseTask failed; using heuristic fallback.");
            return await _fallback.ParseTaskAsync(input, categories, today);
        }
    }

    // ---------- AI Prioritization Engine ----------

    public async Task<PrioritizationResult> PrioritizeAsync(IReadOnlyList<TaskItem> tasks)
    {
        var pending = tasks.Where(t => t.Status != DailyTaskStatus.Completed).ToList();
        if (pending.Count == 0) return await _fallback.PrioritizeAsync(tasks);

        try
        {
            var taskList = pending.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                priority = t.Priority.ToString(),
                dueTime = t.DueTime?.ToString("HH:mm"),
                carriedForward = t.CarryForwardCount,
                estimatedMinutes = t.EstimatedMinutes
            });

            var system = "You are a productivity coach. Order the user's tasks from most to least important for today, " +
                         "weighing priority, deadlines, and how often a task has been carried forward. " +
                         "Return JSON: {summary:string, tasks:[{taskId:int, rank:int, suggestedPriority:Low|Medium|High, reason:string}]}.";
            var json = await CompleteJsonAsync(system, JsonSerializer.Serialize(new { tasks = taskList }));
            var dto = JsonSerializer.Deserialize<PrioritizationDto>(json, JsonOpts) ?? throw new JsonException("null");

            var result = new PrioritizationResult { Summary = dto.Summary ?? "" };
            foreach (var t in dto.Tasks ?? new())
            {
                var src = pending.FirstOrDefault(p => p.Id == t.TaskId);
                if (src is null) continue;
                result.Tasks.Add(new PrioritizedTask
                {
                    TaskId = t.TaskId,
                    Title = src.Title,
                    Rank = t.Rank,
                    SuggestedPriority = ParseEnum(t.SuggestedPriority, src.Priority),
                    Reason = t.Reason ?? ""
                });
            }
            if (result.Tasks.Count == 0) throw new JsonException("no tasks mapped");
            result.Tasks = result.Tasks.OrderBy(t => t.Rank).ToList();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM Prioritize failed; using heuristic fallback.");
            return await _fallback.PrioritizeAsync(tasks);
        }
    }

    // ---------- Daily Schedule Generator ----------

    public async Task<ScheduleResult> GenerateScheduleAsync(IReadOnlyList<TaskItem> tasks, TimeOnly workStart, TimeOnly workEnd)
    {
        var pending = tasks.Where(t => t.Status != DailyTaskStatus.Completed).ToList();
        if (pending.Count == 0) return await _fallback.GenerateScheduleAsync(tasks, workStart, workEnd);

        try
        {
            var taskList = pending.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                priority = t.Priority.ToString(),
                dueTime = t.DueTime?.ToString("HH:mm"),
                estimatedMinutes = t.EstimatedMinutes ?? 30
            });

            var system = $"Build a realistic time-blocked schedule between {workStart:HH:mm} and {workEnd:HH:mm}. " +
                         "Respect due times, put high-priority/high-effort work earlier, and insert short breaks. " +
                         "Return JSON: {summary:string, blocks:[{start:HH:mm, end:HH:mm, taskId:int|null, title:string, kind:Task|Break|Focus}]}.";
            var json = await CompleteJsonAsync(system, JsonSerializer.Serialize(new { tasks = taskList }));
            var dto = JsonSerializer.Deserialize<ScheduleDto>(json, JsonOpts) ?? throw new JsonException("null");

            var result = new ScheduleResult { Summary = dto.Summary ?? "" };
            foreach (var b in dto.Blocks ?? new())
            {
                var start = ParseTimeOnly(b.Start);
                var end = ParseTimeOnly(b.End);
                if (start is null || end is null) continue;
                result.Blocks.Add(new ScheduleBlock
                {
                    Start = start.Value,
                    End = end.Value,
                    TaskId = b.TaskId,
                    Title = b.Title ?? "Block",
                    Kind = ParseEnum(b.Kind, ScheduleBlockKind.Task)
                });
            }
            if (result.Blocks.Count == 0) throw new JsonException("no blocks");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM Schedule failed; using heuristic fallback.");
            return await _fallback.GenerateScheduleAsync(tasks, workStart, workEnd);
        }
    }

    // ---------- Productivity Insights ----------

    public async Task<InsightResult> GenerateInsightsAsync(DashboardViewModel d)
    {
        try
        {
            var metrics = new
            {
                completedToday = d.CompletedToday,
                plannedToday = d.PlannedToday,
                weeklyCompletionRate = d.WeeklyCompletionRate,
                carryForwardRate = d.CarryForwardRate,
                currentStreak = d.CurrentStreak,
                longestStreak = d.LongestStreak,
                categories = d.CategoryPerformance.Select(c => new { c.Category, c.Total, c.Completed, c.Rate })
            };

            var system = "You are an encouraging productivity analyst. Given the user's metrics, produce a short headline, " +
                         "3-5 concrete insights, and 2-4 actionable suggestions. " +
                         "Return JSON: {headline:string, insights:[string], suggestions:[string]}.";
            var json = await CompleteJsonAsync(system, JsonSerializer.Serialize(metrics));
            var dto = JsonSerializer.Deserialize<InsightDto>(json, JsonOpts) ?? throw new JsonException("null");

            if (string.IsNullOrWhiteSpace(dto.Headline) && (dto.Insights?.Count ?? 0) == 0)
                throw new JsonException("empty");

            return new InsightResult
            {
                Headline = dto.Headline ?? "",
                Insights = dto.Insights ?? new(),
                Suggestions = dto.Suggestions ?? new()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM Insights failed; using heuristic fallback.");
            return await _fallback.GenerateInsightsAsync(d);
        }
    }

    // ---------- Task Recommendation Engine ----------

    public async Task<RecommendationResult> RecommendAsync(RecommendationContext ctx)
    {
        try
        {
            var payload = new
            {
                today = ctx.Today.ToString("yyyy-MM-dd"),
                currentStreak = ctx.CurrentStreak,
                frequentTasks = ctx.FrequentTasks.Select(f => new { f.Title, f.Count, f.Category }),
                stickyTasks = ctx.StickyTasks.Select(s => new { s.Title, s.CarryForwardCount }),
                neglectedCategories = ctx.NeglectedCategories
            };

            var system = "Recommend up to 5 tasks the user should consider adding or tackling today, based on their habits, " +
                         "tasks that keep slipping, and neglected categories. " +
                         "Return JSON: {recommendations:[{title:string, reason:string, suggestedPriority:Low|Medium|High, category:string|null}]}.";
            var json = await CompleteJsonAsync(system, JsonSerializer.Serialize(payload));
            var dto = JsonSerializer.Deserialize<RecommendationDto>(json, JsonOpts) ?? throw new JsonException("null");

            var result = new RecommendationResult();
            foreach (var r in dto.Recommendations ?? new())
            {
                if (string.IsNullOrWhiteSpace(r.Title)) continue;
                result.Recommendations.Add(new TaskRecommendation
                {
                    Title = r.Title!,
                    Reason = r.Reason ?? "",
                    SuggestedPriority = ParseEnum(r.SuggestedPriority, Priority.Medium),
                    CategoryName = r.Category
                });
            }
            if (result.Recommendations.Count == 0) throw new JsonException("empty");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM Recommend failed; using heuristic fallback.");
            return await _fallback.RecommendAsync(ctx);
        }
    }

    // ---------- AI Coach ----------

    public async Task<CoachResult> CoachAsync(CoachContext ctx)
    {
        try
        {
            var payload = new
            {
                today = ctx.Today.ToString("yyyy-MM-dd"),
                name = ctx.DisplayName,
                plannedToday = ctx.PlannedToday,
                completedToday = ctx.CompletedToday,
                pendingToday = ctx.PendingToday,
                topPendingTasks = ctx.TopPendingTasks,
                weeklyCompletionRate = ctx.WeeklyCompletionRate,
                carryForwardRate = ctx.CarryForwardRate,
                currentStreak = ctx.CurrentStreak,
                longestStreak = ctx.LongestStreak,
                habits = ctx.Habits.Select(h => new { h.Name, h.Frequency, h.DoneToday, h.TargetMet, h.CurrentStreak })
            };

            var system =
                "You are a warm, concise productivity & habit coach. Using the user's data, write a short personalised briefing. " +
                "Be encouraging and specific; reference real task and habit names. " +
                "Return JSON: {greeting:string, focus:string, motivation:string, tips:[string], habitNudges:[string]}.";

            var json = await CompleteJsonAsync(system, JsonSerializer.Serialize(payload));
            var dto = JsonSerializer.Deserialize<CoachDto>(json, JsonOpts) ?? throw new JsonException("null");

            if (string.IsNullOrWhiteSpace(dto.Greeting) && string.IsNullOrWhiteSpace(dto.Focus))
                throw new JsonException("empty");

            return new CoachResult
            {
                Greeting = dto.Greeting ?? "",
                Focus = dto.Focus ?? "",
                Motivation = dto.Motivation ?? "",
                Tips = dto.Tips ?? new(),
                HabitNudges = dto.HabitNudges ?? new()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM Coach failed; using heuristic fallback.");
            return await _fallback.CoachAsync(ctx);
        }
    }

    // ---------- parsing helpers ----------

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static DateOnly ParseDateOnly(string? value, DateOnly fallback) =>
        DateOnly.TryParse(value, out var d) ? d : fallback;

    private static TimeOnly? ParseTimeOnly(string? value) =>
        TimeOnly.TryParse(value, out var t) ? t : null;

    // ---------- JSON DTOs ----------

    private class ParsedTaskDto
    {
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public string? Priority { get; set; }
        public string? Energy { get; set; }
        public int? EstimatedMinutes { get; set; }
        public string? Recurrence { get; set; }
        public string? Category { get; set; }
        public string? Summary { get; set; }
    }

    private class PrioritizationDto
    {
        public string? Summary { get; set; }
        public List<PrioritizedTaskDto>? Tasks { get; set; }
    }

    private class PrioritizedTaskDto
    {
        public int TaskId { get; set; }
        public int Rank { get; set; }
        public string? SuggestedPriority { get; set; }
        public string? Reason { get; set; }
    }

    private class ScheduleDto
    {
        public string? Summary { get; set; }
        public List<ScheduleBlockDto>? Blocks { get; set; }
    }

    private class ScheduleBlockDto
    {
        public string? Start { get; set; }
        public string? End { get; set; }
        public int? TaskId { get; set; }
        public string? Title { get; set; }
        public string? Kind { get; set; }
    }

    private class InsightDto
    {
        public string? Headline { get; set; }
        public List<string>? Insights { get; set; }
        public List<string>? Suggestions { get; set; }
    }

    private class RecommendationDto
    {
        public List<RecommendationItemDto>? Recommendations { get; set; }
    }

    private class RecommendationItemDto
    {
        public string? Title { get; set; }
        public string? Reason { get; set; }
        public string? SuggestedPriority { get; set; }
        public string? Category { get; set; }
    }

    private class CoachDto
    {
        public string? Greeting { get; set; }
        public string? Focus { get; set; }
        public string? Motivation { get; set; }
        public List<string>? Tips { get; set; }
        public List<string>? HabitNudges { get; set; }
    }
}
