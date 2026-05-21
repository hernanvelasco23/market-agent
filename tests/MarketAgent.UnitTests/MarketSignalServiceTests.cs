using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class MarketSignalServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesSpyBenchmarkCandles_WhenSpySnapshotIsMissing()
    {
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot(
                "MSFT",
                price: 105m,
                openPrice: 104m,
                highPrice: 106m,
                lowPrice: 101m,
                previousClose: 100m)
        ]);
        var history = new StubHistoricalMarketDataService();
        var analyzer = new TechnicalMarketSignalAnalyzer(new EmptyTechnicalIndicatorService(), new RiskPositionOptions());
        var signalHistory = new RecordingSignalSnapshotHistoryRepository();
        var service = new MarketSignalService(
            snapshots,
            analyzer,
            history,
            signalHistory,
            NullLogger<MarketSignalService>.Instance);

        var result = await service.GenerateAsync();
        var signal = Assert.Single(result.Signals);

        Assert.Contains("MSFT", history.RequestedSymbols);
        Assert.Contains("SPY", history.RequestedSymbols);
        Assert.Equal(3m, signal.RelativeStrengthVsSpy);
    }

    [Fact]
    public async Task GenerateAsync_PersistsGeneratedSignalSnapshots()
    {
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot(
                "MSFT",
                price: 105m,
                openPrice: 104m,
                highPrice: 106m,
                lowPrice: 101m,
                previousClose: 100m)
        ]);
        var signalHistory = new RecordingSignalSnapshotHistoryRepository();
        var service = new MarketSignalService(
            snapshots,
            new TechnicalMarketSignalAnalyzer(new EmptyTechnicalIndicatorService(), new RiskPositionOptions()),
            new StubHistoricalMarketDataService(),
            signalHistory,
            NullLogger<MarketSignalService>.Instance);

        var result = await service.GenerateAsync();

        Assert.NotEqual(Guid.Empty, signalHistory.RunId);
        Assert.Equal(result.GeneratedAtUtc, signalHistory.CreatedAtUtc);
        Assert.Equal("Scanner", signalHistory.Source);
        Assert.Null(signalHistory.MarketRegime);
        Assert.Null(signalHistory.TriggeredAlertsJson);
        Assert.Single(signalHistory.Signals);
    }

    [Fact]
    public async Task GenerateAsync_PassesCalibratedSignalScoreToPersistence()
    {
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot(
                "NVDA",
                price: 120m,
                openPrice: 100m,
                highPrice: 121m,
                lowPrice: 90m,
                previousClose: 100m,
                volume: 1500000m)
        ]);
        var signalHistory = new RecordingSignalSnapshotHistoryRepository();
        var service = new MarketSignalService(
            snapshots,
            new TechnicalMarketSignalAnalyzer(
                new StubTechnicalIndicatorService(new TechnicalIndicators(
                    Ema9: 112m,
                    Ema20: 100m,
                    Ema50: 90m,
                    Rsi14: 70m,
                    Atr14: 4m,
                    AverageVolume10: 1000000m,
                    AverageVolume20: 500000m)),
                new RiskPositionOptions()),
            new StubHistoricalMarketDataService(),
            signalHistory,
            NullLogger<MarketSignalService>.Instance);

        var result = await service.GenerateAsync();
        var generatedSignal = Assert.Single(result.Signals);
        var persistedSignal = Assert.Single(signalHistory.Signals);

        Assert.True(generatedSignal.RawScore > 85m);
        Assert.Equal(generatedSignal.Score, persistedSignal.Score);
        Assert.Equal(generatedSignal.RawScore, persistedSignal.RawScore);
        Assert.True(persistedSignal.Score < persistedSignal.RawScore);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSignals_WhenPersistenceFails()
    {
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot(
                "MSFT",
                price: 105m,
                openPrice: 104m,
                highPrice: 106m,
                lowPrice: 101m,
                previousClose: 100m)
        ]);
        var service = new MarketSignalService(
            snapshots,
            new TechnicalMarketSignalAnalyzer(new EmptyTechnicalIndicatorService(), new RiskPositionOptions()),
            new StubHistoricalMarketDataService(),
            new ThrowingSignalSnapshotHistoryRepository(new InvalidOperationException("SQL unavailable")),
            NullLogger<MarketSignalService>.Instance);

        var result = await service.GenerateAsync();

        Assert.Single(result.Signals);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotSwallowPersistenceCancellation()
    {
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot(
                "MSFT",
                price: 105m,
                openPrice: 104m,
                highPrice: 106m,
                lowPrice: 101m,
                previousClose: 100m)
        ]);
        var service = new MarketSignalService(
            snapshots,
            new TechnicalMarketSignalAnalyzer(new EmptyTechnicalIndicatorService(), new RiskPositionOptions()),
            new StubHistoricalMarketDataService(),
            new ThrowingSignalSnapshotHistoryRepository(new OperationCanceledException()),
            NullLogger<MarketSignalService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GenerateAsync());
    }

    private static MarketSnapshot CreateSnapshot(
        string symbol,
        decimal price,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal previousClose,
        decimal volume = 1000000m)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            symbol,
            AssetType.Equity,
            price,
            "USD",
            DateTime.SpecifyKind(new DateTime(2026, 5, 18, 15, 0, 0), DateTimeKind.Utc),
            "UnitTest",
            volume,
            openPrice,
            highPrice,
            lowPrice,
            previousClose);
    }

    private sealed class StubMarketSnapshotRepository : IMarketSnapshotRepository
    {
        private readonly IReadOnlyCollection<MarketSnapshot> _snapshots;

        public StubMarketSnapshotRepository(IReadOnlyCollection<MarketSnapshot> snapshots)
        {
            _snapshots = snapshots;
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshots);
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<MarketSnapshot>>(
                _snapshots
                    .Where(snapshot => snapshot.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
                        snapshot.CapturedAtUtc >= fromUtc &&
                        snapshot.CapturedAtUtc <= toUtc)
                    .OrderBy(snapshot => snapshot.CapturedAtUtc)
                    .ToArray());
        }

        public Task SaveAsync(MarketSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubHistoricalMarketDataService : IHistoricalMarketDataService
    {
        private readonly List<string> _requestedSymbols = [];
        private readonly IReadOnlyCollection<MarketCandle>? _candles;

        public StubHistoricalMarketDataService(IReadOnlyCollection<MarketCandle>? candles = null)
        {
            _candles = candles;
        }

        public IReadOnlyCollection<string> RequestedSymbols => _requestedSymbols;

        public Task<HistoricalMarketDataResult> GetWatchlistCandlesAsync(
            int days,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
            TrackedAsset asset,
            int days,
            CancellationToken cancellationToken = default)
        {
            _requestedSymbols.Add(asset.Symbol);

            IReadOnlyCollection<MarketCandle> candles = _candles is not null &&
                    !asset.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase)
                ? _candles
                : asset.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase)
                ? CreateSpyCandles()
                : [];

            return Task.FromResult(candles);
        }

        private static IReadOnlyCollection<MarketCandle> CreateSpyCandles()
        {
            return
            [
                new MarketCandle(
                    "SPY",
                    AssetType.Etf,
                    DateTime.SpecifyKind(new DateTime(2026, 5, 17), DateTimeKind.Utc),
                    100m,
                    100m,
                    100m,
                    100m,
                    1000000m,
                    "UnitTest"),
                new MarketCandle(
                    "SPY",
                    AssetType.Etf,
                    DateTime.SpecifyKind(new DateTime(2026, 5, 18), DateTimeKind.Utc),
                    102m,
                    102m,
                    102m,
                    102m,
                    1000000m,
                    "UnitTest")
            ];
        }
    }

    private sealed class EmptyTechnicalIndicatorService : ITechnicalIndicatorService
    {
        public TechnicalIndicators Calculate(IReadOnlyCollection<MarketCandle> candles)
        {
            return new TechnicalIndicators(null, null, null, null, null, null, null);
        }
    }

    private sealed class StubTechnicalIndicatorService : ITechnicalIndicatorService
    {
        private readonly TechnicalIndicators _indicators;

        public StubTechnicalIndicatorService(TechnicalIndicators indicators)
        {
            _indicators = indicators;
        }

        public TechnicalIndicators Calculate(IReadOnlyCollection<MarketCandle> candles)
        {
            return _indicators;
        }
    }

    private sealed class RecordingSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
    {
        public Guid RunId { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        public IReadOnlyCollection<MarketSignal> Signals { get; private set; } = [];

        public string? MarketRegime { get; private set; }

        public string? TriggeredAlertsJson { get; private set; }

        public string? Source { get; private set; }

        public Task AppendAsync(
            Guid runId,
            DateTime createdAtUtc,
            IReadOnlyCollection<MarketSignal> signals,
            string? marketRegime,
            string? triggeredAlertsJson,
            string source,
            CancellationToken cancellationToken = default)
        {
            RunId = runId;
            CreatedAtUtc = createdAtUtc;
            Signals = signals;
            MarketRegime = marketRegime;
            TriggeredAlertsJson = triggeredAlertsJson;
            Source = source;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
            Guid signalSnapshotId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
    {
        private readonly Exception _exception;

        public ThrowingSignalSnapshotHistoryRepository(Exception exception)
        {
            _exception = exception;
        }

        public Task AppendAsync(
            Guid runId,
            DateTime createdAtUtc,
            IReadOnlyCollection<MarketSignal> signals,
            string? marketRegime,
            string? triggeredAlertsJson,
            string source,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException(_exception);
        }

        public Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
            Guid signalSnapshotId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
