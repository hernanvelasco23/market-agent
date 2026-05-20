using MarketAgent.Application.Abstractions;
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
}
