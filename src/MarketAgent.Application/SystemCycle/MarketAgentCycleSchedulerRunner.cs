using System.Diagnostics;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.SystemCycle;

public sealed class MarketAgentCycleSchedulerRunner : IMarketAgentCycleSchedulerRunner
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly MarketAgentSchedulerOptions _options;
    private readonly ISchedulerDiagnosticsState _diagnosticsState;
    private readonly ILogger<MarketAgentCycleSchedulerRunner> _logger;
    private DateTime? _lastClosedMarketLogUtc;

    public MarketAgentCycleSchedulerRunner(
        IServiceScopeFactory serviceScopeFactory,
        MarketAgentSchedulerOptions options,
        ISchedulerDiagnosticsState diagnosticsState,
        ILogger<MarketAgentCycleSchedulerRunner> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options;
        _diagnosticsState = diagnosticsState;
        _logger = logger;
    }

    public DateTime? LastCycleRunUtc { get; private set; }

    public async Task<SchedulerRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        return await RunOnceAsync(
            new SchedulerRunRequest(
                BypassEnabled: false,
                BypassMarketHours: false,
                RunEmailDelivery: null),
            cancellationToken);
    }

    public async Task<SchedulerRunResult> RunOnceAsync(
        SchedulerRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        if (!_options.Enabled && !request.BypassEnabled)
        {
            _diagnosticsState.MarkSkipped("Scheduler is disabled.");
            _logger.LogInformation("MarketAgent scheduled cycle skipped because scheduler is disabled.");

            return new SchedulerRunResult(
                startedAtUtc,
                DateTime.UtcNow,
                stopwatch.ElapsedMilliseconds,
                CycleExecuted: false,
                CycleSucceeded: false,
                EmailDeliveryExecuted: false,
                SkippedReason: "Scheduler is disabled.");
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();

            if (_options.MarketHoursOnly && !request.BypassMarketHours)
            {
                var marketHoursService = scope.ServiceProvider.GetRequiredService<IMarketHoursService>();
                if (!marketHoursService.IsMarketOpen(startedAtUtc))
                {
                    LogMarketClosedSkip(startedAtUtc);
                    _diagnosticsState.MarkSkipped("Market is closed.");

                    return new SchedulerRunResult(
                        startedAtUtc,
                        DateTime.UtcNow,
                        stopwatch.ElapsedMilliseconds,
                        CycleExecuted: false,
                        CycleSucceeded: false,
                        EmailDeliveryExecuted: false,
                        SkippedReason: "Market is closed.");
                }
            }

            _logger.LogInformation("MarketAgent scheduled cycle started at {StartedAtUtc}.", startedAtUtc);
            _diagnosticsState.MarkCycleStarted(startedAtUtc);

            var cycleService = scope.ServiceProvider.GetRequiredService<IManualSystemCycleService>();
            var cycleResult = await cycleService.RunAsync(
                new ManualSystemCycleRequest(OutcomeLimit: null, AlertLimit: null),
                cancellationToken);

            var emailDeliveryExecuted = false;
            var shouldRunEmailDelivery = request.RunEmailDelivery ?? _options.RunEmailDelivery;

            if (cycleResult.OverallSuccess && shouldRunEmailDelivery)
            {
                _logger.LogInformation("MarketAgent scheduled email delivery started after successful cycle.");

                var emailDeliveryService = scope.ServiceProvider.GetRequiredService<IEmailAlertDeliveryService>();
                var emailResult = await emailDeliveryService.DeliverAsync(
                    new EmailAlertDeliveryRequest(Limit: null, RetryFailed: false, SinceMinutes: null),
                    cancellationToken);

                emailDeliveryExecuted = true;

                _logger.LogInformation(
                    "MarketAgent scheduled email delivery finished. Attempted: {AttemptedCount}. Delivered: {DeliveredCount}. Failed: {FailedCount}.",
                    emailResult.AttemptedCount,
                    emailResult.DeliveredCount,
                    emailResult.FailedCount);
            }
            else if (!cycleResult.OverallSuccess && shouldRunEmailDelivery)
            {
                _logger.LogInformation("MarketAgent scheduled email delivery skipped because cycle did not succeed.");
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "MarketAgent scheduled cycle finished. Success: {OverallSuccess}. Duration: {DurationMs} ms. ExecutedSteps: {ExecutedStepCount}. FailedStep: {FailedStepName}.",
                cycleResult.OverallSuccess,
                stopwatch.ElapsedMilliseconds,
                cycleResult.ExecutedStepCount,
                cycleResult.FailedStepName);

            LastCycleRunUtc = DateTime.UtcNow;
            _diagnosticsState.MarkCycleFinished(LastCycleRunUtc.Value, cycleResult.OverallSuccess);

            return new SchedulerRunResult(
                startedAtUtc,
                DateTime.UtcNow,
                stopwatch.ElapsedMilliseconds,
                CycleExecuted: true,
                CycleSucceeded: cycleResult.OverallSuccess,
                EmailDeliveryExecuted: emailDeliveryExecuted,
                SkippedReason: null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogError(exception, "MarketAgent scheduled cycle failed after {DurationMs} ms.", stopwatch.ElapsedMilliseconds);
            _diagnosticsState.MarkError(exception.Message);

            return new SchedulerRunResult(
                startedAtUtc,
                DateTime.UtcNow,
                stopwatch.ElapsedMilliseconds,
                CycleExecuted: false,
                CycleSucceeded: false,
                EmailDeliveryExecuted: false,
                SkippedReason: exception.Message);
        }
    }

    private void LogMarketClosedSkip(DateTime utcNow)
    {
        if (_lastClosedMarketLogUtc is null || utcNow - _lastClosedMarketLogUtc.Value >= TimeSpan.FromHours(1))
        {
            _lastClosedMarketLogUtc = utcNow;
            _logger.LogInformation("MarketAgent scheduled cycle skipped because market is closed.");
            return;
        }

        _logger.LogDebug("MarketAgent scheduled cycle skipped because market is closed.");
    }
}
