using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Application.Signals;

public sealed class MarketSignalService : IMarketSignalService
{
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;

    public MarketSignalService(
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketSignalAnalyzer marketSignalAnalyzer)
    {
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketSignalAnalyzer = marketSignalAnalyzer;
    }

    public async Task<MarketSignalRunResult> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);
        var signals = _marketSignalAnalyzer.Analyze(snapshots);
        var generatedAtUtc = signals.Count > 0
            ? signals.Max(signal => signal.GeneratedAtUtc)
            : DateTime.UtcNow;

        return new MarketSignalRunResult(generatedAtUtc, signals);
    }
}
