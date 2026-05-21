using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.Signals;

public sealed class MarketSignalService : IMarketSignalService
{
    private static readonly TrackedAsset SpyBenchmark = new("SPY", AssetType.Etf, "USD");

    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly ISignalSnapshotHistoryRepository _signalSnapshotHistoryRepository;
    private readonly ILogger<MarketSignalService> _logger;

    public MarketSignalService(
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketSignalAnalyzer marketSignalAnalyzer,
        IHistoricalMarketDataService historicalMarketDataService,
        ISignalSnapshotHistoryRepository signalSnapshotHistoryRepository,
        ILogger<MarketSignalService> logger)
    {
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketSignalAnalyzer = marketSignalAnalyzer;
        _historicalMarketDataService = historicalMarketDataService;
        _signalSnapshotHistoryRepository = signalSnapshotHistoryRepository;
        _logger = logger;
    }

    public async Task<MarketSignalRunResult> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);
        var candles = await GetHistoricalCandlesAsync(snapshots, cancellationToken);
        var signals = _marketSignalAnalyzer.Analyze(snapshots, candles);
        LogNormalizedScores(signals);
        var generatedAtUtc = signals.Count > 0
            ? signals.Max(signal => signal.GeneratedAtUtc)
            : DateTime.UtcNow;
        var runId = Guid.NewGuid();

        await PersistSignalHistoryAsync(runId, generatedAtUtc, signals, cancellationToken);

        return new MarketSignalRunResult(generatedAtUtc, signals);
    }

    private void LogNormalizedScores(IReadOnlyCollection<MarketSignal> signals)
    {
        foreach (var signal in signals)
        {
            if (signal.RawScore is null || signal.RawScore == signal.Score)
            {
                continue;
            }

            _logger.LogInformation(
                "Signal score normalized for {Symbol} {Setup}: rawScore={RawScore}, calibratedScore={CalibratedScore}, normalizationDelta={NormalizationDelta}.",
                signal.Symbol,
                signal.SetupType,
                signal.RawScore,
                signal.Score,
                signal.Score - signal.RawScore.Value);
        }
    }

    private async Task PersistSignalHistoryAsync(
        Guid runId,
        DateTime generatedAtUtc,
        IReadOnlyCollection<MarketSignal> signals,
        CancellationToken cancellationToken)
    {
        try
        {
            await _signalSnapshotHistoryRepository.AppendAsync(
                runId,
                generatedAtUtc,
                signals,
                marketRegime: null,
                triggeredAlertsJson: null,
                source: "Scanner",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to persist signal history for scanner run {RunId}. Returning current scanner response.",
                runId);
        }
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
