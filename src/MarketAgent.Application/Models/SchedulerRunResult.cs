namespace MarketAgent.Application.Models;

public sealed record SchedulerRunResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long DurationMs,
    bool CycleExecuted,
    bool CycleSucceeded,
    bool EmailDeliveryExecuted,
    string? SkippedReason);
