namespace MarketAgent.Application.Models;

public sealed record MarketBriefingResult(
    DateTime GeneratedAtUtc,
    string MarketRegime,
    string Summary,
    string SignalSummary,
    IReadOnlyCollection<MarketBriefingAllSignalItem> AllSignals,
    IReadOnlyCollection<MarketBriefingOpportunityItem> TopOpportunities,
    IReadOnlyCollection<MarketBriefingPullbackItem> WatchlistPullbacks,
    IReadOnlyCollection<MarketBriefingRiskItem> TopRisks,
    IReadOnlyCollection<string> Highlights,
    IReadOnlyCollection<string> Risks,
    IReadOnlyCollection<string> WatchItems,
    MarketBriefingDiagnostics Diagnostics,
    string? Warning = null);

public sealed record MarketBriefingAllSignalItem(
    string Symbol,
    decimal Score,
    string SetupType,
    string Action,
    string Timeframe,
    string Confidence,
    string Reason,
    decimal? Ema9,
    decimal? Ema20,
    decimal? Ema50,
    decimal? Rsi14,
    decimal? Atr14,
    bool? AboveVwap,
    decimal? RelativeStrengthVsSpy);

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

public sealed record MarketBriefingDiagnostics(
    int AnalyzedSymbolsCount,
    int ReturnedSignalsCount,
    IReadOnlyCollection<string> MissingSymbols);
