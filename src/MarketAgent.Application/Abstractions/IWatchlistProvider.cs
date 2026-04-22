using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IWatchlistProvider
{
    Task<IReadOnlyCollection<TrackedAsset>> GetTrackedAssetsAsync(
        CancellationToken cancellationToken = default);
}
