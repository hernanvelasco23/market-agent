using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

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
        var service = new MarketSignalService(snapshots, analyzer, history);

        var result = await service.GenerateAsync();
        var signal = Assert.Single(result.Signals);

        Assert.Contains("MSFT", history.RequestedSymbols);
        Assert.Contains("SPY", history.RequestedSymbols);
        Assert.Equal(3m, signal.RelativeStrengthVsSpy);
    }

    private static MarketSnapshot CreateSnapshot(
        string symbol,
        decimal price,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal previousClose)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            symbol,
            AssetType.Equity,
            price,
            "USD",
            DateTime.SpecifyKind(new DateTime(2026, 5, 18, 15, 0, 0), DateTimeKind.Utc),
            "UnitTest",
            volume: 1000000m,
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

        public Task SaveAsync(MarketSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubHistoricalMarketDataService : IHistoricalMarketDataService
    {
        private readonly List<string> _requestedSymbols = [];

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

            IReadOnlyCollection<MarketCandle> candles = asset.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase)
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
}
