using MarketAgent.Application.Abstractions;
using MarketAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class EfMarketSnapshotRepository : IMarketSnapshotRepository
{
    private readonly MarketAgentDbContext _dbContext;

    public EfMarketSnapshotRepository(MarketAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var latestKeys = await _dbContext.MarketSnapshots
            .AsNoTracking()
            .GroupBy(snapshot => snapshot.Symbol)
            .Select(group => new
            {
                Symbol = group.Key,
                CapturedAtUtc = group.Max(snapshot => snapshot.CapturedAtUtc)
            })
            .ToArrayAsync(cancellationToken);

        var latest = new List<MarketSnapshot>();

        foreach (var key in latestKeys)
        {
            var snapshot = await _dbContext.MarketSnapshots
                .AsNoTracking()
                .Where(item => item.Symbol == key.Symbol &&
                    item.CapturedAtUtc == key.CapturedAtUtc)
                .OrderByDescending(item => item.Id)
                .FirstAsync(cancellationToken);

            latest.Add(ToDomain(snapshot));
        }

        return latest
            .OrderBy(snapshot => snapshot.Symbol)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
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

        var snapshots = await _dbContext.MarketSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Symbol == symbol.Trim().ToUpperInvariant() &&
                snapshot.CapturedAtUtc >= fromUtc &&
                snapshot.CapturedAtUtc <= toUtc)
            .OrderBy(snapshot => snapshot.CapturedAtUtc)
            .ToArrayAsync(cancellationToken);

        return snapshots
            .Select(ToDomain)
            .ToArray();
    }

    public async Task SaveAsync(
        MarketSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var exists = await _dbContext.MarketSnapshots.AnyAsync(
            item => item.Symbol == snapshot.Symbol &&
                item.Source == snapshot.Source &&
                item.CapturedAtUtc == snapshot.CapturedAtUtc,
            cancellationToken);

        if (exists)
        {
            return;
        }

        await _dbContext.MarketSnapshots.AddAsync(ToEntity(snapshot), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PersistedMarketSnapshot ToEntity(MarketSnapshot snapshot)
    {
        return new PersistedMarketSnapshot
        {
            Id = snapshot.Id,
            Symbol = snapshot.Symbol,
            AssetType = snapshot.AssetType,
            Price = snapshot.Price,
            Currency = snapshot.Currency,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Source = snapshot.Source,
            Volume = snapshot.Volume,
            OpenPrice = snapshot.OpenPrice,
            HighPrice = snapshot.HighPrice,
            LowPrice = snapshot.LowPrice,
            PreviousClose = snapshot.PreviousClose
        };
    }

    private static MarketSnapshot ToDomain(PersistedMarketSnapshot snapshot)
    {
        return new MarketSnapshot(
            snapshot.Id,
            snapshot.Symbol,
            snapshot.AssetType,
            snapshot.Price,
            snapshot.Currency,
            DateTime.SpecifyKind(snapshot.CapturedAtUtc, DateTimeKind.Utc),
            snapshot.Source,
            snapshot.Volume,
            snapshot.OpenPrice,
            snapshot.HighPrice,
            snapshot.LowPrice,
            snapshot.PreviousClose);
    }
}
