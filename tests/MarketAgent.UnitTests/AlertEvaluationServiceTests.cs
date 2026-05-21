using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Alerts;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class AlertEvaluationServiceTests
{
    [Fact]
    public async Task EvaluateAsync_CreatesAlert_ForEligibleCandidate()
    {
        var signalSnapshotId = Guid.NewGuid();
        var alertRepository = new RecordingAlertEventRepository();
        var service = CreateService(
            [CreateCandidate(signalSnapshotId, score: 90m, confidence: "High", action: "Candidate", priceAtSignal: 123.45m)],
            alertRepository,
            [new SignalOutcomeSetupSummaryItem("MomentumContinuation", 3, 3, 1.25m, 1, 0.5m, 0, null)]);

        var result = await service.EvaluateAsync(limit: 10);
        var alert = Assert.Single(alertRepository.Saved);
        using var reasonJson = JsonDocument.Parse(alert.ReasonJson);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.SkippedRuleCount);
        Assert.Equal(signalSnapshotId, alert.SignalSnapshotId);
        Assert.Equal(AlertTypes.HighQualityCandidate, alert.AlertType);
        Assert.Equal(AlertDeliveryStatuses.InternalOnly, alert.DeliveryStatus);
        Assert.Equal(123.45m, alert.PriceAtSignal);
        Assert.Equal(90m, reasonJson.RootElement.GetProperty("score").GetDecimal());
        Assert.Equal("High", reasonJson.RootElement.GetProperty("confidence").GetString());
        Assert.True(reasonJson.RootElement.GetProperty("ruleDecisions").GetProperty("meetsSetupPerformance").GetBoolean());
    }

    [Theory]
    [InlineData(84.99, "High", "Candidate", 100.0)]
    [InlineData(90.0, "Low", "Candidate", 100.0)]
    [InlineData(90.0, "High", "Watch for confirmation", 100.0)]
    [InlineData(90, "High", "Candidate", null)]
    public async Task EvaluateAsync_SkipsCandidate_WhenRequiredRuleFails(
        double score,
        string confidence,
        string action,
        double? priceAtSignal)
    {
        var alertRepository = new RecordingAlertEventRepository();
        var service = CreateService(
            [CreateCandidate(Guid.NewGuid(), (decimal)score, confidence, action, priceAtSignal is null ? null : (decimal)priceAtSignal.Value)],
            alertRepository,
            []);

        var result = await service.EvaluateAsync(limit: 10);

        Assert.Empty(alertRepository.Saved);
        Assert.Equal(1, result.SkippedRuleCount);
        Assert.Equal(0, result.CreatedCount);
    }

    [Fact]
    public async Task EvaluateAsync_SkipsDuplicateSignalSnapshotAndAlertType()
    {
        var signalSnapshotId = Guid.NewGuid();
        var alertRepository = new RecordingAlertEventRepository(
            [CreateAlertKey(signalSnapshotId, AlertTypes.HighQualityCandidate)]);
        var service = CreateService(
            [CreateCandidate(signalSnapshotId, score: 95m, confidence: "Medium", action: "Candidate", priceAtSignal: 100m)],
            alertRepository,
            []);

        var result = await service.EvaluateAsync(limit: 10);

        Assert.Empty(alertRepository.Saved);
        Assert.Equal(1, result.SkippedDuplicateCount);
        Assert.Equal(0, result.CreatedCount);
    }

    [Fact]
    public async Task EvaluateAsync_BlocksAlert_WhenSetupAnalyticsAreNonPositive()
    {
        var alertRepository = new RecordingAlertEventRepository();
        var service = CreateService(
            [CreateCandidate(Guid.NewGuid(), score: 95m, confidence: "High", action: "Candidate", priceAtSignal: 100m)],
            alertRepository,
            [new SignalOutcomeSetupSummaryItem("MomentumContinuation", 3, 3, 0m, 1, 0.5m, 0, null)]);

        var result = await service.EvaluateAsync(limit: 10);

        Assert.Empty(alertRepository.Saved);
        Assert.Equal(1, result.SkippedRuleCount);
        Assert.Equal(0, result.CreatedCount);
    }

    private static AlertEvaluationService CreateService(
        IReadOnlyCollection<AlertEvaluationCandidate> candidates,
        IAlertEventRepository alertRepository,
        IReadOnlyCollection<SignalOutcomeSetupSummaryItem> setupItems)
    {
        return new AlertEvaluationService(
            new StubSignalSnapshotHistoryRepository(candidates),
            alertRepository,
            new StubSignalOutcomeService(setupItems),
            NullLogger<AlertEvaluationService>.Instance);
    }

    private static AlertEvaluationCandidate CreateCandidate(
        Guid signalSnapshotId,
        decimal score,
        string confidence,
        string action,
        decimal? priceAtSignal)
    {
        return new AlertEvaluationCandidate(
            signalSnapshotId,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "MSFT",
            "MomentumContinuation",
            action,
            score,
            confidence,
            priceAtSignal);
    }

    private static string CreateAlertKey(Guid signalSnapshotId, string alertType)
    {
        return $"{signalSnapshotId:N}:{alertType}";
    }

    private sealed class StubSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
    {
        private readonly IReadOnlyCollection<AlertEvaluationCandidate> _candidates;

        public StubSignalSnapshotHistoryRepository(IReadOnlyCollection<AlertEvaluationCandidate> candidates)
        {
            _candidates = candidates;
        }

        public Task AppendAsync(
            Guid runId,
            DateTime createdAtUtc,
            IReadOnlyCollection<MarketAgent.Domain.Entities.MarketSignal> signals,
            string? marketRegime,
            string? triggeredAlertsJson,
            string source,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<AlertEvaluationCandidate>> GetAlertCandidatesAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AlertEvaluationCandidate>>(
                _candidates.Take(limit).ToArray());
        }

        public Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
            Guid signalSnapshotId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingAlertEventRepository : IAlertEventRepository
    {
        private readonly HashSet<string> _existingKeys;
        private readonly List<AlertEventRecord> _saved = [];

        public RecordingAlertEventRepository(IReadOnlyCollection<string>? existingKeys = null)
        {
            _existingKeys = (existingKeys ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<AlertEventRecord> Saved => _saved;

        public Task<IReadOnlyCollection<AlertEventItem>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlySet<string>> GetExistingAlertKeysAsync(
            IReadOnlyCollection<AlertEvaluationCandidate> candidates,
            string alertType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlySet<string>>(_existingKeys);
        }

        public Task SaveAsync(
            AlertEventRecord alertEvent,
            CancellationToken cancellationToken = default)
        {
            _saved.Add(alertEvent);
            _existingKeys.Add(CreateAlertKey(alertEvent.SignalSnapshotId, alertEvent.AlertType));

            return Task.CompletedTask;
        }
    }

    private sealed class StubSignalOutcomeService : ISignalOutcomeService
    {
        private readonly IReadOnlyCollection<SignalOutcomeSetupSummaryItem> _setupItems;

        public StubSignalOutcomeService(IReadOnlyCollection<SignalOutcomeSetupSummaryItem> setupItems)
        {
            _setupItems = setupItems;
        }

        public Task<SignalOutcomeEvaluationResult> EvaluateAsync(
            int? limit,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SignalOutcomeSummary> GetSummaryAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SignalOutcomeSetupSummary> GetSetupSummaryAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SignalOutcomeSetupSummary(
                DateTime.UtcNow,
                _setupItems.Count,
                null,
                null,
                null,
                null,
                _setupItems));
        }

        public Task<SignalOutcomeScoreBucketSummary> GetScoreBucketSummaryAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
