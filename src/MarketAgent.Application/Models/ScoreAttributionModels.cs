namespace MarketAgent.Application.Models;

public sealed record ScoreAttribution(
    decimal BaseScore,
    decimal UncappedScore,
    decimal RawScore,
    decimal CalibratedScore,
    decimal FinalScore,
    bool WasCapped,
    bool WasNormalized,
    string? CalibrationReason,
    string? DominantPositiveFactor,
    string? DominantNegativeFactor,
    IReadOnlyCollection<ScoreContribution> PositiveContributions,
    IReadOnlyCollection<ScoreContribution> NegativeContributions);

public sealed record ScoreCalibrationResult(
    decimal RawScore,
    decimal CalibratedScore,
    bool WasNormalized,
    string? Reason);

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
    int CappedRawScoreCount,
    decimal? AverageUncappedScore,
    decimal? HighestUncappedScore,
    decimal? AverageRawScore,
    decimal? AverageCalibratedScore,
    decimal? Top10RawScoreRange,
    decimal? Top10CalibratedScoreRange);
