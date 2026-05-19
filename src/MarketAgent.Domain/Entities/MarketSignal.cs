using MarketAgent.Domain.Enums;

namespace MarketAgent.Domain.Entities;

public sealed class MarketSignal
{
    public MarketSignal(
        string symbol,
        AssetType assetType,
        MarketSignalType signalType,
        decimal score,
        string setupType,
        string reason,
        string action,
        string timeframe,
        string confidence,
        decimal trend,
        decimal? rsi,
        decimal? ema9,
        decimal? ema20,
        decimal? ema50,
        decimal? atr14,
        decimal? averageVolume10,
        decimal? averageVolume20,
        bool? aboveVwap,
        decimal? relativeStrengthVsSpy,
        decimal? relativeVolume,
        decimal? recoveryFromLowPercent,
        bool strongIntradayRecovery,
        decimal? gapPercent,
        bool gapRecovery,
        bool openingRedReversalDetected,
        decimal? openGapPercent,
        decimal? openingRedReversalRecoveryFromLowPercent,
        bool reclaimOpen,
        bool reclaimPreviousClose,
        decimal? ema20Slope,
        decimal? ema50Slope,
        bool strongTrendSlope,
        decimal? distanceFromEma20Percent,
        string? extensionRisk,
        bool momentumContinuation,
        decimal? drawdown,
        decimal? entry,
        decimal? stop,
        decimal? target,
        decimal? takeProfit1,
        decimal? takeProfit2,
        decimal? takeProfit3,
        decimal? riskReward1,
        decimal? riskReward2,
        decimal? riskReward3,
        decimal? riskPerShare,
        decimal? maxRiskAmount,
        decimal? suggestedPositionSize,
        decimal? regimeSizingMultiplier,
        IReadOnlyCollection<MarketSignalScoreFactor> scoreBreakdown,
        DateTime generatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Market signal symbol is required.", nameof(symbol));
        }

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Market signal score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Market signal reason is required.", nameof(reason));
        }

        if (string.IsNullOrWhiteSpace(setupType))
        {
            throw new ArgumentException("Market signal setup type is required.", nameof(setupType));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Market signal action is required.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(timeframe))
        {
            throw new ArgumentException("Market signal timeframe is required.", nameof(timeframe));
        }

        if (string.IsNullOrWhiteSpace(confidence))
        {
            throw new ArgumentException("Market signal confidence is required.", nameof(confidence));
        }

        if (trend is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(trend), "Market signal trend must be between -1 and 1.");
        }

        if (generatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Market signal generation time must be UTC.", nameof(generatedAtUtc));
        }

        ArgumentNullException.ThrowIfNull(scoreBreakdown);

        Symbol = symbol.Trim().ToUpperInvariant();
        AssetType = assetType;
        SignalType = signalType;
        Score = score;
        SetupType = setupType.Trim();
        Reason = reason.Trim();
        Action = action.Trim();
        Timeframe = timeframe.Trim();
        Confidence = confidence.Trim();
        Trend = trend;
        Rsi = rsi;
        Ema9 = ema9;
        Ema20 = ema20;
        Ema50 = ema50;
        Atr14 = atr14;
        AverageVolume10 = averageVolume10;
        AverageVolume20 = averageVolume20;
        AboveVwap = aboveVwap;
        RelativeStrengthVsSpy = relativeStrengthVsSpy;
        RelativeVolume = relativeVolume;
        RecoveryFromLowPercent = recoveryFromLowPercent;
        StrongIntradayRecovery = strongIntradayRecovery;
        GapPercent = gapPercent;
        GapRecovery = gapRecovery;
        OpeningRedReversalDetected = openingRedReversalDetected;
        OpenGapPercent = openGapPercent;
        OpeningRedReversalRecoveryFromLowPercent = openingRedReversalRecoveryFromLowPercent;
        ReclaimOpen = reclaimOpen;
        ReclaimPreviousClose = reclaimPreviousClose;
        Ema20Slope = ema20Slope;
        Ema50Slope = ema50Slope;
        StrongTrendSlope = strongTrendSlope;
        DistanceFromEma20Percent = distanceFromEma20Percent;
        ExtensionRisk = extensionRisk;
        MomentumContinuation = momentumContinuation;
        Drawdown = drawdown;
        Entry = entry;
        Stop = stop;
        Target = target;
        TakeProfit1 = takeProfit1;
        TakeProfit2 = takeProfit2;
        TakeProfit3 = takeProfit3;
        RiskReward1 = riskReward1;
        RiskReward2 = riskReward2;
        RiskReward3 = riskReward3;
        RiskPerShare = riskPerShare;
        MaxRiskAmount = maxRiskAmount;
        SuggestedPositionSize = suggestedPositionSize;
        RegimeSizingMultiplier = regimeSizingMultiplier;
        ScoreBreakdown = scoreBreakdown;
        GeneratedAtUtc = generatedAtUtc;
    }

    public string Symbol { get; }

    public AssetType AssetType { get; }

    public MarketSignalType SignalType { get; }

    public decimal Score { get; }

    public string SetupType { get; }

    public string Reason { get; }

    public string Action { get; }

    public string Timeframe { get; }

    public string Confidence { get; }

    public decimal Trend { get; }

    public decimal? Rsi { get; }

    public decimal? Ema9 { get; }

    public decimal? Ema20 { get; }

    public decimal? Ema50 { get; }

    public decimal? Atr14 { get; }

    public decimal? AverageVolume10 { get; }

    public decimal? AverageVolume20 { get; }

    public bool? AboveVwap { get; }

    public decimal? RelativeStrengthVsSpy { get; }

    public decimal? RelativeVolume { get; }

    public decimal? RecoveryFromLowPercent { get; }

    public bool StrongIntradayRecovery { get; }

    public decimal? GapPercent { get; }

    public bool GapRecovery { get; }

    public bool OpeningRedReversalDetected { get; }

    public decimal? OpenGapPercent { get; }

    public decimal? OpeningRedReversalRecoveryFromLowPercent { get; }

    public bool ReclaimOpen { get; }

    public bool ReclaimPreviousClose { get; }

    public decimal? Ema20Slope { get; }

    public decimal? Ema50Slope { get; }

    public bool StrongTrendSlope { get; }

    public decimal? DistanceFromEma20Percent { get; }

    public decimal? ExtensionFromEma20Percent => DistanceFromEma20Percent;

    public string? ExtensionRisk { get; }

    public bool MomentumContinuation { get; }

    public decimal? Drawdown { get; }

    public decimal? Entry { get; }

    public decimal? Stop { get; }

    public decimal? Target { get; }

    public decimal? TakeProfit1 { get; }

    public decimal? TakeProfit2 { get; }

    public decimal? TakeProfit3 { get; }

    public decimal? RiskReward1 { get; }

    public decimal? RiskReward2 { get; }

    public decimal? RiskReward3 { get; }

    public decimal? RiskPerShare { get; }

    public decimal? MaxRiskAmount { get; }

    public decimal? SuggestedPositionSize { get; }

    public decimal? RegimeSizingMultiplier { get; }

    public IReadOnlyCollection<MarketSignalScoreFactor> ScoreBreakdown { get; }

    public DateTime GeneratedAtUtc { get; }
}
