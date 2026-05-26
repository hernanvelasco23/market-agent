using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Application.SystemCycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class MarketAgentCycleSchedulerRunnerTests
{
    [Fact]
    public async Task RunOnceAsync_DoesNotRunCycle_WhenDisabled()
    {
        var cycleService = new RecordingManualSystemCycleService();
        var service = CreateRunner(
            new MarketAgentSchedulerOptions { Enabled = false },
            cycleService);

        var result = await service.RunOnceAsync();

        Assert.False(result.CycleExecuted);
        Assert.Equal(0, cycleService.RunCount);
        Assert.Equal("Scheduler is disabled.", result.SkippedReason);
    }

    [Fact]
    public async Task RunOnceAsync_RunsCycle_WhenEnabled()
    {
        var cycleService = new RecordingManualSystemCycleService();
        var service = CreateRunner(
            new MarketAgentSchedulerOptions { Enabled = true, MarketHoursOnly = false },
            cycleService);

        var result = await service.RunOnceAsync();

        Assert.True(result.CycleExecuted);
        Assert.True(result.CycleSucceeded);
        Assert.Equal(1, cycleService.RunCount);
    }

    [Fact]
    public async Task RunOnceAsync_SkipsCycle_WhenMarketIsClosed()
    {
        var cycleService = new RecordingManualSystemCycleService();
        var service = CreateRunner(
            new MarketAgentSchedulerOptions { Enabled = true, MarketHoursOnly = true },
            cycleService,
            marketHoursService: new FixedMarketHoursService(isOpen: false));

        var result = await service.RunOnceAsync();

        Assert.False(result.CycleExecuted);
        Assert.Equal(0, cycleService.RunCount);
        Assert.Equal("Market is closed.", result.SkippedReason);
    }

    [Fact]
    public async Task RunOnceAsync_RunsEmailDelivery_WhenConfiguredAndCycleSucceeds()
    {
        var cycleService = new RecordingManualSystemCycleService();
        var emailService = new RecordingEmailAlertDeliveryService();
        var service = CreateRunner(
            new MarketAgentSchedulerOptions
            {
                Enabled = true,
                MarketHoursOnly = false,
                RunEmailDelivery = true
            },
            cycleService,
            emailService);

        var result = await service.RunOnceAsync();

        Assert.True(result.CycleExecuted);
        Assert.True(result.CycleSucceeded);
        Assert.True(result.EmailDeliveryExecuted);
        Assert.Equal(1, emailService.RunCount);
    }

    [Fact]
    public async Task RunOnceAsync_SkipsEmailDelivery_WhenCycleFails()
    {
        var cycleService = new RecordingManualSystemCycleService(overallSuccess: false);
        var emailService = new RecordingEmailAlertDeliveryService();
        var service = CreateRunner(
            new MarketAgentSchedulerOptions
            {
                Enabled = true,
                MarketHoursOnly = false,
                RunEmailDelivery = true
            },
            cycleService,
            emailService);

        var result = await service.RunOnceAsync();

        Assert.True(result.CycleExecuted);
        Assert.False(result.CycleSucceeded);
        Assert.False(result.EmailDeliveryExecuted);
        Assert.Equal(0, emailService.RunCount);
    }

    [Fact]
    public async Task RunOnceAsync_SurvivesCycleExceptions()
    {
        var cycleService = new RecordingManualSystemCycleService(exception: new InvalidOperationException("cycle exploded"));
        var service = CreateRunner(
            new MarketAgentSchedulerOptions { Enabled = true, MarketHoursOnly = false },
            cycleService);

        var result = await service.RunOnceAsync();

        Assert.False(result.CycleExecuted);
        Assert.False(result.CycleSucceeded);
        Assert.Equal("cycle exploded", result.SkippedReason);
    }

    private static MarketAgentCycleSchedulerRunner CreateRunner(
        MarketAgentSchedulerOptions options,
        IManualSystemCycleService cycleService,
        IEmailAlertDeliveryService? emailService = null,
        IMarketHoursService? marketHoursService = null)
    {
        return new MarketAgentCycleSchedulerRunner(
            new TestServiceScopeFactory(
                cycleService,
                emailService ?? new RecordingEmailAlertDeliveryService(),
                marketHoursService ?? new FixedMarketHoursService(isOpen: true)),
            options,
            new SchedulerDiagnosticsState(),
            NullLogger<MarketAgentCycleSchedulerRunner>.Instance);
    }

    private sealed class TestServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public TestServiceScopeFactory(
            IManualSystemCycleService cycleService,
            IEmailAlertDeliveryService emailService,
            IMarketHoursService marketHoursService)
        {
            _serviceProvider = new TestServiceProvider(cycleService, emailService, marketHoursService);
        }

        public IServiceScope CreateScope() => new TestServiceScope(_serviceProvider);
    }

    private sealed class TestServiceScope : IServiceScope
    {
        public TestServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly IManualSystemCycleService _cycleService;
        private readonly IEmailAlertDeliveryService _emailService;
        private readonly IMarketHoursService _marketHoursService;

        public TestServiceProvider(
            IManualSystemCycleService cycleService,
            IEmailAlertDeliveryService emailService,
            IMarketHoursService marketHoursService)
        {
            _cycleService = cycleService;
            _emailService = emailService;
            _marketHoursService = marketHoursService;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IManualSystemCycleService))
            {
                return _cycleService;
            }

            if (serviceType == typeof(IEmailAlertDeliveryService))
            {
                return _emailService;
            }

            if (serviceType == typeof(IMarketHoursService))
            {
                return _marketHoursService;
            }

            return null;
        }
    }

    private sealed class RecordingManualSystemCycleService : IManualSystemCycleService
    {
        private readonly bool _overallSuccess;
        private readonly Exception? _exception;

        public RecordingManualSystemCycleService(bool overallSuccess = true, Exception? exception = null)
        {
            _overallSuccess = overallSuccess;
            _exception = exception;
        }

        public int RunCount { get; private set; }

        public Task<ManualSystemCycleResult> RunAsync(
            ManualSystemCycleRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCount++;

            if (_exception is not null)
            {
                throw _exception;
            }

            var now = DateTime.UtcNow;
            return Task.FromResult(new ManualSystemCycleResult(
                now,
                now,
                0,
                _overallSuccess,
                ExecutedStepCount: _overallSuccess ? 4 : 1,
                SuccessfulStepCount: _overallSuccess ? 4 : 0,
                FailedStepName: _overallSuccess ? null : "ingestion",
                Steps: []));
        }
    }

    private sealed class RecordingEmailAlertDeliveryService : IEmailAlertDeliveryService
    {
        public int RunCount { get; private set; }

        public Task<EmailAlertDeliveryResult> DeliverAsync(
            EmailAlertDeliveryRequest request,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            var now = DateTime.UtcNow;
            return Task.FromResult(new EmailAlertDeliveryResult(
                now,
                now,
                AttemptedCount: 0,
                DeliveredCount: 0,
                FailedCount: 0,
                SkippedCount: 0,
                StaleSkippedCount: 0,
                DuplicateSkippedCount: 0,
                Failures: []));
        }
    }

    private sealed class FixedMarketHoursService : IMarketHoursService
    {
        private readonly bool _isOpen;

        public FixedMarketHoursService(bool isOpen)
        {
            _isOpen = isOpen;
        }

        public bool IsMarketOpen(DateTime utcNow) => _isOpen;
    }
}
