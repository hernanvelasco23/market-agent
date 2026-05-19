using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Abstractions;

public interface ITechnicalIndicatorService
{
    TechnicalIndicators Calculate(
        IReadOnlyCollection<MarketCandle> candles);
}
