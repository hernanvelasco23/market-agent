namespace MarketAgent.Application.Models;

public sealed record PriceIngestionResult(
    int TotalRequested,
    int Succeeded,
    int Failed,
    IReadOnlyCollection<PriceIngestionFailure> Failures,
    DateTime ExecutedAtUtc);
