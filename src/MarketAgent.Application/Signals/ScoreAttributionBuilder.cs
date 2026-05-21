using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;

namespace MarketAgent.Application.Signals;

public static class ScoreAttributionBuilder
{
    public const decimal DefaultBaseScore = 50m;

    public static ScoreAttribution Build(
        decimal finalScore,
        IReadOnlyCollection<MarketSignalScoreFactor> scoreBreakdown,
        decimal baseScore = DefaultBaseScore)
    {
        return Build(finalScore, finalScore, scoreBreakdown, baseScore);
    }

    public static ScoreAttribution Build(
        decimal rawScore,
        decimal finalScore,
        IReadOnlyCollection<MarketSignalScoreFactor> scoreBreakdown,
        decimal baseScore = DefaultBaseScore)
    {
        ArgumentNullException.ThrowIfNull(scoreBreakdown);

        var contributions = scoreBreakdown
            .Select(factor => new ScoreContribution(
                NormalizeFactor(factor.Label),
                factor.Points,
                NormalizeFactor(factor.Label)))
            .ToArray();
        var positiveContributions = contributions
            .Where(contribution => contribution.Points > 0)
            .OrderByDescending(contribution => contribution.Points)
            .ThenBy(contribution => contribution.Factor, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var negativeContributions = contributions
            .Where(contribution => contribution.Points < 0)
            .OrderBy(contribution => contribution.Points)
            .ThenBy(contribution => contribution.Factor, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var uncappedScore = baseScore + contributions.Sum(contribution => contribution.Points);
        var calibration = ScoreCalibrationService.Calibrate(rawScore);

        return new ScoreAttribution(
            baseScore,
            uncappedScore,
            calibration.RawScore,
            calibration.CalibratedScore,
            finalScore,
            calibration.NormalizationDelta,
            uncappedScore > calibration.RawScore && calibration.RawScore == 100m,
            calibration.WasNormalized,
            calibration.Reason,
            positiveContributions.FirstOrDefault()?.Factor,
            negativeContributions.FirstOrDefault()?.Factor,
            positiveContributions,
            negativeContributions);
    }

    public static ScoreAttributionDiagnostics BuildDiagnostics(
        IReadOnlyCollection<ScoreAttribution> attributions)
    {
        ArgumentNullException.ThrowIfNull(attributions);

        if (attributions.Count == 0)
        {
            return new ScoreAttributionDiagnostics(0, 0, null, null, null, null, null, null);
        }

        var top10RawScores = attributions
            .OrderByDescending(attribution => attribution.RawScore)
            .Take(10)
            .Select(attribution => attribution.RawScore)
            .ToArray();
        var top10CalibratedScores = attributions
            .OrderByDescending(attribution => attribution.CalibratedScore)
            .Take(10)
            .Select(attribution => attribution.CalibratedScore)
            .ToArray();

        return new ScoreAttributionDiagnostics(
            attributions.Count,
            attributions.Count(attribution => attribution.WasCapped),
            Round(attributions.Average(attribution => attribution.UncappedScore)),
            Round(attributions.Max(attribution => attribution.UncappedScore)),
            Round(attributions.Average(attribution => attribution.RawScore)),
            Round(attributions.Average(attribution => attribution.CalibratedScore)),
            CalculateRange(top10RawScores),
            CalculateRange(top10CalibratedScores));
    }

    private static string NormalizeFactor(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim();
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? CalculateRange(IReadOnlyCollection<decimal> values)
    {
        return values.Count == 0
            ? null
            : Round(values.Max() - values.Min());
    }
}
