namespace MarketAgent.Application.Models;

public sealed record MarketBriefingResult(
    DateTime GeneratedAtUtc,
    string MarketRegime,
    string Summary,
    string SignalSummary,
    IReadOnlyCollection<MarketBriefingOpportunityItem> TopOpportunities,
    IReadOnlyCollection<MarketBriefingPullbackItem> WatchlistPullbacks,
    IReadOnlyCollection<MarketBriefingRiskItem> TopRisks,
    IReadOnlyCollection<string> Highlights,
    IReadOnlyCollection<string> Risks,
    IReadOnlyCollection<string> WatchItems);

public sealed record MarketBriefingOpportunityItem(
    string Symbol,
    decimal Score,
    string Reason,
    decimal? Entry,
    decimal? Stop,
    decimal? Target,
    string Action,
    string Timeframe,
    string Confidence);

public sealed record MarketBriefingPullbackItem(
    string Symbol,
    decimal Score,
    string Reason,
    decimal? Entry,
    decimal? Stop,
    decimal? Target,
    bool ConfirmationNeeded,
    string Action,
    string Timeframe,
    string Confidence);

public sealed record MarketBriefingRiskItem(
    string Symbol,
    decimal Score,
    string Reason,
    string RiskType,
    string Action,
    string Timeframe,
    string Confidence);
