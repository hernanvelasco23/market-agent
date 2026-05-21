using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IManualSystemCycleService
{
    Task<ManualSystemCycleResult> RunAsync(
        ManualSystemCycleRequest request,
        CancellationToken cancellationToken = default);
}
