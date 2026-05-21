using MarketAgent.Application.Models;

namespace MarketAgent.Application.Signals;

public static class ScoreCalibrationService
{
    public const decimal SoftCapThreshold = 85m;
    public const decimal CompressionFactor = 0.55m;
    public const decimal MaxScore = 100m;
    public const decimal MinScore = 0m;

    public static ScoreCalibrationResult Calibrate(decimal rawScore)
    {
        var calibratedScore = rawScore <= SoftCapThreshold
            ? rawScore
            : SoftCapThreshold + ((rawScore - SoftCapThreshold) * CompressionFactor);
        calibratedScore = Round(Clamp(calibratedScore, MinScore, MaxScore));
        var normalizedRawScore = Round(rawScore);
        var wasNormalized = calibratedScore != normalizedRawScore;

        return new ScoreCalibrationResult(
            normalizedRawScore,
            calibratedScore,
            wasNormalized,
            wasNormalized
                ? $"Soft cap applied above {SoftCapThreshold:0.##} with compression factor {CompressionFactor:0.##}."
                : null);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
