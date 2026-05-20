using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class NoOpSignalOutcomeRepository : ISignalOutcomeRepository
{
    private readonly ILogger<NoOpSignalOutcomeRepository> _logger;

    public NoOpSignalOutcomeRepository(ILogger<NoOpSignalOutcomeRepository> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyCollection<SignalOutcomeEvaluationCandidate>> GetEvaluationCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Signal outcome persistence is disabled; no outcome candidates will be evaluated.");

        return Task.FromResult<IReadOnlyCollection<SignalOutcomeEvaluationCandidate>>([]);
    }

    public Task UpsertAsync(
        SignalOutcomeRecord outcome,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyCollection<SignalOutcomeItem>>([]);
    }
}
