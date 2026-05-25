using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class NoOpSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
{
    private readonly ILogger<NoOpSignalSnapshotHistoryRepository> _logger;

    public NoOpSignalSnapshotHistoryRepository(ILogger<NoOpSignalSnapshotHistoryRepository> logger)
    {
        _logger = logger;
    }

    public Task AppendAsync(
        Guid runId,
        DateTime createdAtUtc,
        IReadOnlyCollection<MarketSignal> signals,
        string? marketRegime,
        string? triggeredAlertsJson,
        string source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (signals.Count > 0)
        {
            _logger.LogInformation(
                "Signal history persistence is disabled; skipped {SignalCount} signal snapshots for run {RunId}.",
                signals.Count,
                runId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyCollection<AlertEvaluationCandidate>>([]);
    }

    public Task<MarketSignalRunResult?> GetLatestRunAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<MarketSignalRunResult?>(null);
    }

    public Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
        Guid signalSnapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<ScoreAttributionSnapshot?>(null);
    }
}
