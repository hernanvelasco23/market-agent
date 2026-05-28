using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Watchlists;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class WatchlistHydrationServiceTests
{
    [Fact]
    public async Task HydrateAsync_SkipsFreshSnapshot_WhenForceIsFalse()
    {
        var now = DateTime.UtcNow;
        var repository = new RecordingMarketSnapshotRepository(
        [
            CreateSnapshot("NVDA", now.AddMinutes(-3), 100m)
        ]);
        var provider = new RecordingMarketDataProvider();
        var service = CreateService(
            repository,
            provider,
            marketOpen: true);

        var result = await service.HydrateAsync(new WatchlistHydrationRequest(["NVDA"]));
        var item = Assert.Single(result.Results);

        Assert.Equal(WatchlistHydrationStatuses.SkippedFresh, item.Status);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(repository.Saved);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task HydrateAsync_UpdatesSnapshotAndSignal_WhenSymbolNeedsHydration()
    {
        var repository = new RecordingMarketSnapshotRepository([]);
        var provider = new RecordingMarketDataProvider
        {
            Results =
            {
                ["NVDA"] = new MarketDataResult(
                    "NVDA",
                    AssetType.Equity,
                    123.45m,
                    "USD",
                    DateTime.UtcNow,
                    "Test")
            }
        };
        var analyzer = new RecordingMarketSignalAnalyzer();
        var signalHistory = new RecordingSignalSnapshotHistoryRepository();
        var service = CreateService(
            repository,
            provider,
            analyzer: analyzer,
            signalHistory: signalHistory);

        var result = await service.HydrateAsync(new WatchlistHydrationRequest(["NVDA"]));
        var item = Assert.Single(result.Results);

        Assert.Equal(WatchlistHydrationStatuses.Updated, item.Status);
        Assert.True(item.SnapshotCreated);
        Assert.True(item.SignalCreated);
        Assert.Equal(123.45m, item.CurrentPrice);
        Assert.Single(repository.Saved);
        Assert.Single(signalHistory.AppendedSignals);
        Assert.Equal(1, result.UpdatedCount);
    }

    [Fact]
    public async Task HydrateAsync_ReturnsInvalidSymbol_ForUnknownSymbol()
    {
        var service = CreateService(
            new RecordingMarketSnapshotRepository([]),
            new RecordingMarketDataProvider());

        var result = await service.HydrateAsync(new WatchlistHydrationRequest(["NOTREAL"]));
        var item = Assert.Single(result.Results);

        Assert.Equal(WatchlistHydrationStatuses.InvalidSymbol, item.Status);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public async Task HydrateAsync_ReturnsNoData_WhenProviderHasNoMarketData()
    {
        var provider = new RecordingMarketDataProvider();
        var service = CreateService(
            new RecordingMarketSnapshotRepository([]),
            provider);

        var result = await service.HydrateAsync(new WatchlistHydrationRequest(["NVDA"]));
        var item = Assert.Single(result.Results);

        Assert.Equal(WatchlistHydrationStatuses.NoData, item.Status);
        Assert.Equal(1, result.NoDataCount);
    }

    private static WatchlistHydrationService CreateService(
        RecordingMarketSnapshotRepository repository,
        RecordingMarketDataProvider provider,
        RecordingMarketSignalAnalyzer? analyzer = null,
        RecordingSignalSnapshotHistoryRepository? signalHistory = null,
        bool marketOpen = true)
    {
        return new WatchlistHydrationService(
            new StubWatchlistProvider(),
            new StubMarketDataProviderResolver(provider),
            repository,
            analyzer ?? new RecordingMarketSignalAnalyzer(),
            new StubHistoricalMarketDataService(),
            signalHistory ?? new RecordingSignalSnapshotHistoryRepository(),
            new StubMarketHoursService(marketOpen),
            NullLogger<WatchlistHydrationService>.Instance);
    }

    private static MarketSnapshot CreateSnapshot(
        string symbol,
        DateTime capturedAtUtc,
        decimal price)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            symbol,
            AssetType.Equity,
            price,
            "USD",
            DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc),
            "Test");
    }

    private sealed class StubWatchlistProvider : IWatchlistProvider
    {
        public Task<IReadOnlyCollection<TrackedAsset>> GetTrackedAssetsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<TrackedAsset>>(
            [
                new("NVDA", AssetType.Equity, "USD"),
                new("SPY", AssetType.Etf, "USD")
            ]);
        }
    }

    private sealed class StubMarketDataProviderResolver : IMarketDataProviderResolver
    {
        private readonly RecordingMarketDataProvider _provider;

        public StubMarketDataProviderResolver(RecordingMarketDataProvider provider)
        {
            _provider = provider;
        }

        public IMarketDataProvider Resolve(TrackedAsset asset)
        {
            return _provider;
        }
    }

    private sealed class RecordingMarketDataProvider : IMarketDataProvider
    {
        public Dictionary<string, MarketDataResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int CallCount { get; private set; }

        public bool CanHandle(TrackedAsset asset)
        {
            return true;
        }

        public Task<MarketDataResult> GetLatestAsync(
            TrackedAsset asset,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (!Results.TryGetValue(asset.Symbol, out var result))
            {
                throw new InvalidOperationException("No market data available.");
            }

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingMarketSnapshotRepository : IMarketSnapshotRepository
    {
        private readonly List<MarketSnapshot> _snapshots;

        public RecordingMarketSnapshotRepository(IReadOnlyCollection<MarketSnapshot> snapshots)
        {
            _snapshots = snapshots.ToList();
        }

        public List<MarketSnapshot> Saved { get; } = [];

        public Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<MarketSnapshot>>(_snapshots.Concat(Saved).ToArray());
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<MarketSnapshot>>([]);
        }

        public Task SaveAsync(
            MarketSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Saved.Add(snapshot);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMarketSignalAnalyzer : IMarketSignalAnalyzer
    {
        public IReadOnlyCollection<MarketSignal> Analyze(
            IReadOnlyCollection<MarketSnapshot> snapshots,
            IReadOnlyCollection<MarketCandle>? candles = null)
        {
            return snapshots
                .Where(snapshot => !snapshot.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase))
                .Select(snapshot => new MarketSignal(
                    snapshot.Symbol,
                    snapshot.AssetType,
                    MarketSignalType.Bullish,
                    88m,
                    "MomentumContinuation",
                    "Test signal",
                    "Candidate",
                    "Intraday",
                    "High",
                    0.5m,
                    rsi: null,
                    ema9: null,
                    ema20: null,
                    ema50: null,
                    atr14: null,
                    averageVolume10: null,
                    averageVolume20: null,
                    aboveVwap: null,
                    relativeStrengthVsSpy: null,
                    relativeVolume: null,
                    recoveryFromLowPercent: null,
                    strongIntradayRecovery: false,
                    gapPercent: null,
                    gapRecovery: false,
                    openingRedReversalDetected: false,
                    openGapPercent: null,
                    openingRedReversalRecoveryFromLowPercent: null,
                    reclaimOpen: false,
                    reclaimPreviousClose: false,
                    ema20Slope: null,
                    ema50Slope: null,
                    strongTrendSlope: false,
                    distanceFromEma20Percent: null,
                    extensionRisk: null,
                    momentumContinuation: true,
                    drawdown: null,
                    entry: snapshot.Price,
                    stop: snapshot.Price * 0.98m,
                    target: snapshot.Price * 1.04m,
                    takeProfit1: snapshot.Price * 1.02m,
                    takeProfit2: snapshot.Price * 1.04m,
                    takeProfit3: snapshot.Price * 1.06m,
                    riskReward1: 1m,
                    riskReward2: 2m,
                    riskReward3: 3m,
                    riskPerShare: null,
                    maxRiskAmount: null,
                    suggestedPositionSize: null,
                    regimeSizingMultiplier: null,
                    scoreBreakdown: [],
                    generatedAtUtc: DateTime.UtcNow,
                    rawScore: 90m))
                .ToArray();
        }
    }

    private sealed class StubHistoricalMarketDataService : IHistoricalMarketDataService
    {
        public Task<HistoricalMarketDataResult> GetWatchlistCandlesAsync(
            int days,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HistoricalMarketDataResult(DateTime.UtcNow, days, [], []));
        }

        public Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
            TrackedAsset asset,
            int days,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<MarketCandle>>([]);
        }
    }

    private sealed class RecordingSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
    {
        public List<MarketSignal> AppendedSignals { get; } = [];

        public Task AppendAsync(
            Guid runId,
            DateTime createdAtUtc,
            IReadOnlyCollection<MarketSignal> signals,
            string? marketRegime,
            string? triggeredAlertsJson,
            string source,
            CancellationToken cancellationToken = default)
        {
            AppendedSignals.AddRange(signals);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AlertEvaluationCandidate>>([]);
        }

        public Task<MarketSignalRunResult?> GetLatestRunAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MarketSignalRunResult?>(null);
        }

        public Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
            Guid signalSnapshotId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ScoreAttributionSnapshot?>(null);
        }
    }

    private sealed class StubMarketHoursService : IMarketHoursService
    {
        private readonly bool _marketOpen;

        public StubMarketHoursService(bool marketOpen)
        {
            _marketOpen = marketOpen;
        }

        public bool IsMarketOpen(DateTime utcNow)
        {
            return _marketOpen;
        }
    }
}
