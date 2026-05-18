using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IMarketSignalAnalyzer
{
    IReadOnlyCollection<MarketSignal> Analyze(
        IReadOnlyCollection<MarketSnapshot> snapshots);
}
