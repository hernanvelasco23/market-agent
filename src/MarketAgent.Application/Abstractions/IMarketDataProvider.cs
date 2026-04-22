using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IMarketDataProvider
{
    Task<MarketDataResult> GetLatestAsync(
        TrackedAsset asset,
        CancellationToken cancellationToken = default);
}
