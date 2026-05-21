using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IAlertEventRepository
{
    Task<IReadOnlyCollection<AlertEventItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetExistingAlertKeysAsync(
        IReadOnlyCollection<AlertEvaluationCandidate> candidates,
        string alertType,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        AlertEventRecord alertEvent,
        CancellationToken cancellationToken = default);
}
