using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IMarketSnapshotRepository
{
    Task SaveAsync(
        MarketSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
