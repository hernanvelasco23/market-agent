using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
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

    public async Task<MarketSignalRunResult?> GetLatestRunAsync(
        CancellationToken cancellationToken = default)
    {
        var latestRun = await _dbContext.SignalSnapshots
            .AsNoTracking()
            .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
            .Select(snapshot => new { snapshot.RunId, snapshot.CreatedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRun is null)
        {
            return null;
        }

        var rows = await _dbContext.SignalSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.RunId == latestRun.RunId)
            .OrderByDescending(snapshot => snapshot.Score)
            .ThenBy(snapshot => snapshot.Symbol)
            .ToArrayAsync(cancellationToken);

        return new MarketSignalRunResult(
            EnsureUtc(latestRun.CreatedAtUtc),
            rows.Select(ToMarketSignal).ToArray());
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
            signal.RawScore ?? signal.Score,
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

    private static MarketSignal ToMarketSignal(PersistedSignalSnapshot snapshot)
    {
        return new MarketSignal(
            snapshot.Symbol,
            AssetType.Equity,
            ParseSignalType(snapshot.SignalType),
            snapshot.Score,
            snapshot.Setup,
            snapshot.Reason ?? "Persisted signal snapshot.",
            snapshot.Action,
            snapshot.Timeframe ?? "Intraday",
            snapshot.Confidence,
            trend: 0m,
            snapshot.Rsi,
            snapshot.Ema9,
            snapshot.Ema20,
            snapshot.Ema50,
            atr14: null,
            averageVolume10: null,
            averageVolume20: null,
            aboveVwap: null,
            snapshot.RelativeStrengthVsSpy,
            snapshot.RelativeVolume,
            snapshot.RecoveryFromLowPercent,
            strongIntradayRecovery: false,
            gapPercent: null,
            gapRecovery: false,
            snapshot.OpeningRedReversalDetected,
            snapshot.OpenGapPercent,
            openingRedReversalRecoveryFromLowPercent: null,
            reclaimOpen: false,
            reclaimPreviousClose: false,
            ema20Slope: null,
            ema50Slope: null,
            strongTrendSlope: false,
            snapshot.ExtensionFromEma20Percent,
            extensionRisk: null,
            momentumContinuation: snapshot.Setup.Contains("Momentum", StringComparison.OrdinalIgnoreCase),
            drawdown: null,
            snapshot.Entry,
            snapshot.Stop,
            snapshot.Target,
            snapshot.Target,
            null,
            null,
            null,
            null,
            null,
            riskPerShare: null,
            maxRiskAmount: null,
            suggestedPositionSize: null,
            regimeSizingMultiplier: null,
            ParseScoreBreakdown(snapshot.ScoreBreakdownJson),
            EnsureUtc(snapshot.CreatedAtUtc),
            rawScore: null);
    }

    private static IReadOnlyCollection<MarketSignalScoreFactor> ParseScoreBreakdown(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<MarketSignalScoreFactor[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static MarketSignalType ParseSignalType(string? value)
    {
        return Enum.TryParse<MarketSignalType>(value, ignoreCase: true, out var signalType)
            ? signalType
            : MarketSignalType.Neutral;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
