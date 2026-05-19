using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.UnitTests;

public sealed class TechnicalMarketSignalAnalyzerTests
{
    private readonly TechnicalMarketSignalAnalyzer _analyzer = new(new EmptyTechnicalIndicatorService(), new RiskPositionOptions());

    [Fact]
    public void Analyze_ReturnsBullishSignal_WhenPriceShowsRelativeStrength()
    {
        var snapshot = CreateSnapshot(
            "ABNB",
            price: 108m,
            openPrice: 100m,
            highPrice: 110m,
            lowPrice: 98m,
            previousClose: 99m);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.Equal(MarketSignalType.Bullish, signal.SignalType);
        Assert.Equal("Candidate", signal.Action);
        Assert.Equal("Intraday", signal.Timeframe);
        Assert.Equal("Medium", signal.Confidence);
        Assert.True(signal.Score > 60m);
        Assert.True(signal.Trend > 0m);
        Assert.Contains("relative strength", signal.Reason);
    }

    [Fact]
    public void Analyze_ReturnsRiskSignal_WhenIntradayWeaknessIsSharp()
    {
        var snapshot = CreateSnapshot(
            "SPY",
            price: 90m,
            openPrice: 100m,
            highPrice: 110m,
            lowPrice: 89m,
            previousClose: 100m);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.Equal(MarketSignalType.Risk, signal.SignalType);
        Assert.Equal("Avoid / high risk", signal.Action);
        Assert.Equal("WatchOnly", signal.Timeframe);
        Assert.Equal("Low", signal.Confidence);
        Assert.True(signal.Score < 40m);
        Assert.True(signal.Trend < 0m);
        Assert.Contains("intraday weakness", signal.Reason);
    }

    [Fact]
    public void Analyze_KeepsRsiNull_WhenHistoricalDataIsMissing()
    {
        var snapshot = CreateSnapshot(
            "BTC",
            price: 65000m,
            openPrice: 64000m,
            highPrice: 66000m,
            lowPrice: 63000m,
            previousClose: 64500m,
            assetType: AssetType.Crypto);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.Null(signal.Rsi);
    }

    [Fact]
    public void Analyze_ScoresStrongerSetupAboveWeakerSetup()
    {
        var strong = CreateSnapshot(
            "QQQ",
            price: 105m,
            openPrice: 100m,
            highPrice: 106m,
            lowPrice: 99m,
            previousClose: 100m);
        var weak = CreateSnapshot(
            "IWM",
            price: 96m,
            openPrice: 100m,
            highPrice: 104m,
            lowPrice: 95m,
            previousClose: 100m);

        var signals = _analyzer.Analyze([weak, strong]).ToDictionary(signal => signal.Symbol);

        Assert.True(signals["QQQ"].Score > signals["IWM"].Score);
    }

    [Fact]
    public void Analyze_UsesCautiousPullbackWording_WhenScoreIsNotBullish()
    {
        var snapshot = CreateSnapshot(
            "MELI",
            price: 98m,
            openPrice: 99m,
            highPrice: 101m,
            lowPrice: 97m,
            previousClose: 99m);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.Equal("Watch for confirmation", signal.Action);
        Assert.Equal("WatchOnly", signal.Timeframe);
        Assert.Equal("Low", signal.Confidence);
        Assert.Contains("Weak pullback near support; confirmation needed", signal.Reason);
        Assert.DoesNotContain("controlled pullback near support", signal.Reason);
    }

    [Fact]
    public void Analyze_RewardsStrongRecoveryFromSessionLow()
    {
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 108m,
            openPrice: 100m,
            highPrice: 110m,
            lowPrice: 90m,
            previousClose: 100m);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.True(signal.StrongIntradayRecovery);
        Assert.Equal(90m, signal.RecoveryFromLowPercent);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Very strong recovery from session low" && factor.Points == 12m);
        Assert.Contains("buyers absorbed early selling pressure", signal.Reason);
    }

    [Fact]
    public void Analyze_DetectsGapDownRecovery()
    {
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 98m,
            openPrice: 90m,
            highPrice: 100m,
            lowPrice: 88m,
            previousClose: 100m);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.Equal(-10m, signal.GapPercent);
        Assert.True(signal.GapRecovery);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Gap-down recovery");
        Assert.Contains("buyers absorbed early selling pressure", signal.Reason);
    }

    [Fact]
    public void Analyze_ClassifiesExtendedMomentumAsWatch()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: 126m,
            Ema20: 100m,
            Ema50: 85m,
            Rsi14: 68m,
            Atr14: 4m,
            AverageVolume10: 1200000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "NVDA",
            price: 130m,
            openPrice: 126m,
            highPrice: 132m,
            lowPrice: 124m,
            previousClose: 124m);
        var candles = CreateTrendingCandles("NVDA", start: 72m, step: 1.1m, count: 60);

        var signal = Assert.Single(analyzer.Analyze([snapshot], candles));

        Assert.Equal("High", signal.ExtensionRisk);
        Assert.Equal("Watch for pullback or breakout confirmation", signal.Action);
        Assert.True(signal.MomentumContinuation);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "High extension risk" && factor.Points == -10m);
    }

    [Fact]
    public void Analyze_RewardsPositiveEmaSlope()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: 104m,
            Ema20: 100m,
            Ema50: 92m,
            Rsi14: 62m,
            Atr14: 2m,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "MSFT",
            price: 106m,
            openPrice: 103m,
            highPrice: 107m,
            lowPrice: 102m,
            previousClose: 102m);
        var candles = CreateTrendingCandles("MSFT", start: 70m, step: 0.7m, count: 60);

        var signal = Assert.Single(analyzer.Analyze([snapshot], candles));

        Assert.True(signal.Ema20Slope > 0m);
        Assert.True(signal.Ema50Slope > 0m);
        Assert.True(signal.StrongTrendSlope);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Positive EMA20 slope" && factor.Points == 8m);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Positive EMA50 slope" && factor.Points == 10m);
    }

    [Fact]
    public void Analyze_ReducesIntradayWeaknessPenalty_WhenRecoveryIsStrong()
    {
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 105m,
            openPrice: 110m,
            highPrice: 112m,
            lowPrice: 80m,
            previousClose: 112m);

        var signal = Assert.Single(_analyzer.Analyze([snapshot]));

        Assert.True(signal.StrongIntradayRecovery);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Intraday weakness reduced due to recovery");
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Intraday weakness");
    }

    [Fact]
    public void Analyze_KeepsHighRiskClassification_WhenBelowEma20AndEma50()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: 108m,
            Ema20: 120m,
            Ema50: 130m,
            Rsi14: 28m,
            Atr14: 3m,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "PATH",
            price: 100m,
            openPrice: 108m,
            highPrice: 109m,
            lowPrice: 99m,
            previousClose: 110m);
        var candles = CreateTrendingCandles("PATH", start: 140m, step: -0.6m, count: 60);

        var signal = Assert.Single(analyzer.Analyze([snapshot], candles));

        Assert.Equal(MarketSignalType.Risk, signal.SignalType);
        Assert.Equal("Avoid / high risk", signal.Action);
        Assert.True(signal.Score < 40m);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Price below EMA20");
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Price below EMA50");
    }

    private static MarketSnapshot CreateSnapshot(
        string symbol,
        decimal price,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal previousClose,
        AssetType assetType = AssetType.Equity)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            symbol,
            assetType,
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

    private static IReadOnlyCollection<MarketCandle> CreateTrendingCandles(
        string symbol,
        decimal start,
        decimal step,
        int count)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var close = start + (step * index);
                var open = close - (step / 2m);
                var high = Math.Max(open, close) + 1m;
                var low = Math.Min(open, close) - 1m;

                return new MarketCandle(
                    symbol,
                    AssetType.Equity,
                    DateTime.SpecifyKind(new DateTime(2026, 3, 1), DateTimeKind.Utc).AddDays(index),
                    open,
                    high,
                    low,
                    close,
                    1000000m,
                    "UnitTest");
            })
            .ToArray();
    }

    private static TechnicalMarketSignalAnalyzer CreateAnalyzer(TechnicalIndicators indicators)
    {
        return new TechnicalMarketSignalAnalyzer(new StubTechnicalIndicatorService(indicators), new RiskPositionOptions());
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
}
