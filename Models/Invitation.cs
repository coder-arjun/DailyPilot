using System.ComponentModel.DataAnnotations;

namespace DailyPilot.Models;

/// <summary>
/// A reusable event category for invitation lists (Birthday, Wedding, …). Managed
/// via the "event master" form; each user gets a seeded default set they can extend.
/// </summary>
public class EventType
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Bootstrap icon name (without the "bi-" prefix), e.g. "gift".</summary>
    [StringLength(40)]
    public string Icon { get; set; } = "calendar-event";

    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An invitation list — a named group of people (e.g. "Office colleagues") for a
/// specific event, so nobody gets missed. Invitees are tracked as invited / not yet.
/// </summary>
public class InviteList
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>Snapshot of the chosen event name (decoupled from EventType lifecycle).</summary>
    [StringLength(60)]
    public string EventName { get; set; } = string.Empty;

    /// <summary>Icon snapshot for display.</summary>
    [StringLength(40)]
    public string EventIcon { get; set; } = "calendar-event";

    [StringLength(300)]
    public string? Notes { get; set; }

    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invitee> Invitees { get; set; } = new List<Invitee>();
}

/// <summary>A single person on an invitation list.</summary>
public class Invitee
{
    public int Id { get; set; }

    public int InviteListId { get; set; }
    public InviteList? InviteList { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    public string? Contact { get; set; }

    public bool IsInvited { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? InvitedAt { get; set; }
}
