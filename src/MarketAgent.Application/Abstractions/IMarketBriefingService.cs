using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IMarketBriefingService
{
    Task<MarketBriefingResult> GenerateAsync(
        CancellationToken cancellationToken = default);
}
