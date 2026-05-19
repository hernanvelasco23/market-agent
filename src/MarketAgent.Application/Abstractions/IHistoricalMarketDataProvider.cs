using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IHistoricalMarketDataProvider
{
    bool CanHandle(TrackedAsset asset);

    Task<IReadOnlyCollection<MarketCandle>> GetDailyCandlesAsync(
        TrackedAsset asset,
        int days,
        CancellationToken cancellationToken = default);
}
