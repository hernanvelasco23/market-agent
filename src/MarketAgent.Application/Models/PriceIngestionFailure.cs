namespace MarketAgent.Application.Models;

public sealed record PriceIngestionFailure(
    string Symbol,
    string Reason,
    string? Source = null);
