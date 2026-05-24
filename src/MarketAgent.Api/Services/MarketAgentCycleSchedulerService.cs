using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Api.Services;

public sealed class MarketAgentCycleSchedulerService : BackgroundService
{
    private readonly IMarketAgentCycleSchedulerRunner _runner;
    private readonly MarketAgentSchedulerOptions _options;
    private readonly ILogger<MarketAgentCycleSchedulerService> _logger;

    public MarketAgentCycleSchedulerService(
        IMarketAgentCycleSchedulerRunner runner,
        MarketAgentSchedulerOptions options,
        ILogger<MarketAgentCycleSchedulerService> logger)
    {
        _runner = runner;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _options.GetSafeIntervalMinutes();

        _logger.LogInformation(
            "MarketAgent scheduler startup. Enabled: {Enabled}. IntervalMinutes: {IntervalMinutes}. RunEmailDelivery: {RunEmailDelivery}. MarketHoursOnly: {MarketHoursOnly}. RunOnStartup: {RunOnStartup}.",
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

        if (_options.RunOnStartup)
        {
            await _runner.RunOnceAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _runner.RunOnceAsync(stoppingToken);
        }
    }
}
