using MarketAgent.Domain.Enums;

namespace MarketAgent.Application.Models;

public sealed record MarketDataResult(
    string Symbol,
    AssetType AssetType,
    decimal Price,
    string Currency,
    DateTime CapturedAtUtc,
    string Source,
    decimal? Volume = null,
    decimal? OpenPrice = null,
    decimal? HighPrice = null,
    decimal? LowPrice = null,
    decimal? PreviousClose = null);
