using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class NoOpAlertEventRepository : IAlertEventRepository
{
    private readonly ILogger<NoOpAlertEventRepository> _logger;

    public NoOpAlertEventRepository(ILogger<NoOpAlertEventRepository> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyCollection<AlertEventItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyCollection<AlertEventItem>>([]);
    }

    public Task<IReadOnlySet<string>> GetExistingAlertKeysAsync(
        IReadOnlyCollection<AlertEvaluationCandidate> candidates,
        string alertType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    public Task SaveAsync(
        AlertEventRecord alertEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Alert event persistence is disabled; skipped alert {AlertType} for signal snapshot {SignalSnapshotId}.",
            alertEvent.AlertType,
            alertEvent.SignalSnapshotId);

        return Task.CompletedTask;
    }
}
