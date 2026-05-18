using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Application.Briefing;

public sealed class MarketBriefingService : IMarketBriefingService
{
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketBriefingGenerator _marketBriefingGenerator;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;

    public MarketBriefingService(
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketBriefingGenerator marketBriefingGenerator,
        IMarketSignalAnalyzer marketSignalAnalyzer)
    {
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketBriefingGenerator = marketBriefingGenerator;
        _marketSignalAnalyzer = marketSignalAnalyzer;
    }

    public async Task<MarketBriefingResult> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            return new MarketBriefingResult(
                DateTime.UtcNow,
                "No data",
                "No market snapshots are available yet.",
                "No calculated market signals are available yet.",
                [],
                [],
                [],
                [],
                [],
                []);
        }

        var signals = _marketSignalAnalyzer.Analyze(snapshots);

        return await _marketBriefingGenerator.GenerateAsync(snapshots, signals, cancellationToken);
    }
}
