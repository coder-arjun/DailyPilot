using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CategoriesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User)!;

    public async Task<IActionResult> Index()
    {
        var categories = await _db.Categories
            .Where(c => c.UserId == UserId)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string color)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var exists = await _db.Categories.AnyAsync(c => c.UserId == UserId && c.Name == name.Trim());
            if (exists)
            {
                TempData["Error"] = "A category with that name already exists.";
            }
            else
            {
                _db.Categories.Add(new Category
                {
                    Name = name.Trim(),
                    Color = string.IsNullOrWhiteSpace(color) ? "#0d6efd" : color,
                    UserId = UserId
                });
                await _db.SaveChangesAsync();
                TempData["Success"] = "Category added.";
            }
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Tasks)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);
        if (category is not null)
        {
            // Detach tasks from the category before removing it (ClientSetNull).
            foreach (var task in category.Tasks)
                task.CategoryId = null;

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}
