namespace MarketAgent.Application.Models;

public sealed record WatchlistHydrationRequest(
    IReadOnlyCollection<string>? Symbols,
    bool Force = false);

public sealed record WatchlistHydrationResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int RequestedCount,
    int UpdatedCount,
    int SkippedCount,
    int NoDataCount,
    int ErrorCount,
    IReadOnlyCollection<WatchlistHydrationItemResult> Results);

public sealed record WatchlistHydrationItemResult(
    string Symbol,
    string Status,
    string Reason,
    bool SnapshotCreated,
    bool SignalCreated,
    DateTime? LastSnapshotAtUtc,
    decimal? CurrentPrice);

public static class WatchlistHydrationStatuses
{
    public const string Updated = "Updated";
    public const string SkippedFresh = "SkippedFresh";
    public const string NoData = "NoData";
    public const string Error = "Error";
    public const string InvalidSymbol = "InvalidSymbol";
}
