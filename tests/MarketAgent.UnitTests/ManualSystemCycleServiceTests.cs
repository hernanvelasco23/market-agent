using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.SystemCycle;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class ManualSystemCycleServiceTests
{
    [Fact]
    public async Task RunAsync_ExecutesAllStepsInOrder_WhenAllSucceed()
    {
        var calls = new List<string>();
        var service = CreateService(calls);

        var result = await service.RunAsync(new ManualSystemCycleRequest(OutcomeLimit: 25, AlertLimit: 10));

        Assert.True(result.OverallSuccess);
        Assert.Equal(4, result.ExecutedStepCount);
        Assert.Equal(4, result.SuccessfulStepCount);
        Assert.Null(result.FailedStepName);
        Assert.Equal(["ingestion", "signals", "outcomes", "alerts"], calls);
        Assert.Equal(["ingestion", "signals", "outcomes", "alerts"], result.Steps.Select(step => step.Name));
        Assert.All(result.Steps, step => Assert.True(step.Success));
        Assert.All(result.Steps, step => Assert.Null(step.ErrorMessage));
        Assert.True(result.FinishedAtUtc >= result.StartedAtUtc);
        Assert.True(result.DurationMs >= 0);
    }

    [Fact]
    public async Task RunAsync_StopsAfterIngestionFailure()
    {
        var calls = new List<string>();
        var service = CreateService(
            calls,
            ingestionException: new InvalidOperationException("ingestion failed"));

        var result = await service.RunAsync(new ManualSystemCycleRequest(null, null));

        Assert.False(result.OverallSuccess);
        Assert.Equal(1, result.ExecutedStepCount);
        Assert.Equal(0, result.SuccessfulStepCount);
        Assert.Equal("ingestion", result.FailedStepName);
        Assert.Equal(["ingestion"], calls);

        var step = Assert.Single(result.Steps);
        Assert.Equal("ingestion", step.Name);
        Assert.False(step.Success);
        Assert.Equal("ingestion failed", step.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_StopsAfterSignalFailure_AndPreservesPriorSuccessfulStep()
    {
        var calls = new List<string>();
        var service = CreateService(
            calls,
            signalsException: new InvalidOperationException("signals failed"));

        var result = await service.RunAsync(new ManualSystemCycleRequest(null, null));

        Assert.False(result.OverallSuccess);
        Assert.Equal(2, result.ExecutedStepCount);
        Assert.Equal(1, result.SuccessfulStepCount);
        Assert.Equal("signals", result.FailedStepName);
        Assert.Equal(["ingestion", "signals"], calls);
        Assert.True(result.Steps.First().Success);
        Assert.False(result.Steps.Last().Success);
    }

    [Fact]
    public async Task RunAsync_StopsAfterOutcomeFailure_BeforeAlerts()
    {
        var calls = new List<string>();
        var service = CreateService(
            calls,
            outcomesException: new InvalidOperationException("outcomes failed"));

        var result = await service.RunAsync(new ManualSystemCycleRequest(null, null));

        Assert.False(result.OverallSuccess);
        Assert.Equal(3, result.ExecutedStepCount);
        Assert.Equal(2, result.SuccessfulStepCount);
        Assert.Equal("outcomes", result.FailedStepName);
        Assert.Equal(["ingestion", "signals", "outcomes"], calls);
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WhenAlertsFailAfterPriorStepsSucceed()
    {
        var calls = new List<string>();
        var service = CreateService(
            calls,
            alertsException: new InvalidOperationException("alerts failed"));

        var result = await service.RunAsync(new ManualSystemCycleRequest(null, null));

        Assert.False(result.OverallSuccess);
        Assert.Equal(4, result.ExecutedStepCount);
        Assert.Equal(3, result.SuccessfulStepCount);
        Assert.Equal("alerts", result.FailedStepName);
        Assert.Equal(["ingestion", "signals", "outcomes", "alerts"], calls);
        Assert.Equal("alerts failed", result.Steps.Last().ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_PassesLimitsToOutcomeAndAlertEvaluators()
    {
        var calls = new List<string>();
        var outcomeService = new RecordingSignalOutcomeService(calls);
        var alertService = new RecordingAlertEvaluationService(calls);
        var service = CreateService(calls, outcomeService: outcomeService, alertService: alertService);

        await service.RunAsync(new ManualSystemCycleRequest(OutcomeLimit: 33, AlertLimit: 44));

        Assert.Equal(33, outcomeService.LastLimit);
        Assert.Equal(44, alertService.LastLimit);
    }

    [Fact]
    public async Task RunAsync_RejectsOverlappingCycles_WithSharedGuard()
    {
        var calls = new List<string>();
        var blockingIngestion = new BlockingPriceIngestionService(calls);
        var firstService = CreateService(calls, priceIngestionService: blockingIngestion);
        var secondService = CreateService(calls);

        var firstRun = firstService.RunAsync(new ManualSystemCycleRequest(null, null));
        await blockingIngestion.Started.Task;

        var secondResult = await secondService.RunAsync(new ManualSystemCycleRequest(null, null));

        blockingIngestion.Release();
        var firstResult = await firstRun;

        Assert.False(secondResult.OverallSuccess);
        Assert.Equal(0, secondResult.ExecutedStepCount);
        Assert.Equal("cycle", secondResult.FailedStepName);
        Assert.True(firstResult.OverallSuccess);
    }

    private static ManualSystemCycleService CreateService(
        List<string> calls,
        Exception? ingestionException = null,
        Exception? signalsException = null,
        Exception? outcomesException = null,
        Exception? alertsException = null,
        IPriceIngestionService? priceIngestionService = null,
        RecordingSignalOutcomeService? outcomeService = null,
        RecordingAlertEvaluationService? alertService = null)
    {
        return new ManualSystemCycleService(
            priceIngestionService ?? new RecordingPriceIngestionService(calls, ingestionException),
            new RecordingMarketSignalService(calls, signalsException),
            outcomeService ?? new RecordingSignalOutcomeService(calls, outcomesException),
            alertService ?? new RecordingAlertEvaluationService(calls, alertsException),
            NullLogger<ManualSystemCycleService>.Instance);
    }

    private sealed class RecordingPriceIngestionService : IPriceIngestionService
    {
        private readonly List<string> _calls;
        private readonly Exception? _exception;

        public RecordingPriceIngestionService(List<string> calls, Exception? exception)
        {
            _calls = calls;
            _exception = exception;
        }

        public Task<PriceIngestionResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add("ingestion");
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new PriceIngestionResult(1, 1, 0, [], DateTime.UtcNow));
        }
    }

    private sealed class BlockingPriceIngestionService : IPriceIngestionService
    {
        private readonly List<string> _calls;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingPriceIngestionService(List<string> calls)
        {
            _calls = calls;
        }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<PriceIngestionResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add("ingestion");
            Started.SetResult();
            await _release.Task.WaitAsync(cancellationToken);

            return new PriceIngestionResult(1, 1, 0, [], DateTime.UtcNow);
        }

        public void Release()
        {
            _release.SetResult();
        }
    }

    private sealed class RecordingMarketSignalService : IMarketSignalService
    {
        private readonly List<string> _calls;
        private readonly Exception? _exception;

        public RecordingMarketSignalService(List<string> calls, Exception? exception)
        {
            _calls = calls;
            _exception = exception;
        }

        public Task<MarketSignalRunResult> GenerateAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add("signals");
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new MarketSignalRunResult(DateTime.UtcNow, []));
        }
    }

    private sealed class RecordingSignalOutcomeService : ISignalOutcomeService
    {
        private readonly List<string> _calls;
        private readonly Exception? _exception;

        public RecordingSignalOutcomeService(List<string> calls, Exception? exception = null)
        {
            _calls = calls;
            _exception = exception;
        }

        public int? LastLimit { get; private set; }

        public Task<SignalOutcomeEvaluationResult> EvaluateAsync(int? limit, CancellationToken cancellationToken = default)
        {
            _calls.Add("outcomes");
            LastLimit = limit;
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new SignalOutcomeEvaluationResult(DateTime.UtcNow, limit ?? 0, 0, 0, 0, 0, 0, 0));
        }

        public Task<IReadOnlyCollection<SignalOutcomeItem>> GetOutcomesAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SignalOutcomeItem>>([]);

        public Task<SignalOutcomeSummary> GetSummaryAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SignalOutcomeSetupSummary> GetSetupSummaryAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SignalOutcomeScoreBucketSummary> GetScoreBucketSummaryAsync(
            SignalOutcomeQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAlertEvaluationService : IAlertEvaluationService
    {
        private readonly List<string> _calls;
        private readonly Exception? _exception;

        public RecordingAlertEvaluationService(List<string> calls, Exception? exception = null)
        {
            _calls = calls;
            _exception = exception;
        }

        public int? LastLimit { get; private set; }

        public Task<AlertEvaluationResult> EvaluateAsync(int? limit, CancellationToken cancellationToken = default)
        {
            _calls.Add("alerts");
            LastLimit = limit;
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new AlertEvaluationResult(DateTime.UtcNow, limit ?? 0, 0, 0, 0, 0, 0));
        }

        public Task<IReadOnlyCollection<AlertEventItem>> GetAlertsAsync(
            AlertEventQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AlertEventItem>>([]);
    }
}
