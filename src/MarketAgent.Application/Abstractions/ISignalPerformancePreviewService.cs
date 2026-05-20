using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface ISignalPerformancePreviewService
{
    Task<SignalPerformancePreviewResult> GenerateAsync(
        int days,
        CancellationToken cancellationToken = default);
}
