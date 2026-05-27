using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class EfSignalOutcomeRepository : ISignalOutcomeRepository
{
    private readonly MarketAgentDbContext _dbContext;

    public EfSignalOutcomeRepository(MarketAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<SignalOutcomeEvaluationCandidate>> GetEvaluationCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SignalSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Outcome == null ||
                snapshot.Outcome.EvaluationStatus == SignalOutcomeStatuses.Pending ||
                snapshot.Outcome.EvaluationStatus == SignalOutcomeStatuses.Failed)
            .OrderBy(snapshot => snapshot.CreatedAtUtc)
            .Take(limit)
            .Select(snapshot => new SignalOutcomeEvaluationCandidate(
                snapshot.Id,
                snapshot.CreatedAtUtc,
                snapshot.RunId,
                snapshot.Symbol,
                snapshot.Setup,
                snapshot.Action,
                snapshot.Score,
                snapshot.Confidence,
                snapshot.Price))
            .ToArrayAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        SignalOutcomeRecord outcome,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SignalOutcomes
            .SingleOrDefaultAsync(
                item => item.SignalSnapshotId == outcome.SignalSnapshotId,
                cancellationToken);

        if (existing is null)
        {
            await _dbContext.SignalOutcomes.AddAsync(ToEntity(outcome), cancellationToken);
        }
        else
        {
            Apply(outcome, existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = _dbContext.SignalSnapshots
            .AsNoTracking()
            .GroupJoin(
                _dbContext.SignalOutcomes.AsNoTracking(),
                snapshot => snapshot.Id,
                outcome => outcome.SignalSnapshotId,
                (snapshot, outcomes) => new { Snapshot = snapshot, Outcome = outcomes.FirstOrDefault() });

        if (!string.IsNullOrWhiteSpace(query.Symbol))
        {
            rows = rows.Where(row => row.Snapshot.Symbol == query.Symbol);
        }

        if (query.Days is not null)
        {
            var cutoffUtc = DateTime.UtcNow.AddDays(-query.Days.Value);
            rows = rows.Where(row => row.Snapshot.CreatedAtUtc >= cutoffUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            rows = rows.Where(row => row.Outcome != null &&
                row.Outcome.EvaluationStatus == query.Status);
        }

        if (query.IsSuccessful is not null)
        {
            rows = rows.Where(row => row.Outcome != null &&
                row.Outcome.IsSuccessful == query.IsSuccessful);
        }

        return await rows
            .OrderByDescending(row => row.Snapshot.CreatedAtUtc)
            .Take(query.Limit ?? 100)
            .Select(row => new SignalOutcomeItem(
                row.Snapshot.Id,
                row.Snapshot.CreatedAtUtc,
                row.Snapshot.RunId,
                row.Snapshot.Symbol,
                row.Snapshot.Setup,
                row.Snapshot.Action,
                row.Snapshot.Score,
                row.Snapshot.Confidence,
                row.Snapshot.RelativeStrengthVsSpy,
                row.Snapshot.RelativeVolume,
                row.Snapshot.ExtensionFromEma20Percent,
                row.Snapshot.Ema9,
                row.Snapshot.Ema20,
                row.Snapshot.Ema50,
                row.Snapshot.Rsi,
                row.Snapshot.Entry,
                row.Snapshot.Stop,
                row.Snapshot.Target,
                row.Snapshot.Target,
                null,
                null,
                null,
                null,
                null,
                CalculatePotentialUpsidePercent(
                    row.Snapshot.Entry,
                    row.Snapshot.Target,
                    null,
                    null,
                    row.Snapshot.Target),
                row.Outcome == null ? null : row.Outcome.EvaluatedAtUtc,
                row.Outcome == null ? null : row.Outcome.EvaluationStatus,
                row.Outcome == null ? null : row.Outcome.PriceAtSignal,
                row.Outcome == null ? null : row.Outcome.PriceAfter15Minutes,
                row.Outcome == null ? null : row.Outcome.PriceAfter1Hour,
                row.Outcome == null ? null : row.Outcome.PriceAfter4Hours,
                row.Outcome == null ? null : row.Outcome.PriceAfter1Day,
                row.Outcome == null ? null : row.Outcome.MaxRunupPercent,
                row.Outcome == null ? null : row.Outcome.MaxDrawdownPercent,
                row.Outcome == null ? null : row.Outcome.OutcomePercent,
                row.Outcome == null ? null : row.Outcome.IsSuccessful,
                row.Outcome == null ? null : row.Outcome.FailureReason))
            .ToArrayAsync(cancellationToken);
    }

    private static decimal? CalculatePotentialUpsidePercent(
        decimal? entry,
        decimal? takeProfit1,
        decimal? takeProfit2,
        decimal? takeProfit3,
        decimal? target)
    {
        if (entry is not > 0m)
        {
            return null;
        }

        var selectedTakeProfit = new[] { takeProfit2, takeProfit1, takeProfit3, target }
            .FirstOrDefault(value => value is > 0m && value > entry);

        return selectedTakeProfit is null
            ? null
            : Math.Round(((selectedTakeProfit.Value - entry.Value) / entry.Value) * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static PersistedSignalOutcome ToEntity(SignalOutcomeRecord outcome)
    {
        var entity = new PersistedSignalOutcome
        {
            Id = outcome.Id == Guid.Empty ? Guid.NewGuid() : outcome.Id,
            SignalSnapshotId = outcome.SignalSnapshotId
        };

        Apply(outcome, entity);

        return entity;
    }

    private static void Apply(SignalOutcomeRecord outcome, PersistedSignalOutcome entity)
    {
        entity.EvaluatedAtUtc = outcome.EvaluatedAtUtc;
        entity.EvaluationStatus = outcome.EvaluationStatus;
        entity.PriceAtSignal = outcome.PriceAtSignal;
        entity.PriceAfter15Minutes = outcome.PriceAfter15Minutes;
        entity.PriceAfter1Hour = outcome.PriceAfter1Hour;
        entity.PriceAfter4Hours = outcome.PriceAfter4Hours;
        entity.PriceAfter1Day = outcome.PriceAfter1Day;
        entity.MaxRunupPercent = outcome.MaxRunupPercent;
        entity.MaxDrawdownPercent = outcome.MaxDrawdownPercent;
        entity.OutcomePercent = outcome.OutcomePercent;
        entity.IsSuccessful = outcome.IsSuccessful;
        entity.FailureReason = string.IsNullOrWhiteSpace(outcome.FailureReason)
            ? null
            : outcome.FailureReason.Length <= 256
                ? outcome.FailureReason
                : outcome.FailureReason[..256];
    }
}
