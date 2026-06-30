namespace DailyPilot.Services;

/// <summary>Abstraction over "now" so background jobs/tests can be reasoned about.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public static class TimeZoneHelper
{
    /// <summary>Resolve a user's local "today" from their stored timezone id.</summary>
    public static DateOnly LocalToday(string timeZoneId, DateTime utcNow)
    {
        var tz = Resolve(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        return DateOnly.FromDateTime(local);
    }

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        if (TryFind(timeZoneId, out var tz)) return tz!;

        // Accept both IANA ("Asia/Kolkata") and Windows ("India Standard Time") ids on any OS.
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var winId) && TryFind(winId, out tz)) return tz!;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId) && TryFind(ianaId, out tz)) return tz!;

        return TimeZoneInfo.Utc;
    }

    /// <summary>Normalise an IANA id to a Windows id when possible (for storage on a Windows host).</summary>
    public static string Normalize(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return "UTC";
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var winId)) return winId;
        return timeZoneId;
    }

    private static bool TryFind(string? id, out TimeZoneInfo? tz)
    {
        tz = null;
        if (string.IsNullOrWhiteSpace(id)) return false;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch { return false; }
    }
}
