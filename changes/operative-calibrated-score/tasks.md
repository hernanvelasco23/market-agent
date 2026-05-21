# Operative Calibrated Score Tasks

## V1 Scope Decisions

- Backend first.
- Make calibrated score operative for new generated signals.
- Do not rewrite frontend.
- Do not backfill historical rows.
- Do not change scoring factors.
- Do not change outcome evaluation.
- Review alert threshold impact before changing thresholds.
- No database migration expected for V1.

## Backend Tasks

### 1. Inspect current score generation flow

- Locate where `MarketSignal.Score` is assigned.
- Locate where `ScoreBreakdownJson` is built.
- Locate where `ScoreAttributionJson` is built during signal persistence.
- Confirm whether score clamping happens before `MarketSignal` construction.

### 2. Identify raw score boundary

- Define raw score as the existing score value before calibration.
- Preserve existing raw calculation and raw clamp behavior.
- Do not alter individual score factors.

### 3. Apply calibration before final signal creation

- Call `ScoreCalibrationService.Calibrate(rawScore)` after existing raw score calculation.
- Use `calibration.CalibratedScore` as the final `MarketSignal.Score`.
- Keep all other signal fields unchanged.

### 4. Update attribution building

- Ensure attribution receives both:
  - raw score
  - operative final score
- Store:
  - `RawScore`
  - `UncappedScore`
  - `CalibratedScore`
  - `FinalScore`
- Set `FinalScore` equal to the operative calibrated score.
- Preserve positive and negative contribution breakdowns.
- Preserve legacy fallback behavior.

### 5. Preserve persistence behavior

- Persist `SignalSnapshot.Score` using the calibrated operative score.
- Persist `ScoreBreakdownJson` unchanged.
- Persist `ScoreAttributionJson` with raw and calibrated metadata.
- Do not add columns in V1.

### 6. Review alert threshold behavior

- Confirm `AlertEvaluationService` reads normal persisted/generated score.
- Keep `Score >= 85` unchanged for V1 unless implementation review shows unacceptable behavior.
- Document that alert gating now uses calibrated score and is stricter.
- Do not introduce raw-score alerting in V1.

### 7. Add tests

Unit tests:

- Raw score below `85` remains unchanged as operative score.
- Raw score at `85` remains unchanged as operative score.
- Raw score above `85` becomes calibrated operative score.
- `MarketSignal.Score` equals calibrated score for newly generated signals.
- `ScoreAttribution.RawScore` preserves raw score.
- `ScoreAttribution.CalibratedScore` equals calibrated score.
- `ScoreAttribution.FinalScore` equals calibrated score.
- Existing score attribution tests still pass after expected updates.
- Existing alert tests still pass or are updated only for calibrated-score expectations.

### 8. Validate no frontend rewrite is needed

- Confirm frontend reads existing `score` field.
- Confirm generated signal response now returns calibrated score.
- Do not change dashboard components in V1.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Manual validation:

- Run ingestion.
- Run signals.
- Pick a high raw-score signal.
- Verify displayed/generated `score` is calibrated.
- Call:

```text
GET /api/signals/{signalSnapshotId}/score-attribution
```

- Verify:
  - `rawScore` preserves the old operative score
  - `calibratedScore` is compressed
  - `finalScore` equals the visible/operative score

Alert validation:

- Run:

```text
POST /api/alerts/evaluate
```

- Verify alert counts after threshold review.
- Confirm duplicate behavior is unchanged.

## Risks

- Alert volume may fall because `Score >= 85` now evaluates calibrated scores.
- Historical rows will mix raw and calibrated score eras.
- Score bucket analytics may shift sharply after the change.
- If attribution builder receives only final score, raw score could be lost.
- Applying calibration in both analyzer and persistence layers could double-compress scores.

## Open Questions

- Should a future migration add explicit `RawScore` and `CalibratedScore` columns?
- Should alert threshold remain `85` after observing calibrated-score volume?
- Should score bucket analytics display a score-version note later?
- Should `FinalScore` be renamed or supplemented with `OperativeScore` in a future version?

## Rollback Plan

Backend:

- Revert the change that assigns `CalibratedScore` to `MarketSignal.Score`.
- Restore raw score as the operative score.
- Keep `ScoreAttributionJson` metadata if still useful.
- Re-run tests.

Database:

- No migration rollback expected.
- Existing calibrated rows can remain or be regenerated if a clean history is required.

Frontend:

- No rollback expected because frontend continues using the same `score` field.

### 7b. Add calibration telemetry logging

- Log:
  - raw score
  - calibrated score
  - normalization delta
  - setup
  - symbol
for signals where normalization was applied.
