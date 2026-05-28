using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.Watchlists;

public sealed class WatchlistHydrationService : IWatchlistHydrationService
{
    private const int MaxRequestedSymbols = 10;
    private static readonly TimeSpan OpenMarketFreshnessWindow = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim HydrationLock = new(1, 1);
    private static readonly TrackedAsset SpyBenchmark = new("SPY", AssetType.Etf, "USD");
    private static readonly IReadOnlyDictionary<string, TrackedAsset> HydratableAssets = BuildHydratableAssets();

    private readonly IWatchlistProvider _watchlistProvider;
    private readonly IMarketDataProviderResolver _marketDataProviderResolver;
    private readonly IMarketSnapshotRepository _marketSnapshotRepository;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;
    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly ISignalSnapshotHistoryRepository _signalSnapshotHistoryRepository;
    private readonly IMarketHoursService _marketHoursService;
    private readonly ILogger<WatchlistHydrationService> _logger;

    public WatchlistHydrationService(
        IWatchlistProvider watchlistProvider,
        IMarketDataProviderResolver marketDataProviderResolver,
        IMarketSnapshotRepository marketSnapshotRepository,
        IMarketSignalAnalyzer marketSignalAnalyzer,
        IHistoricalMarketDataService historicalMarketDataService,
        ISignalSnapshotHistoryRepository signalSnapshotHistoryRepository,
        IMarketHoursService marketHoursService,
        ILogger<WatchlistHydrationService> logger)
    {
        _watchlistProvider = watchlistProvider;
        _marketDataProviderResolver = marketDataProviderResolver;
        _marketSnapshotRepository = marketSnapshotRepository;
        _marketSignalAnalyzer = marketSignalAnalyzer;
        _historicalMarketDataService = historicalMarketDataService;
        _signalSnapshotHistoryRepository = signalSnapshotHistoryRepository;
        _marketHoursService = marketHoursService;
        _logger = logger;
    }

    public async Task<WatchlistHydrationResult> HydrateAsync(
        WatchlistHydrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var symbols = NormalizeSymbols(request.Symbols);

        if (!await HydrationLock.WaitAsync(0, cancellationToken))
        {
            var skippedResults = symbols
                .Select(symbol => new WatchlistHydrationItemResult(
                    symbol,
                    WatchlistHydrationStatuses.Error,
                    "Watchlist hydration is already running.",
                    SnapshotCreated: false,
                    SignalCreated: false,
                    LastSnapshotAtUtc: null,
                    CurrentPrice: null))
                .ToArray();

            return BuildResult(startedAtUtc, skippedResults);
        }

        try
        {
            _logger.LogInformation(
                "Watchlist hydration started for {SymbolCount} symbols. Force: {Force}.",
                symbols.Count,
                request.Force);

            var trackedAssets = await BuildAssetCatalogAsync(cancellationToken);
            var existingSnapshots = await GetLatestSnapshotsBySymbolAsync(cancellationToken);
            var isMarketOpen = _marketHoursService.IsMarketOpen(startedAtUtc);
            var results = new List<WatchlistHydrationItemResult>();

            foreach (var symbol in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!trackedAssets.TryGetValue(symbol, out var asset))
                {
                    results.Add(new WatchlistHydrationItemResult(
                        symbol,
                        WatchlistHydrationStatuses.InvalidSymbol,
                        "Symbol is not available for watchlist hydration.",
                        SnapshotCreated: false,
                        SignalCreated: false,
                        LastSnapshotAtUtc: null,
                        CurrentPrice: null));
                    continue;
                }

                existingSnapshots.TryGetValue(symbol, out var latestSnapshot);
                if (!request.Force && IsFreshEnough(latestSnapshot, startedAtUtc, isMarketOpen))
                {
                    results.Add(new WatchlistHydrationItemResult(
                        symbol,
                        WatchlistHydrationStatuses.SkippedFresh,
                        isMarketOpen
                            ? "Recent snapshot already exists."
                            : "Latest snapshot is acceptable outside market hours.",
                        SnapshotCreated: false,
                        SignalCreated: false,
                        LastSnapshotAtUtc: latestSnapshot?.CapturedAtUtc,
                        CurrentPrice: latestSnapshot?.Price));
                    continue;
                }

                results.Add(await HydrateSymbolAsync(
                    asset,
                    existingSnapshots,
                    cancellationToken));
            }

            return BuildResult(startedAtUtc, results);
        }
        finally
        {
            HydrationLock.Release();
        }
    }

    private async Task<WatchlistHydrationItemResult> HydrateSymbolAsync(
        TrackedAsset asset,
        Dictionary<string, MarketSnapshot> latestSnapshots,
        CancellationToken cancellationToken)
    {
        MarketSnapshot? snapshot = null;

        try
        {
            var provider = _marketDataProviderResolver.Resolve(asset);
            var marketData = await provider.GetLatestAsync(asset, cancellationToken);
            snapshot = MapToSnapshot(marketData);

            await _marketSnapshotRepository.SaveAsync(snapshot, cancellationToken);
            latestSnapshots[asset.Symbol] = snapshot;

            var signalCreated = await TryGenerateSignalAsync(
                asset,
                snapshot,
                latestSnapshots,
                cancellationToken);

            return new WatchlistHydrationItemResult(
                asset.Symbol,
                WatchlistHydrationStatuses.Updated,
                signalCreated
                    ? "Snapshot and signal generated."
                    : "Snapshot updated. No active signal generated.",
                SnapshotCreated: true,
                SignalCreated: signalCreated,
                LastSnapshotAtUtc: snapshot.CapturedAtUtc,
                CurrentPrice: snapshot.Price);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "No market data available while hydrating watchlist symbol {Symbol}.",
                asset.Symbol);

            return new WatchlistHydrationItemResult(
                asset.Symbol,
                WatchlistHydrationStatuses.NoData,
                exception.Message,
                SnapshotCreated: false,
                SignalCreated: false,
                LastSnapshotAtUtc: snapshot?.CapturedAtUtc,
                CurrentPrice: snapshot?.Price);
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(
                exception,
                "Market data provider does not support watchlist symbol {Symbol}.",
                asset.Symbol);

            return new WatchlistHydrationItemResult(
                asset.Symbol,
                WatchlistHydrationStatuses.NoData,
                exception.Message,
                SnapshotCreated: false,
                SignalCreated: false,
                LastSnapshotAtUtc: snapshot?.CapturedAtUtc,
                CurrentPrice: snapshot?.Price);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Watchlist hydration failed for symbol {Symbol}.",
                asset.Symbol);

            return new WatchlistHydrationItemResult(
                asset.Symbol,
                WatchlistHydrationStatuses.Error,
                exception.Message,
                SnapshotCreated: snapshot is not null,
                SignalCreated: false,
                LastSnapshotAtUtc: snapshot?.CapturedAtUtc,
                CurrentPrice: snapshot?.Price);
        }
    }

    private async Task<bool> TryGenerateSignalAsync(
        TrackedAsset asset,
        MarketSnapshot snapshot,
        Dictionary<string, MarketSnapshot> latestSnapshots,
        CancellationToken cancellationToken)
    {
        var analysisSnapshots = new List<MarketSnapshot> { snapshot };
        if (!asset.Symbol.Equals(SpyBenchmark.Symbol, StringComparison.OrdinalIgnoreCase) &&
            latestSnapshots.TryGetValue(SpyBenchmark.Symbol, out var spySnapshot))
        {
            analysisSnapshots.Add(spySnapshot);
        }

        try
        {
            var candles = await GetHistoricalCandlesAsync(asset, cancellationToken);
            var signals = _marketSignalAnalyzer
                .Analyze(analysisSnapshots, candles)
                .Where(signal => signal.Symbol.Equals(asset.Symbol, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (signals.Length == 0)
            {
                return false;
            }

            var generatedAtUtc = signals.Max(signal => signal.GeneratedAtUtc);
            await _signalSnapshotHistoryRepository.AppendAsync(
                Guid.NewGuid(),
                generatedAtUtc,
                signals,
                marketRegime: null,
                triggeredAlertsJson: null,
                source: "WatchlistHydration",
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Snapshot hydration succeeded but signal generation failed for watchlist symbol {Symbol}.",
                asset.Symbol);

            return false;
        }
    }

    private async Task<IReadOnlyCollection<MarketCandle>> GetHistoricalCandlesAsync(
        TrackedAsset asset,
        CancellationToken cancellationToken)
    {
        var candles = new List<MarketCandle>();

        foreach (var historicalAsset in new[] { asset, SpyBenchmark }
            .GroupBy(item => item.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()))
        {
            try
            {
                candles.AddRange(await _historicalMarketDataService.GetCandlesAsync(
                    historicalAsset,
                    HistoricalMarketDataService.DefaultDays,
                    cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Historical candles enrich signals but should not block a fresh latest-price hydration.
            }
        }

        return candles;
    }

    private async Task<Dictionary<string, TrackedAsset>> BuildAssetCatalogAsync(
        CancellationToken cancellationToken)
    {
        var catalog = new Dictionary<string, TrackedAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in HydratableAssets.Values)
        {
            catalog[asset.Symbol.ToUpperInvariant()] = asset;
        }

        var trackedAssets = await _watchlistProvider.GetTrackedAssetsAsync(cancellationToken);

        foreach (var asset in trackedAssets)
        {
            catalog[asset.Symbol.ToUpperInvariant()] = asset;
        }

        return catalog;
    }

    private async Task<Dictionary<string, MarketSnapshot>> GetLatestSnapshotsBySymbolAsync(
        CancellationToken cancellationToken)
    {
        var snapshots = await _marketSnapshotRepository.GetAllAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key.ToUpperInvariant(),
                group => group.OrderByDescending(snapshot => snapshot.CapturedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsFreshEnough(
        MarketSnapshot? snapshot,
        DateTime utcNow,
        bool isMarketOpen)
    {
        if (snapshot is null)
        {
            return false;
        }

        if (!isMarketOpen)
        {
            return true;
        }

        return snapshot.CapturedAtUtc >= utcNow.Subtract(OpenMarketFreshnessWindow);
    }

    private static IReadOnlyList<string> NormalizeSymbols(IReadOnlyCollection<string>? symbols)
    {
        return (symbols ?? Array.Empty<string>())
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRequestedSymbols)
            .ToArray();
    }

    private static WatchlistHydrationResult BuildResult(
        DateTime startedAtUtc,
        IReadOnlyCollection<WatchlistHydrationItemResult> results)
    {
        var finishedAtUtc = DateTime.UtcNow;

        return new WatchlistHydrationResult(
            startedAtUtc,
            finishedAtUtc,
            results.Count,
            results.Count(item => item.Status == WatchlistHydrationStatuses.Updated),
            results.Count(item => item.Status == WatchlistHydrationStatuses.SkippedFresh),
            results.Count(item => item.Status == WatchlistHydrationStatuses.NoData),
            results.Count(item => item.Status == WatchlistHydrationStatuses.Error ||
                item.Status == WatchlistHydrationStatuses.InvalidSymbol),
            results);
    }

    private static MarketSnapshot MapToSnapshot(MarketDataResult marketData)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            marketData.Symbol,
            marketData.AssetType,
            marketData.Price,
            marketData.Currency,
            EnsureUtc(marketData.CapturedAtUtc),
            marketData.Source,
            marketData.Volume,
            marketData.OpenPrice,
            marketData.HighPrice,
            marketData.LowPrice,
            marketData.PreviousClose);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static IReadOnlyDictionary<string, TrackedAsset> BuildHydratableAssets()
    {
        var assets = new[]
        {
            new TrackedAsset("NVDA", AssetType.Equity, "USD"),
            new TrackedAsset("MSFT", AssetType.Equity, "USD"),
            new TrackedAsset("AAPL", AssetType.Equity, "USD"),
            new TrackedAsset("AMZN", AssetType.Equity, "USD"),
            new TrackedAsset("AXP", AssetType.Equity, "USD"),
            new TrackedAsset("BRK.B", AssetType.Equity, "USD"),
            new TrackedAsset("GOOGL", AssetType.Equity, "USD"),
            new TrackedAsset("GOOG", AssetType.Equity, "USD"),
            new TrackedAsset("META", AssetType.Equity, "USD"),
            new TrackedAsset("ORCL", AssetType.Equity, "USD"),
            new TrackedAsset("IBM", AssetType.Equity, "USD"),
            new TrackedAsset("TSLA", AssetType.Equity, "USD"),
            new TrackedAsset("AMD", AssetType.Equity, "USD"),
            new TrackedAsset("MELI", AssetType.Equity, "USD"),
            new TrackedAsset("NU", AssetType.Equity, "USD"),
            new TrackedAsset("GGAL", AssetType.Equity, "USD"),
            new TrackedAsset("YPF", AssetType.Equity, "USD"),
            new TrackedAsset("BMA", AssetType.Equity, "USD"),
            new TrackedAsset("PAM", AssetType.Equity, "USD"),
            new TrackedAsset("TGS", AssetType.Equity, "USD"),
            new TrackedAsset("VIST", AssetType.Equity, "USD"),
            new TrackedAsset("PBR", AssetType.Equity, "USD"),
            new TrackedAsset("VALE", AssetType.Equity, "USD"),
            new TrackedAsset("BBD", AssetType.Equity, "USD"),
            new TrackedAsset("ITUB", AssetType.Equity, "USD"),
            new TrackedAsset("JPM", AssetType.Equity, "USD"),
            new TrackedAsset("BAC", AssetType.Equity, "USD"),
            new TrackedAsset("GS", AssetType.Equity, "USD"),
            new TrackedAsset("XOM", AssetType.Equity, "USD"),
            new TrackedAsset("CVX", AssetType.Equity, "USD"),
            new TrackedAsset("TTE", AssetType.Equity, "USD"),
            new TrackedAsset("NIO", AssetType.Equity, "USD"),
            new TrackedAsset("PYPL", AssetType.Equity, "USD"),
            new TrackedAsset("SHOP", AssetType.Equity, "USD"),
            new TrackedAsset("COST", AssetType.Equity, "USD"),
            new TrackedAsset("MSTR", AssetType.Equity, "USD"),
            new TrackedAsset("COIN", AssetType.Equity, "USD"),
            new TrackedAsset("IBIT", AssetType.Etf, "USD"),
            new TrackedAsset("ETHA", AssetType.Etf, "USD"),
            new TrackedAsset("KO", AssetType.Equity, "USD"),
            new TrackedAsset("PEP", AssetType.Equity, "USD"),
            new TrackedAsset("PG", AssetType.Equity, "USD"),
            new TrackedAsset("WMT", AssetType.Equity, "USD"),
            new TrackedAsset("DIS", AssetType.Equity, "USD"),
            new TrackedAsset("NFLX", AssetType.Equity, "USD"),
            new TrackedAsset("PLTR", AssetType.Equity, "USD"),
            new TrackedAsset("ASML", AssetType.Equity, "USD"),
            new TrackedAsset("TSM", AssetType.Equity, "USD"),
            new TrackedAsset("AVGO", AssetType.Equity, "USD"),
            new TrackedAsset("QCOM", AssetType.Equity, "USD"),
            new TrackedAsset("MRVL", AssetType.Equity, "USD"),
            new TrackedAsset("PDD", AssetType.Equity, "USD"),
            new TrackedAsset("TM", AssetType.Equity, "USD"),
            new TrackedAsset("UBER", AssetType.Equity, "USD"),
            new TrackedAsset("CVS", AssetType.Equity, "USD"),
            new TrackedAsset("GLOB", AssetType.Equity, "USD"),
            new TrackedAsset("RGTI", AssetType.Equity, "USD"),
            new TrackedAsset("RKLB", AssetType.Equity, "USD"),
            new TrackedAsset("QQQ", AssetType.Etf, "USD"),
            SpyBenchmark
        };

        return assets.ToDictionary(
            asset => asset.Symbol,
            asset => asset,
            StringComparer.OrdinalIgnoreCase);
    }
}
