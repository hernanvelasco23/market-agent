using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Xunit.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class TechnicalMarketSignalAnalyzerTests
{
    private readonly TechnicalMarketSignalAnalyzer _analyzer = new(new EmptyTechnicalIndicatorService(), new RiskPositionOptions());
    private readonly ITestOutputHelper _output;

    public TechnicalMarketSignalAnalyzerTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
    public void Analyze_RewardsStrongEmaSlopeWithoutStackingIndividualSlopeBonuses()
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
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Strong positive EMA20 and EMA50 slope" && factor.Points == 15m);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Positive EMA20 slope");
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Positive EMA50 slope");
    }

    [Fact]
    public void Analyze_DoesNotClampCommonBullishSetupTo100()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: 102m,
            Ema20: 100m,
            Ema50: 98m,
            Rsi14: 62m,
            Atr14: 2m,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "MSFT",
            price: 103m,
            openPrice: 102m,
            highPrice: 104m,
            lowPrice: 100m,
            previousClose: 101m);
        var candles = CreateTrendingCandles("MSFT", start: 100m, step: 0m, count: 60);

        var signal = Assert.Single(analyzer.Analyze([snapshot], candles));

        // Regression: a normal bullish day can earn constructive points, but should not
        // automatically saturate at 100 just because historical indicators are present.
        var unclampedContribution = signal.ScoreBreakdown.Sum(factor => factor.Points);
        var unclampedScore = 50m + unclampedContribution;
        var breakdown = string.Join(
            Environment.NewLine,
            signal.ScoreBreakdown.Select(factor => $"  {factor.Label}: {factor.Points:+0.##;-0.##}"));

        _output.WriteLine($"Symbol: {signal.Symbol}");
        _output.WriteLine($"Final score: {signal.Score}");
        _output.WriteLine($"Unclamped score approximation: {unclampedScore}");
        _output.WriteLine("Score contribution breakdown:");
        _output.WriteLine(breakdown);

        Assert.InRange(signal.Score, 70m, 99.99m);
        Assert.Equal(unclampedScore, signal.Score);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Strong positive EMA20 and EMA50 slope");
    }

    [Fact]
    public void Analyze_CalculatesRelativeStrengthRelativeVolumeAndEma20Extension()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: 103m,
            Ema20: 100m,
            Ema50: 96m,
            Rsi14: 58m,
            Atr14: 2m,
            AverageVolume10: 750000m,
            AverageVolume20: 500000m));
        var snapshot = CreateSnapshot(
            "MSFT",
            price: 105m,
            openPrice: 104m,
            highPrice: 106m,
            lowPrice: 101m,
            previousClose: 100m);
        var spyCandles = CreateFlatSpyCandles(previousClose: 100m, latestClose: 102m);

        var signal = Assert.Single(analyzer.Analyze([snapshot], spyCandles));

        Assert.Equal(3m, signal.RelativeStrengthVsSpy);
        Assert.Equal(2m, signal.RelativeVolume);
        Assert.Equal(5m, signal.DistanceFromEma20Percent);
        Assert.Equal(5m, signal.ExtensionFromEma20Percent);
    }

    [Fact]
    public void Analyze_CalculatesRelativeStrengthFromSpySnapshot_WhenSpyCandlesAreMissing()
    {
        var msft = CreateSnapshot(
            "MSFT",
            price: 105m,
            openPrice: 104m,
            highPrice: 106m,
            lowPrice: 101m,
            previousClose: 100m);
        var spy = CreateSnapshot(
            "SPY",
            price: 102m,
            openPrice: 101m,
            highPrice: 103m,
            lowPrice: 100m,
            previousClose: 100m,
            assetType: AssetType.Etf);

        var signals = _analyzer.Analyze([msft, spy]).ToDictionary(signal => signal.Symbol);

        Assert.Equal(3m, signals["MSFT"].RelativeStrengthVsSpy);
        Assert.Null(signals["SPY"].RelativeStrengthVsSpy);
    }

    [Fact]
    public void Analyze_CalculatesRelativeStrengthFromHistoricalClose_WhenSnapshotPreviousCloseIsMissing()
    {
        var snapshot = CreateSnapshot(
            "MSFT",
            price: 105m,
            openPrice: 104m,
            highPrice: 106m,
            lowPrice: 101m,
            previousClose: 0m);
        var candles = CreateFlatSpyCandles(previousClose: 100m, latestClose: 102m)
            .Concat(
            [
                new MarketCandle(
                    "MSFT",
                    AssetType.Equity,
                    DateTime.SpecifyKind(new DateTime(2026, 5, 17), DateTimeKind.Utc),
                    100m,
                    100m,
                    100m,
                    100m,
                    1000000m,
                    "UnitTest")
            ])
            .ToArray();

        var signal = Assert.Single(_analyzer.Analyze([snapshot], candles));

        Assert.Equal(3m, signal.RelativeStrengthVsSpy);
    }

    [Fact]
    public void Analyze_LeavesRelativeVolumeAndEma20ExtensionNull_WhenDenominatorsAreMissing()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: 0m,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: null,
            AverageVolume20: 0m));
        var snapshot = CreateSnapshot(
            "MSFT",
            price: 105m,
            openPrice: 104m,
            highPrice: 106m,
            lowPrice: 101m,
            previousClose: 100m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.Null(signal.RelativeStrengthVsSpy);
        Assert.Null(signal.RelativeVolume);
        Assert.Null(signal.DistanceFromEma20Percent);
        Assert.Null(signal.ExtensionFromEma20Percent);
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
    public void Analyze_DetectsOpeningRedReversal_WhenRedOpenRecoversAboveOpen()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 99m,
            openPrice: 98m,
            highPrice: 100m,
            lowPrice: 96m,
            previousClose: 100m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.True(signal.OpeningRedReversalDetected);
        Assert.Equal(-2m, signal.OpenGapPercent);
        Assert.Equal(3.13m, signal.OpeningRedReversalRecoveryFromLowPercent);
        Assert.True(signal.ReclaimOpen);
        Assert.False(signal.ReclaimPreviousClose);
        Assert.True(signal.Score < 100m);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal" && factor.Points == 6m);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Reclaimed previous close after red open");
    }

    [Fact]
    public void Analyze_DetectsStrongOpeningRedReversal_WhenPreviousCloseIsReclaimed()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 101m,
            openPrice: 98m,
            highPrice: 102m,
            lowPrice: 96m,
            previousClose: 100m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.True(signal.OpeningRedReversalDetected);
        Assert.True(signal.ReclaimOpen);
        Assert.True(signal.ReclaimPreviousClose);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal" && factor.Points == 6m);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Reclaimed previous close after red open" && factor.Points == 4m);
    }

    [Fact]
    public void Analyze_DoesNotDetectOpeningRedReversal_WhenRedOpenFailsToReclaimOpen()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 97.5m,
            openPrice: 98m,
            highPrice: 100m,
            lowPrice: 96m,
            previousClose: 100m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.False(signal.OpeningRedReversalDetected);
        Assert.False(signal.ReclaimOpen);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal");
    }

    [Fact]
    public void Analyze_DoesNotDetectOpeningRedReversal_WhenOpenIsGreen()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 103m,
            openPrice: 101m,
            highPrice: 104m,
            lowPrice: 100m,
            previousClose: 100m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.False(signal.OpeningRedReversalDetected);
        Assert.Equal(1m, signal.OpenGapPercent);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal");
    }

    [Fact]
    public void Analyze_OpeningRedReversalHandlesZeroPreviousCloseAndLowSafely()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 99m,
            openPrice: 98m,
            highPrice: 100m,
            lowPrice: 0m,
            previousClose: 0m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.False(signal.OpeningRedReversalDetected);
        Assert.Null(signal.OpenGapPercent);
        Assert.Null(signal.OpeningRedReversalRecoveryFromLowPercent);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal");
    }

    [Fact]
    public void Analyze_OpeningRedReversalHandlesMissingLowSafely()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 99m,
            openPrice: 98m,
            highPrice: 100m,
            lowPrice: null,
            previousClose: 100m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.False(signal.OpeningRedReversalDetected);
        Assert.Null(signal.OpeningRedReversalRecoveryFromLowPercent);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal");
    }

    [Fact]
    public void Analyze_OpeningRedReversalDoesNotTrigger_WhenAverageVolumeIsMissing()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: null,
            Ema20: null,
            Ema50: null,
            Rsi14: null,
            Atr14: null,
            AverageVolume10: null,
            AverageVolume20: 0m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 99m,
            openPrice: 98m,
            highPrice: 100m,
            lowPrice: 96m,
            previousClose: 100m,
            volume: 1600000m);

        var signal = Assert.Single(analyzer.Analyze([snapshot]));

        Assert.Null(signal.RelativeVolume);
        Assert.False(signal.OpeningRedReversalDetected);
        Assert.DoesNotContain(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal");
    }

    [Fact]
    public void Analyze_OpeningRedReversalBonusDoesNotPushScoreAbove100()
    {
        var analyzer = CreateAnalyzer(new TechnicalIndicators(
            Ema9: 99m,
            Ema20: 97m,
            Ema50: 95m,
            Rsi14: 68m,
            Atr14: 2m,
            AverageVolume10: 1000000m,
            AverageVolume20: 1000000m));
        var snapshot = CreateSnapshot(
            "RKLB",
            price: 101m,
            openPrice: 98m,
            highPrice: 102m,
            lowPrice: 90m,
            previousClose: 100m,
            volume: 2000000m);
        var candles = CreateTrendingCandles("RKLB", start: 70m, step: 0.7m, count: 60);

        var signal = Assert.Single(analyzer.Analyze([snapshot], candles));

        Assert.True(signal.OpeningRedReversalDetected);
        Assert.InRange(signal.Score, 0m, 100m);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Opening red reversal" && factor.Points == 6m);
        Assert.Contains(signal.ScoreBreakdown, factor => factor.Label == "Reclaimed previous close after red open" && factor.Points == 4m);
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
        decimal? lowPrice,
        decimal previousClose,
        AssetType assetType = AssetType.Equity,
        decimal volume = 1000000m)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            symbol,
            assetType,
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

    private static IReadOnlyCollection<MarketCandle> CreateFlatSpyCandles(
        decimal previousClose,
        decimal latestClose)
    {
        return
        [
            new MarketCandle(
                "SPY",
                AssetType.Equity,
                DateTime.SpecifyKind(new DateTime(2026, 5, 17), DateTimeKind.Utc),
                previousClose,
                previousClose,
                previousClose,
                previousClose,
                1000000m,
                "UnitTest"),
            new MarketCandle(
                "SPY",
                AssetType.Equity,
                DateTime.SpecifyKind(new DateTime(2026, 5, 18), DateTimeKind.Utc),
                latestClose,
                latestClose,
                latestClose,
                latestClose,
                1000000m,
                "UnitTest")
        ];
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
