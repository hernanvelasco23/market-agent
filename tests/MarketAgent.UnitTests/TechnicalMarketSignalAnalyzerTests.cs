using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.UnitTests;

public sealed class TechnicalMarketSignalAnalyzerTests
{
    private readonly TechnicalMarketSignalAnalyzer _analyzer = new(new EmptyTechnicalIndicatorService());

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

    private sealed class EmptyTechnicalIndicatorService : ITechnicalIndicatorService
    {
        public TechnicalIndicators Calculate(IReadOnlyCollection<MarketCandle> candles)
        {
            return new TechnicalIndicators(null, null, null, null, null, null, null);
        }
    }
}
