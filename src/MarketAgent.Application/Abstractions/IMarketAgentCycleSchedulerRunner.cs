using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IMarketAgentCycleSchedulerRunner
{
    DateTime? LastCycleRunUtc { get; }

    Task<SchedulerRunResult> RunOnceAsync(CancellationToken cancellationToken = default);
}
