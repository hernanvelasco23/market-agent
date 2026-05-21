using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IScoreAttributionService
{
    Task<SignalScoreAttributionResult?> GetAsync(
        Guid signalSnapshotId,
        CancellationToken cancellationToken = default);
}
