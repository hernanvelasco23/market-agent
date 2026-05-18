using MarketAgent.Application.Abstractions;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Application.Signals;

public sealed class TechnicalMarketSignalAnalyzer : IMarketSignalAnalyzer
{
    private const int RsiPeriod = 14;

    public IReadOnlyCollection<MarketSignal> Analyze(
        IReadOnlyCollection<MarketSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count == 0)
        {
            return [];
        }

        var generatedAtUtc = DateTime.UtcNow;

        return snapshots
            .GroupBy(snapshot => snapshot.Symbol)
            .Select(group => AnalyzeLatest(group.OrderBy(snapshot => snapshot.CapturedAtUtc).ToArray(), generatedAtUtc))
            .OrderByDescending(signal => signal.Score)
            .ThenBy(signal => signal.Symbol)
            .ToArray();
    }

    private static MarketSignal AnalyzeLatest(
        IReadOnlyList<MarketSnapshot> snapshots,
        DateTime generatedAtUtc)
    {
        var latest = snapshots[^1];
        var rsi = CalculateRsi(snapshots);
        var drawdown = CalculateDrawdown(latest);
        var rangePosition = CalculateRangePosition(latest);
        var intradayChange = CalculatePercentChange(latest.Price, latest.OpenPrice);
        var previousCloseChange = CalculatePercentChange(latest.Price, latest.PreviousClose);
        var trend = CalculateTrend(rangePosition, intradayChange, previousCloseChange);

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
        var signalType = roundedScore >= 55m
            ? MarketSignalType.Bullish
            : roundedScore < 40m ? MarketSignalType.Risk : MarketSignalType.Neutral;
        var action = DetermineAction(roundedScore);
        var timeframe = DetermineTimeframe(roundedScore, rangePosition);
        var confidence = DetermineConfidence(roundedScore, rangePosition);
        var reason = BuildReason(reasons, roundedScore);

        return new MarketSignal(
            latest.Symbol,
            latest.AssetType,
            signalType,
            roundedScore,
            reason,
            action,
            timeframe,
            confidence,
            roundedTrend,
            Round(rsi),
            Round(drawdown),
            Round(latest.Price),
            CalculateStop(latest),
            CalculateTarget(latest),
            generatedAtUtc);
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

    private static string DetermineAction(decimal score)
    {
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
        decimal? previousCloseChange)
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

        return trend;
    }

    private static decimal? CalculatePercentChange(decimal current, decimal? baseline)
    {
        return baseline is > 0
            ? ((current - baseline.Value) / baseline.Value) * 100m
            : null;
    }

    private static decimal? CalculateStop(MarketSnapshot snapshot)
    {
        if (snapshot.LowPrice is > 0)
        {
            return Round(snapshot.LowPrice.Value * 0.99m);
        }

        return snapshot.Price > 0
            ? Round(snapshot.Price * 0.97m)
            : null;
    }

    private static decimal? CalculateTarget(MarketSnapshot snapshot)
    {
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
