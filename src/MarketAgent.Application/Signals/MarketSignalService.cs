using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Application.Signals;

public sealed class MarketSignalService : IMarketSignalService
{
    private static readonly TrackedAsset SpyBenchmark = new("SPY", AssetType.Etf, "USD");

    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;
    private readonly IHistoricalMarketDataService _historicalMarketDataService;

    public MarketSignalService(
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketSignalAnalyzer marketSignalAnalyzer,
        IHistoricalMarketDataService historicalMarketDataService)
    {
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketSignalAnalyzer = marketSignalAnalyzer;
        _historicalMarketDataService = historicalMarketDataService;
    }

    public async Task<MarketSignalRunResult> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);
        var candles = await GetHistoricalCandlesAsync(snapshots, cancellationToken);
        var signals = _marketSignalAnalyzer.Analyze(snapshots, candles);
        var generatedAtUtc = signals.Count > 0
            ? signals.Max(signal => signal.GeneratedAtUtc)
            : DateTime.UtcNow;

        return new MarketSignalRunResult(generatedAtUtc, signals);
    }

    private async Task<IReadOnlyCollection<MarketCandle>> GetHistoricalCandlesAsync(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var candles = new List<MarketCandle>();

        var assets = snapshots
            .GroupBy(item => item.Symbol)
            .Select(group => group.OrderByDescending(item => item.CapturedAtUtc).First())
            .Select(snapshot => new TrackedAsset(snapshot.Symbol, snapshot.AssetType, snapshot.Currency))
            .ToList();

        if (!assets.Any(asset => asset.Symbol.Equals(SpyBenchmark.Symbol, StringComparison.OrdinalIgnoreCase)))
        {
            assets.Add(SpyBenchmark);
        }

        foreach (var asset in assets)
        {
            try
            {
                candles.AddRange(await _historicalMarketDataService.GetCandlesAsync(
                    asset,
                    HistoricalMarketDataService.DefaultDays,
                    cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Historical data enriches signals, but missing history must not break latest snapshot analysis.
            }
        }

        return candles;
    }
}
