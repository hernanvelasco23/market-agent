using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface IHistoricalCandleRepository
{
    Task<IReadOnlyCollection<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default);

    Task SaveCandlesAsync(
        string symbol,
        IReadOnlyCollection<MarketCandle> candles,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetLatestCandleDateAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}
