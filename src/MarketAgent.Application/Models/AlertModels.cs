namespace MarketAgent.Application.Models;

public static class AlertDeliveryStatuses
{
    public const string InternalOnly = "InternalOnly";
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
    string DeliveryStatus);

public sealed record AlertEventQuery(int? Limit);

public sealed record AlertEvaluationResult(
    DateTime EvaluatedAtUtc,
    int RequestedLimit,
    int CandidatesScanned,
    int CreatedCount,
    int SkippedDuplicateCount,
    int SkippedRuleCount,
    int FailedCount);
