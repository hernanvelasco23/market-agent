using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Infrastructure.Indicators;

public sealed class TechnicalIndicatorService : ITechnicalIndicatorService
{
    public TechnicalIndicators Calculate(
        IReadOnlyCollection<MarketCandle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);

        var orderedCandles = candles
            .OrderBy(candle => candle.OccurredAtUtc)
            .ToArray();

        if (orderedCandles.Length == 0)
        {
            return new TechnicalIndicators(null, null, null, null, null, null, null);
        }

        return new TechnicalIndicators(
            CalculateEma(orderedCandles, 9),
            CalculateEma(orderedCandles, 20),
            CalculateEma(orderedCandles, 50),
            CalculateRsi(orderedCandles, 14),
            CalculateAtr(orderedCandles, 14),
            CalculateAverageVolume(orderedCandles, 10),
            CalculateAverageVolume(orderedCandles, 20));
    }

    private static decimal? CalculateEma(IReadOnlyList<MarketCandle> candles, int period)
    {
        if (candles.Count < period)
        {
            return null;
        }

        var multiplier = 2m / (period + 1);
        var ema = candles
            .Take(period)
            .Average(candle => candle.Close);

        foreach (var candle in candles.Skip(period))
        {
            ema = ((candle.Close - ema) * multiplier) + ema;
        }

        return Round(ema);
    }

    private static decimal? CalculateRsi(IReadOnlyList<MarketCandle> candles, int period)
    {
        if (candles.Count < period + 1)
        {
            return null;
        }

        var closes = candles
            .TakeLast(period + 1)
            .Select(candle => candle.Close)
            .ToArray();
        var gains = 0m;
        var losses = 0m;

        for (var index = 1; index < closes.Length; index++)
        {
            var change = closes[index] - closes[index - 1];

            if (change > 0)
            {
                gains += change;
            }
            else
            {
                losses += Math.Abs(change);
            }
        }

        var averageGain = gains / period;
        var averageLoss = losses / period;

        if (averageLoss == 0m)
        {
            return 100m;
        }

        var relativeStrength = averageGain / averageLoss;
        return Round(100m - (100m / (1m + relativeStrength)));
    }

    private static decimal? CalculateAtr(IReadOnlyList<MarketCandle> candles, int period)
    {
        if (candles.Count < period + 1)
        {
            return null;
        }

        var trueRanges = new List<decimal>();

        for (var index = 1; index < candles.Count; index++)
        {
            var candle = candles[index];
            var previousClose = candles[index - 1].Close;
            var highLow = candle.High - candle.Low;
            var highPreviousClose = Math.Abs(candle.High - previousClose);
            var lowPreviousClose = Math.Abs(candle.Low - previousClose);

            trueRanges.Add(Math.Max(highLow, Math.Max(highPreviousClose, lowPreviousClose)));
        }

        return Round(trueRanges.TakeLast(period).Average());
    }

    private static decimal? CalculateAverageVolume(IReadOnlyList<MarketCandle> candles, int period)
    {
        var volumes = candles
            .TakeLast(period)
            .Select(candle => candle.Volume)
            .Where(volume => volume.HasValue)
            .Select(volume => volume!.Value)
            .ToArray();

        return volumes.Length < period
            ? null
            : Round(volumes.Average());
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
