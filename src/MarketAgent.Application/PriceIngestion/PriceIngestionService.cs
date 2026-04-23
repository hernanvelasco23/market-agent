using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.PriceIngestion;

public sealed class PriceIngestionService : IPriceIngestionService
{
    private readonly IWatchlistProvider _watchlistProvider;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;

    public PriceIngestionService(
        IWatchlistProvider watchlistProvider,
        IMarketDataProvider marketDataProvider,
        IMarketSnapshotRepository marketSnapshotRepository)
    {
        _watchlistProvider = watchlistProvider;
        _marketDataProvider = marketDataProvider;
        _marketSnapshotRepository = marketSnapshotRepository;
    }

    public async Task<PriceIngestionResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var executedAtUtc = DateTime.UtcNow;
        var trackedAssets = await _watchlistProvider.GetTrackedAssetsAsync(cancellationToken);
        var failures = new List<PriceIngestionFailure>();
        var succeeded = 0;

        foreach (var asset in trackedAssets)
        {
            try
            {
                var marketData = await _marketDataProvider.GetLatestAsync(asset, cancellationToken);
                var snapshot = MapToSnapshot(marketData);

                await _marketSnapshotRepository.SaveAsync(snapshot, cancellationToken);
                succeeded++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new PriceIngestionFailure(
                    asset.Symbol,
                    exception.Message));
            }
        }

        return new PriceIngestionResult(
            trackedAssets.Count,
            succeeded,
            failures.Count,
            failures,
            executedAtUtc);
    }

    private static MarketSnapshot MapToSnapshot(MarketDataResult marketData)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            marketData.Symbol,
            marketData.AssetType,
            marketData.Price,
            marketData.Currency,
            marketData.CapturedAtUtc,
            marketData.Source,
            marketData.Volume,
            marketData.OpenPrice,
            marketData.HighPrice,
            marketData.LowPrice,
            marketData.PreviousClose);
    }
}
