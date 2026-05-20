using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface ISignalOutcomeRepository
{
    Task<IReadOnlyCollection<SignalOutcomeEvaluationCandidate>> GetEvaluationCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        SignalOutcomeRecord outcome,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default);
}
