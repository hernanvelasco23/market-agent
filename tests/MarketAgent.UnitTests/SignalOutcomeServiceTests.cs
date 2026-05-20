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
        return new SignalOutcomeItem(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-2),
            Guid.NewGuid(),
            symbol,
            "Momentum",
            "Candidate",
            65m,
            "Medium",
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
