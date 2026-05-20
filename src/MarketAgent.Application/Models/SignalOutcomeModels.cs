namespace MarketAgent.Application.Models;

public static class SignalOutcomeStatuses
{
    public const string Pending = "Pending";
    public const string Evaluated = "Evaluated";
    public const string Unevaluable = "Unevaluable";
    public const string Failed = "Failed";
}

public sealed record SignalOutcomeEvaluationCandidate(
    Guid SignalSnapshotId,
    DateTime CreatedAtUtc,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    string Confidence,
    decimal? Price);

public sealed record SignalOutcomeRecord(
    Guid Id,
    Guid SignalSnapshotId,
    DateTime EvaluatedAtUtc,
    string EvaluationStatus,
    decimal? PriceAtSignal,
    decimal? PriceAfter15Minutes,
    decimal? PriceAfter1Hour,
    decimal? PriceAfter4Hours,
    decimal? PriceAfter1Day,
    decimal? MaxRunupPercent,
    decimal? MaxDrawdownPercent,
    decimal? OutcomePercent,
    bool? IsSuccessful,
    string? FailureReason);

public sealed record SignalOutcomeItem(
    Guid SignalSnapshotId,
    DateTime SignalCreatedAtUtc,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    string Confidence,
    DateTime? EvaluatedAtUtc,
    string? EvaluationStatus,
    decimal? PriceAtSignal,
    decimal? PriceAfter15Minutes,
    decimal? PriceAfter1Hour,
    decimal? PriceAfter4Hours,
    decimal? PriceAfter1Day,
    decimal? MaxRunupPercent,
    decimal? MaxDrawdownPercent,
    decimal? OutcomePercent,
    bool? IsSuccessful,
    string? FailureReason);

public sealed record SignalOutcomeQuery(
    string? Symbol,
    string? Status,
    bool? IsSuccessful,
    int? Days,
    int? Limit);

public sealed record SignalOutcomeEvaluationResult(
    DateTime EvaluatedAtUtc,
    int RequestedLimit,
    int CandidatesScanned,
    int UpdatedPartialCount,
    int EvaluatedCount,
    int PendingCount,
    int UnevaluableCount,
    int FailedCount);

public sealed record SignalOutcomeSummary(
    DateTime GeneratedAtUtc,
    int TotalCount,
    int EvaluatedCount,
    int PendingCount,
    int UnevaluableCount,
    int FailedCount,
    int SuccessfulCount,
    int UnsuccessfulCount,
    decimal? WinRate,
    decimal? AverageOutcomePercent,
    decimal? AverageMaxRunupPercent,
    decimal? AverageMaxDrawdownPercent,
    int CountWith15m,
    decimal? AverageReturn15m,
    int CountWith1h,
    decimal? AverageReturn1h,
    int CountWith4h,
    decimal? AverageReturn4h,
    string? Best15mSymbol,
    string? Worst15mSymbol,
    decimal? Best15mReturnPercent,
    decimal? Worst15mReturnPercent,
    string? Best1hSymbol,
    decimal? Best1hReturnPercent,
    string? Worst1hSymbol,
    decimal? Worst1hReturnPercent);
