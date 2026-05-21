using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.Alerts;

public sealed class AlertEvaluationService : IAlertEvaluationService
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;
    private const decimal HighQualityScoreThreshold = 85m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISignalSnapshotHistoryRepository _signalSnapshotHistoryRepository;
    private readonly IAlertEventRepository _alertEventRepository;
    private readonly ISignalOutcomeService _signalOutcomeService;
    private readonly ILogger<AlertEvaluationService> _logger;

    public AlertEvaluationService(
        ISignalSnapshotHistoryRepository signalSnapshotHistoryRepository,
        IAlertEventRepository alertEventRepository,
        ISignalOutcomeService signalOutcomeService,
        ILogger<AlertEvaluationService> logger)
    {
        _signalSnapshotHistoryRepository = signalSnapshotHistoryRepository;
        _alertEventRepository = alertEventRepository;
        _signalOutcomeService = signalOutcomeService;
        _logger = logger;
    }

    public async Task<AlertEvaluationResult> EvaluateAsync(
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var requestedLimit = NormalizeLimit(limit);
        var evaluatedAtUtc = EnsureUtc(DateTime.UtcNow);
        var candidates = await _signalSnapshotHistoryRepository.GetAlertCandidatesAsync(
            requestedLimit,
            cancellationToken);
        var existingKeys = new HashSet<string>(
            await _alertEventRepository.GetExistingAlertKeysAsync(
                candidates,
                AlertTypes.HighQualityCandidate,
                cancellationToken),
            StringComparer.OrdinalIgnoreCase);
        var setupReturns = await GetSetupAverageReturnsAsync(cancellationToken);

        var createdCount = 0;
        var skippedDuplicateCount = 0;
        var skippedRuleCount = 0;
        var failedCount = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var alertKey = CreateAlertKey(candidate.SignalSnapshotId, AlertTypes.HighQualityCandidate);
                if (existingKeys.Contains(alertKey))
                {
                    skippedDuplicateCount++;
                    continue;
                }

                var decision = EvaluateCandidate(candidate, setupReturns);
                if (!decision.ShouldCreate)
                {
                    skippedRuleCount++;
                    continue;
                }

                var alertEvent = CreateAlertEvent(candidate, evaluatedAtUtc, decision);
                await _alertEventRepository.SaveAsync(alertEvent, cancellationToken);
                existingKeys.Add(alertKey);
                createdCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedCount++;
                _logger.LogError(
                    exception,
                    "Failed to evaluate alert candidate for signal snapshot {SignalSnapshotId}.",
                    candidate.SignalSnapshotId);
            }
        }

        return new AlertEvaluationResult(
            evaluatedAtUtc,
            requestedLimit,
            candidates.Count,
            createdCount,
            skippedDuplicateCount,
            skippedRuleCount,
            failedCount);
    }

    public Task<IReadOnlyCollection<AlertEventItem>> GetAlertsAsync(
        AlertEventQuery query,
        CancellationToken cancellationToken = default)
    {
        return _alertEventRepository.GetRecentAsync(
            NormalizeLimit(query.Limit),
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, decimal?>> GetSetupAverageReturnsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var setupSummary = await _signalOutcomeService.GetSetupSummaryAsync(
                new SignalOutcomeQuery(null, null, null, null, MaxLimit),
                cancellationToken);

            return setupSummary.Items.ToDictionary(
                item => NormalizeText(item.Setup),
                item => item.AverageReturn15m,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Setup analytics were unavailable during alert evaluation; continuing without setup performance gating.");
            return new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static AlertRuleDecision EvaluateCandidate(
        AlertEvaluationCandidate candidate,
        IReadOnlyDictionary<string, decimal?> setupReturns)
    {
        var normalizedAction = NormalizeText(candidate.Action);
        var normalizedConfidence = NormalizeText(candidate.Confidence);
        var normalizedSetup = NormalizeText(candidate.Setup);
        var isCandidate = normalizedAction.Equals("Candidate", StringComparison.OrdinalIgnoreCase);
        var meetsScore = candidate.Score >= HighQualityScoreThreshold;
        var meetsConfidence = normalizedConfidence.Equals("High", StringComparison.OrdinalIgnoreCase) ||
            normalizedConfidence.Equals("Medium", StringComparison.OrdinalIgnoreCase);
        var hasPrice = candidate.PriceAtSignal is > 0;
        var setupAnalyticsApplied = setupReturns.TryGetValue(normalizedSetup, out var setupAverageReturn15m) &&
            setupAverageReturn15m.HasValue;
        var meetsSetupPerformance = !setupAnalyticsApplied || setupAverageReturn15m > 0m;
        var shouldCreate = isCandidate &&
            meetsScore &&
            meetsConfidence &&
            hasPrice &&
            meetsSetupPerformance;

        return new AlertRuleDecision(
            shouldCreate,
            normalizedSetup,
            normalizedConfidence,
            setupAverageReturn15m,
            setupAnalyticsApplied,
            isCandidate,
            meetsScore,
            meetsConfidence,
            hasPrice,
            meetsSetupPerformance);
    }

    private static AlertEventRecord CreateAlertEvent(
        AlertEvaluationCandidate candidate,
        DateTime createdAtUtc,
        AlertRuleDecision decision)
    {
        var title = $"{candidate.Symbol} high-quality candidate";
        var message = $"{candidate.Symbol} generated a {decision.Confidence} confidence Candidate signal with score {candidate.Score:0.##}.";
        var reasonJson = JsonSerializer.Serialize(
            new
            {
                score = candidate.Score,
                confidence = decision.Confidence,
                setup = decision.Setup,
                setupAverageReturn15m = decision.SetupAverageReturn15m,
                thresholdsUsed = new
                {
                    minimumScore = HighQualityScoreThreshold,
                    allowedConfidences = new[] { "High", "Medium" },
                    requiredAction = "Candidate",
                    requiredSetupAverageReturn15mWhenAvailable = "> 0"
                },
                ruleDecisions = new
                {
                    isCandidate = decision.IsCandidate,
                    meetsScore = decision.MeetsScore,
                    meetsConfidence = decision.MeetsConfidence,
                    hasPriceAtSignal = decision.HasPriceAtSignal,
                    setupAnalyticsApplied = decision.SetupAnalyticsApplied,
                    meetsSetupPerformance = decision.MeetsSetupPerformance
                }
            },
            JsonOptions);

        return new AlertEventRecord(
            Guid.NewGuid(),
            EnsureUtc(createdAtUtc),
            candidate.SignalSnapshotId,
            candidate.RunId,
            candidate.Symbol,
            decision.Setup,
            candidate.Action,
            candidate.Score,
            decision.Confidence,
            candidate.PriceAtSignal!.Value,
            AlertTypes.HighQualityCandidate,
            title,
            message,
            reasonJson,
            AlertDeliveryStatuses.InternalOnly);
    }

    private static string CreateAlertKey(Guid signalSnapshotId, string alertType)
    {
        return $"{signalSnapshotId:N}:{alertType}";
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim();
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

    private sealed record AlertRuleDecision(
        bool ShouldCreate,
        string Setup,
        string Confidence,
        decimal? SetupAverageReturn15m,
        bool SetupAnalyticsApplied,
        bool IsCandidate,
        bool MeetsScore,
        bool MeetsConfidence,
        bool HasPriceAtSignal,
        bool MeetsSetupPerformance);
}
