# Score Attribution Engine Tasks

## V1 Scope Decisions

- Backend only.
- Add explainability, not recalibration.
- Do not change scoring behavior.
- Do not change alert rules.
- Do not modify frontend.
- Keep changes additive and minimal.
- Prefer structured JSON persistence on `SignalSnapshots`.
- Reuse existing `ScoreBreakdownJson` and `MarketSignal.ScoreBreakdown` where possible.

## Backend Tasks

### 1. Add score attribution models

- Add `ScoreAttribution`.
- Add `ScoreContribution`.
- Add `SignalScoreAttributionResult`.
- Include:
  - base score
  - uncapped score
  - final score
  - was capped
  - dominant positive factor
  - dominant negative factor
  - positive contributions
  - negative contributions

### 2. Add attribution builder

- Add a small service/helper such as `ScoreAttributionBuilder`.
- Inputs:
  - final score
  - score breakdown factors
  - base score
- Outputs:
  - structured attribution model
- Keep the builder pure and unit-testable.

Rules:

- Positive contributions use factors with `Points > 0`.
- Negative contributions use factors with `Points < 0`.
- Uncapped score equals `baseScore + sum(points)`.
- `wasCapped` is true when `uncappedScore > finalScore && finalScore == 100`.
- Dominant positive factor is the largest positive contribution.
- Dominant negative factor is the largest absolute negative contribution.

### 3. Add persistence field

- Add nullable `ScoreAttributionJson` to `PersistedSignalSnapshot`.
- Add EF mapping:

```text
nvarchar(max)
```

- Keep existing `ScoreBreakdownJson`.

### 4. Add migration

- Add migration:

```text
AddScoreAttributionToSignalSnapshots
```

- Migration should only add/drop `ScoreAttributionJson`.
- Do not modify existing score values.

### 5. Capture attribution during signal persistence

- In `EfSignalSnapshotHistoryRepository`, build attribution from `MarketSignal.ScoreBreakdown`.
- Serialize attribution into `ScoreAttributionJson`.
- Keep existing `ScoreBreakdownJson` serialization unchanged.
- Use base score `50` for V1 unless analyzer exposes a better source.

### 6. Add score attribution query

- Add repository method to retrieve a signal snapshot by ID with:
  - signal metadata
  - score
  - `ScoreAttributionJson`
  - `ScoreBreakdownJson`
- If `ScoreAttributionJson` exists, deserialize and return it.
- If missing, reconstruct from `ScoreBreakdownJson` as a legacy fallback.
- Return null if signal snapshot does not exist.

### 7. Add service abstraction

- Add `IScoreAttributionService`.
- Add `ScoreAttributionService`.
- Keep endpoint logic thin.

### 8. Add API endpoint

- Add:

```text
GET /api/signals/{signalSnapshotId}/score-attribution
```

Behavior:

- Return `200 OK` with attribution result when found.
- Return `404 NotFound` when signal snapshot does not exist.
- Do not change existing signal endpoints.

### 9. Add tests

Builder tests:

- Separates positive/negative contributions.
- Computes uncapped score.
- Detects capped score.
- Finds dominant positive factor.
- Finds dominant negative factor.
- Handles empty breakdown.

Persistence/service tests where existing structure supports it:

- New generated signal writes `ScoreAttributionJson`.
- Legacy fallback works from `ScoreBreakdownJson`.
- Missing snapshot returns null/not found behavior.

Regression tests:

- Existing scoring tests still pass.
- Existing signal generation tests still pass.
- Existing alert engine tests still pass.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Manual:

- Apply migration.
- Run signal generation.
- Pick a persisted `SignalSnapshotId`.
- Run:

```text
GET /api/signals/{signalSnapshotId}/score-attribution
```

- Confirm:
  - base score is present
  - final score matches persisted signal score
  - capped score is identified for score 100 saturation
  - contribution JSON is readable

## Risks

- Base score is assumed to be `50` in V1.
- Attribution built from existing labels may lack stable machine-readable factor IDs.
- Uncapped score may be approximate if analyzer clamps before all factors are applied.
- Historical snapshots may only have `ScoreBreakdownJson`; fallback should be clearly handled.
- Score saturation investigation may reveal scoring issues, but V1 intentionally does not recalibrate.

## Open Questions

- Should attribution include `isApproximate` for legacy fallback rows?
- Should factor names remain labels or be upgraded to stable IDs now?
- Should `ReasonJson` in alert events copy the dominant factor in a later feature?
- Should future analytics aggregate dominant factors across score-100 signals?
- Should attribution be queryable by run, symbol, setup, or only by snapshot ID in V1?

## Rollback Plan

Backend:

- Remove score attribution models.
- Remove builder/service/repository methods.
- Remove score attribution endpoint.
- Remove `ScoreAttributionJson` property and mapping.
- Remove migration or apply migration down.
- Remove attribution tests.

Frontend:

- No rollback expected in V1.

Database:

- Drop `SignalSnapshots.ScoreAttributionJson` if the migration was applied.

### 10. Add capped-score diagnostics

- Add simple diagnostics for score saturation:
  - count of capped signals
  - average uncapped score
  - highest uncapped score
- Keep diagnostics internal/backend-only in V1.
