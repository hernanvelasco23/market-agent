namespace MarketAgent.Application.Models;

public sealed record SchedulerDiagnosticsSnapshot(
    DateTime AppStartedAtUtc,
    bool SchedulerRegistered,
    bool SchedulerEnabled,
    int IntervalMinutes,
    bool MarketHoursOnly,
    bool RunOnStartup,
    bool RunEmailDelivery,
    DateTime? LastSchedulerStartedAtUtc,
    DateTime? LastCycleStartedAtUtc,
    DateTime? LastCycleFinishedAtUtc,
    bool? LastCycleSucceeded,
    string? LastSkipReason,
    string? LastError);

public sealed record SchedulerRunRequest(
    bool BypassEnabled,
    bool BypassMarketHours,
    bool? RunEmailDelivery);
