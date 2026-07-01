using System.Globalization;
using System.Text.RegularExpressions;
using DailyPilot.Models;
using DailyPilot.ViewModels;

namespace DailyPilot.Services.Ai;

/// <summary>
/// Deterministic, rules-based implementation of the Phase 2 AI features. Used when
/// no LLM is configured, and also as the fallback for the Semantic Kernel assistant
/// when a model call fails. Produces genuinely useful results with zero dependencies.
/// </summary>
public partial class HeuristicAiTaskAssistant : IAiTaskAssistant
{
    public bool IsLlmEnabled => false;

    // ---------- Natural Language Task Creation ----------

    public Task<ParsedTaskResult> ParseTaskAsync(string input, IReadOnlyList<string> categories, DateOnly today)
    {
        var original = (input ?? string.Empty).Trim();
        var working = " " + original + " ";
        var lower = working.ToLowerInvariant();
        var result = new ParsedTaskResult { PlannedDate = today };
        var removals = new List<string>();

        // --- Priority ---
        if (Regex.IsMatch(lower, @"\b(urgent|asap|important|high priority|critical|must)\b"))
            result.Priority = Priority.High;
        else if (Regex.IsMatch(lower, @"\b(low priority|whenever|someday|optional|eventually)\b"))
            result.Priority = Priority.Low;
        foreach (Match m in Regex.Matches(lower, @"\b(urgent|asap|important|high priority|low priority|critical|whenever|someday|optional)\b"))
            removals.Add(m.Value);

        // --- Energy ---
        if (Regex.IsMatch(lower, @"\b(deep work|focus|focused|brainstorm|design|write)\b"))
            result.EnergyLevel = EnergyLevel.High;
        else if (Regex.IsMatch(lower, @"\b(quick|easy|simple|trivial)\b"))
            result.EnergyLevel = EnergyLevel.Low;

        // --- Recurrence ---
        if (Regex.IsMatch(lower, @"\b(every day|daily|each day)\b")) { result.Recurrence = RecurrencePattern.Daily; removals.Add("every day"); removals.Add("daily"); }
        else if (Regex.IsMatch(lower, @"\b(every week|weekly)\b")) { result.Recurrence = RecurrencePattern.Weekly; removals.Add("every week"); removals.Add("weekly"); }
        else if (Regex.IsMatch(lower, @"\b(every month|monthly)\b")) { result.Recurrence = RecurrencePattern.Monthly; removals.Add("every month"); removals.Add("monthly"); }
        else if (Regex.IsMatch(lower, @"\b(weekdays|every weekday)\b")) { result.Recurrence = RecurrencePattern.Weekdays; removals.Add("weekdays"); }

        // --- Duration ---
        var durMatch = Regex.Match(lower, @"\b(?:for\s+)?(\d+)\s*(hours?|hrs?|h|minutes?|mins?|m)\b");
        if (durMatch.Success)
        {
            var n = int.Parse(durMatch.Groups[1].Value);
            var unit = durMatch.Groups[2].Value;
            result.EstimatedMinutes = unit.StartsWith('h') ? n * 60 : n;
            removals.Add(durMatch.Value);
        }

        // --- Date ---
        result.PlannedDate = ParseDate(lower, today, removals);

        // --- Time ---
        var time = ParseTime(lower, removals);
        if (time is not null) result.DueTime = time;

        // --- Category (match user categories, "#cat", or "in <cat>") ---
        foreach (var cat in categories)
        {
            if (Regex.IsMatch(lower, $@"(#|in\s+|category\s+){Regex.Escape(cat.ToLowerInvariant())}\b")
                || Regex.IsMatch(lower, $@"\b{Regex.Escape(cat.ToLowerInvariant())}\b"))
            {
                result.CategoryName = cat;
                removals.Add("#" + cat.ToLowerInvariant());
                break;
            }
        }

        // --- Title: strip recognised tokens & connector words ---
        var title = original;
        foreach (var r in removals.Where(r => !string.IsNullOrWhiteSpace(r)).OrderByDescending(r => r.Length))
            title = Regex.Replace(title, Regex.Escape(r), " ", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\b(at|on|by|for|every|in|the|a)\b\s*$", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"#\w+", " ");
        title = Regex.Replace(title, @"\s{2,}", " ").Trim(' ', '-', ',', '.');
        result.Title = string.IsNullOrWhiteSpace(title) ? original : char.ToUpper(title[0]) + title[1..];

        result.Summary = BuildParseSummary(result);
        return Task.FromResult(result);
    }

    // ---------- Task Break-down (subtasks) ----------

    public Task<BreakdownResult> BreakdownAsync(string title, string? notes, int? estimatedMinutes)
    {
        var t = (title ?? string.Empty).Trim();
        var lower = t.ToLowerInvariant();
        var steps = new List<string>();

        // Keyword-tailored templates for common task shapes.
        if (Regex.IsMatch(lower, @"\b(email|reply|write to|message)\b"))
            steps.AddRange(new[] { "Clarify the key point to communicate", $"Draft the {(lower.Contains("email") ? "email" : "message")}", "Proofread and adjust the tone", "Send and note any follow-up" });
        else if (Regex.IsMatch(lower, @"\b(report|document|doc|proposal|essay|article|blog)\b"))
            steps.AddRange(new[] { "Outline the structure and key points", "Gather supporting data / references", "Write the first draft", "Edit and refine", "Final review and share" });
        else if (Regex.IsMatch(lower, @"\b(meeting|call|sync|1:1|standup|interview)\b"))
            steps.AddRange(new[] { "Set the agenda / goal", "Prepare notes and questions", "Attend and capture decisions", "Send follow-up actions" });
        else if (Regex.IsMatch(lower, @"\b(bug|fix|issue|error|debug)\b"))
            steps.AddRange(new[] { "Reproduce the problem", "Identify the root cause", "Implement the fix", "Test and verify", "Deploy / close out" });
        else if (Regex.IsMatch(lower, @"\b(feature|build|implement|develop|create|design)\b"))
            steps.AddRange(new[] { "Define requirements and scope", "Sketch the approach / design", $"Build: {t}", "Test it works", "Review and ship" });
        else if (Regex.IsMatch(lower, @"\b(plan|organize|organise|prepare|arrange|event|trip|party)\b"))
            steps.AddRange(new[] { "List everything involved", "Decide dates / logistics", "Handle bookings / purchases", "Confirm details", "Final check the day before" });
        else if (Regex.IsMatch(lower, @"\b(study|learn|revise|research|read)\b"))
            steps.AddRange(new[] { "Gather the materials", "Skim for the big picture", "Deep-dive the key parts", "Take notes / summarise", "Self-test what you learned" });
        else if (Regex.IsMatch(lower, @"\b(clean|tidy|declutter|chore|laundry|groceries|shopping)\b"))
            steps.AddRange(new[] { "List what needs doing", "Gather what you need", $"Do it: {t}", "Put everything away" });
        else
            steps.AddRange(new[] { "Clarify the goal and what 'done' looks like", "Gather what you need to start", $"Do the core work: {t}", "Review and double-check", "Wrap up / share the result" });

        // For a big task, add an explicit break/checkpoint step.
        if (estimatedMinutes is > 90)
            steps.Insert(Math.Min(2, steps.Count), "Break the work into 25–50 min focus sessions");

        var result = new BreakdownResult
        {
            Steps = steps,
            Summary = $"Broke “{(string.IsNullOrWhiteSpace(t) ? "task" : t)}” into {steps.Count} steps."
        };
        return Task.FromResult(result);
    }

    private static DateOnly ParseDate(string lower, DateOnly today, List<string> removals)
    {
        if (Regex.IsMatch(lower, @"\btomorrow\b")) { removals.Add("tomorrow"); return today.AddDays(1); }
        if (Regex.IsMatch(lower, @"\btoday\b")) { removals.Add("today"); return today; }
        if (Regex.IsMatch(lower, @"\btonight\b")) { removals.Add("tonight"); return today; }

        var inDays = Regex.Match(lower, @"\bin\s+(\d+)\s+days?\b");
        if (inDays.Success) { removals.Add(inDays.Value); return today.AddDays(int.Parse(inDays.Groups[1].Value)); }

        if (Regex.IsMatch(lower, @"\bnext week\b")) { removals.Add("next week"); return today.AddDays(7); }

        // Weekday names → next occurrence
        string[] days = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
        for (int i = 0; i < days.Length; i++)
        {
            if (Regex.IsMatch(lower, $@"\b(next\s+)?{days[i]}\b"))
            {
                removals.Add(days[i]);
                var target = (DayOfWeek)i;
                int delta = ((int)target - (int)today.DayOfWeek + 7) % 7;
                if (delta == 0) delta = 7; // next, not today
                return today.AddDays(delta);
            }
        }
        return today;
    }

    private static TimeOnly? ParseTime(string lower, List<string> removals)
    {
        // 3pm / 3:30 pm
        var m = Regex.Match(lower, @"\b(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)\b");
        if (m.Success)
        {
            removals.Add(m.Value);
            int h = int.Parse(m.Groups[1].Value);
            int min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            var ampm = m.Groups[3].Value;
            if (ampm == "pm" && h < 12) h += 12;
            if (ampm == "am" && h == 12) h = 0;
            if (h <= 23 && min <= 59) return new TimeOnly(h, min);
        }
        // 24-hour "at 15:00"
        var m24 = Regex.Match(lower, @"\bat\s+(\d{1,2}):(\d{2})\b");
        if (m24.Success)
        {
            removals.Add(m24.Value);
            int h = int.Parse(m24.Groups[1].Value), min = int.Parse(m24.Groups[2].Value);
            if (h <= 23 && min <= 59) return new TimeOnly(h, min);
        }
        return null;
    }

    private static string BuildParseSummary(ParsedTaskResult r)
    {
        var parts = new List<string> { $"“{r.Title}”", $"on {r.PlannedDate:ddd dd MMM}" };
        if (r.DueTime is not null) parts.Add($"at {r.DueTime:HH:mm}");
        parts.Add($"{r.Priority} priority");
        if (r.EstimatedMinutes is not null) parts.Add($"~{r.EstimatedMinutes} min");
        if (r.Recurrence != RecurrencePattern.None) parts.Add($"repeats {r.Recurrence}");
        if (r.CategoryName is not null) parts.Add($"in {r.CategoryName}");
        return "Understood: " + string.Join(", ", parts) + ".";
    }

    // ---------- AI Prioritization Engine ----------

    public Task<PrioritizationResult> PrioritizeAsync(IReadOnlyList<TaskItem> tasks)
    {
        var pending = tasks.Where(t => t.Status != DailyTaskStatus.Completed).ToList();
        var scored = pending
            .Select(t => new { Task = t, Score = ScoreTask(t) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var result = new PrioritizationResult();
        int rank = 1;
        foreach (var x in scored)
        {
            result.Tasks.Add(new PrioritizedTask
            {
                TaskId = x.Task.Id,
                Title = x.Task.Title,
                Rank = rank++,
                SuggestedPriority = x.Task.Priority,
                Reason = ReasonFor(x.Task)
            });
        }
        result.Summary = pending.Count == 0
            ? "Nothing pending — enjoy the clear list!"
            : $"Ordered {pending.Count} task(s) by urgency, deadlines and how often they've slipped.";
        return Task.FromResult(result);
    }

    private static double ScoreTask(TaskItem t)
    {
        double score = (int)t.Priority * 30;
        score += t.CarryForwardCount * 15;             // overdue urgency
        if (t.DueTime is not null) score += 20 + (1440 - t.DueTime.Value.ToTimeSpan().TotalMinutes) / 100.0;
        if (t.EnergyLevel == EnergyLevel.High) score += 5;
        return score;
    }

    private static string ReasonFor(TaskItem t)
    {
        var reasons = new List<string>();
        if (t.Priority == Priority.High) reasons.Add("high priority");
        if (t.CarryForwardCount > 0) reasons.Add($"carried forward {t.CarryForwardCount}×");
        if (t.DueTime is not null) reasons.Add($"due {t.DueTime:HH:mm}");
        if (reasons.Count == 0) reasons.Add("scheduled for today");
        return char.ToUpper(reasons[0][0]) + string.Join(", ", reasons)[1..];
    }

    // ---------- Daily Schedule Generator ----------

    public Task<ScheduleResult> GenerateScheduleAsync(IReadOnlyList<TaskItem> tasks, TimeOnly workStart, TimeOnly workEnd)
    {
        var ordered = tasks
            .Where(t => t.Status != DailyTaskStatus.Completed)
            .OrderByDescending(ScoreTask)
            .ToList();

        var result = new ScheduleResult();
        var cursor = workStart;
        int sinceBreak = 0;
        int placed = 0;

        foreach (var t in ordered)
        {
            var minutes = t.EstimatedMinutes is > 0 ? t.EstimatedMinutes!.Value : 30;
            var end = cursor.Add(TimeSpan.FromMinutes(minutes));
            if (end > workEnd) break; // out of working hours

            result.Blocks.Add(new ScheduleBlock
            {
                Start = cursor, End = end, TaskId = t.Id, Title = t.Title, Kind = ScheduleBlockKind.Task
            });
            cursor = end;
            placed++;
            sinceBreak += minutes;

            // Insert a 10-minute break roughly every 90 minutes of work.
            if (sinceBreak >= 90)
            {
                var bEnd = cursor.Add(TimeSpan.FromMinutes(10));
                if (bEnd <= workEnd)
                {
                    result.Blocks.Add(new ScheduleBlock { Start = cursor, End = bEnd, Title = "Short break", Kind = ScheduleBlockKind.Break });
                    cursor = bEnd;
                    sinceBreak = 0;
                }
            }
        }

        var notPlaced = ordered.Count - placed;
        result.Summary = placed == 0
            ? "No tasks to schedule within your working hours."
            : $"Scheduled {placed} task(s) between {workStart:HH:mm} and {workEnd:HH:mm}."
              + (notPlaced > 0 ? $" {notPlaced} didn't fit — consider extending hours or deferring." : "");
        return Task.FromResult(result);
    }

    // ---------- Productivity Insights ----------

    public Task<InsightResult> GenerateInsightsAsync(DashboardViewModel d)
    {
        var r = new InsightResult
        {
            Headline = d.WeeklyCompletionRate switch
            {
                >= 80 => "You're on fire this week! 🔥",
                >= 50 => "Solid, steady progress this week. 👍",
                > 0 => "Let's build some momentum. 💪",
                _ => "A fresh start — let's get the first win. 🌱"
            }
        };

        r.Insights.Add($"Today: {d.CompletedToday}/{d.PlannedToday} tasks done ({d.TodayCompletionRate}%).");
        r.Insights.Add($"Weekly completion rate: {d.WeeklyCompletionRate}%.");
        r.Insights.Add($"Carry-forward rate: {d.CarryForwardRate}% of tasks slipped to another day.");
        r.Insights.Add($"Current streak: {d.CurrentStreak} day(s); your best is {d.LongestStreak}.");

        var best = d.CategoryPerformance.OrderByDescending(c => c.Rate).FirstOrDefault();
        var worst = d.CategoryPerformance.Where(c => c.Total > 0).OrderBy(c => c.Rate).FirstOrDefault();
        if (best is not null) r.Insights.Add($"Strongest area: {best.Category} ({best.Rate}% complete).");
        if (worst is not null && worst.Category != best?.Category) r.Insights.Add($"Needs attention: {worst.Category} ({worst.Rate}% complete).");

        if (d.CarryForwardRate >= 30) r.Suggestions.Add("Your carry-forward rate is high — try planning fewer tasks or breaking big ones into smaller steps.");
        if (d.PendingToday > 0) r.Suggestions.Add($"You have {d.PendingToday} task(s) left today — pick the most important one and start now.");
        if (d.CurrentStreak == 0) r.Suggestions.Add("Complete at least one task today to start a new streak.");
        if (worst is not null && worst.Rate < 50) r.Suggestions.Add($"Schedule a focused block for {worst.Category} tasks.");
        if (r.Suggestions.Count == 0) r.Suggestions.Add("Great balance — keep doing what you're doing!");

        return Task.FromResult(r);
    }

    // ---------- Task Recommendation Engine ----------

    public Task<RecommendationResult> RecommendAsync(RecommendationContext ctx)
    {
        var r = new RecommendationResult();

        foreach (var s in ctx.StickyTasks.OrderByDescending(x => x.CarryForwardCount).Take(3))
            r.Recommendations.Add(new TaskRecommendation
            {
                Title = s.Title,
                Reason = $"Carried forward {s.CarryForwardCount}× — knock it out today to stop it slipping.",
                SuggestedPriority = Priority.High
            });

        foreach (var f in ctx.FrequentTasks.OrderByDescending(x => x.Count).Take(3))
            r.Recommendations.Add(new TaskRecommendation
            {
                Title = f.Title,
                Reason = $"You do this often ({f.Count}× recently) — add today's instance?",
                SuggestedPriority = Priority.Medium,
                CategoryName = f.Category
            });

        foreach (var c in ctx.NeglectedCategories.Take(2))
            r.Recommendations.Add(new TaskRecommendation
            {
                Title = $"Plan something for {c}",
                Reason = $"You haven't touched {c} recently.",
                SuggestedPriority = Priority.Low,
                CategoryName = c
            });

        if (ctx.CurrentStreak > 0)
            r.Recommendations.Add(new TaskRecommendation
            {
                Title = "Keep your streak alive",
                Reason = $"You're on a {ctx.CurrentStreak}-day streak — complete a task today to extend it.",
                SuggestedPriority = Priority.Medium
            });

        return Task.FromResult(r);
    }

    // ---------- AI Coach ----------

    public Task<CoachResult> CoachAsync(CoachContext ctx)
    {
        var name = string.IsNullOrWhiteSpace(ctx.DisplayName) ? "there" : ctx.DisplayName;
        var r = new CoachResult
        {
            Greeting = $"Hi {name} — here's your coaching for {ctx.Today:dddd, dd MMM}."
        };

        // Focus
        if (ctx.PendingToday == 0 && ctx.PlannedToday > 0)
            r.Focus = "You've cleared every task planned for today. Outstanding — consider planning tomorrow.";
        else if (ctx.TopPendingTasks.Count > 0)
            r.Focus = $"Your #1 focus right now: “{ctx.TopPendingTasks[0]}”. Start there before anything else.";
        else
            r.Focus = "Nothing is on your list yet today — add your top 1-3 priorities to get moving.";

        // Motivation
        r.Motivation = ctx.CurrentStreak switch
        {
            >= 7 => $"🔥 {ctx.CurrentStreak}-day streak — you're in a serious groove. Protect it today.",
            >= 1 => $"You're on a {ctx.CurrentStreak}-day streak. One completed task keeps it alive.",
            _ => "Every streak starts with a single completed task. Let's get today's first win."
        };

        // Tips
        if (ctx.PendingToday > 5)
            r.Tips.Add($"You have {ctx.PendingToday} tasks today — that's a lot. Pick the top 3 and treat the rest as stretch goals.");
        if (ctx.CarryForwardRate >= 30)
            r.Tips.Add($"Your carry-forward rate is {ctx.CarryForwardRate}%. Try smaller, more specific tasks so they're easier to finish.");
        if (ctx.WeeklyCompletionRate >= 80)
            r.Tips.Add("Completion rate above 80% this week — great consistency. Keep the momentum.");
        if (ctx.CompletedToday > 0)
            r.Tips.Add($"You've already completed {ctx.CompletedToday} task(s) today. Nice start!");
        if (r.Tips.Count == 0)
            r.Tips.Add("Block 25 focused minutes on your most important task — momentum builds from there.");

        // Habit nudges
        var pendingHabits = ctx.Habits.Where(h => !h.TargetMet).ToList();
        foreach (var h in pendingHabits.Take(3))
        {
            r.HabitNudges.Add(h.CurrentStreak > 0
                ? $"Don't break your {h.CurrentStreak}-{(h.Frequency == "Weekly" ? "week" : "day")} streak on “{h.Name}” — check it in today."
                : $"Build the “{h.Name}” habit — a check-in today starts the streak.");
        }
        if (ctx.Habits.Count > 0 && pendingHabits.Count == 0)
            r.HabitNudges.Add("All habits on track today. 💪");

        return Task.FromResult(r);
    }
}
