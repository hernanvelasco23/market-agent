namespace MarketAgent.Application.Models;

public sealed record ScoreAttribution(
    decimal BaseScore,
    decimal UncappedScore,
    decimal FinalScore,
    bool WasCapped,
    string? DominantPositiveFactor,
    string? DominantNegativeFactor,
    IReadOnlyCollection<ScoreContribution> PositiveContributions,
    IReadOnlyCollection<ScoreContribution> NegativeContributions);

public sealed record ScoreContribution(
    string Factor,
    decimal Points,
    string Reason);

public sealed record SignalScoreAttributionResult(
    Guid SignalSnapshotId,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    DateTime CreatedAtUtc,
    ScoreAttribution Attribution);

public sealed record ScoreAttributionSnapshot(
    Guid SignalSnapshotId,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    DateTime CreatedAtUtc,
    string? ScoreAttributionJson,
    string? ScoreBreakdownJson);

public sealed record ScoreAttributionDiagnostics(
    int TotalCount,
    int CappedCount,
    decimal? AverageUncappedScore,
    decimal? HighestUncappedScore);
