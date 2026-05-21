using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class EfSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MarketAgentDbContext _dbContext;

    public EfSignalSnapshotHistoryRepository(MarketAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AppendAsync(
        Guid runId,
        DateTime createdAtUtc,
        IReadOnlyCollection<MarketSignal> signals,
        string? marketRegime,
        string? triggeredAlertsJson,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0)
        {
            return;
        }

        var normalizedCreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        var rows = signals.Select(signal => ToSnapshot(
            runId,
            normalizedCreatedAtUtc,
            signal,
            marketRegime,
            triggeredAlertsJson,
            source));

        await _dbContext.SignalSnapshots.AddRangeAsync(rows, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SignalSnapshots
            .AsNoTracking()
            .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
            .Take(limit)
            .Select(snapshot => new AlertEvaluationCandidate(
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

    public async Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
        Guid signalSnapshotId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SignalSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Id == signalSnapshotId)
            .Select(snapshot => new ScoreAttributionSnapshot(
                snapshot.Id,
                snapshot.RunId,
                snapshot.Symbol,
                snapshot.Setup,
                snapshot.Action,
                snapshot.Score,
                snapshot.CreatedAtUtc,
                snapshot.ScoreAttributionJson,
                snapshot.ScoreBreakdownJson))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static PersistedSignalSnapshot ToSnapshot(
        Guid runId,
        DateTime createdAtUtc,
        MarketSignal signal,
        string? marketRegime,
        string? triggeredAlertsJson,
        string source)
    {
        var scoreAttribution = ScoreAttributionBuilder.Build(
            signal.Score,
            signal.ScoreBreakdown);

        return new PersistedSignalSnapshot
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc,
            RunId = runId,
            Symbol = signal.Symbol,
            Setup = signal.SetupType,
            Action = signal.Action,
            Score = signal.Score,
            Confidence = signal.Confidence,
            Price = signal.Entry,
            RelativeStrengthVsSpy = signal.RelativeStrengthVsSpy,
            RelativeVolume = signal.RelativeVolume,
            ExtensionFromEma20Percent = signal.ExtensionFromEma20Percent,
            MarketRegime = marketRegime,
            TriggeredAlertsJson = triggeredAlertsJson,
            Ema9 = signal.Ema9,
            Ema20 = signal.Ema20,
            Ema50 = signal.Ema50,
            Rsi = signal.Rsi,
            Source = string.IsNullOrWhiteSpace(source) ? "Scanner" : source.Trim(),
            Timeframe = signal.Timeframe,
            Reason = signal.Reason,
            SignalType = signal.SignalType.ToString(),
            Entry = signal.Entry,
            Stop = signal.Stop,
            Target = signal.Target,
            ScoreBreakdownJson = JsonSerializer.Serialize(signal.ScoreBreakdown, JsonOptions),
            ScoreAttributionJson = JsonSerializer.Serialize(scoreAttribution, JsonOptions),
            OpeningRedReversalDetected = signal.OpeningRedReversalDetected,
            OpenGapPercent = signal.OpenGapPercent,
            RecoveryFromLowPercent = signal.RecoveryFromLowPercent
        };
    }
}
