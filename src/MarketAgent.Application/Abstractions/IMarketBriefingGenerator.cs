using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IMarketBriefingGenerator
{
    Task<MarketBriefingResult> GenerateAsync(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        CancellationToken cancellationToken = default);
}
