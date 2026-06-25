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
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
