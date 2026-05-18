using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Models;

public sealed record MarketSignalRunResult(
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<MarketSignal> Signals);
