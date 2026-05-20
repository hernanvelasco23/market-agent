using MarketAgent.Application.Abstractions;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class InMemoryMarketSnapshotRepository : IMarketSnapshotRepository
{
    private readonly object _lock = new();
    private readonly List<MarketSnapshot> _snapshots = [];

    public Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyCollection<MarketSnapshot>>(_snapshots.ToArray());
        }
    }

    public Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Market snapshot symbol is required.", nameof(symbol));
        }

        if (fromUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Future snapshot start time must be UTC.", nameof(fromUtc));
        }

        if (toUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Future snapshot end time must be UTC.", nameof(toUtc));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyCollection<MarketSnapshot>>(
                _snapshots
                    .Where(snapshot => snapshot.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
                        snapshot.CapturedAtUtc >= fromUtc &&
                        snapshot.CapturedAtUtc <= toUtc)
                    .OrderBy(snapshot => snapshot.CapturedAtUtc)
                    .ToArray());
        }
    }

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
