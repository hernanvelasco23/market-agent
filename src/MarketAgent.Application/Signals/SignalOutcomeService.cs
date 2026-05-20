using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.Signals;

public sealed class SignalOutcomeService : ISignalOutcomeService
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;
    private const int MinimumSetupRankingSampleSize = 3;

    private static readonly ScoreBucketDefinition[] ScoreBuckets =
    [
        new("0-20", 0m, 20m, decimal.MinValue),
        new("21-40", 21m, 40m, 20m),
        new("41-60", 41m, 60m, 40m),
        new("61-80", 61m, 80m, 60m),
        new("81-100", 81m, 100m, 80m)
    ];

    private static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    private static readonly TimeSpan FourHours = TimeSpan.FromHours(4);
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    private readonly ISignalOutcomeRepository _signalOutcomeRepository;
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly ILogger<SignalOutcomeService> _logger;

    public SignalOutcomeService(
        ISignalOutcomeRepository signalOutcomeRepository,
        IMarketSnapshotRepository marketSnapshotRepository,
        ILogger<SignalOutcomeService> logger)
    {
        _signalOutcomeRepository = signalOutcomeRepository;
        _marketSnapshotRepository = marketSnapshotRepository;
        _logger = logger;
    }

    public async Task<SignalOutcomeEvaluationResult> EvaluateAsync(
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var requestedLimit = NormalizeLimit(limit);
        var evaluatedAtUtc = EnsureUtc(DateTime.UtcNow);
        var candidates = await _signalOutcomeRepository.GetEvaluationCandidatesAsync(
            requestedLimit,
            cancellationToken);

        var updatedPartialCount = 0;
        var evaluatedCount = 0;
        var pendingCount = 0;
        var unevaluableCount = 0;
        var failedCount = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await EvaluateCandidateAsync(candidate, evaluatedAtUtc, cancellationToken);
            await _signalOutcomeRepository.UpsertAsync(outcome, cancellationToken);

            if (outcome.EvaluationStatus == SignalOutcomeStatuses.Pending &&
                HasPartialCheckpointData(outcome))
            {
                updatedPartialCount++;
            }

            switch (outcome.EvaluationStatus)
            {
                case SignalOutcomeStatuses.Evaluated:
                    evaluatedCount++;
                    break;
                case SignalOutcomeStatuses.Pending:
                    pendingCount++;
                    break;
                case SignalOutcomeStatuses.Unevaluable:
                    unevaluableCount++;
                    break;
                case SignalOutcomeStatuses.Failed:
                    failedCount++;
                    break;
            }
        }

        return new SignalOutcomeEvaluationResult(
            evaluatedAtUtc,
            requestedLimit,
            candidates.Count,
            updatedPartialCount,
            evaluatedCount,
            pendingCount,
            unevaluableCount,
            failedCount);
    }

    public Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default)
    {
        return _signalOutcomeRepository.GetOutcomesAsync(
            NormalizeQuery(query),
            cancellationToken);
    }

    public async Task<SignalOutcomeSummary> GetSummaryAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default)
    {
        var outcomes = await GetOutcomesAsync(query, cancellationToken);
        var evaluated = outcomes
            .Where(outcome => outcome.EvaluationStatus == SignalOutcomeStatuses.Evaluated)
            .ToArray();
        var returns15m = GetCheckpointReturns(outcomes, outcome => outcome.PriceAfter15Minutes);
        var returns1h = GetCheckpointReturns(outcomes, outcome => outcome.PriceAfter1Hour);
        var returns4h = GetCheckpointReturns(outcomes, outcome => outcome.PriceAfter4Hours);
        var best15m = returns15m
            .OrderByDescending(item => item.ReturnPercent)
            .ThenBy(item => item.Symbol)
            .FirstOrDefault();
        var worst15m = returns15m
            .OrderBy(item => item.ReturnPercent)
            .ThenBy(item => item.Symbol)
            .FirstOrDefault();
        var best1h = returns1h
            .OrderByDescending(item => item.ReturnPercent)
            .ThenBy(item => item.Symbol)
            .FirstOrDefault();
        var worst1h = returns1h
            .OrderBy(item => item.ReturnPercent)
            .ThenBy(item => item.Symbol)
            .FirstOrDefault();
        var successfulCount = evaluated.Count(outcome => outcome.IsSuccessful == true);
        var unsuccessfulCount = evaluated.Count(outcome => outcome.IsSuccessful == false);

        return new SignalOutcomeSummary(
            EnsureUtc(DateTime.UtcNow),
            outcomes.Count,
            evaluated.Length,
            outcomes.Count(outcome => outcome.EvaluationStatus == SignalOutcomeStatuses.Pending),
            outcomes.Count(outcome => outcome.EvaluationStatus == SignalOutcomeStatuses.Unevaluable),
            outcomes.Count(outcome => outcome.EvaluationStatus == SignalOutcomeStatuses.Failed),
            successfulCount,
            unsuccessfulCount,
            evaluated.Length == 0 ? null : RoundPercent(successfulCount * 100m / evaluated.Length),
            Average(evaluated.Select(outcome => outcome.OutcomePercent)),
            Average(evaluated.Select(outcome => outcome.MaxRunupPercent)),
            Average(evaluated.Select(outcome => outcome.MaxDrawdownPercent)),
            returns15m.Length,
            Average(returns15m.Select(item => (decimal?)item.ReturnPercent)),
            returns1h.Length,
            Average(returns1h.Select(item => (decimal?)item.ReturnPercent)),
            returns4h.Length,
            Average(returns4h.Select(item => (decimal?)item.ReturnPercent)),
            best15m?.Symbol,
            worst15m?.Symbol,
            best15m?.ReturnPercent,
            worst15m?.ReturnPercent,
            best1h?.Symbol,
            best1h?.ReturnPercent,
            worst1h?.Symbol,
            worst1h?.ReturnPercent);
    }

    public async Task<SignalOutcomeSetupSummary> GetSetupSummaryAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default)
    {
        var outcomes = await GetOutcomesAsync(query, cancellationToken);
        var items = outcomes
            .GroupBy(outcome => NormalizeSetup(outcome.Setup), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupOutcomes = group.ToArray();
                var returns15m = GetCheckpointReturns(groupOutcomes, outcome => outcome.PriceAfter15Minutes);
                var returns1h = GetCheckpointReturns(groupOutcomes, outcome => outcome.PriceAfter1Hour);
                var returns4h = GetCheckpointReturns(groupOutcomes, outcome => outcome.PriceAfter4Hours);

                return new SignalOutcomeSetupSummaryItem(
                    group.Key,
                    groupOutcomes.Length,
                    returns15m.Length,
                    Average(returns15m.Select(item => (decimal?)item.ReturnPercent)),
                    returns1h.Length,
                    Average(returns1h.Select(item => (decimal?)item.ReturnPercent)),
                    returns4h.Length,
                    Average(returns4h.Select(item => (decimal?)item.ReturnPercent)));
            })
            .OrderByDescending(item => item.AverageReturn15m ?? decimal.MinValue)
            .ThenBy(item => item.Setup, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rankableItems = items
            .Where(item => item.CountWith15m >= MinimumSetupRankingSampleSize &&
                item.AverageReturn15m.HasValue)
            .ToArray();
        var bestSetup = rankableItems
            .OrderByDescending(item => item.AverageReturn15m)
            .ThenBy(item => item.Setup, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var worstSetup = rankableItems
            .OrderBy(item => item.AverageReturn15m)
            .ThenBy(item => item.Setup, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new SignalOutcomeSetupSummary(
            EnsureUtc(DateTime.UtcNow),
            items.Length,
            bestSetup?.Setup,
            bestSetup?.AverageReturn15m,
            worstSetup?.Setup,
            worstSetup?.AverageReturn15m,
            items);
    }

    public async Task<SignalOutcomeScoreBucketSummary> GetScoreBucketSummaryAsync(
        SignalOutcomeQuery query,
        CancellationToken cancellationToken = default)
    {
        var outcomes = await GetOutcomesAsync(query, cancellationToken);
        var confidenceItems = outcomes
            .GroupBy(outcome => NormalizeConfidence(outcome.Confidence), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var aggregate = AggregatePartialPerformance(group.ToArray());

                return new SignalOutcomeConfidenceSummaryItem(
                    group.Key,
                    aggregate.Count,
                    aggregate.CountWith15m,
                    aggregate.AverageReturn15m,
                    aggregate.CountWith1h,
                    aggregate.AverageReturn1h,
                    aggregate.BestSymbol15m,
                    aggregate.WorstSymbol15m);
            })
            .OrderBy(item => ConfidenceSortOrder(item.Confidence))
            .ThenBy(item => item.Confidence, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var scoreBucketItems = outcomes
            .GroupBy(outcome => GetScoreBucket(outcome.Score))
            .Select(group =>
            {
                var aggregate = AggregatePartialPerformance(group.ToArray());

                return new SignalOutcomeScoreBucketSummaryItem(
                    group.Key.Label,
                    group.Key.MinScore,
                    group.Key.MaxScore,
                    aggregate.Count,
                    aggregate.CountWith15m,
                    aggregate.AverageReturn15m,
                    aggregate.CountWith1h,
                    aggregate.AverageReturn1h,
                    aggregate.BestSymbol15m,
                    aggregate.WorstSymbol15m);
            })
            .OrderBy(item => item.MinScore ?? decimal.MaxValue)
            .ThenBy(item => item.Bucket, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SignalOutcomeScoreBucketSummary(
            EnsureUtc(DateTime.UtcNow),
            confidenceItems,
            scoreBucketItems);
    }

    private async Task<SignalOutcomeRecord> EvaluateCandidateAsync(
        SignalOutcomeEvaluationCandidate candidate,
        DateTime evaluatedAtUtc,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = EnsureUtc(candidate.CreatedAtUtc);
        evaluatedAtUtc = EnsureUtc(evaluatedAtUtc);

        if (candidate.Price is null or <= 0)
        {
            return CreateUnevaluableOutcome(
                candidate,
                evaluatedAtUtc,
                "Signal baseline price is missing.");
        }

        try
        {
            var horizonEndUtc = EnsureUtc(createdAtUtc.Add(OneDay));
            var snapshotSearchEndUtc = EnsureUtc(DateTime.MaxValue);
            var snapshots = await _marketSnapshotRepository.GetFutureMarketSnapshotsAsync(
                candidate.Symbol,
                createdAtUtc,
                snapshotSearchEndUtc,
                cancellationToken);
            var windowSnapshots = snapshots
                .Where(snapshot => EnsureUtc(snapshot.CapturedAtUtc) >= createdAtUtc)
                .OrderBy(snapshot => EnsureUtc(snapshot.CapturedAtUtc))
                .ToArray();

            if (windowSnapshots.Length == 0)
            {
                return CreatePendingOutcome(
                    candidate,
                    evaluatedAtUtc,
                    candidate.Price,
                    "No future market snapshots available yet.");
            }

            var priceAfter15Minutes = SelectCheckpoint(candidate, windowSnapshots, "15m", EnsureUtc(createdAtUtc.Add(FifteenMinutes)));
            var priceAfter1Hour = SelectCheckpoint(candidate, windowSnapshots, "1h", EnsureUtc(createdAtUtc.Add(OneHour)));
            var priceAfter4Hours = SelectCheckpoint(candidate, windowSnapshots, "4h", EnsureUtc(createdAtUtc.Add(FourHours)));
            var priceAfter1Day = SelectCheckpoint(candidate, windowSnapshots, "1d", horizonEndUtc);
            var direction = DetermineDirection(candidate.Action);
            var partialWindowSnapshots = GetPartialWindowSnapshots(
                windowSnapshots,
                priceAfter15Minutes,
                priceAfter1Hour,
                priceAfter4Hours,
                priceAfter1Day);
            var maxRunupPercent = partialWindowSnapshots.Length == 0
                ? null
                : (decimal?)CalculateMaxRunupPercent(candidate.Price.Value, partialWindowSnapshots, direction);
            var maxDrawdownPercent = partialWindowSnapshots.Length == 0
                ? null
                : (decimal?)CalculateMaxDrawdownPercent(candidate.Price.Value, partialWindowSnapshots, direction);
            var outcomePercent = priceAfter1Day is null
                ? null
                : (decimal?)CalculatePercentChange(candidate.Price.Value, priceAfter1Day.Snapshot.Price);
            var status = priceAfter1Day is null
                ? SignalOutcomeStatuses.Pending
                : SignalOutcomeStatuses.Evaluated;

            return new SignalOutcomeRecord(
                Guid.NewGuid(),
                candidate.SignalSnapshotId,
                evaluatedAtUtc,
                status,
                candidate.Price,
                priceAfter15Minutes?.Snapshot.Price,
                priceAfter1Hour?.Snapshot.Price,
                priceAfter4Hours?.Snapshot.Price,
                priceAfter1Day?.Snapshot.Price,
                maxRunupPercent,
                maxDrawdownPercent,
                outcomePercent,
                outcomePercent is null ? null : DetermineSuccess(outcomePercent.Value, direction),
                status == SignalOutcomeStatuses.Pending ? "Outcome horizon has not elapsed." : null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to evaluate signal outcome for signal snapshot {SignalSnapshotId}.",
                candidate.SignalSnapshotId);

            return new SignalOutcomeRecord(
                Guid.NewGuid(),
                candidate.SignalSnapshotId,
                evaluatedAtUtc,
                SignalOutcomeStatuses.Failed,
                candidate.Price,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                exception.Message);
        }
    }

    private static SignalOutcomeRecord CreatePendingOutcome(
        SignalOutcomeEvaluationCandidate candidate,
        DateTime evaluatedAtUtc,
        decimal? priceAtSignal,
        string reason)
    {
        return new SignalOutcomeRecord(
            Guid.NewGuid(),
            candidate.SignalSnapshotId,
            evaluatedAtUtc,
            SignalOutcomeStatuses.Pending,
            priceAtSignal,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            reason);
    }

    private static SignalOutcomeRecord CreateUnevaluableOutcome(
        SignalOutcomeEvaluationCandidate candidate,
        DateTime evaluatedAtUtc,
        string reason)
    {
        return new SignalOutcomeRecord(
            Guid.NewGuid(),
            candidate.SignalSnapshotId,
            evaluatedAtUtc,
            SignalOutcomeStatuses.Unevaluable,
            candidate.Price,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            reason);
    }

    private CheckpointPrice? SelectCheckpoint(
        SignalOutcomeEvaluationCandidate candidate,
        IReadOnlyCollection<MarketSnapshot> snapshots,
        string checkpointName,
        DateTime checkpointUtc)
    {
        checkpointUtc = EnsureUtc(checkpointUtc);

        var snapshot = snapshots
            .Where(snapshot => EnsureUtc(snapshot.CapturedAtUtc) >= checkpointUtc)
            .OrderBy(snapshot => EnsureUtc(snapshot.CapturedAtUtc))
            .FirstOrDefault();

        if (snapshot is null)
        {
            _logger.LogInformation(
                "Signal outcome checkpoint {CheckpointName} for {Symbol} signal {SignalSnapshotId}: checkpointUtc={CheckpointUtc}, no matching market snapshot found.",
                checkpointName,
                candidate.Symbol,
                candidate.SignalSnapshotId,
                checkpointUtc);

            return null;
        }

        _logger.LogInformation(
            "Signal outcome checkpoint {CheckpointName} for {Symbol} signal {SignalSnapshotId}: checkpointUtc={CheckpointUtc}, matchedSnapshotUtc={MatchedSnapshotUtc}, matchedSnapshotPrice={MatchedSnapshotPrice}.",
            checkpointName,
            candidate.Symbol,
            candidate.SignalSnapshotId,
            checkpointUtc,
            EnsureUtc(snapshot.CapturedAtUtc),
            snapshot.Price);

        return new CheckpointPrice(snapshot);
    }

    private static MarketSnapshot[] GetPartialWindowSnapshots(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        params CheckpointPrice?[] checkpoints)
    {
        var latestCheckpointAtUtc = checkpoints
            .Where(checkpoint => checkpoint is not null)
            .Select(checkpoint => EnsureUtc(checkpoint!.Snapshot.CapturedAtUtc))
            .DefaultIfEmpty()
            .Max();

        if (latestCheckpointAtUtc == default)
        {
            return [];
        }

        return snapshots
            .Where(snapshot => EnsureUtc(snapshot.CapturedAtUtc) <= latestCheckpointAtUtc)
            .OrderBy(snapshot => snapshot.CapturedAtUtc)
            .ToArray();
    }

    private static bool HasPartialCheckpointData(SignalOutcomeRecord outcome)
    {
        return outcome.PriceAfter15Minutes.HasValue ||
            outcome.PriceAfter1Hour.HasValue ||
            outcome.PriceAfter4Hours.HasValue ||
            outcome.MaxRunupPercent.HasValue ||
            outcome.MaxDrawdownPercent.HasValue;
    }

    private static decimal CalculateMaxRunupPercent(
        decimal baseline,
        IReadOnlyCollection<MarketSnapshot> snapshots,
        SignalOutcomeDirection direction)
    {
        var bestPrice = direction == SignalOutcomeDirection.Bearish
            ? snapshots.Min(GetLowPrice)
            : snapshots.Max(GetHighPrice);

        return direction == SignalOutcomeDirection.Bearish
            ? RoundPercent((baseline - bestPrice) / baseline * 100m)
            : RoundPercent((bestPrice - baseline) / baseline * 100m);
    }

    private static decimal CalculateMaxDrawdownPercent(
        decimal baseline,
        IReadOnlyCollection<MarketSnapshot> snapshots,
        SignalOutcomeDirection direction)
    {
        var worstPrice = direction == SignalOutcomeDirection.Bearish
            ? snapshots.Max(GetHighPrice)
            : snapshots.Min(GetLowPrice);

        return direction == SignalOutcomeDirection.Bearish
            ? RoundPercent((baseline - worstPrice) / baseline * 100m)
            : RoundPercent((worstPrice - baseline) / baseline * 100m);
    }

    private static decimal CalculatePercentChange(decimal baseline, decimal price)
    {
        return RoundPercent((price - baseline) / baseline * 100m);
    }

    private static CheckpointReturn[] GetCheckpointReturns(
        IReadOnlyCollection<SignalOutcomeItem> outcomes,
        Func<SignalOutcomeItem, decimal?> checkpointSelector)
    {
        return outcomes
            .Where(outcome => outcome.PriceAtSignal is > 0 &&
                checkpointSelector(outcome).HasValue)
            .Select(outcome => new CheckpointReturn(
                outcome.Symbol,
                CalculatePercentChange(outcome.PriceAtSignal!.Value, checkpointSelector(outcome)!.Value)))
            .ToArray();
    }

    private static PartialPerformanceAggregate AggregatePartialPerformance(
        IReadOnlyCollection<SignalOutcomeItem> outcomes)
    {
        var returns15m = GetCheckpointReturns(outcomes, outcome => outcome.PriceAfter15Minutes);
        var returns1h = GetCheckpointReturns(outcomes, outcome => outcome.PriceAfter1Hour);
        var best15m = returns15m
            .OrderByDescending(item => item.ReturnPercent)
            .ThenBy(item => item.Symbol)
            .FirstOrDefault();
        var worst15m = returns15m
            .OrderBy(item => item.ReturnPercent)
            .ThenBy(item => item.Symbol)
            .FirstOrDefault();

        return new PartialPerformanceAggregate(
            outcomes.Count,
            returns15m.Length,
            Average(returns15m.Select(item => (decimal?)item.ReturnPercent)),
            returns1h.Length,
            Average(returns1h.Select(item => (decimal?)item.ReturnPercent)),
            best15m?.Symbol,
            worst15m?.Symbol);
    }

    private static decimal GetHighPrice(MarketSnapshot snapshot)
    {
        return snapshot.HighPrice ?? snapshot.Price;
    }

    private static decimal GetLowPrice(MarketSnapshot snapshot)
    {
        return snapshot.LowPrice ?? snapshot.Price;
    }

    private static bool? DetermineSuccess(
        decimal outcomePercent,
        SignalOutcomeDirection direction)
    {
        return direction switch
        {
            SignalOutcomeDirection.Bullish => outcomePercent > 0,
            SignalOutcomeDirection.Bearish => outcomePercent < 0,
            _ => null
        };
    }

    private static SignalOutcomeDirection DetermineDirection(string action)
    {
        if (action.Contains("short", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("sell", StringComparison.OrdinalIgnoreCase))
        {
            return SignalOutcomeDirection.Bearish;
        }

        if (action.Contains("avoid", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("watch", StringComparison.OrdinalIgnoreCase))
        {
            return SignalOutcomeDirection.Neutral;
        }

        return SignalOutcomeDirection.Bullish;
    }

    private static int NormalizeLimit(int? limit)
    {
        return Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
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

    private static SignalOutcomeQuery NormalizeQuery(SignalOutcomeQuery query)
    {
        return query with
        {
            Limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit),
            Days = query.Days is null ? null : Math.Clamp(query.Days.Value, 1, 3650),
            Symbol = string.IsNullOrWhiteSpace(query.Symbol) ? null : query.Symbol.Trim().ToUpperInvariant(),
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim()
        };
    }

    private static string NormalizeSetup(string? setup)
    {
        return string.IsNullOrWhiteSpace(setup)
            ? "Unknown"
            : setup.Trim();
    }

    private static string NormalizeConfidence(string? confidence)
    {
        if (string.IsNullOrWhiteSpace(confidence))
        {
            return "Unknown";
        }

        return confidence.Trim() switch
        {
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => "High",
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => "Medium",
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => "Low",
            var value => value
        };
    }

    private static int ConfidenceSortOrder(string confidence)
    {
        return confidence switch
        {
            "High" => 0,
            "Medium" => 1,
            "Low" => 2,
            "Unknown" => 3,
            _ => 4
        };
    }

    private static ScoreBucketDefinition GetScoreBucket(decimal score)
    {
        foreach (var bucket in ScoreBuckets)
        {
            if (score > bucket.MinExclusive && score <= bucket.MaxScore)
            {
                return bucket;
            }

            if (bucket.MinExclusive == 0m && score >= 0m && score <= bucket.MaxScore)
            {
                return bucket;
            }
        }

        return new ScoreBucketDefinition("OutOfRange", null, null, decimal.MaxValue);
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var concreteValues = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return concreteValues.Length == 0
            ? null
            : RoundPercent(concreteValues.Average());
    }

    private static decimal RoundPercent(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private enum SignalOutcomeDirection
    {
        Bullish,
        Bearish,
        Neutral
    }

    private sealed record CheckpointPrice(MarketSnapshot Snapshot);

    private sealed record CheckpointReturn(string Symbol, decimal ReturnPercent);

    private sealed record PartialPerformanceAggregate(
        int Count,
        int CountWith15m,
        decimal? AverageReturn15m,
        int CountWith1h,
        decimal? AverageReturn1h,
        string? BestSymbol15m,
        string? WorstSymbol15m);

    private sealed record ScoreBucketDefinition(
        string Label,
        decimal? MinScore,
        decimal? MaxScore,
        decimal MinExclusive);
}
