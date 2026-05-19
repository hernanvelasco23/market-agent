using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Application.Signals;

public sealed class TechnicalMarketSignalAnalyzer : IMarketSignalAnalyzer
{
    private const int RsiPeriod = 14;
    private readonly ITechnicalIndicatorService _technicalIndicatorService;
    private readonly RiskPositionOptions _riskPositionOptions;

    public TechnicalMarketSignalAnalyzer(
        ITechnicalIndicatorService technicalIndicatorService,
        RiskPositionOptions riskPositionOptions)
    {
        _technicalIndicatorService = technicalIndicatorService;
        _riskPositionOptions = riskPositionOptions;
    }

    public IReadOnlyCollection<MarketSignal> Analyze(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        IReadOnlyCollection<MarketCandle>? candles = null)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count == 0)
        {
            return [];
        }

        var generatedAtUtc = DateTime.UtcNow;
        var candlesBySymbol = (candles ?? [])
            .GroupBy(candle => candle.Symbol)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var marketRegime = DetermineMarketRegime(candlesBySymbol);
        var spyChange = CalculateSpyChange(candlesBySymbol);

        return snapshots
            .GroupBy(snapshot => snapshot.Symbol)
            .Select(group =>
            {
                candlesBySymbol.TryGetValue(group.Key, out var symbolCandles);

                return AnalyzeLatest(
                    group.OrderBy(snapshot => snapshot.CapturedAtUtc).ToArray(),
                    symbolCandles ?? [],
                    marketRegime,
                    spyChange,
                    generatedAtUtc);
            })
            .OrderByDescending(signal => signal.Score)
            .ThenBy(signal => signal.Symbol)
            .ToArray();
    }

    private MarketSignal AnalyzeLatest(
        IReadOnlyList<MarketSnapshot> snapshots,
        IReadOnlyCollection<MarketCandle> candles,
        string marketRegime,
        decimal? spyChange,
        DateTime generatedAtUtc)
    {
        var latest = snapshots[^1];
        var indicators = _technicalIndicatorService.Calculate(candles);
        var rsi = indicators.Rsi14 ?? CalculateRsi(snapshots);
        var drawdown = CalculateDrawdown(latest);
        var rangePosition = CalculateRangePosition(latest);
        var recoveryFromLowPercent = rangePosition;
        var strongIntradayRecovery = recoveryFromLowPercent is > 0.70m;
        var intradayChange = CalculatePercentChange(latest.Price, latest.OpenPrice);
        var previousCloseChange = CalculatePercentChange(latest.Price, latest.PreviousClose);
        var gapPercent = CalculatePercentChange(latest.OpenPrice ?? latest.Price, latest.PreviousClose);
        var gapRecovery = CalculateGapRecovery(latest, gapPercent, recoveryFromLowPercent);
        var ema20Slope = CalculateEmaSlope(candles, 20, lookback: 5);
        var ema50Slope = CalculateEmaSlope(candles, 50, lookback: 5);
        var distanceFromEma20Percent = CalculatePercentChange(latest.Price, indicators.Ema20);
        var extensionRisk = DetermineExtensionRisk(distanceFromEma20Percent);
        var strongTrendSlope = IsStrongTrendSlope(ema20Slope, ema50Slope);
        var trend = CalculateTrend(rangePosition, intradayChange, previousCloseChange, latest.Price, indicators);

        var score = 50m;
        var reasons = new List<string>();
        var scoreBreakdown = new List<MarketSignalScoreFactor>();

        if (previousCloseChange is >= 1m)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "Positive move versus previous close",
                Math.Min(previousCloseChange.Value * 4m, 16m),
                ref score);
        }

        if (intradayChange is >= 0.5m)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "Intraday relative strength",
                Math.Min(intradayChange.Value * 3m, 12m),
                ref score);
        }

        if (rangePosition is >= 0.7m)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "Price closed near the session high",
                (rangePosition.Value - 0.5m) * 20m,
                ref score);
        }

        ApplyRecoveryScoring(recoveryFromLowPercent, reasons, scoreBreakdown, ref score);
        ApplyGapRecoveryScoring(gapRecovery, latest, indicators, reasons, scoreBreakdown, ref score);

        var isControlledPullbackNearSupport = IsControlledPullbackNearSupport(latest, drawdown, rangePosition);
        if (isControlledPullbackNearSupport)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "Controlled pullback near support",
                8m,
                ref score);
        }

        if (rsi is < 35m && rangePosition is <= 0.4m)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "RSI oversold near support",
                6m,
                ref score);
        }

        ApplyIndicatorScoring(latest, indicators, ema20Slope, ema50Slope, reasons, scoreBreakdown, ref score);
        ApplyExtensionRiskScoring(extensionRisk, reasons, scoreBreakdown, ref score);
        ApplyMarketRegimeScoring(latest, marketRegime, reasons, scoreBreakdown, ref score);

        if (drawdown is <= -8m)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "Sharp drawdown from the session high",
                -Math.Min(Math.Abs(drawdown.Value) * 1.5m, 24m),
                ref score);
        }

        if (intradayChange is <= -1m)
        {
            var penalty = Math.Min(Math.Abs(intradayChange.Value) * 4m, 18m);
            if (strongIntradayRecovery || gapRecovery)
            {
                penalty *= 0.5m;
            }

            AddScoreFactor(
                scoreBreakdown,
                reasons,
                strongIntradayRecovery || gapRecovery
                    ? "Intraday weakness reduced due to recovery"
                    : "Intraday weakness",
                -penalty,
                ref score);
        }

        if (rangePosition is <= 0.25m && !isControlledPullbackNearSupport)
        {
            AddScoreFactor(
                scoreBreakdown,
                reasons,
                "Price closed near the session low",
                -8m,
                ref score);
        }

        var roundedScore = Round(Clamp(score, 0m, 100m));
        var roundedTrend = Round(Clamp(trend, -1m, 1m));
        var relativeStrengthVsSpy = CalculateRelativeStrengthVsSpy(latest, spyChange);
        ApplyRelativeStrengthScoring(relativeStrengthVsSpy, reasons, scoreBreakdown, ref roundedScore);
        var hasMixedSignals = HasMixedSignals(reasons);
        var momentumContinuation = IsMomentumContinuation(
            latest,
            indicators,
            rsi,
            recoveryFromLowPercent,
            ema20Slope,
            ema50Slope);
        ApplyMomentumContinuationScoring(momentumContinuation, reasons, scoreBreakdown, ref roundedScore);
        ApplyHighTightConsolidationScoring(
            latest,
            candles,
            indicators,
            ema20Slope,
            reasons,
            scoreBreakdown,
            ref roundedScore);
        roundedScore = Round(Clamp(roundedScore, 0m, 100m));
        var setupType = DetermineSetupType(reasons, roundedScore, hasMixedSignals, momentumContinuation, extensionRisk);
        var signalType = roundedScore >= 55m
            ? MarketSignalType.Bullish
            : roundedScore < 40m ? MarketSignalType.Risk : MarketSignalType.Neutral;
        var action = DetermineAction(roundedScore, hasMixedSignals, extensionRisk);
        var timeframe = DetermineTimeframe(roundedScore, rangePosition);
        var confidence = DetermineConfidence(roundedScore, rangePosition, momentumContinuation);
        var reason = BuildReason(reasons, roundedScore);
        var entry = Round(latest.Price);
        var stop = CalculateStop(latest, indicators, setupType);
        var takeProfits = CalculateTakeProfits(entry, indicators);
        var fallbackTarget = CalculateTarget(latest, indicators);
        var target = takeProfits.TakeProfit2 ?? fallbackTarget;
        var riskMetrics = CalculateRiskMetrics(
            entry,
            stop,
            takeProfits,
            marketRegime,
            vix: null);

        return new MarketSignal(
            latest.Symbol,
            latest.AssetType,
            signalType,
            roundedScore,
            setupType,
            reason,
            action,
            timeframe,
            confidence,
            roundedTrend,
            Round(rsi),
            indicators.Ema9,
            indicators.Ema20,
            indicators.Ema50,
            indicators.Atr14,
            indicators.AverageVolume10,
            indicators.AverageVolume20,
            null,
            Round(relativeStrengthVsSpy),
            Round(ToPercent(recoveryFromLowPercent)),
            strongIntradayRecovery,
            Round(gapPercent),
            gapRecovery,
            Round(ema20Slope),
            Round(ema50Slope),
            strongTrendSlope,
            Round(distanceFromEma20Percent),
            extensionRisk,
            momentumContinuation,
            Round(drawdown),
            entry,
            stop,
            target,
            takeProfits.TakeProfit1,
            takeProfits.TakeProfit2,
            takeProfits.TakeProfit3,
            riskMetrics.RiskReward1,
            riskMetrics.RiskReward2,
            riskMetrics.RiskReward3,
            riskMetrics.RiskPerShare,
            riskMetrics.MaxRiskAmount,
            riskMetrics.SuggestedPositionSize,
            riskMetrics.RegimeSizingMultiplier,
            scoreBreakdown
                .Select(item => item with { Points = Round(item.Points) })
                .ToArray(),
            generatedAtUtc);
    }

    private static decimal? CalculateSpyChange(
        IReadOnlyDictionary<string, MarketCandle[]> candlesBySymbol)
    {
        if (!candlesBySymbol.TryGetValue("SPY", out var spyCandles) || spyCandles.Length < 2)
        {
            return null;
        }

        var ordered = spyCandles
            .OrderBy(candle => candle.OccurredAtUtc)
            .TakeLast(2)
            .ToArray();

        return CalculatePercentChange(ordered[^1].Close, ordered[0].Close);
    }

    private static decimal? CalculateRelativeStrengthVsSpy(
        MarketSnapshot snapshot,
        decimal? spyChange)
    {
        if (spyChange is null ||
            snapshot.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var snapshotChange = CalculatePercentChange(snapshot.Price, snapshot.PreviousClose);

        return snapshotChange is null
            ? null
            : snapshotChange.Value - spyChange.Value;
    }

    private static bool HasMixedSignals(IReadOnlyCollection<string> reasons)
    {
        var hasConstructiveSignal = reasons.Any(reason =>
            reason.Contains("above EMA", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("relative strength", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("positive move", StringComparison.OrdinalIgnoreCase));
        var hasWeakSignal = reasons.Any(reason =>
            reason.Contains("session low", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("intraday weakness", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("drawdown", StringComparison.OrdinalIgnoreCase));

        return hasConstructiveSignal && hasWeakSignal;
    }

    private static string DetermineSetupType(
        IReadOnlyCollection<string> reasons,
        decimal score,
        bool hasMixedSignals,
        bool momentumContinuation,
        string? extensionRisk)
    {
        if (score < 40m)
        {
            return "Risk";
        }

        if (momentumContinuation)
        {
            return "MomentumContinuation";
        }

        if (!string.IsNullOrWhiteSpace(extensionRisk) && score >= 55m)
        {
            return "ExtendedMomentum";
        }

        if (hasMixedSignals && score < 60m)
        {
            return "Mixed";
        }

        if (reasons.Any(reason => reason.Contains("pullback near support", StringComparison.OrdinalIgnoreCase)))
        {
            return "Pullback";
        }

        if (score >= 70m)
        {
            return "StrongBullish";
        }

        if (score >= 60m)
        {
            return "BullishContinuation";
        }

        return "Neutral";
    }

    private string DetermineMarketRegime(
        IReadOnlyDictionary<string, MarketCandle[]> candlesBySymbol)
    {
        if (!candlesBySymbol.TryGetValue("SPY", out var spyCandles) || spyCandles.Length == 0)
        {
            return "Neutral";
        }

        var latestClose = spyCandles
            .OrderBy(candle => candle.OccurredAtUtc)
            .Last()
            .Close;
        var indicators = _technicalIndicatorService.Calculate(spyCandles);

        if (indicators.Ema20 is not null && latestClose < indicators.Ema20.Value)
        {
            return "RiskOff";
        }

        if (indicators.Ema20 is not null &&
            indicators.Ema50 is not null &&
            latestClose > indicators.Ema20.Value &&
            latestClose > indicators.Ema50.Value)
        {
            return "RiskOn";
        }

        return "Neutral";
    }

    private static void ApplyMarketRegimeScoring(
        MarketSnapshot snapshot,
        string marketRegime,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (snapshot.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (marketRegime == "RiskOff")
        {
            AddScoreFactor(scoreBreakdown, reasons, "Risk-off market regime filter", -3m, ref score);
        }
        else if (marketRegime == "RiskOn")
        {
            AddScoreFactor(scoreBreakdown, reasons, "Risk-on market regime filter", 2m, ref score);
        }
    }

    private static void ApplyIndicatorScoring(
        MarketSnapshot snapshot,
        TechnicalIndicators indicators,
        decimal? ema20Slope,
        decimal? ema50Slope,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (indicators.Ema9 is not null && snapshot.Price > indicators.Ema9.Value)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Price above EMA9", 4m, ref score);
        }

        if (indicators.Ema20 is not null)
        {
            if (snapshot.Price > indicators.Ema20.Value)
            {
                AddScoreFactor(scoreBreakdown, reasons, "Price above EMA20", 5m, ref score);
            }
            else
            {
                AddScoreFactor(scoreBreakdown, reasons, "Price below EMA20", -6m, ref score);
            }
        }

        if (indicators.Ema50 is not null && snapshot.Price < indicators.Ema50.Value)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Price below EMA50", -8m, ref score);
        }

        if (indicators.Rsi14 is >= 65m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "RSI confirms momentum", 4m, ref score);
        }

        if (indicators.Rsi14 is <= 30m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "RSI remains weak", -4m, ref score);
        }

        if (snapshot.Volume is > 0 && indicators.AverageVolume20 is > 0)
        {
            var volumeRatio = snapshot.Volume.Value / indicators.AverageVolume20.Value;

            if (volumeRatio >= 1.5m)
            {
                AddScoreFactor(scoreBreakdown, reasons, "Volume above 20-day average", 3m, ref score);
            }
        }

        if (ema20Slope is > 0.05m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Positive EMA20 slope", 8m, ref score);
        }
        else if (ema20Slope is < -0.05m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Negative EMA20 slope", -8m, ref score);
        }

        if (ema50Slope is > 0.05m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Positive EMA50 slope", 10m, ref score);
        }
        else if (ema50Slope is < -0.05m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Negative EMA50 slope", -10m, ref score);
        }

        if (IsStrongTrendSlope(ema20Slope, ema50Slope))
        {
            AddScoreFactor(scoreBreakdown, reasons, "Strong positive EMA20 and EMA50 slope", 15m, ref score);
        }
    }

    private static void ApplyRecoveryScoring(
        decimal? recoveryFromLow,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (recoveryFromLow is > 0.85m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Very strong recovery from session low", 12m, ref score);
            AddScoreFactor(scoreBreakdown, reasons, "Buyers absorbed early selling pressure", 3m, ref score);
        }
        else if (recoveryFromLow is > 0.70m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Strong recovery from session low", 8m, ref score);
        }
        else if (recoveryFromLow is < 0.30m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Weak close near session low", -10m, ref score);
        }
    }

    private static void ApplyGapRecoveryScoring(
        bool gapRecovery,
        MarketSnapshot snapshot,
        TechnicalIndicators indicators,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (!gapRecovery)
        {
            return;
        }

        var hasVolumeExpansion = snapshot.Volume is > 0 &&
            indicators.AverageVolume20 is > 0 &&
            snapshot.Volume.Value > indicators.AverageVolume20.Value;
        AddScoreFactor(
            scoreBreakdown,
            reasons,
            hasVolumeExpansion
                ? "Gap-down recovery with volume expansion"
                : "Gap-down recovery",
            hasVolumeExpansion ? 15m : 10m,
            ref score);
        reasons.Add("buyers absorbed early selling pressure");
    }

    private static void ApplyExtensionRiskScoring(
        string? extensionRisk,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (extensionRisk == "Extended")
        {
            AddScoreFactor(scoreBreakdown, reasons, "Extended above EMA20", -5m, ref score);
        }
        else if (extensionRisk == "High")
        {
            AddScoreFactor(scoreBreakdown, reasons, "High extension risk", -10m, ref score);
        }
        else if (extensionRisk == "Climax")
        {
            AddScoreFactor(scoreBreakdown, reasons, "Avoid chasing vertical move", -18m, ref score);
        }
    }

    private static void ApplyMomentumContinuationScoring(
        bool momentumContinuation,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (momentumContinuation)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Momentum continuation", 8m, ref score);
        }
    }

    private static void ApplyHighTightConsolidationScoring(
        MarketSnapshot snapshot,
        IReadOnlyCollection<MarketCandle> candles,
        TechnicalIndicators indicators,
        decimal? ema20Slope,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (candles.Count < 20 || ema20Slope is not > 0 || indicators.Ema20 is null || snapshot.Price < indicators.Ema20.Value)
        {
            return;
        }

        var recentCandles = candles.OrderBy(candle => candle.OccurredAtUtc).TakeLast(20).ToArray();
        var recentLow = recentCandles.Min(candle => candle.Low);
        var recentHigh = recentCandles.Max(candle => candle.High);
        var priorMove = CalculatePercentChange(recentHigh, recentLow);
        var nearRecentHigh = recentHigh > 0 && ((recentHigh - snapshot.Price) / recentHigh) <= 0.08m;

        if (priorMove is >= 20m && nearRecentHigh)
        {
            AddScoreFactor(scoreBreakdown, reasons, "High consolidation after strong momentum move", 6m, ref score);
        }
    }

    private static void ApplyRelativeStrengthScoring(
        decimal? relativeStrengthVsSpy,
        List<string> reasons,
        List<MarketSignalScoreFactor> scoreBreakdown,
        ref decimal score)
    {
        if (relativeStrengthVsSpy is >= 2m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Relative strength vs SPY", 8m, ref score);
        }
        else if (relativeStrengthVsSpy is <= -2m)
        {
            AddScoreFactor(scoreBreakdown, reasons, "Relative weakness vs SPY", -8m, ref score);
        }

        score = Clamp(score, 0m, 100m);
    }

    private static void AddScoreFactor(
        List<MarketSignalScoreFactor> scoreBreakdown,
        List<string> reasons,
        string label,
        decimal points,
        ref decimal score)
    {
        if (points == 0m)
        {
            return;
        }

        score += points;
        scoreBreakdown.Add(new MarketSignalScoreFactor(label, points));
        reasons.Add(ToReasonText(label));
    }

    private static string ToReasonText(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        return char.ToLowerInvariant(label[0]) + label[1..];
    }

    private static string DetermineTimeframe(decimal score, decimal? rangePosition)
    {
        if (score >= 60m && rangePosition is >= 0.7m)
        {
            return "Intraday";
        }

        return score >= 55m
            ? "SwingCandidate"
            : "WatchOnly";
    }

    private static string DetermineConfidence(decimal score, decimal? rangePosition, bool momentumContinuation)
    {
        if (momentumContinuation && score >= 55m)
        {
            return "Medium";
        }

        if (score >= 60m && rangePosition is >= 0.7m)
        {
            return "Medium";
        }

        if (score >= 70m)
        {
            return "High";
        }

        return score >= 55m
            ? "Medium"
            : "Low";
    }

    private static string DetermineAction(decimal score, bool hasMixedSignals, string? extensionRisk)
    {
        if (score < 40m)
        {
            return "Avoid / high risk";
        }

        if (!string.IsNullOrWhiteSpace(extensionRisk))
        {
            return "Watch for pullback or breakout confirmation";
        }

        if (hasMixedSignals && score < 60m)
        {
            return "Watch for confirmation";
        }

        if (score >= 55m)
        {
            return "Candidate";
        }

        return "Watch for confirmation";
    }

    private static string BuildReason(IReadOnlyCollection<string> reasons, decimal score)
    {
        if (reasons.Count == 0)
        {
            return "neutral price action from available snapshot data";
        }

        var normalizedReasons = reasons
            .Select(reason => score < 55m && reason == "controlled pullback near support"
                ? "Weak pullback near support; confirmation needed"
                : reason)
            .ToArray();

        return string.Join("; ", normalizedReasons);
    }

    private static bool IsControlledPullbackNearSupport(
        MarketSnapshot snapshot,
        decimal? drawdown,
        decimal? rangePosition)
    {
        if (snapshot.LowPrice is null || drawdown is null || rangePosition is null)
        {
            return false;
        }

        var supportDistance = CalculatePercentChange(snapshot.Price, snapshot.LowPrice);

        return drawdown is >= -6m and <= -1m &&
            rangePosition <= 0.45m &&
            supportDistance is >= 0m and <= 3m;
    }

    private static decimal? CalculateRsi(IReadOnlyList<MarketSnapshot> snapshots)
    {
        if (snapshots.Count < RsiPeriod + 1)
        {
            return null;
        }

        var closes = snapshots
            .OrderBy(snapshot => snapshot.CapturedAtUtc)
            .TakeLast(RsiPeriod + 1)
            .Select(snapshot => snapshot.Price)
            .ToArray();

        var gains = 0m;
        var losses = 0m;

        for (var index = 1; index < closes.Length; index++)
        {
            var change = closes[index] - closes[index - 1];

            if (change > 0)
            {
                gains += change;
            }
            else
            {
                losses += Math.Abs(change);
            }
        }

        var averageGain = gains / RsiPeriod;
        var averageLoss = losses / RsiPeriod;

        if (averageLoss == 0m)
        {
            return 100m;
        }

        var relativeStrength = averageGain / averageLoss;
        return 100m - (100m / (1m + relativeStrength));
    }

    private static decimal? CalculateDrawdown(MarketSnapshot snapshot)
    {
        return snapshot.HighPrice is > 0
            ? CalculatePercentChange(snapshot.Price, snapshot.HighPrice)
            : null;
    }

    private static decimal? CalculateRangePosition(MarketSnapshot snapshot)
    {
        if (snapshot.HighPrice is not > 0 ||
            snapshot.LowPrice is null ||
            snapshot.HighPrice == snapshot.LowPrice)
        {
            return null;
        }

        var rangePosition = (snapshot.Price - snapshot.LowPrice.Value) /
            (snapshot.HighPrice.Value - snapshot.LowPrice.Value);

        return Clamp(rangePosition, 0m, 1m);
    }

    private static decimal? ToPercent(decimal? ratio)
    {
        return ratio.HasValue ? ratio.Value * 100m : null;
    }

    private static bool CalculateGapRecovery(
        MarketSnapshot snapshot,
        decimal? gapPercent,
        decimal? recoveryFromLow)
    {
        if (gapPercent is not < -5m)
        {
            return false;
        }

        return recoveryFromLow is > 0.75m ||
            (snapshot.OpenPrice is not null && snapshot.Price > snapshot.OpenPrice.Value);
    }

    private static string? DetermineExtensionRisk(decimal? distanceFromEma20Percent)
    {
        return distanceFromEma20Percent switch
        {
            > 35m => "Climax",
            > 25m => "High",
            > 15m => "Extended",
            _ => null
        };
    }

    private static bool IsStrongTrendSlope(decimal? ema20Slope, decimal? ema50Slope)
    {
        return ema20Slope is > 0.20m && ema50Slope is > 0.10m;
    }

    private static bool IsMomentumContinuation(
        MarketSnapshot snapshot,
        TechnicalIndicators indicators,
        decimal? rsi,
        decimal? recoveryFromLow,
        decimal? ema20Slope,
        decimal? ema50Slope)
    {
        return indicators.Ema20 is not null &&
            snapshot.Price > indicators.Ema20.Value &&
            ema20Slope is > 0m &&
            ema50Slope is > 0m &&
            rsi is >= 55m and <= 75m &&
            recoveryFromLow is > 0.6m &&
            (indicators.Ema50 is null || snapshot.Price >= indicators.Ema50.Value);
    }

    private static decimal? CalculateEmaSlope(
        IReadOnlyCollection<MarketCandle> candles,
        int period,
        int lookback)
    {
        var ordered = candles.OrderBy(candle => candle.OccurredAtUtc).ToArray();
        if (ordered.Length < period + lookback)
        {
            return null;
        }

        var currentEma = CalculateEmaAt(ordered, period, ordered.Length);
        var previousEma = CalculateEmaAt(ordered, period, ordered.Length - lookback);

        return currentEma is > 0 && previousEma is > 0
            ? ((currentEma.Value - previousEma.Value) / previousEma.Value) * 100m
            : null;
    }

    private static decimal? CalculateEmaAt(IReadOnlyList<MarketCandle> candles, int period, int count)
    {
        if (count < period)
        {
            return null;
        }

        var selected = candles.Take(count).ToArray();
        var multiplier = 2m / (period + 1);
        var ema = selected.Take(period).Average(candle => candle.Close);

        foreach (var candle in selected.Skip(period))
        {
            ema = ((candle.Close - ema) * multiplier) + ema;
        }

        return ema;
    }

    private static decimal CalculateTrend(
        decimal? rangePosition,
        decimal? intradayChange,
        decimal? previousCloseChange,
        decimal price,
        TechnicalIndicators indicators)
    {
        var trend = 0m;

        if (rangePosition is not null)
        {
            trend += (rangePosition.Value - 0.5m) * 1.2m;
        }

        if (intradayChange is not null)
        {
            trend += Clamp(intradayChange.Value / 10m, -0.3m, 0.3m);
        }

        if (previousCloseChange is not null)
        {
            trend += Clamp(previousCloseChange.Value / 12m, -0.25m, 0.25m);
        }

        if (indicators.Ema20 is > 0)
        {
            trend += price >= indicators.Ema20.Value ? 0.15m : -0.15m;
        }

        if (indicators.Ema50 is > 0)
        {
            trend += price >= indicators.Ema50.Value ? 0.1m : -0.1m;
        }

        return trend;
    }

    private static decimal? CalculatePercentChange(decimal current, decimal? baseline)
    {
        return baseline is > 0
            ? ((current - baseline.Value) / baseline.Value) * 100m
            : null;
    }

    private static decimal? CalculateStop(
        MarketSnapshot snapshot,
        TechnicalIndicators indicators,
        string setupType)
    {
        if (indicators.Atr14 is > 0)
        {
            var multiplier = setupType switch
            {
                "StrongBullish" => 0.8m,
                "BullishContinuation" => 1.0m,
                "Pullback" => 1.2m,
                _ => 1.5m
            };

            return Round(snapshot.Price - (indicators.Atr14.Value * multiplier));
        }

        if (snapshot.LowPrice is > 0)
        {
            return Round(snapshot.LowPrice.Value * 0.99m);
        }

        return snapshot.Price > 0
            ? Round(snapshot.Price * 0.97m)
            : null;
    }

    private static decimal? CalculateTarget(MarketSnapshot snapshot, TechnicalIndicators indicators)
    {
        if (indicators.Atr14 is > 0)
        {
            return Round(snapshot.Price + (indicators.Atr14.Value * 2m));
        }

        if (snapshot.HighPrice is > 0)
        {
            var range = snapshot.LowPrice is > 0
                ? snapshot.HighPrice.Value - snapshot.LowPrice.Value
                : snapshot.HighPrice.Value * 0.03m;

            return Round(snapshot.Price + Math.Max(range, snapshot.Price * 0.03m));
        }

        return snapshot.Price > 0
            ? Round(snapshot.Price * 1.05m)
            : null;
    }

    private static TakeProfitLevels CalculateTakeProfits(
        decimal? entry,
        TechnicalIndicators indicators)
    {
        if (entry is null || indicators.Atr14 is not > 0)
        {
            return new TakeProfitLevels(null, null, null);
        }

        return new TakeProfitLevels(
            Round(entry.Value + (indicators.Atr14.Value * 1.5m)),
            Round(entry.Value + (indicators.Atr14.Value * 2.5m)),
            Round(entry.Value + (indicators.Atr14.Value * 3.5m)));
    }

    private RiskMetrics CalculateRiskMetrics(
        decimal? entry,
        decimal? stop,
        TakeProfitLevels takeProfits,
        string marketRegime,
        decimal? vix)
    {
        decimal? riskPerShare = entry is not null && stop is not null && entry > stop
            ? Round(entry.Value - stop.Value)
            : null;
        var regimeSizingMultiplier = CalculateRegimeSizingMultiplier(marketRegime, vix);
        var maxRiskAmount = CalculateMaxRiskAmount(regimeSizingMultiplier);
        decimal? suggestedPositionSize = riskPerShare is > 0 && maxRiskAmount is not null
            ? Round(maxRiskAmount.Value / riskPerShare.GetValueOrDefault())
            : null;

        return new RiskMetrics(
            riskPerShare,
            maxRiskAmount,
            suggestedPositionSize,
            regimeSizingMultiplier,
            CalculateRiskReward(entry, stop, takeProfits.TakeProfit1),
            CalculateRiskReward(entry, stop, takeProfits.TakeProfit2),
            CalculateRiskReward(entry, stop, takeProfits.TakeProfit3));
    }

    private decimal? CalculateMaxRiskAmount(decimal regimeSizingMultiplier)
    {
        if (_riskPositionOptions.AccountSize is not > 0 ||
            _riskPositionOptions.RiskPercentPerTrade is not > 0)
        {
            return null;
        }

        return Round(
            _riskPositionOptions.AccountSize.Value *
            _riskPositionOptions.RiskPercentPerTrade.Value *
            regimeSizingMultiplier);
    }

    private static decimal CalculateRegimeSizingMultiplier(string marketRegime, decimal? vix)
    {
        var multiplier = marketRegime switch
        {
            "RiskOn" => 1.0m,
            "RiskOff" => 0.25m,
            _ => 0.5m
        };

        if (vix is > 25m)
        {
            multiplier *= 0.5m;
        }

        return multiplier;
    }

    private static decimal? CalculateRiskReward(decimal? entry, decimal? stop, decimal? target)
    {
        if (entry is null || stop is null || target is null || entry <= stop)
        {
            return null;
        }

        var risk = entry.Value - stop.Value;
        var reward = target.Value - entry.Value;

        return risk <= 0m || reward <= 0m
            ? null
            : Round(reward / risk);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static decimal? Round(decimal? value)
    {
        return value.HasValue
            ? Round(value.Value)
            : null;
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record TakeProfitLevels(
        decimal? TakeProfit1,
        decimal? TakeProfit2,
        decimal? TakeProfit3);

    private sealed record RiskMetrics(
        decimal? RiskPerShare,
        decimal? MaxRiskAmount,
        decimal? SuggestedPositionSize,
        decimal RegimeSizingMultiplier,
        decimal? RiskReward1,
        decimal? RiskReward2,
        decimal? RiskReward3);
}
