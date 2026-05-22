namespace MarketAgent.Application.Models;

public static class AlertDeliveryStatuses
{
    public const string InternalOnly = "InternalOnly";
    public const string Delivered = "Delivered";
    public const string DeliveryFailed = "DeliveryFailed";
}

public static class AlertTypes
{
    public const string HighQualityCandidate = "HighQualityCandidate";
}

public sealed record AlertEvaluationCandidate(
    Guid SignalSnapshotId,
    DateTime SignalCreatedAtUtc,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    string Confidence,
    decimal? PriceAtSignal);

public sealed record AlertEventRecord(
    Guid Id,
    DateTime CreatedAtUtc,
    Guid SignalSnapshotId,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    string Confidence,
    decimal PriceAtSignal,
    string AlertType,
    string Title,
    string Message,
    string ReasonJson,
    string DeliveryStatus);

public sealed record AlertEventItem(
    Guid Id,
    DateTime CreatedAtUtc,
    Guid SignalSnapshotId,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    string Confidence,
    decimal PriceAtSignal,
    string AlertType,
    string Title,
    string Message,
    string ReasonJson,
    string DeliveryStatus,
    decimal? Entry = null,
    decimal? TakeProfit1 = null,
    decimal? TakeProfit2 = null,
    decimal? TakeProfit3 = null,
    decimal? RiskReward1 = null,
    decimal? RiskReward2 = null,
    decimal? RiskReward3 = null);

public sealed record AlertEventQuery(int? Limit);

public sealed record AlertEvaluationResult(
    DateTime EvaluatedAtUtc,
    int RequestedLimit,
    int CandidatesScanned,
    int CreatedCount,
    int SkippedDuplicateCount,
    int SkippedRuleCount,
    int FailedCount);

public sealed record EmailAlertDeliveryRequest(
    int? Limit,
    bool RetryFailed,
    int? SinceMinutes);

public sealed record EmailAlertDeliveryResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int AttemptedCount,
    int DeliveredCount,
    int FailedCount,
    int SkippedCount,
    int StaleSkippedCount,
    int DuplicateSkippedCount,
    IReadOnlyCollection<EmailAlertDeliveryFailure> Failures);

public sealed record EmailAlertDeliveryFailure(
    Guid AlertEventId,
    string Symbol,
    string Message);

public sealed record EmailMessage(
    string FromEmail,
    string FromName,
    string ToEmail,
    string Subject,
    string HtmlBody);
