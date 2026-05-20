using MarketAgent.Domain.Enums;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class PersistedMarketSnapshot
{
    public Guid Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public AssetType AssetType { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }

    public string Source { get; set; } = string.Empty;

    public decimal? Volume { get; set; }

    public decimal? OpenPrice { get; set; }

    public decimal? HighPrice { get; set; }

    public decimal? LowPrice { get; set; }

    public decimal? PreviousClose { get; set; }
}
