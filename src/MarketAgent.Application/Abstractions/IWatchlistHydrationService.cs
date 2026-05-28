using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IWatchlistHydrationService
{
    Task<WatchlistHydrationResult> HydrateAsync(
        WatchlistHydrationRequest request,
        CancellationToken cancellationToken = default);
}
