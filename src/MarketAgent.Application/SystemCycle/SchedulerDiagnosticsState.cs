using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Application.SystemCycle;

public sealed class SchedulerDiagnosticsState : ISchedulerDiagnosticsState
{
    private readonly object _sync = new();

    private readonly DateTime _appStartedAtUtc = DateTime.UtcNow;
    private bool _schedulerRegistered;
    private bool _schedulerEnabled;
    private int _intervalMinutes;
    private bool _marketHoursOnly;
    private bool _runOnStartup;
    private bool _runEmailDelivery;
    private DateTime? _lastSchedulerStartedAtUtc;
    private DateTime? _lastCycleStartedAtUtc;
    private DateTime? _lastCycleFinishedAtUtc;
    private bool? _lastCycleSucceeded;
    private string? _lastSkipReason;
    private string? _lastError;

    public SchedulerDiagnosticsSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new SchedulerDiagnosticsSnapshot(
                _appStartedAtUtc,
                _schedulerRegistered,
                _schedulerEnabled,
                _intervalMinutes,
                _marketHoursOnly,
                _runOnStartup,
                _runEmailDelivery,
                _lastSchedulerStartedAtUtc,
                _lastCycleStartedAtUtc,
                _lastCycleFinishedAtUtc,
                _lastCycleSucceeded,
                _lastSkipReason,
                _lastError);
        }
    }

    public void MarkRegistered(MarketAgentSchedulerOptions options)
    {
        lock (_sync)
        {
            _schedulerRegistered = true;
            ApplyOptions(options);
        }
    }

    public void MarkSchedulerStarted(MarketAgentSchedulerOptions options, DateTime startedAtUtc)
    {
        lock (_sync)
        {
            _lastSchedulerStartedAtUtc = startedAtUtc;
            ApplyOptions(options);
        }
    }

    public void MarkCycleStarted(DateTime startedAtUtc)
    {
        lock (_sync)
        {
            _lastCycleStartedAtUtc = startedAtUtc;
            _lastCycleFinishedAtUtc = null;
            _lastCycleSucceeded = null;
            _lastSkipReason = null;
            _lastError = null;
        }
    }

    public void MarkCycleFinished(DateTime finishedAtUtc, bool succeeded)
    {
        lock (_sync)
        {
            _lastCycleFinishedAtUtc = finishedAtUtc;
            _lastCycleSucceeded = succeeded;
            _lastSkipReason = null;
            if (succeeded)
            {
                _lastError = null;
            }
        }
    }

    public void MarkSkipped(string reason)
    {
        lock (_sync)
        {
            _lastCycleFinishedAtUtc = DateTime.UtcNow;
            _lastCycleSucceeded = false;
            _lastSkipReason = reason;
        }
    }

    public void MarkError(string error)
    {
        lock (_sync)
        {
            _lastCycleFinishedAtUtc = DateTime.UtcNow;
            _lastCycleSucceeded = false;
            _lastError = error;
        }
    }

    private void ApplyOptions(MarketAgentSchedulerOptions options)
    {
        _schedulerEnabled = options.Enabled;
        _intervalMinutes = options.GetSafeIntervalMinutes();
        _marketHoursOnly = options.MarketHoursOnly;
        _runOnStartup = options.RunOnStartup;
        _runEmailDelivery = options.RunEmailDelivery;
    }
}
