using MarketAgent.Application.Abstractions;

namespace MarketAgent.Application.SystemCycle;

public sealed class UsEquityMarketHoursService : IMarketHoursService
{
    private static readonly TimeOnly MarketOpen = new(9, 30);
    private static readonly TimeOnly MarketClose = new(16, 0);
    private static readonly TimeZoneInfo EasternTimeZone = ResolveEasternTimeZone();

    public bool IsMarketOpen(DateTime utcNow)
    {
        var utc = EnsureUtc(utcNow);
        var eastern = TimeZoneInfo.ConvertTimeFromUtc(utc, EasternTimeZone);

        if (eastern.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var time = TimeOnly.FromDateTime(eastern);
        return time >= MarketOpen && time <= MarketClose;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }
}
