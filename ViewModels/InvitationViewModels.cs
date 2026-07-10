using DailyPilot.Models;

namespace DailyPilot.ViewModels;

public class InviteListCard
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string EventIcon { get; set; } = "calendar-event";
    public int Total { get; set; }
    public int Invited { get; set; }
    public int Pending => Total - Invited;
    public int Percent => Total == 0 ? 0 : (int)System.Math.Round(Invited * 100.0 / Total);
}

public class InvitationsIndexViewModel
{
    public List<InviteListCard> Lists { get; set; } = new();
    public List<EventType> Events { get; set; } = new();
}

public class InviteListDetailsViewModel
{
    public InviteList List { get; set; } = new();
    /// <summary>Invitees ordered so not-yet-invited appear first.</summary>
    public List<Invitee> Invitees { get; set; } = new();

    public int Total => Invitees.Count;
    public int Invited => Invitees.Count(i => i.IsInvited);
    public int Pending => Total - Invited;
    public int Percent => Total == 0 ? 0 : (int)System.Math.Round(Invited * 100.0 / Total);
}
