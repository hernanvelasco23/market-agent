using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class ScoreAttributionServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetAsync_ReturnsPersistedAttribution_WhenJsonExists()
    {
        var signalSnapshotId = Guid.NewGuid();
        var attribution = ScoreAttributionBuilder.Build(
            100m,
            [new MarketSignalScoreFactor("RelativeStrengthVsSpy", 60m)]);
        var service = CreateService(new ScoreAttributionSnapshot(
            signalSnapshotId,
            Guid.NewGuid(),
            "MSFT",
            "MomentumContinuation",
            "Candidate",
            100m,
            DateTime.UtcNow,
            JsonSerializer.Serialize(attribution, JsonOptions),
            null));

        var result = await service.GetAsync(signalSnapshotId);

        Assert.NotNull(result);
        Assert.Equal(signalSnapshotId, result.SignalSnapshotId);
        Assert.Equal(110m, result.Attribution.UncappedScore);
        Assert.True(result.Attribution.WasCapped);
        Assert.Equal("RelativeStrengthVsSpy", result.Attribution.DominantPositiveFactor);
    }

    [Fact]
    public async Task GetAsync_ReconstructsAttribution_FromLegacyScoreBreakdownJson()
    {
        var signalSnapshotId = Guid.NewGuid();
        var scoreBreakdownJson = JsonSerializer.Serialize<IReadOnlyCollection<MarketSignalScoreFactor>>(
            [
                new MarketSignalScoreFactor("PositiveEmaStack", 15m),
                new MarketSignalScoreFactor("OverextensionRisk", -5m)
            ],
            JsonOptions);
        var service = CreateService(new ScoreAttributionSnapshot(
            signalSnapshotId,
            Guid.NewGuid(),
            "AAPL",
            "Pullback",
            "Watch for confirmation",
            60m,
            DateTime.UtcNow,
            null,
            scoreBreakdownJson));

        var result = await service.GetAsync(signalSnapshotId);

        Assert.NotNull(result);
        Assert.Equal(60m, result.Attribution.UncappedScore);
        Assert.False(result.Attribution.WasCapped);
        Assert.Equal("PositiveEmaStack", result.Attribution.DominantPositiveFactor);
        Assert.Equal("OverextensionRisk", result.Attribution.DominantNegativeFactor);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenSnapshotDoesNotExist()
    {
        var service = CreateService(null);

        var result = await service.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    private static ScoreAttributionService CreateService(ScoreAttributionSnapshot? snapshot)
    {
        return new ScoreAttributionService(
            new StubSignalSnapshotHistoryRepository(snapshot),
            NullLogger<ScoreAttributionService>.Instance);
    }

    private sealed class StubSignalSnapshotHistoryRepository : ISignalSnapshotHistoryRepository
    {
        private readonly ScoreAttributionSnapshot? _snapshot;

        public StubSignalSnapshotHistoryRepository(ScoreAttributionSnapshot? snapshot)
        {
            _snapshot = snapshot;
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
            throw new NotSupportedException();
        }

        public Task<ScoreAttributionSnapshot?> GetScoreAttributionSnapshotAsync(
            Guid signalSnapshotId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot?.SignalSnapshotId == signalSnapshotId
                ? _snapshot
                : null);
        }
    }
}
