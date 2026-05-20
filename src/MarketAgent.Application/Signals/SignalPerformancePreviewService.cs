using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Signals;

public sealed class SignalPerformancePreviewService : ISignalPerformancePreviewService
{
    private const int DefaultDays = 180;
    private const int MaxDays = 300;
    private const int MinimumLookbackCandles = 20;
    private const int InsufficientSampleThreshold = 3;
    private const int LowSampleThreshold = 10;
    private const int WinRateSampleThreshold = 5;

    private static readonly IReadOnlyCollection<string> SupportedSignalTypes =
    [
        "MomentumContinuation",
        "OpeningRedReversal",
        "Pullback",
        "OverextendedWarning"
    ];

    private static readonly IReadOnlyCollection<string> PreviewWarnings =
    [
        "Historical samples are reconstructed from available OHLCV candles and may differ from real-time emitted signals.",
        "Small sample sizes are not statistically reliable.",
        "This preview is educational and diagnostic only, not trading advice.",
        "Historical outcomes do not guarantee future performance."
    ];

    private readonly IHistoricalMarketDataService _historicalMarketDataService;
    private readonly IMarketSignalAnalyzer _marketSignalAnalyzer;

    public SignalPerformancePreviewService(
        IHistoricalMarketDataService historicalMarketDataService,
        IMarketSignalAnalyzer marketSignalAnalyzer)
    {
        _historicalMarketDataService = historicalMarketDataService;
        _marketSignalAnalyzer = marketSignalAnalyzer;
    }

    public async Task<SignalPerformancePreviewResult> GenerateAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        var requestedDays = NormalizeDays(days);
        var historical = await _historicalMarketDataService.GetWatchlistCandlesAsync(
            requestedDays,
            cancellationToken);
        var samplesByType = SupportedSignalTypes.ToDictionary(
            signalType => signalType,
            _ => new List<SignalPerformanceSample>(),
            StringComparer.OrdinalIgnoreCase);
        var candlesBySymbol = historical.Candles
            .GroupBy(candle => candle.Symbol)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(candle => candle.OccurredAtUtc)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var (symbol, candles) in candlesBySymbol)
        {
            if (symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase) ||
                candles.Length <= MinimumLookbackCandles)
            {
                continue;
            }

            for (var index = MinimumLookbackCandles; index < candles.Length - 1; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = candles[index];
                var previous = candles[index - 1];
                var snapshot = CreateSnapshot(candidate, previous);
                var analyzerCandles = GetAnalyzerCandles(candidate, candles, candlesBySymbol);
                var signal = _marketSignalAnalyzer
                    .Analyze([snapshot], analyzerCandles)
                    .FirstOrDefault(item => item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

                if (signal is null)
                {
                    continue;
                }

                foreach (var signalType in GetMatchedSignalTypes(signal))
                {
                    if (!samplesByType.TryGetValue(signalType, out var samples))
                    {
                        continue;
                    }

                    samples.Add(CreateSample(candles, index));
                }
            }
        }

        var items = SupportedSignalTypes
            .Select(signalType => CreateItem(signalType, samplesByType[signalType]))
            .ToArray();

        return new SignalPerformancePreviewResult(
            DateTime.UtcNow,
            requestedDays,
            items,
            PreviewWarnings,
            historical.Failures);
    }

    private static int NormalizeDays(int days)
    {
        return Math.Clamp(days <= 0 ? DefaultDays : days, 1, MaxDays);
    }

    private static MarketSnapshot CreateSnapshot(MarketCandle candidate, MarketCandle previous)
    {
        return new MarketSnapshot(
            Guid.NewGuid(),
            candidate.Symbol,
            candidate.AssetType,
            candidate.Close,
            "USD",
            candidate.OccurredAtUtc,
            candidate.Source,
            candidate.Volume,
            candidate.Open,
            candidate.High,
            candidate.Low,
            previous.Close);
    }

    private static IReadOnlyCollection<MarketCandle> GetAnalyzerCandles(
        MarketCandle candidate,
        IReadOnlyCollection<MarketCandle> symbolCandles,
        IReadOnlyDictionary<string, MarketCandle[]> candlesBySymbol)
    {
        var analyzerCandles = symbolCandles
            .Where(candle => candle.OccurredAtUtc <= candidate.OccurredAtUtc)
            .ToList();

        if (!candidate.Symbol.Equals("SPY", StringComparison.OrdinalIgnoreCase) &&
            candlesBySymbol.TryGetValue("SPY", out var spyCandles))
        {
            analyzerCandles.AddRange(spyCandles.Where(candle => candle.OccurredAtUtc <= candidate.OccurredAtUtc));
        }

        return analyzerCandles;
    }

    private static IEnumerable<string> GetMatchedSignalTypes(MarketSignal signal)
    {
        if (signal.MomentumContinuation ||
            signal.SetupType.Equals("MomentumContinuation", StringComparison.OrdinalIgnoreCase))
        {
            yield return "MomentumContinuation";
        }

        if (signal.OpeningRedReversalDetected)
        {
            yield return "OpeningRedReversal";
        }

        if (signal.SetupType.Equals("Pullback", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Pullback";
        }

        if (!string.IsNullOrWhiteSpace(signal.ExtensionRisk) ||
            signal.SetupType.Equals("ExtendedMomentum", StringComparison.OrdinalIgnoreCase))
        {
            yield return "OverextendedWarning";
        }
    }

    private static SignalPerformanceSample CreateSample(IReadOnlyList<MarketCandle> candles, int index)
    {
        var entryClose = candles[index].Close;

        return new SignalPerformanceSample(
            CalculateForwardReturn(candles, index, 1, entryClose),
            CalculateForwardReturn(candles, index, 3, entryClose),
            CalculateForwardReturn(candles, index, 5, entryClose));
    }

    private static decimal? CalculateForwardReturn(
        IReadOnlyList<MarketCandle> candles,
        int index,
        int horizon,
        decimal entryClose)
    {
        var futureIndex = index + horizon;

        if (entryClose <= 0m || futureIndex >= candles.Count)
        {
            return null;
        }

        return Round(((candles[futureIndex].Close - entryClose) / entryClose) * 100m);
    }

    private static SignalPerformancePreviewItem CreateItem(
        string signalType,
        IReadOnlyCollection<SignalPerformanceSample> samples)
    {
        var sampleCount = samples.Count;
        var isInsufficientData = sampleCount < InsufficientSampleThreshold;
        var hasLowSampleWarning = sampleCount > 0 && sampleCount < LowSampleThreshold;

        return new SignalPerformancePreviewItem(
            signalType,
            sampleCount,
            isInsufficientData,
            hasLowSampleWarning,
            Average(samples.Select(sample => sample.ForwardReturn1Day)),
            Average(samples.Select(sample => sample.ForwardReturn3Day)),
            Average(samples.Select(sample => sample.ForwardReturn5Day)),
            WinRate(samples.Select(sample => sample.ForwardReturn1Day)),
            WinRate(samples.Select(sample => sample.ForwardReturn3Day)),
            WinRate(samples.Select(sample => sample.ForwardReturn5Day)));
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var availableValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return availableValues.Length == 0
            ? null
            : Round(availableValues.Average());
    }

    private static decimal? WinRate(IEnumerable<decimal?> values)
    {
        var availableValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        if (availableValues.Length < WinRateSampleThreshold)
        {
            return null;
        }

        return Round((availableValues.Count(value => value > 0m) / (decimal)availableValues.Length) * 100m);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record SignalPerformanceSample(
        decimal? ForwardReturn1Day,
        decimal? ForwardReturn3Day,
        decimal? ForwardReturn5Day);
}
