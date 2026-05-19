using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IHistoricalMarketDataService
{
    Task<HistoricalMarketDataResult> GetWatchlistCandlesAsync(
        int days,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
        TrackedAsset asset,
        int days,
        CancellationToken cancellationToken = default);
}
