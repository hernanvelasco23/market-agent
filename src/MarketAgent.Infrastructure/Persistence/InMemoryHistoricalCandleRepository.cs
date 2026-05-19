using MarketAgent.Application.Abstractions;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class InMemoryHistoricalCandleRepository : IHistoricalCandleRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<MarketCandle>> _candlesBySymbol = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var requestedDays = Math.Clamp(days, 1, 300);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_candlesBySymbol.TryGetValue(normalizedSymbol, out var candles))
            {
                return Task.FromResult<IReadOnlyCollection<MarketCandle>>([]);
            }

            return Task.FromResult<IReadOnlyCollection<MarketCandle>>(
                candles
                    .OrderBy(candle => candle.OccurredAtUtc)
                    .TakeLast(requestedDays)
                    .ToArray());
        }
    }

    public Task SaveCandlesAsync(
        string symbol,
        IReadOnlyCollection<MarketCandle> candles,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        ArgumentNullException.ThrowIfNull(candles);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_candlesBySymbol.TryGetValue(normalizedSymbol, out var existingCandles))
            {
                existingCandles = [];
                _candlesBySymbol[normalizedSymbol] = existingCandles;
            }

            var mergedCandles = existingCandles
                .Concat(candles.Where(candle => candle.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(candle => new { candle.Symbol, candle.OccurredAtUtc })
                .Select(group => group.OrderByDescending(candle => candle.Source).First())
                .OrderBy(candle => candle.OccurredAtUtc)
                .ToList();

            _candlesBySymbol[normalizedSymbol] = mergedCandles;
        }

        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLatestCandleDateAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_candlesBySymbol.TryGetValue(normalizedSymbol, out var candles) ||
                candles.Count == 0)
            {
                return Task.FromResult<DateTime?>(null);
            }

            return Task.FromResult<DateTime?>(candles.Max(candle => candle.OccurredAtUtc.Date));
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Historical candle symbol is required.", nameof(symbol));
        }

        return symbol.Trim().ToUpperInvariant();
    }
}
