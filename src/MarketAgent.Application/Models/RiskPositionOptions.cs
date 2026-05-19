namespace MarketAgent.Application.Models;

public sealed class RiskPositionOptions
{
    public const string SectionName = "RiskPosition";

    public decimal? AccountSize { get; set; }

    public decimal? RiskPercentPerTrade { get; set; }
}
