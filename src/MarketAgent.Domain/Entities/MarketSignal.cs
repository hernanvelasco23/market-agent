using MarketAgent.Domain.Enums;

namespace MarketAgent.Domain.Entities;

public sealed class MarketSignal
{
    public MarketSignal(
        string symbol,
        AssetType assetType,
        MarketSignalType signalType,
        decimal score,
        string reason,
        string action,
        string timeframe,
        string confidence,
        decimal trend,
        decimal? rsi,
        decimal? drawdown,
        decimal? entry,
        decimal? stop,
        decimal? target,
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

        Symbol = symbol.Trim().ToUpperInvariant();
        AssetType = assetType;
        SignalType = signalType;
        Score = score;
        Reason = reason.Trim();
        Action = action.Trim();
        Timeframe = timeframe.Trim();
        Confidence = confidence.Trim();
        Trend = trend;
        Rsi = rsi;
        Drawdown = drawdown;
        Entry = entry;
        Stop = stop;
        Target = target;
        GeneratedAtUtc = generatedAtUtc;
    }

    public string Symbol { get; }

    public AssetType AssetType { get; }

    public MarketSignalType SignalType { get; }

    public decimal Score { get; }

    public string Reason { get; }

    public string Action { get; }

    public string Timeframe { get; }

    public string Confidence { get; }

    public decimal Trend { get; }

    public decimal? Rsi { get; }

    public decimal? Drawdown { get; }

    public decimal? Entry { get; }

    public decimal? Stop { get; }

    public decimal? Target { get; }

    public DateTime GeneratedAtUtc { get; }
}
