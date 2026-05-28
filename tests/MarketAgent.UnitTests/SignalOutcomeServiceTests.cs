using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class SignalOutcomeServiceTests
{
    [Fact]
    public async Task EvaluateAsync_StoresEvaluatedOutcome_WhenFutureMarketSnapshotsExist()
    {
        var createdAtUtc = DateTime.UtcNow.AddDays(-3);
        var signalSnapshotId = Guid.NewGuid();
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(signalSnapshotId, createdAtUtc, price: 100m, action: "Candidate")
        ]);
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot("MSFT", createdAtUtc.AddMinutes(15), high: 103m, low: 99m, price: 102m),
            CreateSnapshot("MSFT", createdAtUtc.AddHours(1), high: 104m, low: 98m, price: 103m),
            CreateSnapshot("MSFT", createdAtUtc.AddHours(4), high: 105m, low: 97m, price: 104m),
            CreateSnapshot("MSFT", createdAtUtc.AddDays(1), high: 110m, low: 96m, price: 108m)
        ]);
        var service = new SignalOutcomeService(
            repository,
            snapshots,
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(SignalOutcomeStatuses.Evaluated, outcome.EvaluationStatus);
        Assert.Equal(signalSnapshotId, outcome.SignalSnapshotId);
        Assert.Equal(102m, outcome.PriceAfter15Minutes);
        Assert.Equal(103m, outcome.PriceAfter1Hour);
        Assert.Equal(104m, outcome.PriceAfter4Hours);
        Assert.Equal(108m, outcome.PriceAfter1Day);
        Assert.Equal(10m, outcome.MaxRunupPercent);
        Assert.Equal(-4m, outcome.MaxDrawdownPercent);
        Assert.Equal(8m, outcome.OutcomePercent);
        Assert.True(outcome.IsSuccessful);
    }

    [Fact]
    public async Task EvaluateAsync_MarksUnevaluable_WhenBaselinePriceIsMissing()
    {
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(Guid.NewGuid(), DateTime.UtcNow.AddDays(-3), price: null, action: "Candidate")
        ]);
        var service = new SignalOutcomeService(
            repository,
            new StubMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.UnevaluableCount);
        Assert.Equal(SignalOutcomeStatuses.Unevaluable, outcome.EvaluationStatus);
        Assert.Equal("Signal baseline price is missing.", outcome.FailureReason);
    }

    [Fact]
    public async Task EvaluateAsync_MarksPending_WhenNoCheckpointHasElapsed()
    {
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), price: 100m, action: "Candidate")
        ]);
        var service = new SignalOutcomeService(
            repository,
            new StubMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(SignalOutcomeStatuses.Pending, outcome.EvaluationStatus);
        Assert.Equal("No future market snapshots available yet.", outcome.FailureReason);
    }

    [Fact]
    public async Task EvaluateAsync_UpdatesPartialCheckpoints_WhileOutcomeHorizonIsPending()
    {
        var createdAtUtc = DateTime.UtcNow.AddHours(-2);
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(Guid.NewGuid(), createdAtUtc, price: 100m, action: "Candidate")
        ]);
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot("MSFT", createdAtUtc.AddMinutes(15), high: 103m, low: 99m, price: 102m),
            CreateSnapshot("MSFT", createdAtUtc.AddHours(1), high: 106m, low: 98m, price: 105m)
        ]);
        var service = new SignalOutcomeService(
            repository,
            snapshots,
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.UpdatedPartialCount);
        Assert.Equal(SignalOutcomeStatuses.Pending, outcome.EvaluationStatus);
        Assert.Equal(102m, outcome.PriceAfter15Minutes);
        Assert.Equal(105m, outcome.PriceAfter1Hour);
        Assert.Null(outcome.PriceAfter4Hours);
        Assert.Null(outcome.PriceAfter1Day);
        Assert.Equal(6m, outcome.MaxRunupPercent);
        Assert.Equal(-2m, outcome.MaxDrawdownPercent);
        Assert.Null(outcome.OutcomePercent);
        Assert.Null(outcome.IsSuccessful);
    }

    [Fact]
    public async Task EvaluateAsync_UsesFirstSnapshotAfterCheckpoint_WhenTimestampIsNotExact()
    {
        var createdAtUtc = DateTime.UtcNow.AddHours(-2);
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(Guid.NewGuid(), createdAtUtc, price: 100m, action: "Candidate")
        ]);
        var snapshots = new StubMarketSnapshotRepository(
        [
            CreateSnapshot("MSFT", createdAtUtc.AddMinutes(103), high: 106m, low: 98m, price: 105m),
            CreateSnapshot("MSFT", createdAtUtc.AddMinutes(112), high: 107m, low: 97m, price: 106m)
        ]);
        var service = new SignalOutcomeService(
            repository,
            snapshots,
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.UpdatedPartialCount);
        Assert.Equal(SignalOutcomeStatuses.Pending, outcome.EvaluationStatus);
        Assert.Equal(105m, outcome.PriceAfter15Minutes);
        Assert.Equal(105m, outcome.PriceAfter1Hour);
        Assert.Null(outcome.PriceAfter4Hours);
        Assert.Null(outcome.PriceAfter1Day);
    }

    private static SignalOutcomeEvaluationCandidate CreateCandidate(
        Guid signalSnapshotId,
        DateTime createdAtUtc,
        decimal? price,
        string action)
    {
        return new SignalOutcomeEvaluationCandidate(
            signalSnapshotId,
            createdAtUtc,
            Guid.NewGuid(),
            "MSFT",
            "Momentum",
            action,
            65m,
            "Medium",
            price);
    }

    [Fact]
    public async Task EvaluateAsync_KeepsPending_WhenNoFutureMarketSnapshotsExist()
    {
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), price: 100m, action: "Candidate")
        ]);
        var service = new SignalOutcomeService(
            repository,
            new StubMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(SignalOutcomeStatuses.Pending, outcome.EvaluationStatus);
        Assert.Equal("No future market snapshots available yet.", outcome.FailureReason);
    }

    [Fact]
    public async Task EvaluateAsync_NormalizesUnspecifiedSignalCreatedAtUtc()
    {
        var createdAtUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-2), DateTimeKind.Unspecified);
        var repository = new RecordingSignalOutcomeRepository(
        [
            CreateCandidate(Guid.NewGuid(), createdAtUtc, price: 100m, action: "Candidate")
        ]);
        var service = new SignalOutcomeService(
            repository,
            new StrictUtcMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var result = await service.EvaluateAsync(limit: 10);
        var outcome = Assert.Single(repository.Upserts);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(SignalOutcomeStatuses.Pending, outcome.EvaluationStatus);
        Assert.Equal("No future market snapshots available yet.", outcome.FailureReason);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesPartialCheckpointMetrics()
    {
        var repository = new RecordingSignalOutcomeRepository(
            [],
            [
                CreateOutcomeItem("MSFT", priceAtSignal: 100m, priceAfter15Minutes: 105m, priceAfter1Hour: 110m, priceAfter4Hours: null),
                CreateOutcomeItem("NVDA", priceAtSignal: 200m, priceAfter15Minutes: 190m, priceAfter1Hour: null, priceAfter4Hours: 220m),
                CreateOutcomeItem("AAPL", priceAtSignal: 150m, priceAfter15Minutes: null, priceAfter1Hour: 153m, priceAfter4Hours: null)
            ]);
        var service = new SignalOutcomeService(
            repository,
            new StubMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var summary = await service.GetSummaryAsync(new SignalOutcomeQuery(null, null, null, null, null));

        Assert.Equal(0, summary.EvaluatedCount);
        Assert.Null(summary.WinRate);
        Assert.Null(summary.AverageOutcomePercent);
        Assert.Equal(2, summary.CountWith15m);
        Assert.Equal(0m, summary.AverageReturn15m);
        Assert.Equal(2, summary.CountWith1h);
        Assert.Equal(6m, summary.AverageReturn1h);
        Assert.Equal(1, summary.CountWith4h);
        Assert.Equal(10m, summary.AverageReturn4h);
        Assert.Equal("MSFT", summary.Best15mSymbol);
        Assert.Equal("NVDA", summary.Worst15mSymbol);
        Assert.Equal(5m, summary.Best15mReturnPercent);
        Assert.Equal(-5m, summary.Worst15mReturnPercent);
        Assert.Equal("MSFT", summary.Best1hSymbol);
        Assert.Equal(10m, summary.Best1hReturnPercent);
        Assert.Equal("AAPL", summary.Worst1hSymbol);
        Assert.Equal(2m, summary.Worst1hReturnPercent);
    }

    [Fact]
    public async Task GetSetupSummaryAsync_GroupsOutcomesByNormalizedSetup()
    {
        var repository = new RecordingSignalOutcomeRepository(
            [],
            [
                CreateOutcomeItem("MSFT", " MomentumContinuation ", priceAtSignal: 100m, priceAfter15Minutes: 103m, priceAfter1Hour: 104m, priceAfter4Hours: null),
                CreateOutcomeItem("NVDA", "momentumcontinuation", priceAtSignal: 200m, priceAfter15Minutes: 210m, priceAfter1Hour: 212m, priceAfter4Hours: null),
                CreateOutcomeItem("RKLB", "MomentumContinuation", priceAtSignal: 50m, priceAfter15Minutes: 55m, priceAfter1Hour: null, priceAfter4Hours: null),
                CreateOutcomeItem("AAPL", "Pullback", priceAtSignal: 100m, priceAfter15Minutes: 98m, priceAfter1Hour: 99m, priceAfter4Hours: null),
                CreateOutcomeItem("ABNB", "Pullback", priceAtSignal: 100m, priceAfter15Minutes: 99m, priceAfter1Hour: null, priceAfter4Hours: null),
                CreateOutcomeItem("PATH", "", priceAtSignal: 100m, priceAfter15Minutes: 101m, priceAfter1Hour: null, priceAfter4Hours: null)
            ]);
        var service = new SignalOutcomeService(
            repository,
            new StubMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var summary = await service.GetSetupSummaryAsync(new SignalOutcomeQuery(null, null, null, null, null));
        var momentum = Assert.Single(summary.Items, item => item.Setup == "MomentumContinuation");
        var pullback = Assert.Single(summary.Items, item => item.Setup == "Pullback");
        var unknown = Assert.Single(summary.Items, item => item.Setup == "Unknown");

        Assert.Equal(3, summary.TotalSetupCount);
        Assert.Equal(3, momentum.Count);
        Assert.Equal(3, momentum.CountWith15m);
        Assert.Equal(6m, momentum.AverageReturn15m);
        Assert.Equal(2, momentum.CountWith1h);
        Assert.Equal(5m, momentum.AverageReturn1h);
        Assert.Equal(2, pullback.CountWith15m);
        Assert.Equal(-1.5m, pullback.AverageReturn15m);
        Assert.Equal(1, unknown.CountWith15m);
        Assert.Equal("MomentumContinuation", summary.BestSetup);
        Assert.Equal(6m, summary.BestSetupAverageReturn15m);
        Assert.Equal("MomentumContinuation", summary.WorstSetup);
        Assert.Equal(6m, summary.WorstSetupAverageReturn15m);
    }

    [Fact]
    public async Task GetScoreBucketSummaryAsync_GroupsByConfidenceAndScoreBuckets()
    {
        var repository = new RecordingSignalOutcomeRepository(
            [],
            [
                CreateOutcomeItem("MSFT", "Momentum", 82m, " High ", priceAtSignal: 100m, priceAfter15Minutes: 110m, priceAfter1Hour: 112m, priceAfter4Hours: null),
                CreateOutcomeItem("NVDA", "Momentum", 79m, "high", priceAtSignal: 200m, priceAfter15Minutes: 210m, priceAfter1Hour: null, priceAfter4Hours: null),
                CreateOutcomeItem("AAPL", "Pullback", 65m, "Medium", priceAtSignal: 100m, priceAfter15Minutes: 97m, priceAfter1Hour: 99m, priceAfter4Hours: null),
                CreateOutcomeItem("ABNB", "Pullback", 35m, " Low ", priceAtSignal: 100m, priceAfter15Minutes: 102m, priceAfter1Hour: 103m, priceAfter4Hours: null),
                CreateOutcomeItem("PATH", "Risk", 15m, "", priceAtSignal: 100m, priceAfter15Minutes: null, priceAfter1Hour: null, priceAfter4Hours: null),
                CreateOutcomeItem("TSLA", "Risk", 105m, "Medium", priceAtSignal: 100m, priceAfter15Minutes: 101m, priceAfter1Hour: null, priceAfter4Hours: null)
            ]);
        var service = new SignalOutcomeService(
            repository,
            new StubMarketSnapshotRepository([]),
            NullLogger<SignalOutcomeService>.Instance);

        var summary = await service.GetScoreBucketSummaryAsync(new SignalOutcomeQuery(null, null, null, null, null));
        var high = Assert.Single(summary.ConfidenceItems, item => item.Confidence == "High");
        var medium = Assert.Single(summary.ConfidenceItems, item => item.Confidence == "Medium");
        var low = Assert.Single(summary.ConfidenceItems, item => item.Confidence == "Low");
        var unknown = Assert.Single(summary.ConfidenceItems, item => item.Confidence == "Unknown");
        var bucket61To80 = Assert.Single(summary.ScoreBucketItems, item => item.Bucket == "61-80");
        var bucket81To100 = Assert.Single(summary.ScoreBucketItems, item => item.Bucket == "81-100");
        var outOfRange = Assert.Single(summary.ScoreBucketItems, item => item.Bucket == "OutOfRange");

        Assert.Equal(2, high.Count);
        Assert.Equal(2, high.CountWith15m);
        Assert.Equal(7.5m, high.AverageReturn15m);
        Assert.Equal(1, high.CountWith1h);
        Assert.Equal(12m, high.AverageReturn1h);
        Assert.Equal("MSFT", high.BestSymbol15m);
        Assert.Equal("NVDA", high.WorstSymbol15m);
        Assert.Equal(2, medium.Count);
        Assert.Equal(-1m, medium.AverageReturn15m);
        Assert.Equal(1, low.Count);
        Assert.Equal(2m, low.AverageReturn15m);
        Assert.Equal(1, unknown.Count);
        Assert.Equal(0, unknown.CountWith15m);
        Assert.Equal(2, bucket61To80.Count);
        Assert.Equal(1m, bucket61To80.AverageReturn15m);
        Assert.Equal(1, bucket81To100.Count);
        Assert.Equal(10m, bucket81To100.AverageReturn15m);
        Assert.Equal(1, outOfRange.Count);
        Assert.Equal(1m, outOfRange.AverageReturn15m);
    }

    private static MarketSnapshot CreateSnapshot(
        string symbol,
        DateTime capturedAtUtc,
        decimal high,
        decimal low,
        decimal price)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            symbol,
            AssetType.Equity,
            price,
            "USD",
            DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc),
            "UnitTest",
            volume: 1000000m,
            openPrice: price,
            high,
            low,
            previousClose: price);
    }

    private static SignalOutcomeItem CreateOutcomeItem(
        string symbol,
        decimal priceAtSignal,
        decimal? priceAfter15Minutes,
        decimal? priceAfter1Hour,
        decimal? priceAfter4Hours)
    {
        return CreateOutcomeItem(symbol, "Momentum", 65m, "Medium", priceAtSignal, priceAfter15Minutes, priceAfter1Hour, priceAfter4Hours);
    }

    private static SignalOutcomeItem CreateOutcomeItem(
        string symbol,
        string setup,
        decimal priceAtSignal,
        decimal? priceAfter15Minutes,
        decimal? priceAfter1Hour,
        decimal? priceAfter4Hours)
    {
        return CreateOutcomeItem(symbol, setup, 65m, "Medium", priceAtSignal, priceAfter15Minutes, priceAfter1Hour, priceAfter4Hours);
    }

    private static SignalOutcomeItem CreateOutcomeItem(
        string symbol,
        string setup,
        decimal score,
        string confidence,
        decimal priceAtSignal,
        decimal? priceAfter15Minutes,
        decimal? priceAfter1Hour,
        decimal? priceAfter4Hours)
    {
        return new SignalOutcomeItem(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-2),
            Guid.NewGuid(),
            symbol,
            setup,
            "Candidate",
            score,
            confidence,
            priceAtSignal,
            2.4m,
            1.8m,
            4.2m,
            priceAtSignal * 0.99m,
            priceAtSignal * 0.97m,
            priceAtSignal * 0.94m,
            61m,
            priceAtSignal,
            priceAtSignal * 0.98m,
            priceAtSignal * 1.04m,
            priceAtSignal * 1.02m,
            priceAtSignal * 1.04m,
            priceAtSignal * 1.06m,
            1.5m,
            2.5m,
            3.5m,
            4m,
            DateTime.UtcNow,
            SignalOutcomeStatuses.Pending,
            priceAtSignal,
            priceAfter15Minutes,
            priceAfter1Hour,
            priceAfter4Hours,
            null,
            null,
            null,
            null,
            null,
            "Outcome horizon has not elapsed.");
    }

    private sealed class RecordingSignalOutcomeRepository : ISignalOutcomeRepository
    {
        private readonly IReadOnlyCollection<SignalOutcomeEvaluationCandidate> _candidates;
        private readonly IReadOnlyCollection<SignalOutcomeItem> _outcomes;
        private readonly List<SignalOutcomeRecord> _upserts = [];

        public RecordingSignalOutcomeRepository(
            IReadOnlyCollection<SignalOutcomeEvaluationCandidate> candidates,
            IReadOnlyCollection<SignalOutcomeItem>? outcomes = null)
        {
            _candidates = candidates;
            _outcomes = outcomes ?? [];
        }

        public IReadOnlyCollection<SignalOutcomeRecord> Upserts => _upserts;

        public Task<IReadOnlyCollection<SignalOutcomeEvaluationCandidate>> GetEvaluationCandidatesAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<SignalOutcomeEvaluationCandidate>>(
                _candidates.Take(limit).ToArray());
        }

        public Task UpsertAsync(
            SignalOutcomeRecord outcome,
            CancellationToken cancellationToken = default)
        {
            _upserts.Add(outcome);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_outcomes);
        }
    }

    private sealed class StubMarketSnapshotRepository : IMarketSnapshotRepository
    {
        private readonly IReadOnlyCollection<MarketSnapshot> _snapshots;

        public StubMarketSnapshotRepository(IReadOnlyCollection<MarketSnapshot> snapshots)
        {
            _snapshots = snapshots;
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<MarketSnapshot>>(
                _snapshots
                    .Where(snapshot => snapshot.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
                        snapshot.CapturedAtUtc >= fromUtc &&
                        snapshot.CapturedAtUtc <= toUtc)
                    .OrderBy(snapshot => snapshot.CapturedAtUtc)
                    .ToArray());
        }

        public Task SaveAsync(
            MarketSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StrictUtcMarketSnapshotRepository : IMarketSnapshotRepository
    {
        private readonly IReadOnlyCollection<MarketSnapshot> _snapshots;

        public StrictUtcMarketSnapshotRepository(IReadOnlyCollection<MarketSnapshot> snapshots)
        {
            _snapshots = snapshots;
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MarketSnapshot>> GetFutureMarketSnapshotsAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
        {
            if (fromUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Future snapshot start time must be UTC.", nameof(fromUtc));
            }

            if (toUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Future snapshot end time must be UTC.", nameof(toUtc));
            }

            return Task.FromResult(_snapshots);
        }

        public Task SaveAsync(
            MarketSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
