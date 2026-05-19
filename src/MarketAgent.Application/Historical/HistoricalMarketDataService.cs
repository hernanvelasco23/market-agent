using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Historical;

public sealed class HistoricalMarketDataService : IHistoricalMarketDataService
{
    public const int DefaultDays = 90;

    private readonly IWatchlistProvider _watchlistProvider;
    private readonly IEnumerable<IHistoricalMarketDataProvider> _providers;
    private readonly IHistoricalCandleRepository _historicalCandleRepository;

    public HistoricalMarketDataService(
        IWatchlistProvider watchlistProvider,
        IEnumerable<IHistoricalMarketDataProvider> providers,
        IHistoricalCandleRepository historicalCandleRepository)
    {
        _watchlistProvider = watchlistProvider;
        _providers = providers;
        _historicalCandleRepository = historicalCandleRepository;
    }

    public async Task<HistoricalMarketDataResult> GetWatchlistCandlesAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        var requestedDays = NormalizeDays(days);
        var assets = await _watchlistProvider.GetTrackedAssetsAsync(cancellationToken);
        var candles = new List<MarketCandle>();
        var failures = new List<PriceIngestionFailure>();

        foreach (var asset in assets)
        {
            try
            {
                candles.AddRange(await GetCandlesAsync(asset, requestedDays, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new PriceIngestionFailure(
                    asset.Symbol,
                    exception.Message,
                    "HistoricalMarketDataProvider"));
            }
        }

        return new HistoricalMarketDataResult(
            DateTime.UtcNow,
            requestedDays,
            candles
                .OrderBy(candle => candle.Symbol)
                .ThenBy(candle => candle.OccurredAtUtc)
                .ToArray(),
            failures);
    }

    public async Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
        TrackedAsset asset,
        int days,
        CancellationToken cancellationToken = default)
    {
        var requestedDays = NormalizeDays(days);
        var cachedCandles = await _historicalCandleRepository.GetCandlesAsync(
            asset.Symbol,
            requestedDays,
            cancellationToken);
        var latestCandleDate = await _historicalCandleRepository.GetLatestCandleDateAsync(
            asset.Symbol,
            cancellationToken);

        if (HasEnoughFreshCachedCandles(cachedCandles, requestedDays, latestCandleDate))
        {
            return cachedCandles;
        }

        var provider = _providers.FirstOrDefault(item => item.CanHandle(asset));

        if (provider is null)
        {
            return cachedCandles;
        }

        var daysToFetch = CalculateDaysToFetch(requestedDays, cachedCandles.Count, latestCandleDate);
        var fetchedCandles = await provider.GetDailyCandlesAsync(asset, daysToFetch, cancellationToken);

        if (fetchedCandles.Count > 0)
        {
            await _historicalCandleRepository.SaveCandlesAsync(
                asset.Symbol,
                fetchedCandles,
                cancellationToken);
        }

        return await _historicalCandleRepository.GetCandlesAsync(
            asset.Symbol,
            requestedDays,
            cancellationToken);
    }

    private static int NormalizeDays(int days)
    {
        return Math.Clamp(days, 1, 300);
    }

    private static bool HasEnoughFreshCachedCandles(
        IReadOnlyCollection<MarketCandle> cachedCandles,
        int requestedDays,
        DateTime? latestCandleDate)
    {
        return cachedCandles.Count >= requestedDays &&
            IsFreshDailyCandle(latestCandleDate) &&
            !IsMarketCurrentlyOpen(DateTime.UtcNow);
    }

    private static int CalculateDaysToFetch(
        int requestedDays,
        int cachedCount,
        DateTime? latestCandleDate)
    {
        if (cachedCount == 0 || latestCandleDate is null)
        {
            return requestedDays;
        }

        var missingCount = Math.Max(0, requestedDays - cachedCount);
        var staleDays = Math.Max(0, (DateTime.UtcNow.Date - latestCandleDate.Value.Date).Days + 1);
        var refreshDays = IsMarketCurrentlyOpen(DateTime.UtcNow) ? 1 : 0;

        return Math.Clamp(
            Math.Max(missingCount, Math.Max(staleDays, refreshDays)),
            1,
            requestedDays);
    }

    private static bool IsFreshDailyCandle(DateTime? latestCandleDate)
    {
        if (latestCandleDate is null)
        {
            return false;
        }

        var latestDate = latestCandleDate.Value.Date;
        var today = DateTime.UtcNow.Date;

        return latestDate == today ||
            latestDate == GetPreviousTradingDay(today);
    }

    private static DateTime GetPreviousTradingDay(DateTime date)
    {
        var previous = date.AddDays(-1);

        while (previous.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            previous = previous.AddDays(-1);
        }

        return previous;
    }

    private static bool IsMarketCurrentlyOpen(DateTime utcNow)
    {
        if (utcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var time = utcNow.TimeOfDay;

        return time >= new TimeSpan(13, 30, 0) &&
            time <= new TimeSpan(20, 0, 0);
    }
}
