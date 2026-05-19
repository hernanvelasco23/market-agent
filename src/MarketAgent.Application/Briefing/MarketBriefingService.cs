using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Briefing;

public sealed class MarketBriefingService : IMarketBriefingService
{
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketBriefingGenerator _marketBriefingGenerator;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;
    private readonly IHistoricalMarketDataService _historicalMarketDataService;

    public MarketBriefingService(
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketBriefingGenerator marketBriefingGenerator,
        IMarketSignalAnalyzer marketSignalAnalyzer,
        IHistoricalMarketDataService historicalMarketDataService)
    {
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketBriefingGenerator = marketBriefingGenerator;
        _marketSignalAnalyzer = marketSignalAnalyzer;
        _historicalMarketDataService = historicalMarketDataService;
    }

    public async Task<MarketBriefingResult> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            return new MarketBriefingResult(
                DateTime.UtcNow,
                "No data",
                "No market snapshots are available yet.",
                "No calculated market signals are available yet.",
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                new MarketBriefingDiagnostics(0, 0, []));
        }

        var candles = await GetHistoricalCandlesAsync(snapshots, cancellationToken);
        var signals = _marketSignalAnalyzer.Analyze(snapshots, candles);

        return await _marketBriefingGenerator.GenerateAsync(snapshots, signals, cancellationToken);
    }

    private async Task<IReadOnlyCollection<MarketCandle>> GetHistoricalCandlesAsync(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var candles = new List<MarketCandle>();

        foreach (var snapshot in snapshots
            .GroupBy(item => item.Symbol)
            .Select(group => group.OrderByDescending(item => item.CapturedAtUtc).First()))
        {
            try
            {
                var asset = new TrackedAsset(snapshot.Symbol, snapshot.AssetType, snapshot.Currency);
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
                // Briefing generation can still explain snapshot-based signals when history is unavailable.
            }
        }

        return candles;
    }
}
