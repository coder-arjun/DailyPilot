using DailyPilot.Controllers.Api.Dtos;
using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Controllers.Api;

/// <summary>
/// Mobile API for invitation lists — mirrors <see cref="DailyPilot.Controllers.InvitationsController"/>
/// (organise people to invite per event, tick each off as invited). Every query is scoped to
/// <see cref="ApiControllerBase.CurrentUserId"/>. See <see cref="Dtos.InviteeDto"/> for the
/// WhatsApp-share-text note — no such feature exists in the web app today.
/// </summary>
[Route("api/v1/invitations")]
public class InvitationsApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public InvitationsApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    private static readonly (string Name, string Icon)[] DefaultEvents =
    {
        ("Birthday", "gift"), ("Wedding", "heart"), ("Anniversary", "calendar-heart"),
        ("Party", "balloon"), ("Corporate Event", "briefcase"), ("Baby Shower", "emoji-smile"),
        ("Housewarming", "house-heart"), ("Get-together", "people"),
    };

    /// <summary>Seed a starter set of event types the first time the user hits this API — mirrors
    /// <c>InvitationsController.EnsureEventDefaultsAsync</c>.</summary>
    private async Task EnsureEventDefaultsAsync()
    {
        if (await _db.EventTypes.AnyAsync(e => e.UserId == CurrentUserId)) return;
        foreach (var (name, icon) in DefaultEvents)
            _db.EventTypes.Add(new EventType { Name = name, Icon = icon, UserId = CurrentUserId });
        await _db.SaveChangesAsync();
    }

    // GET api/v1/invitations
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await EnsureEventDefaultsAsync();

        var cards = await _db.InviteLists
            .Where(l => l.UserId == CurrentUserId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                l.Id,
                l.GroupName,
                l.EventName,
                l.EventIcon,
                l.CreatedAt,
                Total = l.Invitees.Count,
                Invited = l.Invitees.Count(i => i.IsInvited),
            })
            .ToListAsync();

        return Ok(cards.Select(c =>
            InviteListSummaryDto.From(c.Id, c.GroupName, c.EventName, c.EventIcon, c.CreatedAt, c.Total, c.Invited)));
    }

    // POST api/v1/invitations
    [HttpPost]
    public async Task<IActionResult> Create(InviteListCreateRequest request)
    {
        var groupName = request.GroupName.Trim();
        if (groupName.Length == 0) return ApiError(400, "Please enter a group name.");

        var ev = await _db.EventTypes.FirstOrDefaultAsync(e => e.Id == request.EventTypeId && e.UserId == CurrentUserId);
        var list = new InviteList
        {
            GroupName = groupName.Length > 100 ? groupName[..100] : groupName,
            EventName = ev?.Name ?? "Event",
            EventIcon = ev?.Icon ?? "calendar-event",
            UserId = CurrentUserId,
        };
        _db.InviteLists.Add(list);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = list.Id }, InviteListDetailDto.From(list));
    }

    // GET api/v1/invitations/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var list = await _db.InviteLists
            .Include(l => l.Invitees)
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);
        return list is null ? ApiError(404, "Invitation list not found.") : Ok(InviteListDetailDto.From(list));
    }

    // DELETE api/v1/invitations/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteList(int id)
    {
        var list = await _db.InviteLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);
        if (list is null) return ApiError(404, "Invitation list not found.");
        _db.InviteLists.Remove(list);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST api/v1/invitations/{id}/invitees
    [HttpPost("{id:int}/invitees")]
    public async Task<IActionResult> AddInvitee(int id, InviteeCreateRequest request)
    {
        var list = await _db.InviteLists.FirstOrDefaultAsync(l => l.Id == id && l.UserId == CurrentUserId);
        if (list is null) return ApiError(404, "Invitation list not found.");

        var name = request.Name.Trim();
        if (name.Length == 0) return ApiError(400, "Please enter a name.");
        var contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim();

        var invitee = new Invitee
        {
            InviteListId = id,
            Name = name.Length > 120 ? name[..120] : name,
            Contact = contact is { Length: > 60 } ? contact[..60] : contact,
        };
        _db.Invitees.Add(invitee);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id }, InviteeDto.From(invitee, list.EventName, list.GroupName));
    }

    // POST api/v1/invitations/invitees/{id}/toggle
    [HttpPost("invitees/{id:int}/toggle")]
    public async Task<IActionResult> ToggleInvitee(int id)
    {
        var invitee = await _db.Invitees
            .Include(i => i.InviteList)
            .FirstOrDefaultAsync(i => i.Id == id && i.InviteList!.UserId == CurrentUserId);
        if (invitee is null) return ApiError(404, "Invitee not found.");

        invitee.IsInvited = !invitee.IsInvited;
        invitee.InvitedAt = invitee.IsInvited ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();

        return Ok(InviteeDto.From(invitee, invitee.InviteList!.EventName, invitee.InviteList!.GroupName));
    }

    // DELETE api/v1/invitations/invitees/{id}
    [HttpDelete("invitees/{id:int}")]
    public async Task<IActionResult> DeleteInvitee(int id)
    {
        var invitee = await _db.Invitees
            .Include(i => i.InviteList)
            .FirstOrDefaultAsync(i => i.Id == id && i.InviteList!.UserId == CurrentUserId);
        if (invitee is null) return ApiError(404, "Invitee not found.");

        _db.Invitees.Remove(invitee);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---- Event master (mirrors InvitationsController.Events/CreateEvent) ----

    // GET api/v1/invitations/events
    [HttpGet("events")]
    public async Task<IActionResult> Events()
    {
        await EnsureEventDefaultsAsync();
        var events = await _db.EventTypes.Where(e => e.UserId == CurrentUserId).OrderBy(e => e.Name).ToListAsync();
        return Ok(events.Select(EventTypeDto.From));
    }

    // POST api/v1/invitations/events
    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent(EventTypeCreateRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return ApiError(400, "Please enter an event name.");

        var exists = await _db.EventTypes.AnyAsync(e => e.UserId == CurrentUserId && e.Name == name);
        if (exists) return ApiError(409, "An event type with that name already exists.");

        var ev = new EventType
        {
            Name = name.Length > 60 ? name[..60] : name,
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "calendar-event" : request.Icon.Trim(),
            UserId = CurrentUserId,
        };
        _db.EventTypes.Add(ev);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Events), EventTypeDto.From(ev));
    }
}
