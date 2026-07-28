using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Data;
using DailyPilot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

/// <summary>Mobile counterpart of <see cref="SearchController"/> — global search, capped per kind.</summary>
[Route("api/v1/search")]
public class SearchApiController : ApiControllerBase
{
    private const int MaxPerKind = 20;

    private readonly ApplicationDbContext _db;
    private readonly IWorkspaceService _workspaces;

    public SearchApiController(ApplicationDbContext db, IWorkspaceService workspaces)
    {
        _db = db;
        _workspaces = workspaces;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? q)
    {
        var term = q?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            return ApiError(400, "q is required.");

        var userId = CurrentUserId;

        var tasks = await _db.Tasks
            .Where(t => t.UserId == userId && (t.Title.Contains(term) || (t.Notes != null && t.Notes.Contains(term))))
            .OrderByDescending(t => t.PlannedDate)
            .Take(MaxPerKind)
            .ToListAsync();

        var habits = await _db.Habits
            .Where(h => h.UserId == userId && (h.Name.Contains(term) || (h.Description != null && h.Description.Contains(term))))
            .OrderBy(h => h.Name)
            .Take(MaxPerKind)
            .ToListAsync();

        var categories = await _db.Categories
            .Where(c => c.UserId == userId && c.Name.Contains(term))
            .OrderBy(c => c.Name)
            .Take(MaxPerKind)
            .ToListAsync();

        var tags = await _db.Tags
            .Where(t => t.UserId == userId && t.Name.Contains(term))
            .OrderBy(t => t.Name)
            .Take(MaxPerKind)
            .ToListAsync();

        var myWsIds = (await _workspaces.GetUserWorkspacesAsync(userId)).Select(w => w.Id).ToList();
        var workspaces = await _db.Workspaces
            .Where(w => myWsIds.Contains(w.Id) && (w.Name.Contains(term) || (w.Description != null && w.Description.Contains(term))))
            .OrderBy(w => w.Name)
            .Take(MaxPerKind)
            .ToListAsync();

        var dto = new SearchResultsDto(
            tasks.Select(SearchTaskDto.From).ToList(),
            habits.Select(SearchHabitDto.From).ToList(),
            categories.Select(SearchCategoryDto.From).ToList(),
            tags.Select(SearchTagDto.From).ToList(),
            workspaces.Select(SearchWorkspaceDto.From).ToList());

        return Ok(dto);
    }
}
