using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Application.Briefing;

public sealed class MarketBriefingService : IMarketBriefingService
{
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketBriefingGenerator _marketBriefingGenerator;

    public MarketBriefingService(
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketBriefingGenerator marketBriefingGenerator)
    {
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketBriefingGenerator = marketBriefingGenerator;
    }

    public async Task<MarketBriefingResult> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            return new MarketBriefingResult(
                DateTime.UtcNow,
                "No market snapshots are available yet.",
                [],
                [],
                []);
        }

        return await _marketBriefingGenerator.GenerateAsync(snapshots, cancellationToken);
    }
}
