using MarketAgent.Domain.Enums;

namespace MarketAgent.Domain.Entities;

public sealed class MarketCandle
{
    public MarketCandle(
        string symbol,
        AssetType assetType,
        DateTime occurredAtUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal? volume,
        string source)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Market candle symbol is required.", nameof(symbol));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Market candle time must be UTC.", nameof(occurredAtUtc));
        }

        if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(close), "Market candle prices must be greater than zero.");
        }

        if (high < low)
        {
            throw new ArgumentException("Market candle high cannot be lower than low.", nameof(high));
        }

        if (volume is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Market candle volume cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Market candle source is required.", nameof(source));
        }

        Symbol = symbol.Trim().ToUpperInvariant();
        AssetType = assetType;
        OccurredAtUtc = occurredAtUtc;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        Source = source.Trim();
    }

    public string Symbol { get; }

    public AssetType AssetType { get; }

    public DateTime OccurredAtUtc { get; }

    public decimal Open { get; }

    public decimal High { get; }

    public decimal Low { get; }

    public decimal Close { get; }

    public decimal? Volume { get; }

    public string Source { get; }
}
