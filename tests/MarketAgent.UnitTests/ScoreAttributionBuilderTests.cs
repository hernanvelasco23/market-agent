using MarketAgent.Application.Signals;
using MarketAgent.Domain.Entities;

namespace MarketAgent.UnitTests;

public sealed class ScoreAttributionBuilderTests
{
    [Fact]
    public void Build_SeparatesContributionsAndDetectsCappedScore()
    {
        var attribution = ScoreAttributionBuilder.Build(
            finalScore: 100m,
            scoreBreakdown:
            [
                new MarketSignalScoreFactor("RelativeStrengthVsSpy", 20m),
                new MarketSignalScoreFactor("PositiveEmaStack", 15m),
                new MarketSignalScoreFactor("OverextensionRisk", -5m),
                new MarketSignalScoreFactor("MomentumContinuation", 30m)
            ]);

        Assert.Equal(50m, attribution.BaseScore);
        Assert.Equal(110m, attribution.UncappedScore);
        Assert.Equal(100m, attribution.RawScore);
        Assert.Equal(93.25m, attribution.CalibratedScore);
        Assert.Equal(100m, attribution.FinalScore);
        Assert.Equal(-6.75m, attribution.NormalizationDelta);
        Assert.True(attribution.WasCapped);
        Assert.True(attribution.WasNormalized);
        Assert.NotNull(attribution.CalibrationReason);
        Assert.Equal("MomentumContinuation", attribution.DominantPositiveFactor);
        Assert.Equal("OverextensionRisk", attribution.DominantNegativeFactor);
        Assert.Equal(3, attribution.PositiveContributions.Count);
        Assert.Single(attribution.NegativeContributions);
    }

    [Fact]
    public void Build_HandlesEmptyBreakdown()
    {
        var attribution = ScoreAttributionBuilder.Build(50m, []);

        Assert.Equal(50m, attribution.BaseScore);
        Assert.Equal(50m, attribution.UncappedScore);
        Assert.Equal(50m, attribution.RawScore);
        Assert.Equal(50m, attribution.CalibratedScore);
        Assert.Equal(0m, attribution.NormalizationDelta);
        Assert.False(attribution.WasNormalized);
        Assert.Null(attribution.CalibrationReason);
        Assert.False(attribution.WasCapped);
        Assert.Null(attribution.DominantPositiveFactor);
        Assert.Null(attribution.DominantNegativeFactor);
        Assert.Empty(attribution.PositiveContributions);
        Assert.Empty(attribution.NegativeContributions);
    }

    [Fact]
    public void BuildDiagnostics_ReturnsCappedCountsAndUncappedScores()
    {
        var capped = ScoreAttributionBuilder.Build(
            100m,
            [new MarketSignalScoreFactor("RelativeStrengthVsSpy", 60m)]);
        var uncapped = ScoreAttributionBuilder.Build(
            75m,
            [new MarketSignalScoreFactor("PositiveEmaStack", 25m)]);

        var diagnostics = ScoreAttributionBuilder.BuildDiagnostics([capped, uncapped]);

        Assert.Equal(2, diagnostics.TotalCount);
        Assert.Equal(1, diagnostics.CappedRawScoreCount);
        Assert.Equal(92.5m, diagnostics.AverageUncappedScore);
        Assert.Equal(110m, diagnostics.HighestUncappedScore);
        Assert.Equal(87.5m, diagnostics.AverageRawScore);
        Assert.Equal(84.13m, diagnostics.AverageCalibratedScore);
        Assert.Equal(25m, diagnostics.Top10RawScoreRange);
        Assert.Equal(18.25m, diagnostics.Top10CalibratedScoreRange);
    }

    [Fact]
    public void Build_PreservesRawScoreAndUsesCalibratedFinalScore_WhenCalibrationIsOperative()
    {
        var attribution = ScoreAttributionBuilder.Build(
            rawScore: 100m,
            finalScore: 93.25m,
            scoreBreakdown:
            [
                new MarketSignalScoreFactor("RelativeStrengthVsSpy", 60m)
            ]);

        Assert.Equal(110m, attribution.UncappedScore);
        Assert.Equal(100m, attribution.RawScore);
        Assert.Equal(93.25m, attribution.CalibratedScore);
        Assert.Equal(93.25m, attribution.FinalScore);
        Assert.Equal(-6.75m, attribution.NormalizationDelta);
        Assert.True(attribution.WasCapped);
        Assert.True(attribution.WasNormalized);
    }
}
