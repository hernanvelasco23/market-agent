using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface ISignalOutcomeService
{
    Task<SignalOutcomeEvaluationResult> EvaluateAsync(
        int? limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default);

    Task<SignalOutcomeSummary> GetSummaryAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default);
}
