namespace MarketAgent.Application.Models;

public sealed record TechnicalIndicators(
    decimal? Ema9,
    decimal? Ema20,
    decimal? Ema50,
    decimal? Rsi14,
    decimal? Atr14,
    decimal? AverageVolume10,
    decimal? AverageVolume20);
