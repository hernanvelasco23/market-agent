using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IMarketSignalAnalyzer
{
    IReadOnlyCollection<MarketSignal> Analyze(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        IReadOnlyCollection<MarketCandle>? candles = null);
}
