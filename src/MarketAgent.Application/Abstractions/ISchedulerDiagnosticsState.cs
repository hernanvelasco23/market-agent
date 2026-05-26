using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface ISchedulerDiagnosticsState
{
    SchedulerDiagnosticsSnapshot GetSnapshot();

    void MarkRegistered(MarketAgentSchedulerOptions options);

    void MarkSchedulerStarted(MarketAgentSchedulerOptions options, DateTime startedAtUtc);

    void MarkCycleStarted(DateTime startedAtUtc);

    void MarkCycleFinished(DateTime finishedAtUtc, bool succeeded);

    void MarkSkipped(string reason);

    void MarkError(string error);
}
