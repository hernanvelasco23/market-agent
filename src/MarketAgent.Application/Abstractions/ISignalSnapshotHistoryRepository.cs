using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface ISignalSnapshotHistoryRepository
{
    Task AppendAsync(
        Guid runId,
        DateTime createdAtUtc,
        IReadOnlyCollection<MarketSignal> signals,
        string? marketRegime,
        string? triggeredAlertsJson,
        string source,
        CancellationToken cancellationToken = default);
}
