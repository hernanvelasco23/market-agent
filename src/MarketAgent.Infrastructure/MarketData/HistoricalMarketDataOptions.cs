namespace MarketAgent.Infrastructure.MarketData;

public sealed class HistoricalMarketDataOptions
{
    public const string SectionName = "MarketData:Historical";

    public string? StooqApiKey { get; set; }
}
