using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IMarketSignalService
{
    Task<MarketSignalRunResult> GenerateAsync(
        CancellationToken cancellationToken = default);
}
