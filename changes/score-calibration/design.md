# Score Calibration and Normalization Design

## Current Architecture Context

Backend:

- `TechnicalMarketSignalAnalyzer` starts from a base score around `50`.
- It adds existing score factors into `ScoreBreakdown`.
- It clamps the score to `0..100`.
- Additional late scoring factors can also push already-high scores upward.
- `MarketSignal.Score` is currently the final displayed/persisted score.
- `ScoreAttributionJson` can expose `baseScore`, `uncappedScore`, `finalScore`, and capped state.

Known issue:

- Several strong candidates reach or approach `100`, reducing ranking separation.

## Design Principle

V1 should not rewrite scoring.

Instead:

```text
existing scoring factors -> raw score -> calibration -> calibrated score
```

The system should preserve:

- raw score
- calibrated score
- calibration reason
- whether normalization was applied
- score attribution details

## Proposed Model Changes

Add calibration metadata to score attribution first, because attribution is already the explainability boundary.

Possible updated model:

```csharp
public sealed record ScoreAttribution(
    decimal BaseScore,
    decimal UncappedScore,
    decimal RawScore,
    decimal CalibratedScore,
    decimal FinalScore,
    bool WasCapped,
    bool WasNormalized,
    string? CalibrationReason,
    string? DominantPositiveFactor,
    string? DominantNegativeFactor,
    IReadOnlyCollection<ScoreContribution> PositiveContributions,
    IReadOnlyCollection<ScoreContribution> NegativeContributions);
```

If adding fields to the existing record is too disruptive, add a nested model:

```csharp
public sealed record ScoreCalibrationMetadata(
    decimal RawScore,
    decimal CalibratedScore,
    bool WasNormalized,
    string? CalibrationReason);
```

Recommended persistence:

- Keep `SignalSnapshots.Score` as the currently operative score.
- Add nullable fields only if needed:
  - `RawScore decimal(9,2) null`
  - `CalibratedScore decimal(9,2) null`
  - `CalibrationReason nvarchar(256) null`
  - `WasNormalized bit null`

Alternative:

- Store calibration data only inside `ScoreAttributionJson`.

V1 recommendation:

- Store calibration metadata in `ScoreAttributionJson` first.
- Add physical columns later only if querying/filtering calibrated-vs-raw score becomes necessary.

## Calibration Helper

Add a pure helper:

```csharp
public static class ScoreCalibrationService
{
    public static ScoreCalibrationResult Calibrate(decimal rawScore);
}
```

Model:

```csharp
public sealed record ScoreCalibrationResult(
    decimal RawScore,
    decimal CalibratedScore,
    bool WasNormalized,
    string? Reason);
```

## Recommended V1 Algorithm: Soft Cap

Parameters:

```text
softCapThreshold = 85
compressionFactor = 0.55
maxScore = 100
```

Formula:

```text
if rawScore <= 85:
    calibrated = rawScore
else:
    calibrated = 85 + (rawScore - 85) * 0.55

calibrated = clamp(calibrated, 0, 100)
```

Examples:

```text
raw 85   -> 85
raw 90   -> 87.75
raw 95   -> 90.50
raw 99.24 -> 92.83
raw 100  -> 93.25
raw 108.29 -> 97.81
```

This improves separation:

- `NVDA uncappedScore 108.29` remains stronger than `V uncappedScore 99.24`.
- Both no longer collapse into the same top band.

## Raw vs Uncapped vs Calibrated

Definitions:

- `UncappedScore`: base score plus all contributions before hard clamp.
- `RawScore`: existing score after current analyzer scoring/clamp path.
- `CalibratedScore`: normalized score after soft cap.
- `FinalScore`: the score used by downstream consumers.

Open decision:

- Should `FinalScore` become `CalibratedScore` immediately?
- Or should V1 calculate calibration diagnostics but keep `FinalScore` equal to old `RawScore`?

Conservative V1 recommendation:

- Phase 1: persist diagnostics only; keep operative `MarketSignal.Score` unchanged.
- Phase 2: switch operative score to calibrated score after reviewing distribution.

If the user explicitly wants behavior change in V1:

- Use `CalibratedScore` as `MarketSignal.Score`.
- Preserve `RawScore` in attribution.
- Revisit alert thresholds because `Score >= 85` becomes stricter.

## Alert Compatibility

Current alert rule uses score threshold.

Risk:

- If `MarketSignal.Score` changes from raw to calibrated, `Score >= 85` alerts may become less frequent.

Options:

1. Keep alerts using raw score for now.
2. Switch alerts to calibrated score and lower threshold later.
3. Include both in `ReasonJson`.

V1 recommendation:

- Do not change alert rules.
- If calibrated score becomes operative, update alert `ReasonJson` to include raw score in a follow-up.

## Diagnostics

Add backend/internal diagnostics:

```csharp
public sealed record ScoreCalibrationDiagnostics(
    int TotalCount,
    int CappedRawScoreCount,
    decimal? AverageRawScore,
    decimal? AverageCalibratedScore,
    decimal? HighestRawScore,
    decimal? HighestCalibratedScore,
    decimal? TopScoreDispersionBefore,
    decimal? TopScoreDispersionAfter);
```

Distribution buckets:

```text
0-20
21-40
41-60
61-80
81-90
91-95
96-100
```

Top-score dispersion:

- Standard deviation or range among top N scores.
- V1 can use simple range:

```text
max(topN) - min(topN)
```

## API Design

No frontend-facing API is required in V1.

Optional backend diagnostic endpoint later:

```text
GET /api/signals/score-calibration/diagnostics
```

V1 can keep diagnostics internal through services/tests/logs.

## Database Design

V1 diagnostic-only approach:

- No new database columns beyond existing `ScoreAttributionJson`.
- Store raw/calibrated fields inside attribution JSON.

If operative score changes:

- Existing `SignalSnapshots.Score` remains final operative score.
- Raw/calibrated details live in `ScoreAttributionJson`.

No migration required unless dedicated columns are added.

## Testing Strategy

Unit tests:

- Raw score below threshold is unchanged.
- Raw score above threshold is compressed.
- Calibrated score is clamped to `0..100`.
- `WasNormalized` is true only when raw score changes.
- Examples produce expected values:
  - `99.24 -> 92.83`
  - `108.29 -> 97.81`
- Diagnostics compare before/after distribution.

Regression tests:

- Existing analyzer tests still pass.
- Alert engine tests still pass.
- Score attribution tests still pass.

If operative score changes:

- Update only tests that assert exact score values where calibration is intentionally applied.
- Add explicit tests proving ranking is preserved for sample scores.

## Risks and Open Questions

- Should V1 be diagnostic-only or make calibrated score operative?
- Which threshold/compression factor should ship first?
- Should calibration apply only above `85`, or should it also smooth mid-range scores?
- Should `MomentumContinuation` get a specific overheating penalty later?
- Should alert rules read raw or calibrated score?
- Should historical signals be backfilled with calibration metadata?

## Rollback Plan

Diagnostic-only rollback:

- Remove calibration helper and diagnostics.
- Stop writing calibration fields into `ScoreAttributionJson`.

Operative-score rollback:

- Restore `MarketSignal.Score` to raw score.
- Keep calibration metadata for analysis if useful.
- No frontend rollback expected.
