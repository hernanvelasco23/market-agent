using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Models;

public sealed record HistoricalMarketDataResult(
    DateTime GeneratedAtUtc,
    int RequestedDays,
    IReadOnlyCollection<MarketCandle> Candles,
    IReadOnlyCollection<PriceIngestionFailure> Failures);
