namespace MarketAgent.Application.Models;

public sealed record MarketBriefingResult(
    DateTime GeneratedAtUtc,
    string Summary,
    IReadOnlyCollection<string> Highlights,
    IReadOnlyCollection<string> Risks,
    IReadOnlyCollection<string> WatchItems);
