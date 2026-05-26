using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Api.Services;

public sealed class MarketAgentCycleSchedulerService : BackgroundService
{
    private readonly IMarketAgentCycleSchedulerRunner _runner;
    private readonly MarketAgentSchedulerOptions _options;
    private readonly ISchedulerDiagnosticsState _diagnosticsState;
    private readonly ILogger<MarketAgentCycleSchedulerService> _logger;

    public MarketAgentCycleSchedulerService(
        IMarketAgentCycleSchedulerRunner runner,
        MarketAgentSchedulerOptions options,
        ISchedulerDiagnosticsState diagnosticsState,
        ILogger<MarketAgentCycleSchedulerService> logger)
    {
        _runner = runner;
        _options = options;
        _diagnosticsState = diagnosticsState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _options.GetSafeIntervalMinutes();
        var startedAtUtc = DateTime.UtcNow;
        _diagnosticsState.MarkSchedulerStarted(_options, startedAtUtc);

        _logger.LogInformation(
            "MarketAgent scheduler startup. Registered: {Registered}. Enabled: {Enabled}. IntervalMinutes: {IntervalMinutes}. RunEmailDelivery: {RunEmailDelivery}. MarketHoursOnly: {MarketHoursOnly}. RunOnStartup: {RunOnStartup}.",
            true,
            _options.Enabled,
            intervalMinutes,
            _options.RunEmailDelivery,
            _options.MarketHoursOnly,
            _options.RunOnStartup);

        if (!_options.Enabled)
        {
            _logger.LogInformation("MarketAgent scheduler is disabled.");
            return;
        }

        try
        {
            if (_options.RunOnStartup)
            {
                _logger.LogInformation("MarketAgent scheduler startup run requested.");
                await RunSafelyAsync(stoppingToken);
            }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MarketAgent scheduler is stopping.");
        }
        catch (Exception exception)
        {
            _diagnosticsState.MarkError(exception.Message);
            _logger.LogError(exception, "MarketAgent scheduler loop stopped unexpectedly.");
        }
    }

    private async Task RunSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _runner.RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _diagnosticsState.MarkError(exception.Message);
            _logger.LogError(exception, "MarketAgent scheduler tick failed unexpectedly; the loop will continue.");
        }
    }
}
