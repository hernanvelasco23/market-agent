namespace MarketAgent.Application.Models;

public sealed record ManualSystemCycleRequest(
    int? OutcomeLimit,
    int? AlertLimit);

public sealed record ManualSystemCycleResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long DurationMs,
    bool OverallSuccess,
    int ExecutedStepCount,
    int SuccessfulStepCount,
    string? FailedStepName,
    IReadOnlyCollection<ManualSystemCycleStepResult> Steps);

public sealed record ManualSystemCycleStepResult(
    string Name,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long DurationMs,
    bool Success,
    string? ErrorMessage,
    object? Result);
