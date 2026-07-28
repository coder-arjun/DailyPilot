using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

/// <summary>Mobile counterpart of <see cref="HistoryController"/> (FR-008 Task History).</summary>
[Route("api/v1/history")]
public class HistoryApiController : ApiControllerBase
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _db;

    public HistoryApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1)
    {
        if (page < 1) page = 1;

        var query = _db.TaskHistories
            .Where(h => h.UserId == CurrentUserId)
            .OrderByDescending(h => h.Timestamp);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var dto = new HistoryPageDto(
            items.Select(TaskHistoryDto.From).ToList(),
            page,
            (int)Math.Ceiling(total / (double)PageSize));

        return Ok(dto);
    }
}
