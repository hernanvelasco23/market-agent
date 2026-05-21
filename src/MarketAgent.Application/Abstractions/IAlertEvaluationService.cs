using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IAlertEvaluationService
{
    Task<AlertEvaluationResult> EvaluateAsync(
        int? limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AlertEventItem>> GetAlertsAsync(
        AlertEventQuery query,
        CancellationToken cancellationToken = default);
}
