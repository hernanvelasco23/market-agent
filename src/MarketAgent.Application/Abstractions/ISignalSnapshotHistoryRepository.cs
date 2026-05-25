using MarketAgent.Domain.Entities;
using MarketAgent.Application.Models;

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

    Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<MarketSignalRunResult?> GetLatestRunAsync(
        CancellationToken cancellationToken = default);

    Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
        Guid signalSnapshotId,
        CancellationToken cancellationToken = default);
}
