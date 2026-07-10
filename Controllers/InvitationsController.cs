using DailyPilot.Data;
using DailyPilot.Models;
using DailyPilot.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers;

/// <summary>
/// Invitation lists — organise people into groups per event (Birthday, Wedding, …)
/// and tick each person off as invited, so nobody gets missed. Event types are a
/// per-user master list seeded with sensible defaults.
/// </summary>
[Authorize]
public class InvitationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public InvitationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User)!;

    private static readonly (string Name, string Icon)[] DefaultEvents =
    {
        ("Birthday", "gift"), ("Wedding", "heart"), ("Anniversary", "calendar-heart"),
        ("Party", "balloon"), ("Corporate Event", "briefcase"), ("Baby Shower", "emoji-smile"),
        ("Housewarming", "house-heart"), ("Get-together", "people"),
    };

    /// <summary>Seed a starter set of event types the first time the user visits.</summary>
    private async Task EnsureEventDefaultsAsync()
    {
        if (await _db.EventTypes.AnyAsync(e => e.UserId == UserId)) return;
        foreach (var (name, icon) in DefaultEvents)
            _db.EventTypes.Add(new EventType { Name = name, Icon = icon, UserId = UserId });
        await _db.SaveChangesAsync();
    }

    // GET /Invitations
    public async Task<IActionResult> Index()
    {
        await EnsureEventDefaultsAsync();

        var cards = await _db.InviteLists
            .Where(l => l.UserId == UserId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new InviteListCard
            {
                Id = l.Id,
                GroupName = l.GroupName,
                EventName = l.EventName,
                EventIcon = l.EventIcon,
                Total = l.Invitees.Count,
                Invited = l.Invitees.Count(i => i.IsInvited)
            })
            .ToListAsync();

        var vm = new InvitationsIndexViewModel
        {
            Lists = cards,
            Events = await _db.EventTypes.Where(e => e.UserId == UserId).OrderBy(e => e.Name).ToListAsync()
        };
        return View(vm);
    }

    // POST /Invitations/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string groupName, int eventTypeId)
    {
        groupName = (groupName ?? string.Empty).Trim();
        if (groupName.Length == 0)
        {
            TempData["Error"] = "Please enter a group name.";
            return RedirectToAction(nameof(Index));
        }

        var ev = await _db.EventTypes.FirstOrDefaultAsync(e => e.Id == eventTypeId && e.UserId == UserId);
        var list = new InviteList
        {
            GroupName = groupName.Length > 100 ? groupName[..100] : groupName,
            EventName = ev?.Name ?? "Event",
            EventIcon = ev?.Icon ?? "calendar-event",
            UserId = UserId
        };
        _db.InviteLists.Add(list);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = list.Id });
    }

    // GET /Invitations/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var list = await _db.InviteLists
            .Include(l => l.Invitees)
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == UserId);
        if (list is null) return NotFound();

        var vm = new InviteListDetailsViewModel
        {
            List = list,
            // Not-yet-invited first; then by name.
            Invitees = list.Invitees
                .OrderBy(i => i.IsInvited)
                .ThenBy(i => i.Name)
                .ToList()
        };
        return View(vm);
    }

    // POST /Invitations/AddInvitee
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInvitee(int listId, string name, string? contact)
    {
        var list = await _db.InviteLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == UserId);
        if (list is null) return NotFound();

        name = (name ?? string.Empty).Trim();
        if (name.Length > 0)
        {
            _db.Invitees.Add(new Invitee
            {
                InviteListId = listId,
                Name = name.Length > 120 ? name[..120] : name,
                Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim()
            });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id = listId });
    }

    // POST /Invitations/ToggleInvited
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleInvited(int id)
    {
        var invitee = await _db.Invitees
            .Include(i => i.InviteList)
            .FirstOrDefaultAsync(i => i.Id == id && i.InviteList!.UserId == UserId);
        if (invitee is null) return NotFound();

        invitee.IsInvited = !invitee.IsInvited;
        invitee.InvitedAt = invitee.IsInvited ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { ok = true, invited = invitee.IsInvited });
        return RedirectToAction(nameof(Details), new { id = invitee.InviteListId });
    }

    // POST /Invitations/Confirm — save the whole grid's invited state in one action.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int listId, int[]? invitedIds)
    {
        var list = await _db.InviteLists
            .Include(l => l.Invitees)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == UserId);
        if (list is null) return NotFound();

        var set = (invitedIds ?? Array.Empty<int>()).ToHashSet();
        foreach (var p in list.Invitees)
        {
            var invited = set.Contains(p.Id);
            if (p.IsInvited != invited)
            {
                p.IsInvited = invited;
                p.InvitedAt = invited ? DateTime.UtcNow : null;
            }
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Saved — {set.Count} of {list.Invitees.Count} marked as invited.";
        return RedirectToAction(nameof(Details), new { id = listId });
    }

    // POST /Invitations/DeleteInvitee
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInvitee(int id)
    {
        var invitee = await _db.Invitees
            .Include(i => i.InviteList)
            .FirstOrDefaultAsync(i => i.Id == id && i.InviteList!.UserId == UserId);
        if (invitee is null) return NotFound();
        var listId = invitee.InviteListId;
        _db.Invitees.Remove(invitee);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = listId });
    }

    // POST /Invitations/DeleteList
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteList(int id)
    {
        var list = await _db.InviteLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == UserId);
        if (list is not null)
        {
            _db.InviteLists.Remove(list);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Invitation list deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---- Event master ----

    // GET /Invitations/Events
    public async Task<IActionResult> Events()
    {
        await EnsureEventDefaultsAsync();
        var events = await _db.EventTypes.Where(e => e.UserId == UserId).OrderBy(e => e.Name).ToListAsync();
        return View(events);
    }

    // POST /Invitations/CreateEvent
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(string name, string? icon)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            TempData["Error"] = "Please enter an event name.";
            return RedirectToAction(nameof(Events));
        }
        var exists = await _db.EventTypes.AnyAsync(e => e.UserId == UserId && e.Name == name);
        if (!exists)
        {
            _db.EventTypes.Add(new EventType
            {
                Name = name.Length > 60 ? name[..60] : name,
                Icon = string.IsNullOrWhiteSpace(icon) ? "calendar-event" : icon.Trim(),
                UserId = UserId
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Added “{name}”.";
        }
        return RedirectToAction(nameof(Events));
    }

    // POST /Invitations/DeleteEvent
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.EventTypes.FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);
        if (ev is not null)
        {
            _db.EventTypes.Remove(ev);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Events));
    }
}
