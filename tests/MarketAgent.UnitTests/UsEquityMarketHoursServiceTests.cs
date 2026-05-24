using MarketAgent.Application.SystemCycle;

namespace MarketAgent.UnitTests;

public sealed class UsEquityMarketHoursServiceTests
{
    [Fact]
    public void IsMarketOpen_ReturnsTrue_DuringRegularMarketHours()
    {
        var service = new UsEquityMarketHoursService();

        var result = service.IsMarketOpen(new DateTime(2026, 5, 20, 15, 0, 0, DateTimeKind.Utc));

        Assert.True(result);
    }

    [Fact]
    public void IsMarketOpen_ReturnsFalse_AfterRegularMarketHours()
    {
        var service = new UsEquityMarketHoursService();

        var result = service.IsMarketOpen(new DateTime(2026, 5, 20, 21, 0, 0, DateTimeKind.Utc));

        Assert.False(result);
    }

    [Fact]
    public void IsMarketOpen_ReturnsFalse_OnWeekend()
    {
        var service = new UsEquityMarketHoursService();

        var result = service.IsMarketOpen(new DateTime(2026, 5, 23, 15, 0, 0, DateTimeKind.Utc));

        Assert.False(result);
    }
}
