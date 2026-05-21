# Operative Calibrated Score Design

## Current Architecture Context

MarketAgent currently calculates a signal score inside the signal analysis/generation flow. Score calibration V1 is diagnostic-only:

- `MarketSignal.Score` remains raw/operative.
- `ScoreAttributionJson` contains `RawScore` and `CalibratedScore`.
- Dashboard ranking and alert rules still use the raw score through the normal `score` field.

The new change should make calibrated score operative for new generated signals while preserving raw-score explainability.

## Design Principle

Apply calibration after existing raw score calculation and before constructing or persisting the final `MarketSignal`.

Do not remove or rewrite existing scoring factors. The raw score should still be calculated exactly as it is today.

## Score Semantics

After this change:

- `RawScore`: score after existing raw calculation and current clamping behavior.
- `UncappedScore`: base score plus score-breakdown contribution sum.
- `CalibratedScore`: soft-cap normalized score derived from `RawScore`.
- `FinalScore`: operative score used by the app, equal to `CalibratedScore`.
- `MarketSignal.Score`: equal to `CalibratedScore`.
- `SignalSnapshot.Score`: equal to `CalibratedScore`.

V1 should continue using the existing soft-cap configuration:

```text
threshold = 85
compressionFactor = 0.55
maxScore = 100
```

Formula:

```text
if rawScore <= 85:
    calibratedScore = rawScore
else:
    calibratedScore = 85 + (rawScore - 85) * 0.55

calibratedScore = clamp(calibratedScore, 0, 100)
```

## Attribution Flow

The attribution builder needs enough information to preserve both scores.

Recommended signature direction:

```csharp
Build(
    IReadOnlyCollection<ScoreBreakdownItem> scoreBreakdown,
    decimal rawScore,
    decimal finalScore)
```

or an equivalent options object if cleaner.

Rules:

- `RawScore` should receive the pre-calibration operative raw score.
- `CalibratedScore` should receive the normalization result.
- `FinalScore` should receive the actual operative score after calibration.
- `WasNormalized` should remain true when `RawScore != CalibratedScore`.
- `WasCapped` should continue describing whether the raw scoring path hit the raw cap.

Legacy fallback behavior should still reconstruct attribution from older `ScoreBreakdownJson` where possible.

## Signal Generation Flow

Recommended sequence:

1. Existing analyzer calculates raw score and score breakdown.
2. Apply `ScoreCalibrationService.Calibrate(rawScore)`.
3. Construct `MarketSignal` with `Score = calibration.CalibratedScore`.
4. Preserve raw score in attribution metadata.
5. Persist signal snapshot with calibrated `Score`.
6. Persist `ScoreAttributionJson` with raw and calibrated values.

This keeps frontend and downstream services simple: they continue to read `score`.

## API Behavior

No new endpoint is required.

Existing reads should naturally return calibrated scores for new signals:

- signal generation response
- persisted signal snapshots
- outcome analytics
- score/confidence buckets
- alert evaluation candidates
- dashboard scanner data

Existing score-attribution endpoint should show both raw and calibrated values.

## Alert Threshold Review

Alert evaluation currently uses operative score.

With calibrated score operative:

```text
Score >= 85
```

will evaluate calibrated score.

V1 recommendation:

- Keep the threshold unchanged.
- Document that alert gating becomes stricter.
- Revisit after observing manual cycle results.

If alert volume becomes too low, a later change can lower the alert threshold or use attribution raw score explicitly. Do not mix raw-score alerting with calibrated dashboard ranking in V1 unless there is a strong product reason.

## Historical Rows

Do not backfill existing rows.

Consequences:

- Old `SignalSnapshots.Score` values may be raw.
- New `SignalSnapshots.Score` values will be calibrated.
- Analytics grouped by score bucket may combine two score eras.

Possible future mitigation:

- Add `ScoreVersion`.
- Add `RawScore` and `CalibratedScore` persisted columns.
- Backfill `ScoreAttributionJson`.

These are out of scope for V1.

## Testing

Unit tests should cover:

- Raw score below threshold remains operative unchanged.
- Raw score at threshold remains operative unchanged.
- Raw score above threshold becomes calibrated operative score.
- `MarketSignal.Score` equals calibrated score for new generated signals.
- `ScoreAttribution.RawScore` preserves raw score.
- `ScoreAttribution.CalibratedScore` equals calibrated score.
- `ScoreAttribution.FinalScore` equals operative calibrated score.
- Existing score-breakdown JSON behavior is preserved.
- Alert evaluator still reads normal `Score` and does not need code changes unless threshold decisions change.

## Risks and Open Questions

- Where is the safest exact point to apply calibration: analyzer output or persistence boundary?
- Should `RawScore` represent raw capped score or uncapped contribution total?
- Should `FinalScore` be renamed later to `OperativeScore` to avoid ambiguity?
- Should score bucket analytics label the score era?
- Should alert threshold remain `85` after calibration?

## Rollback Plan

- Revert the signal-generation change that assigns calibrated score to `MarketSignal.Score`.
- Keep or remove attribution calibration metadata independently.
- No database schema rollback expected.
- Existing generated calibrated rows can remain or be regenerated if a clean historical split is required.
