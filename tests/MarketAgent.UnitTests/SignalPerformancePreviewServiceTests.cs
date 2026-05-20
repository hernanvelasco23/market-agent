using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.UnitTests;

public sealed class SignalPerformancePreviewServiceTests
{
    [Fact]
    public async Task GenerateAsync_CalculatesForwardReturnsAndWinRate()
    {
        var history = new StubHistoricalMarketDataService(CreateCandles("MSFT", 26));
        var analyzer = new StubMarketSignalAnalyzer(_ => CreateSignal("MSFT", "MomentumContinuation", momentumContinuation: true));
        var service = new SignalPerformancePreviewService(history, analyzer);

        var result = await service.GenerateAsync(180);
        var item = GetItem(result, "MomentumContinuation");

        Assert.Equal(5, item.SampleCount);
        Assert.False(item.IsInsufficientData);
        Assert.True(item.HasLowSampleWarning);
        Assert.Equal(0.82m, item.AverageForwardReturn1Day);
        Assert.Equal(2.48m, item.AverageForwardReturn3Day);
        Assert.Equal(4.17m, item.AverageForwardReturn5Day);
        Assert.Equal(100m, item.WinRate1Day);
        Assert.Null(item.WinRate3Day);
        Assert.Null(item.WinRate5Day);
    }

    [Fact]
    public async Task GenerateAsync_TreatsMissingForwardHorizonsAsNull()
    {
        var history = new StubHistoricalMarketDataService(CreateCandles("MSFT", 22));
        var analyzer = new StubMarketSignalAnalyzer(_ => CreateSignal("MSFT", "MomentumContinuation", momentumContinuation: true));
        var service = new SignalPerformancePreviewService(history, analyzer);

        var result = await service.GenerateAsync(180);
        var item = GetItem(result, "MomentumContinuation");

        Assert.Equal(1, item.SampleCount);
        Assert.True(item.IsInsufficientData);
        Assert.True(item.HasLowSampleWarning);
        Assert.Equal(0.83m, item.AverageForwardReturn1Day);
        Assert.Null(item.AverageForwardReturn3Day);
        Assert.Null(item.AverageForwardReturn5Day);
        Assert.Null(item.WinRate1Day);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotPassFutureCandlesToAnalyzer()
    {
        var history = new StubHistoricalMarketDataService(CreateCandles("MSFT", 24));
        var analyzer = new StubMarketSignalAnalyzer(snapshot => CreateSignal(snapshot.Symbol, "Pullback"));
        var service = new SignalPerformancePreviewService(history, analyzer);

        await service.GenerateAsync(180);

        Assert.NotEmpty(analyzer.AnalyzerCalls);
        Assert.All(analyzer.AnalyzerCalls, call =>
            Assert.True(call.MaxCandleDate <= call.SnapshotDate));
    }

    [Fact]
    public async Task GenerateAsync_MapsOpeningRedReversalSamples()
    {
        var history = new StubHistoricalMarketDataService(CreateCandles("RKLB", 24));
        var analyzer = new StubMarketSignalAnalyzer(snapshot => CreateSignal(
            snapshot.Symbol,
            "MomentumContinuation",
            openingRedReversalDetected: true));
        var service = new SignalPerformancePreviewService(history, analyzer);

        var result = await service.GenerateAsync(180);

        Assert.True(GetItem(result, "OpeningRedReversal").SampleCount > 0);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsInsufficientItems_WhenCandlesAreMissing()
    {
        var history = new StubHistoricalMarketDataService([]);
        var analyzer = new StubMarketSignalAnalyzer(snapshot => CreateSignal(snapshot.Symbol, "MomentumContinuation"));
        var service = new SignalPerformancePreviewService(history, analyzer);

        var result = await service.GenerateAsync(180);

        Assert.All(result.Items, item =>
        {
            Assert.Equal(0, item.SampleCount);
            Assert.True(item.IsInsufficientData);
            Assert.False(item.HasLowSampleWarning);
        });
    }

    private static SignalPerformancePreviewItem GetItem(
        SignalPerformancePreviewResult result,
        string signalType)
    {
        return Assert.Single(result.Items, item => item.SignalType == signalType);
    }

    private static IReadOnlyCollection<MarketCandle> CreateCandles(
        string symbol,
        int count)
    {
        var start = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

        return Enumerable
            .Range(0, count)
            .Select(index =>
            {
                var close = 100m + index;

                return new MarketCandle(
                    symbol,
                    AssetType.Equity,
                    start.AddDays(index),
                    close - 0.5m,
                    close + 1m,
                    close - 1m,
                    close,
                    1000000m,
                    "UnitTest");
            })
            .Concat(CreateSpyCandles(start, count))
            .ToArray();
    }

    private static IEnumerable<MarketCandle> CreateSpyCandles(DateTime start, int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(index =>
            {
                var close = 400m + index;

                return new MarketCandle(
                    "SPY",
                    AssetType.Etf,
                    start.AddDays(index),
                    close - 0.5m,
                    close + 1m,
                    close - 1m,
                    close,
                    5000000m,
                    "UnitTest");
            });
    }

    private static MarketSignal CreateSignal(
        string symbol,
        string setupType,
        bool momentumContinuation = false,
        bool openingRedReversalDetected = false)
    {
        return new MarketSignal(
            symbol,
            AssetType.Equity,
            MarketSignalType.Bullish,
            65m,
            setupType,
            "unit test signal",
            "Candidate",
            "SwingCandidate",
            "Medium",
            0.5m,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            false,
            openingRedReversalDetected,
            openingRedReversalDetected ? -1m : null,
            openingRedReversalDetected ? 2m : null,
            openingRedReversalDetected,
            false,
            null,
            null,
            false,
            setupType == "ExtendedMomentum" ? 8m : null,
            setupType == "ExtendedMomentum" ? "High" : null,
            momentumContinuation,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            DateTime.UtcNow);
    }

    private sealed class StubHistoricalMarketDataService : IHistoricalMarketDataService
    {
        private readonly IReadOnlyCollection<MarketCandle> _candles;

        public StubHistoricalMarketDataService(IReadOnlyCollection<MarketCandle> candles)
        {
            _candles = candles;
        }

        public Task<HistoricalMarketDataResult> GetWatchlistCandlesAsync(
            int days,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HistoricalMarketDataResult(
                DateTime.UtcNow,
                days,
                _candles,
                []));
        }

        public Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
            TrackedAsset asset,
            int days,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubMarketSignalAnalyzer : IMarketSignalAnalyzer
    {
        private readonly Func<MarketSnapshot, MarketSignal> _signalFactory;
        private readonly List<AnalyzerCall> _analyzerCalls = [];

        public StubMarketSignalAnalyzer(Func<MarketSnapshot, MarketSignal> signalFactory)
        {
            _signalFactory = signalFactory;
        }

        public IReadOnlyCollection<AnalyzerCall> AnalyzerCalls => _analyzerCalls;

        public IReadOnlyCollection<MarketSignal> Analyze(
            IReadOnlyCollection<MarketSnapshot> snapshots,
            IReadOnlyCollection<MarketCandle>? candles = null)
        {
            var snapshot = Assert.Single(snapshots);
            _analyzerCalls.Add(new AnalyzerCall(
                snapshot.CapturedAtUtc,
                (candles ?? []).Max(candle => candle.OccurredAtUtc)));

            return [_signalFactory(snapshot)];
        }
    }

    private sealed record AnalyzerCall(
        DateTime SnapshotDate,
        DateTime MaxCandleDate);
}
