using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

[Authorize]
public class HistoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public HistoryController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // FR-008 Task History
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 50;
        var userId = _userManager.GetUserId(User)!;

        var query = _db.TaskHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.Timestamp);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(items);
    }
}
