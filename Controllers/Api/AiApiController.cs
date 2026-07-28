using System.ComponentModel.DataAnnotations;
using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.Services;
using DailyPilot.Services.Ai;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

/// <summary>
/// Mobile counterpart of <see cref="AiController"/>. <see cref="IAiTaskAssistant"/> has no
/// free-form "chat" method — its natural-language entrypoint is
/// <see cref="IAiTaskAssistant.ParseTaskAsync"/>, the same one <c>AiController.QuickAdd</c>
/// uses (PRD §6 "Natural Language Task Creation"). This endpoint mirrors that construction
/// (categories + today), creates the parsed task the same way, and replies with the same
/// human-readable summary QuickAdd shows via TempData.
/// </summary>
[Route("api/v1/ai")]
public class AiApiController : ApiControllerBase
{
    private readonly IAiTaskAssistant _ai;
    private readonly ITaskService _tasks;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AiApiController(IAiTaskAssistant ai, ITaskService tasks, ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _ai = ai;
        _tasks = tasks;
        _db = db;
        _userManager = userManager;
    }

    public sealed record ChatRequest([Required] string Message);

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(ChatRequest request)
    {
        var text = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return ApiError(400, "message must not be empty.");

        var user = await _userManager.FindByIdAsync(CurrentUserId);
        if (user is null) return ApiError(401, "Unknown user.");

        var categories = await _db.Categories.Where(c => c.UserId == CurrentUserId).ToListAsync();
        var today = await _tasks.GetLocalTodayAsync(user);

        var parsed = await _ai.ParseTaskAsync(text, categories.Select(c => c.Name).ToList(), today);

        var task = new TaskItem
        {
            UserId = CurrentUserId,
            Title = parsed.Title,
            Notes = parsed.Notes,
            PlannedDate = parsed.PlannedDate,
            DueTime = parsed.DueTime,
            Priority = parsed.Priority,
            EnergyLevel = parsed.EnergyLevel,
            EstimatedMinutes = parsed.EstimatedMinutes,
            Recurrence = parsed.Recurrence,
            CategoryId = categories.FirstOrDefault(c =>
                string.Equals(c.Name, parsed.CategoryName, StringComparison.OrdinalIgnoreCase))?.Id,
        };
        await _tasks.CreateAsync(CurrentUserId, task);

        return Ok(new { reply = $"✨ {parsed.Summary}" });
    }
}
