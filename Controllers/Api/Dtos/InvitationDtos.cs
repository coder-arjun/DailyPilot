using System.ComponentModel.DataAnnotations;
using DailyPilot.Models;

namespace DailyPilot.Controllers.Api.Dtos;

/// <summary>Card shape for `GET api/v1/invitations` — mirrors <c>InviteListCard</c> (Views/Invitations/Index).</summary>
public sealed record InviteListSummaryDto(
    int Id,
    string GroupName,
    string EventName,
    string EventIcon,
    int Total,
    int Invited,
    int Pending,
    int Percent,
    string CreatedAt)
{
    public static InviteListSummaryDto From(
        int id, string groupName, string eventName, string eventIcon, DateTime createdAt, int total, int invited) => new(
        id, groupName, eventName, eventIcon,
        total, invited, total - invited,
        total == 0 ? 0 : (int)Math.Round(invited * 100.0 / total),
        createdAt.ToString("O"));
}

/// <summary>Mirrors <c>InvitationsController.Create(groupName, eventTypeId)</c>.</summary>
public sealed record InviteListCreateRequest(
    [Required, StringLength(100)] string GroupName,
    int EventTypeId);

/// <summary>
/// Per-invitee wire shape, incl. <see cref="WaShareText"/> — a WhatsApp invite message.
/// NOTE: the web app has no WhatsApp share today (checked Views/Invitations/*.cshtml and
/// wwwroot/js; the only "whatsapp" hit anywhere is an HTML comment in _Layout.cshtml about
/// link-preview metadata, unrelated to sending invites). This template is new, written for
/// the mobile share button. The mobile screen turns it into a URL itself: a phone-looking
/// Contact (7+ digits) → https://wa.me/&lt;digits&gt;?text=..., otherwise the recipient-less
/// https://api.whatsapp.com/send?text=... (opens WhatsApp's own contact picker).
/// </summary>
public sealed record InviteeDto(
    int Id,
    string Name,
    string? Contact,
    bool IsInvited,
    string? InvitedAt,
    string CreatedAt,
    string WaShareText)
{
    public static InviteeDto From(Invitee i, string eventName, string groupName) => new(
        i.Id,
        i.Name,
        i.Contact,
        i.IsInvited,
        i.InvitedAt?.ToString("O"),
        i.CreatedAt.ToString("O"),
        BuildWaShareText(i.Name, eventName, groupName));

    private static string BuildWaShareText(string name, string eventName, string groupName) =>
        $"Hi {name}! You're invited to {eventName} — {groupName}. Hope to see you there! 🎉";
}

/// <summary>Mirrors <c>InvitationsController.AddInvitee(listId, name, contact)</c>.</summary>
public sealed record InviteeCreateRequest(
    [Required, StringLength(120)] string Name,
    [StringLength(60)] string? Contact);

/// <summary>Detail shape for `GET api/v1/invitations/{id}` — mirrors <c>InviteListDetailsViewModel</c>.</summary>
public sealed record InviteListDetailDto(
    int Id,
    string GroupName,
    string EventName,
    string EventIcon,
    string? Notes,
    string CreatedAt,
    int Total,
    int Invited,
    int Pending,
    int Percent,
    List<InviteeDto> Invitees)
{
    public static InviteListDetailDto From(InviteList l)
    {
        // Not-yet-invited first, then by name — same ordering as InviteListDetailsViewModel.
        var invitees = l.Invitees
            .OrderBy(i => i.IsInvited)
            .ThenBy(i => i.Name)
            .Select(i => InviteeDto.From(i, l.EventName, l.GroupName))
            .ToList();
        var total = invitees.Count;
        var invited = invitees.Count(i => i.IsInvited);
        return new(
            l.Id, l.GroupName, l.EventName, l.EventIcon, l.Notes, l.CreatedAt.ToString("O"),
            total, invited, total - invited,
            total == 0 ? 0 : (int)Math.Round(invited * 100.0 / total),
            invitees);
    }
}

public sealed record EventTypeDto(int Id, string Name, string Icon)
{
    public static EventTypeDto From(EventType e) => new(e.Id, e.Name, e.Icon);
}

/// <summary>Mirrors <c>InvitationsController.CreateEvent(name, icon)</c>.</summary>
public sealed record EventTypeCreateRequest(
    [Required, StringLength(60)] string Name,
    [StringLength(40)] string? Icon);
