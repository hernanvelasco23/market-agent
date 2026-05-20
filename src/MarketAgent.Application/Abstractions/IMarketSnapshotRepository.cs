using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IMarketSnapshotRepository
{
    Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        MarketSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
