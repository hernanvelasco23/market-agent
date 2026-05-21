# Operative Calibrated Score

## Problem

Score calibration V1 is diagnostic-only. It stores `RawScore` and `CalibratedScore` in `ScoreAttributionJson`, but the operative `MarketSignal.Score` still uses the raw score.

As a result, the dashboard and alert engine still see many signals near `95-100`, especially strong momentum setups. The score attribution layer explains the saturation, but it does not yet improve ranking separation.

## Goal

Make calibrated score operative for newly generated signals.

After existing scoring factors calculate the raw score, MarketAgent should apply the soft-cap calibration and use the resulting `CalibratedScore` as:

- `MarketSignal.Score`
- persisted `SignalSnapshot.Score`
- dashboard-visible score for new signals
- alert-evaluation score for new signals

The raw score must remain preserved in `ScoreAttributionJson` for explainability and diagnostics.

## Scope

Backend first.

V1 should:

- Use `CalibratedScore` as the operative score for new generated signals.
- Preserve raw score metadata in `ScoreAttributionJson`.
- Preserve `UncappedScore`, `RawScore`, `CalibratedScore`, and `FinalScore`.
- Set `FinalScore` to the operative calibrated score.
- Avoid frontend rewrites.
- Avoid backfilling existing historical rows.
- Keep existing scoring factors intact.
- Keep calibration logic additive and reversible.

## Expected Behavior

Example:

```text
Raw operative score before this change: 100
Calibrated score: 93.25
New operative MarketSignal.Score: 93.25
```

Attribution should still explain the full path:

```json
{
  "baseScore": 50,
  "uncappedScore": 108.29,
  "rawScore": 100,
  "calibratedScore": 93.25,
  "finalScore": 93.25,
  "wasCapped": true,
  "wasNormalized": true
}
```

## Alert Threshold Impact

Current alert rules use:

```text
Score >= 85
```

Once calibrated score becomes operative, this threshold becomes stricter because saturated raw scores will compress downward.

V1 should review alert behavior before implementation:

- Keep `Score >= 85` if stricter alerts are acceptable.
- Consider lowering threshold only if alert volume drops too much.
- Do not silently change alert thresholds without documenting the decision.

Recommended V1 decision:

- Keep alert threshold unchanged at `85`.
- Treat this as intentional stricter quality gating.
- Monitor alert volume after manual runs.

## Historical Data

Existing persisted `SignalSnapshots` should not be backfilled in V1.

This means:

- Old rows may contain raw scores.
- New rows contain calibrated operative scores.
- `ScoreAttributionJson` distinguishes raw and calibrated values for new rows.
- Historical analytics may temporarily mix old raw-score rows and new calibrated-score rows.

If needed later, add a separate backfill/migration proposal.

## Out of Scope

- No frontend rewrite.
- No historical score backfill.
- No alert delivery changes.
- No scoring-factor redesign.
- No outcome-evaluation changes.
- No dashboard ranking rewrite.
- No recalibration of setup/confidence analytics in V1.

## Success Criteria

- New generated signals expose calibrated scores through existing signal APIs.
- Dashboard continues reading `score` normally and displays calibrated values for new signals.
- Raw score remains available in `ScoreAttributionJson`.
- `FinalScore` in attribution equals the operative calibrated score.
- Existing tests pass.
- Alert rule behavior is explicitly reviewed.

## Risks

- Alert volume may drop because `Score >= 85` becomes harder to reach.
- Historical analytics may mix raw and calibrated score eras.
- Users may compare old and new dashboard scores without realizing calibration changed.
- Incorrect attribution ordering could make `RawScore`, `CalibratedScore`, and `FinalScore` confusing.
- If calibration is applied in the wrong layer, persisted signal snapshots and in-memory dashboard signals could diverge.

## Rollback Plan

Backend rollback:

- Stop assigning calibrated score to `MarketSignal.Score`.
- Restore raw score as the operative score.
- Keep diagnostic calibration metadata if still useful.
- No database rollback required if no schema changes are introduced.

Frontend rollback:

- None expected because frontend continues reading the existing `score` field.

Data rollback:

- Existing rows generated during the calibrated-score window can remain as-is or be regenerated if necessary.
- No V1 backfill should be required.
