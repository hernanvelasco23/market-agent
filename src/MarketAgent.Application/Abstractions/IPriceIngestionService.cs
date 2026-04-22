using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IPriceIngestionService
{
    Task<PriceIngestionResult> ExecuteAsync(
        CancellationToken cancellationToken = default);
}
