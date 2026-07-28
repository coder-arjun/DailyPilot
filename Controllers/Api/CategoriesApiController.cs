using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

[Route("api/v1/categories")]
public class CategoriesApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    public CategoriesApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Categories
            .Where(c => c.UserId == CurrentUserId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Color))
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CategoryWriteRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.UserId == CurrentUserId && c.Name == name))
            return ApiError(409, "A category with that name already exists.");

        var category = new Category
        {
            UserId = CurrentUserId,
            Name = name,
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#0d6efd" : request.Color,
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(List), null, CategoryDto.From(category));
    }
}
