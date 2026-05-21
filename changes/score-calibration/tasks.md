# Score Calibration and Normalization Tasks

## V1 Scope Decisions

- Backend only.
- No frontend changes.
- Keep existing scoring factors.
- Do not rewrite the analyzer.
- Prefer additive normalization after existing scoring.
- Preserve raw and calibrated values for diagnostics.
- Preserve score attribution.
- Keep rollback simple.

## Recommended Implementation Path

Start with diagnostic-only calibration.

That means:

- Calculate calibrated scores.
- Persist/expose calibration metadata through score attribution.
- Do not yet change `MarketSignal.Score` used by dashboard, alerts, and existing ranking.

After reviewing diagnostics, a follow-up can make calibrated score operative.

## Backend Tasks

### 1. Add calibration models

- Add `ScoreCalibrationResult`.
- Add `ScoreCalibrationDiagnostics`.
- Optionally add bucket/distribution models if needed.

Include:

- raw score
- calibrated score
- was normalized
- reason

Diagnostics should include:

- total count
- capped raw score count
- average raw score
- average calibrated score
- highest raw score
- highest calibrated score
- top-score dispersion before
- top-score dispersion after

### 2. Add pure calibration helper

- Add `ScoreCalibrationService` or `ScoreCalibrationBuilder`.
- Keep it pure and unit-testable.

V1 soft cap parameters:

```text
softCapThreshold = 85
compressionFactor = 0.55
maxScore = 100
```

Formula:

```text
raw <= 85:
  calibrated = raw

raw > 85:
  calibrated = 85 + (raw - 85) * 0.55
```

Clamp final calibrated value to `0..100`.

### 3. Integrate with score attribution

- Extend `ScoreAttribution` or add nested calibration metadata.
- Include:
  - raw score
  - calibrated score
  - was normalized
  - calibration reason
- Preserve existing fields:
  - base score
  - uncapped score
  - final score
  - was capped
  - dominant factors
  - positive/negative contributions

### 4. Capture calibration metadata during persistence

- In `EfSignalSnapshotHistoryRepository`, when building attribution:
  - use current `MarketSignal.Score` as raw score
  - compute calibrated score
  - store both in `ScoreAttributionJson`
- Preserve existing `ScoreBreakdownJson`.
- Do not change `SignalSnapshots.Score` in diagnostic-only V1.

### 5. Add diagnostics builder

- Build diagnostics from a collection of raw/calibrated score pairs.
- Include distribution before/after if practical.
- Keep diagnostics backend/internal in V1.

### 6. Decide operative score behavior

Default V1:

- Keep operative score unchanged.

If later switched:

- Use calibrated score as `MarketSignal.Score`.
- Preserve raw score in attribution.
- Review alert threshold `Score >= 85`.

Do not switch operative score in this V1 unless explicitly requested during implementation.

### 7. Add tests

Calibration helper tests:

- Score below threshold is unchanged.
- Score at threshold is unchanged.
- Score above threshold is compressed.
- Score above max remains clamped.
- `WasNormalized` is true when raw differs from calibrated.
- `99.24 -> 92.83`.
- `108.29 -> 97.81`.

Attribution integration tests:

- Attribution includes raw score.
- Attribution includes calibrated score.
- Attribution includes calibration reason.
- Existing capped score detection still works.
- Existing score contribution breakdown still works.

Diagnostics tests:

- Capped count works.
- Average raw score works.
- Average calibrated score works.
- Top-score dispersion before/after works.

Regression:

- Existing unit tests pass.
- Alert engine tests pass.
- Score attribution tests pass.

## Optional Future Tasks

### Momentum overheating penalty

- Add a penalty for excessive RSI, EMA extension, or stretched range position.
- Keep separate from V1 calibration.

### Diminishing returns for stacked positives

- Apply decreasing marginal value after positive contribution total exceeds a threshold.
- More invasive; defer until diagnostics prove soft cap is insufficient.

### Dynamic run-level normalization

- Normalize relative to current run distribution.
- Useful later, but risky for historical comparability.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Manual:

- Run signals.
- Query score attribution for saturated and near-saturated signals.
- Confirm:
  - raw score is preserved
  - calibrated score is lower near top range
  - uncapped score remains visible
  - final operative score is unchanged in diagnostic-only V1

Example checks:

```text
NVDA raw/uncapped high score should show normalization metadata.
V near 99 should show calibrated score lower than raw.
```

## Risks

- Diagnostic-only V1 will not immediately change dashboard ranking.
- Operative calibration later may require alert threshold review.
- Soft cap parameters may need tuning.
- Users may confuse raw, calibrated, uncapped, and final score unless naming is clear.
- If attribution remains approximate, calibration diagnostics are also approximate.

## Open Questions

- Should calibrated score become operative in V1 or V2?
- Should alert rules use raw score or calibrated score?
- Should calibration metadata be queryable in aggregate through an endpoint?
- Should historical signals be backfilled?
- Should top-score dispersion use range or standard deviation?

## Rollback Plan

Diagnostic-only rollback:

- Remove calibration helper/models.
- Remove calibration fields from score attribution JSON generation.
- Keep existing score behavior unchanged.

If operative score was changed:

- Restore current raw score as `MarketSignal.Score`.
- Keep or remove calibration metadata depending on whether diagnostics remain useful.

Database:

- No migration expected if calibration is stored inside existing `ScoreAttributionJson`.
