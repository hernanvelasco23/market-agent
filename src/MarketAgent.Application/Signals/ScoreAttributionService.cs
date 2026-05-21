using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.Signals;

public sealed class ScoreAttributionService : IScoreAttributionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISignalSnapshotHistoryRepository _signalSnapshotHistoryRepository;
    private readonly ILogger<ScoreAttributionService> _logger;

    public ScoreAttributionService(
        ISignalSnapshotHistoryRepository signalSnapshotHistoryRepository,
        ILogger<ScoreAttributionService> logger)
    {
        _signalSnapshotHistoryRepository = signalSnapshotHistoryRepository;
        _logger = logger;
    }

    public async Task<SignalScoreAttributionResult?> GetAsync(
        Guid signalSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _signalSnapshotHistoryRepository.GetScoreAttributionSnapshotAsync(
            signalSnapshotId,
            cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var attribution = DeserializeAttribution(snapshot.ScoreAttributionJson) ??
            BuildLegacyAttribution(snapshot.Score, snapshot.ScoreBreakdownJson);
        var diagnostics = ScoreAttributionBuilder.BuildDiagnostics([attribution]);

        _logger.LogDebug(
            "Score attribution diagnostics for signal {SignalSnapshotId}: capped={CappedCount}, averageUncappedScore={AverageUncappedScore}, highestUncappedScore={HighestUncappedScore}.",
            signalSnapshotId,
            diagnostics.CappedRawScoreCount,
            diagnostics.AverageUncappedScore,
            diagnostics.HighestUncappedScore);

        return new SignalScoreAttributionResult(
            snapshot.SignalSnapshotId,
            snapshot.RunId,
            snapshot.Symbol,
            snapshot.Setup,
            snapshot.Action,
            snapshot.Score,
            EnsureUtc(snapshot.CreatedAtUtc),
            attribution);
    }

    private static ScoreAttribution? DeserializeAttribution(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var attribution = JsonSerializer.Deserialize<ScoreAttribution>(json, JsonOptions);

            return attribution is null
                ? null
                : EnsureCalibration(attribution);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ScoreAttribution BuildLegacyAttribution(
        decimal finalScore,
        string? scoreBreakdownJson)
    {
        var factors = DeserializeScoreBreakdown(scoreBreakdownJson);

        return ScoreAttributionBuilder.Build(finalScore, factors);
    }

    private static ScoreAttribution EnsureCalibration(ScoreAttribution attribution)
    {
        if (attribution.RawScore != 0m ||
            attribution.FinalScore == 0m ||
            attribution.CalibratedScore != 0m ||
            attribution.CalibrationReason is not null)
        {
            return attribution.WasNormalized && attribution.NormalizationDelta == 0m
                ? attribution with { NormalizationDelta = Round(attribution.CalibratedScore - attribution.RawScore) }
                : attribution;
        }

        var calibration = ScoreCalibrationService.Calibrate(attribution.FinalScore);

        return attribution with
        {
            RawScore = calibration.RawScore,
            CalibratedScore = calibration.CalibratedScore,
            NormalizationDelta = calibration.NormalizationDelta,
            WasNormalized = calibration.WasNormalized,
            CalibrationReason = calibration.Reason
        };
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyCollection<MarketSignalScoreFactor> DeserializeScoreBreakdown(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyCollection<MarketSignalScoreFactor>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
