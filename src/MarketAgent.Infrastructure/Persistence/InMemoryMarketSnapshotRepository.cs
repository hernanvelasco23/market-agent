using MarketAgent.Application.Abstractions;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class InMemoryMarketSnapshotRepository : IMarketSnapshotRepository
{
    private readonly object _lock = new();
    private readonly List<MarketSnapshot> _snapshots = [];

    public Task SaveAsync(
        MarketSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _snapshots.Add(snapshot);
        }

        return Task.CompletedTask;
    }
}
