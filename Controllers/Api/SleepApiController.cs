using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.Services.Sleep;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DailyPilot.Controllers.Api;

[Route("api/v1/sleep")]
public class SleepApiController : ApiControllerBase
{
    private readonly ISleepService _sleep;
    private readonly ITaskService _tasks;
    private readonly UserManager<ApplicationUser> _userManager;

    public SleepApiController(ISleepService sleep, ITaskService tasks, UserManager<ApplicationUser> userManager)
    {
        _sleep = sleep;
        _tasks = tasks;
        _userManager = userManager;
    }

    public sealed record SleepEntryRequest(
        [Required] string Date,
        [Required] string BedTime,
        string? EstimatedSleepTime,
        string? WakeTime,
        string? TimeOutOfBed,
        [Range(1, 10)] int? Quality,
        bool? PhoneBeforeBed,
        bool? DreamRemembered,
        [StringLength(500)] string? Notes,
        // True when the caller synthesized BedTime rather than the user entering it
        // (mirrors the web SaveMorning fallback). Omitted/null leaves the existing
        // flag on the row untouched — see SleepService.UpsertAsync.
        bool? BedTimeEstimated = null);

    [HttpPut("entry")]
    public async Task<IActionResult> Upsert(SleepEntryRequest request)
    {
        if (!DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", out var date))
            return ApiError(400, "date must be yyyy-MM-dd.");
        if (!TryTime(request.BedTime, out var bed))
            return ApiError(400, "bedTime must be HH:mm.");

        TimeOnly sleepTime;
        if (string.IsNullOrWhiteSpace(request.EstimatedSleepTime))
            sleepTime = bed.AddMinutes(15);
        else if (!TryTime(request.EstimatedSleepTime, out sleepTime))
            return ApiError(400, "estimatedSleepTime must be HH:mm.");

        TimeOnly? wake = null, outOfBed = null;
        if (!string.IsNullOrWhiteSpace(request.WakeTime))
        {
            if (!TryTime(request.WakeTime, out var w)) return ApiError(400, "wakeTime must be HH:mm.");
            wake = w;
            outOfBed = w;
        }
        if (!string.IsNullOrWhiteSpace(request.TimeOutOfBed))
        {
            if (!TryTime(request.TimeOutOfBed, out var o)) return ApiError(400, "timeOutOfBed must be HH:mm.");
            outOfBed = o;
        }
        if (wake is not null && request.Quality is null)
            return ApiError(400, "quality (1-10) is required with wakeTime.");

        var saved = await _sleep.UpsertAsync(CurrentUserId, new SleepEntry
        {
            Date = date,
            BedTime = bed,
            EstimatedSleepTime = sleepTime,
            WakeTime = wake,
            TimeOutOfBed = outOfBed,
            Quality = request.Quality,
            PhoneBeforeBed = request.PhoneBeforeBed,
            DreamRemembered = request.DreamRemembered,
            Notes = request.Notes,
            BedTimeEstimated = request.BedTimeEstimated,
        });
        return Ok(EntryDto(saved));
    }

    [HttpGet("entry")]
    public async Task<IActionResult> Get([FromQuery] string? date)
    {
        var day = await ResolveDateAsync(date);
        if (day is null) return ApiError(400, "date must be yyyy-MM-dd.");
        var entry = await _sleep.GetAsync(CurrentUserId, day.Value);
        return entry is null ? ApiError(404, "No entry for that date.") : Ok(EntryDto(entry));
    }

    [HttpDelete("entry")]
    public async Task<IActionResult> Delete([FromQuery] string date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day))
            return ApiError(400, "date must be yyyy-MM-dd.");
        return await _sleep.DeleteAsync(CurrentUserId, day) ? NoContent() : ApiError(404, "No entry for that date.");
    }

    [HttpGet("entries")]
    public async Task<IActionResult> Range([FromQuery] string from, [FromQuery] string to)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var f) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", out var t) || t < f)
            return ApiError(400, "from/to must be yyyy-MM-dd with to >= from.");
        var rows = await _sleep.GetRangeAsync(CurrentUserId, f, t);
        return Ok(rows.Select(EntryDto));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var today = await LocalTodayAsync();
        var d = await _sleep.BuildDashboardAsync(CurrentUserId, today);
        return Ok(new
        {
            today = d.Today is null ? null : EntryDto(d.Today),
            todayDurationMinutes = d.TodayDurationMinutes,
            todayScore = d.TodayScore,
            todayBand = d.TodayBand,
            averageDurationMinutes = d.AverageDurationMinutes,
            averageBedTime = d.AverageBedTime?.ToString("HH:mm"),
            averageWakeTime = d.AverageWakeTime?.ToString("HH:mm"),
            sleepDebtMinutes = d.SleepDebtMinutes,
            consistencyPct = d.ConsistencyPct,
            idealBedTime = d.IdealBedTime?.ToString("HH:mm"),
            weekendShiftMinutes = d.WeekendShiftMinutes,
            lateSleepDays = d.LateSleepDays,
            earlyWakeDays = d.EarlyWakeDays,
            completeNightCount = d.CompleteNightCount,
            recent = d.Recent.Select(r => new
            {
                date = r.Date.ToString("yyyy-MM-dd"),
                durationMinutes = r.DurationMinutes,
                quality = r.Quality,
            }),
        });
    }

    [HttpGet("insights")]
    public async Task<IActionResult> Insights()
    {
        var today = await LocalTodayAsync();
        var (weekly, personalized) = await _sleep.InsightsAsync(CurrentUserId, today);
        return Ok(new { weekly, personalized });
    }

    private static object EntryDto(SleepEntry e) => new
    {
        date = e.Date.ToString("yyyy-MM-dd"),
        bedTime = e.BedTime.ToString("HH:mm"),
        estimatedSleepTime = e.EstimatedSleepTime.ToString("HH:mm"),
        wakeTime = e.WakeTime?.ToString("HH:mm"),
        timeOutOfBed = e.TimeOutOfBed?.ToString("HH:mm"),
        quality = e.Quality,
        phoneBeforeBed = e.PhoneBeforeBed,
        dreamRemembered = e.DreamRemembered,
        notes = e.Notes,
        isComplete = e.IsComplete,
        bedTimeEstimated = e.BedTimeEstimated,
    };

    private async Task<DateOnly?> ResolveDateAsync(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return await LocalTodayAsync();
        return DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day) ? day : null;
    }

    private async Task<DateOnly> LocalTodayAsync()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId);
        return user is null
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : await _tasks.GetLocalTodayAsync(user);
    }

    private static bool TryTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", out time);
}
