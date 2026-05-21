using System.Diagnostics;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.SystemCycle;

public sealed class ManualSystemCycleService : IManualSystemCycleService
{
    private const string IngestionStepName = "ingestion";
    private const string SignalsStepName = "signals";
    private const string OutcomesStepName = "outcomes";
    private const string AlertsStepName = "alerts";
    private const string CycleStepName = "cycle";
    private static readonly SemaphoreSlim CycleLock = new(1, 1);

    private readonly IPriceIngestionService _priceIngestionService;
    private readonly IMarketSignalService _marketSignalService;
    private readonly ISignalOutcomeService _signalOutcomeService;
    private readonly IAlertEvaluationService _alertEvaluationService;
    private readonly ILogger<ManualSystemCycleService> _logger;

    public ManualSystemCycleService(
        IPriceIngestionService priceIngestionService,
        IMarketSignalService marketSignalService,
        ISignalOutcomeService signalOutcomeService,
        IAlertEvaluationService alertEvaluationService,
        ILogger<ManualSystemCycleService> logger)
    {
        _priceIngestionService = priceIngestionService;
        _marketSignalService = marketSignalService;
        _signalOutcomeService = signalOutcomeService;
        _alertEvaluationService = alertEvaluationService;
        _logger = logger;
    }

    public async Task<ManualSystemCycleResult> RunAsync(
        ManualSystemCycleRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var cycleStopwatch = Stopwatch.StartNew();
        var steps = new List<ManualSystemCycleStepResult>();

        if (!await CycleLock.WaitAsync(0, cancellationToken))
        {
            var rejectedAtUtc = DateTime.UtcNow;
            _logger.LogWarning("Manual system cycle rejected because another cycle is already running.");

            return new ManualSystemCycleResult(
                startedAtUtc,
                rejectedAtUtc,
                cycleStopwatch.ElapsedMilliseconds,
                OverallSuccess: false,
                ExecutedStepCount: 0,
                SuccessfulStepCount: 0,
                FailedStepName: CycleStepName,
                Steps: steps);
        }

        try
        {
            _logger.LogInformation("Manual system cycle started at {StartedAtUtc}.", startedAtUtc);

            if (!await RunStepAsync(
                    IngestionStepName,
                    () => _priceIngestionService.ExecuteAsync(cancellationToken),
                    steps,
                    cancellationToken))
            {
                return BuildResult(startedAtUtc, cycleStopwatch, steps);
            }

            if (!await RunStepAsync(
                    SignalsStepName,
                    () => _marketSignalService.GenerateAsync(cancellationToken),
                    steps,
                    cancellationToken))
            {
                return BuildResult(startedAtUtc, cycleStopwatch, steps);
            }

            if (!await RunStepAsync(
                    OutcomesStepName,
                    () => _signalOutcomeService.EvaluateAsync(request.OutcomeLimit, cancellationToken),
                    steps,
                    cancellationToken))
            {
                return BuildResult(startedAtUtc, cycleStopwatch, steps);
            }

            if (!await RunStepAsync(
                    AlertsStepName,
                    () => _alertEvaluationService.EvaluateAsync(request.AlertLimit, cancellationToken),
                    steps,
                    cancellationToken))
            {
                return BuildResult(startedAtUtc, cycleStopwatch, steps);
            }

            return BuildResult(startedAtUtc, cycleStopwatch, steps);
        }
        finally
        {
            CycleLock.Release();
        }
    }

    private async Task<bool> RunStepAsync<TResult>(
        string stepName,
        Func<Task<TResult>> executeAsync,
        List<ManualSystemCycleStepResult> steps,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Manual system cycle step {StepName} started at {StartedAtUtc}.", stepName, startedAtUtc);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await executeAsync();
            var finishedAtUtc = DateTime.UtcNow;

            steps.Add(new ManualSystemCycleStepResult(
                stepName,
                startedAtUtc,
                finishedAtUtc,
                stopwatch.ElapsedMilliseconds,
                Success: true,
                ErrorMessage: null,
                Result: result));

            _logger.LogInformation(
                "Manual system cycle step {StepName} completed in {DurationMs} ms.",
                stepName,
                stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var finishedAtUtc = DateTime.UtcNow;

            steps.Add(new ManualSystemCycleStepResult(
                stepName,
                startedAtUtc,
                finishedAtUtc,
                stopwatch.ElapsedMilliseconds,
                Success: false,
                ErrorMessage: exception.Message,
                Result: null));

            _logger.LogError(
                exception,
                "Manual system cycle step {StepName} failed after {DurationMs} ms.",
                stepName,
                stopwatch.ElapsedMilliseconds);

            return false;
        }
    }

    private ManualSystemCycleResult BuildResult(
        DateTime startedAtUtc,
        Stopwatch cycleStopwatch,
        IReadOnlyCollection<ManualSystemCycleStepResult> steps)
    {
        cycleStopwatch.Stop();
        var finishedAtUtc = DateTime.UtcNow;
        var failedStepName = steps.FirstOrDefault(step => !step.Success)?.Name;
        var successfulStepCount = steps.Count(step => step.Success);
        var overallSuccess = steps.Count == 4 && successfulStepCount == 4;

        _logger.LogInformation(
            "Manual system cycle finished. Success: {OverallSuccess}. Duration: {DurationMs} ms.",
            overallSuccess,
            cycleStopwatch.ElapsedMilliseconds);

        return new ManualSystemCycleResult(
            startedAtUtc,
            finishedAtUtc,
            cycleStopwatch.ElapsedMilliseconds,
            overallSuccess,
            ExecutedStepCount: steps.Count,
            SuccessfulStepCount: successfulStepCount,
            FailedStepName: failedStepName,
            Steps: steps);
    }
}
