namespace DailyPilot.Services;

/// <summary>
/// Entry points invoked by Hangfire recurring jobs. Kept thin so Hangfire only
/// needs to serialize a parameterless method call; real work lives in the
/// injected services.
/// </summary>
public class BackgroundJobs
{
    private readonly ICarryForwardService _carryForward;
    private readonly INotificationService _notifications;
    private readonly IBackupService _backup;

    public BackgroundJobs(ICarryForwardService carryForward, INotificationService notifications, IBackupService backup)
    {
        _carryForward = carryForward;
        _notifications = notifications;
        _backup = backup;
    }

    /// <summary>FR-005: nightly carry-forward + recurring task materialisation.</summary>
    public Task CarryForwardAsync() => _carryForward.RunForAllUsersAsync();

    /// <summary>FR-007: morning briefings.</summary>
    public Task MorningRemindersAsync() => _notifications.SendMorningBriefingsAsync();

    /// <summary>FR-006: end-of-day summaries.</summary>
    public Task EndOfDayRemindersAsync() => _notifications.SendEndOfDaySummariesAsync();

    /// <summary>PRD §9: daily database backup.</summary>
    public Task BackupDatabaseAsync() => _backup.BackupAsync();
}
