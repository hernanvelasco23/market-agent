namespace MarketAgent.Application.Models;

public sealed record SignalPerformancePreviewResult(
    DateTime GeneratedAtUtc,
    int RequestedDays,
    IReadOnlyCollection<SignalPerformancePreviewItem> Items,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<PriceIngestionFailure> Failures);

public sealed record SignalPerformancePreviewItem(
    string SignalType,
    int SampleCount,
    bool IsInsufficientData,
    bool HasLowSampleWarning,
    decimal? AverageForwardReturn1Day,
    decimal? AverageForwardReturn3Day,
    decimal? AverageForwardReturn5Day,
    decimal? WinRate1Day,
    decimal? WinRate3Day,
    decimal? WinRate5Day);
