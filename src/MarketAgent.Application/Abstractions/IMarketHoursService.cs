namespace MarketAgent.Application.Abstractions;

public interface IMarketHoursService
{
    bool IsMarketOpen(DateTime utcNow);
}
