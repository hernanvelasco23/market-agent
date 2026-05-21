using MarketAgent.Application.Signals;

namespace MarketAgent.UnitTests;

public sealed class ScoreCalibrationServiceTests
{
    [Theory]
    [InlineData(60, 60, false)]
    [InlineData(85, 85, false)]
    [InlineData(90, 87.75, true)]
    [InlineData(99.24, 92.83, true)]
    [InlineData(108.29, 97.81, true)]
    [InlineData(200, 100, true)]
    [InlineData(-5, 0, true)]
    public void Calibrate_AppliesSoftCapWithoutChangingLowScores(
        double rawScore,
        double expectedCalibratedScore,
        bool expectedWasNormalized)
    {
        var result = ScoreCalibrationService.Calibrate((decimal)rawScore);

        Assert.Equal((decimal)expectedCalibratedScore, result.CalibratedScore);
        Assert.Equal(expectedWasNormalized, result.WasNormalized);
    }

    [Fact]
    public void Calibrate_ReturnsReason_WhenScoreIsNormalized()
    {
        var result = ScoreCalibrationService.Calibrate(99.24m);

        Assert.NotNull(result.Reason);
        Assert.Contains("Soft cap", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
