using MarketAgent.Domain.Enums;

namespace MarketAgent.Domain.Entities;

public sealed class MarketSnapshot
{
    public MarketSnapshot(
        Guid id,
        string symbol,
        AssetType assetType,
        decimal price,
        string currency,
        DateTime capturedAtUtc,
        string source,
        decimal? volume = null,
        decimal? openPrice = null,
        decimal? highPrice = null,
        decimal? lowPrice = null,
        decimal? previousClose = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Market snapshot id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Market snapshot symbol is required.", nameof(symbol));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Market snapshot price must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Market snapshot currency is required.", nameof(currency));
        }

        if (capturedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Market snapshot capture time must be UTC.", nameof(capturedAtUtc));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Market snapshot source is required.", nameof(source));
        }

        if (volume is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Market snapshot volume cannot be negative.");
        }

        Id = id;
        Symbol = symbol.Trim().ToUpperInvariant();
        AssetType = assetType;
        Price = price;
        Currency = currency.Trim().ToUpperInvariant();
        CapturedAtUtc = capturedAtUtc;
        Source = source.Trim();
        Volume = volume;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        PreviousClose = previousClose;
    }

    public Guid Id { get; }

    public string Symbol { get; }

    public AssetType AssetType { get; }

    public decimal Price { get; }

    public string Currency { get; }

    public DateTime CapturedAtUtc { get; }

    public string Source { get; }

    public decimal? Volume { get; }

    public decimal? OpenPrice { get; }

    public decimal? HighPrice { get; }

    public decimal? LowPrice { get; }

    public decimal? PreviousClose { get; }
}
