using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Application.Signals;

public sealed class TechnicalMarketSignalAnalyzer : IMarketSignalAnalyzer
{
    private const int RsiPeriod = 14;
    private readonly ITechnicalIndicatorService _technicalIndicatorService;

    public TechnicalMarketSignalAnalyzer(ITechnicalIndicatorService technicalIndicatorService)
    {
        _technicalIndicatorService = technicalIndicatorService;
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
        var intradayChange = CalculatePercentChange(latest.Price, latest.OpenPrice);
        var previousCloseChange = CalculatePercentChange(latest.Price, latest.PreviousClose);
        var trend = CalculateTrend(rangePosition, intradayChange, previousCloseChange, latest.Price, indicators);

        var score = 50m;
        var reasons = new List<string>();

        if (previousCloseChange is >= 1m)
        {
            score += Math.Min(previousCloseChange.Value * 4m, 16m);
            reasons.Add("positive move versus previous close");
        }

        if (intradayChange is >= 0.5m)
        {
            score += Math.Min(intradayChange.Value * 3m, 12m);
            reasons.Add("intraday relative strength");
        }

        if (rangePosition is >= 0.7m)
        {
            score += (rangePosition.Value - 0.5m) * 20m;
            reasons.Add("price closed near the session high");
        }

        if (IsControlledPullbackNearSupport(latest, drawdown, rangePosition))
        {
            score += 8m;
            reasons.Add("controlled pullback near support");
        }

        if (rsi is < 35m && rangePosition is <= 0.4m)
        {
            score += 6m;
            reasons.Add("RSI oversold near support");
        }

        ApplyIndicatorScoring(latest, indicators, reasons, ref score);
        ApplyMarketRegimeScoring(latest, marketRegime, reasons, ref score);

        if (drawdown is <= -8m)
        {
            score -= Math.Min(Math.Abs(drawdown.Value) * 1.5m, 24m);
            reasons.Add("sharp drawdown from the session high");
        }

        if (intradayChange is <= -1m)
        {
            score -= Math.Min(Math.Abs(intradayChange.Value) * 4m, 18m);
            reasons.Add("intraday weakness");
        }

        if (rangePosition is <= 0.25m)
        {
            score -= 8m;
            reasons.Add("price closed near the session low");
        }

        var roundedScore = Round(Clamp(score, 0m, 100m));
        var roundedTrend = Round(Clamp(trend, -1m, 1m));
        var relativeStrengthVsSpy = CalculateRelativeStrengthVsSpy(latest, spyChange);
        var hasMixedSignals = HasMixedSignals(reasons);
        var setupType = DetermineSetupType(reasons, roundedScore, hasMixedSignals);
        var signalType = roundedScore >= 55m
            ? MarketSignalType.Bullish
            : roundedScore < 40m ? MarketSignalType.Risk : MarketSignalType.Neutral;
        var action = DetermineAction(roundedScore, hasMixedSignals);
        var timeframe = DetermineTimeframe(roundedScore, rangePosition);
        var confidence = DetermineConfidence(roundedScore, rangePosition);
        var reason = BuildReason(reasons, roundedScore);

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
            Round(drawdown),
            Round(latest.Price),
            CalculateStop(latest, indicators),
            CalculateTarget(latest, indicators),
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
        bool hasMixedSignals)
    {
        if (score < 40m)
        {
            return "Risk";
        }

        if (hasMixedSignals && score < 60m)
        {
            return "Mixed";
        }

        if (reasons.Any(reason => reason.Contains("pullback near support", StringComparison.OrdinalIgnoreCase)))
        {
            return "Pullback";
        }

        if (score >= 60m)
        {
            return "Momentum";
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
        ref decimal score)
    {
        if (snapshot.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (marketRegime == "RiskOff")
        {
            score -= 3m;
            reasons.Add("risk-off market regime filter");
        }
        else if (marketRegime == "RiskOn")
        {
            score += 2m;
            reasons.Add("risk-on market regime filter");
        }
    }

    private static void ApplyIndicatorScoring(
        MarketSnapshot snapshot,
        TechnicalIndicators indicators,
        List<string> reasons,
        ref decimal score)
    {
        if (indicators.Ema9 is not null && snapshot.Price > indicators.Ema9.Value)
        {
            score += 4m;
            reasons.Add("price above EMA9");
        }

        if (indicators.Ema20 is not null)
        {
            if (snapshot.Price > indicators.Ema20.Value)
            {
                score += 5m;
                reasons.Add("price above EMA20");
            }
            else
            {
                score -= 6m;
                reasons.Add("price below EMA20");
            }
        }

        if (indicators.Ema50 is not null && snapshot.Price < indicators.Ema50.Value)
        {
            score -= 8m;
            reasons.Add("price below EMA50");
        }

        if (indicators.Rsi14 is >= 65m)
        {
            score += 4m;
            reasons.Add("RSI confirms momentum");
        }

        if (indicators.Rsi14 is <= 30m)
        {
            score -= 4m;
            reasons.Add("RSI remains weak");
        }

        if (snapshot.Volume is > 0 && indicators.AverageVolume20 is > 0)
        {
            var volumeRatio = snapshot.Volume.Value / indicators.AverageVolume20.Value;

            if (volumeRatio >= 1.5m)
            {
                score += 3m;
                reasons.Add("volume above 20-day average");
            }
        }
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

    private static string DetermineConfidence(decimal score, decimal? rangePosition)
    {
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

    private static string DetermineAction(decimal score, bool hasMixedSignals)
    {
        if (hasMixedSignals && score < 60m)
        {
            return "Watch for confirmation";
        }

        if (score >= 55m)
        {
            return "Candidate";
        }

        return score < 40m
            ? "Avoid / high risk"
            : "Watch for confirmation";
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

    private static decimal? CalculateStop(MarketSnapshot snapshot, TechnicalIndicators indicators)
    {
        if (indicators.Atr14 is > 0)
        {
            return Round(snapshot.Price - (indicators.Atr14.Value * 1.5m));
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
}
