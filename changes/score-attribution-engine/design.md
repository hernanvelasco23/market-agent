# Score Attribution Engine Design

## Current Architecture Context

Backend:

- `TechnicalMarketSignalAnalyzer` starts scoring around a base score and applies score factors.
- `MarketSignal` contains:
  - `Score`
  - `ScoreBreakdown`
- `MarketSignalScoreFactor` contains:
  - factor label
  - point contribution
- `PersistedSignalSnapshot` stores:
  - `Score`
  - `ScoreBreakdownJson`
- `EfSignalSnapshotHistoryRepository` serializes `MarketSignal.ScoreBreakdown` when saving generated signals.

Important observation:

- Existing `ScoreBreakdownJson` is useful, but it does not explicitly store `baseScore`, `uncappedScore`, `wasCapped`, dominant factors, or separated positive/negative contribution lists.

## Data Model

Recommended Application model:

```csharp
public sealed record ScoreAttribution(
    decimal BaseScore,
    decimal UncappedScore,
    decimal FinalScore,
    bool WasCapped,
    string? DominantPositiveFactor,
    string? DominantNegativeFactor,
    IReadOnlyCollection<ScoreContribution> PositiveContributions,
    IReadOnlyCollection<ScoreContribution> NegativeContributions);

public sealed record ScoreContribution(
    string Factor,
    decimal Points,
    string Reason);
```

Optional response model:

```csharp
public sealed record SignalScoreAttributionResult(
    Guid SignalSnapshotId,
    Guid RunId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    DateTime CreatedAtUtc,
    ScoreAttribution Attribution);
```

## Persistence Design

V1 preferred persistence:

- Add nullable `ScoreAttributionJson` to `SignalSnapshots`.
- Keep existing `ScoreBreakdownJson`.
- Do not normalize contributions into a separate table in V1.

Why JSON column:

- Minimal schema change.
- Fits existing `ScoreBreakdownJson` pattern.
- Avoids a larger refactor of score factor persistence.
- Keeps rollback simple.

Recommended column:

```text
SignalSnapshots.ScoreAttributionJson nvarchar(max) null
```

Historical handling:

- Existing rows will have null `ScoreAttributionJson`.
- The attribution endpoint can either:
  - return a reconstructed legacy attribution from `ScoreBreakdownJson`, or
  - return a clear `Unavailable`/fallback response.

V1 recommendation:

- Reconstruct fallback attribution from `ScoreBreakdownJson` when `ScoreAttributionJson` is missing.
- Mark fallback as approximate if a model field is added for that.

## Attribution Calculation

Base score:

- Use analyzer base score, currently expected to be `50`.
- Store explicitly rather than recomputing from final score.

Positive contributions:

- Every score factor with `Points > 0`.

Negative contributions:

- Every score factor with `Points < 0`.

Dominant positive factor:

- Positive contribution with highest absolute positive points.
- Tie-break by factor name alphabetically.

Dominant negative factor:

- Negative contribution with lowest points, or highest absolute negative points.
- Tie-break by factor name alphabetically.

Uncapped score:

```text
baseScore + sum(all contribution points)
```

Final score:

- Existing signal score.

Was capped:

```text
uncappedScore > finalScore && finalScore == 100
```

Also allow lower-bound cap detection later:

```text
uncappedScore < finalScore && finalScore == 0
```

## Factor Naming

Current `ScoreBreakdown` labels are human-readable. V1 can use those labels as the factor name, but a future improvement should introduce stable factor IDs.

Recommended V1 mapping:

- `Factor`: existing score breakdown label.
- `Reason`: existing score breakdown label or a short generated reason.
- `Points`: existing score breakdown points.

Future V2:

- Add structured factor IDs such as:
  - `RelativeStrengthVsSpy`
  - `PositiveEmaStack`
  - `OpeningRedReversal`
  - `OverextensionRisk`

## Capture Strategy

Option A: Build attribution at persistence time from `MarketSignal.ScoreBreakdown`.

Pros:

- Minimal analyzer changes.
- Does not change scoring behavior.
- Reuses existing generated signal data.
- Easy to add in `EfSignalSnapshotHistoryRepository`.

Cons:

- Requires knowing base score outside analyzer.
- May be approximate if analyzer clamps before all factors are applied.

Option B: Build attribution inside `TechnicalMarketSignalAnalyzer`.

Pros:

- Most accurate capture point.
- Can record uncapped score before final clamp.
- Can later add stable factor IDs at source.

Cons:

- Touches complex scoring logic.
- Higher risk of accidentally changing behavior.

V1 recommendation:

- Use Option A for minimal additive implementation.
- Add tests that prove final `Score` does not change.
- If exact uncapped score is later needed, move attribution capture into the analyzer in a follow-up.

## API Design

Add endpoint:

```text
GET /api/signals/{signalSnapshotId}/score-attribution
```

Suggested service:

```csharp
public interface IScoreAttributionService
{
    Task<SignalScoreAttributionResult?> GetAsync(
        Guid signalSnapshotId,
        CancellationToken cancellationToken = default);
}
```

Suggested repository method:

```csharp
Task<SignalScoreAttributionResult?> GetScoreAttributionAsync(
    Guid signalSnapshotId,
    CancellationToken cancellationToken = default);
```

API behavior:

- `200 OK` when attribution exists or can be reconstructed.
- `404 NotFound` when signal snapshot does not exist.
- Include fallback/reconstructed attribution if `ScoreAttributionJson` is null but `ScoreBreakdownJson` exists.

## Serialization Shape

Recommended persisted JSON:

```json
{
  "baseScore": 50,
  "uncappedScore": 118,
  "finalScore": 100,
  "wasCapped": true,
  "dominantPositiveFactor": "RelativeStrengthVsSpy",
  "dominantNegativeFactor": "OverextensionRisk",
  "positiveContributions": [],
  "negativeContributions": []
}
```

The persisted JSON should be generated with existing web/default JSON serializer settings for consistency.

## Database Changes

Add migration:

```text
AddScoreAttributionToSignalSnapshots
```

Schema change:

```text
ALTER TABLE SignalSnapshots ADD ScoreAttributionJson nvarchar(max) NULL
```

No changes to existing scoring columns.

## Testing

Unit tests:

- Builds attribution from score factors.
- Separates positive and negative contributions.
- Computes `uncappedScore`.
- Detects `wasCapped`.
- Computes dominant positive factor.
- Computes dominant negative factor.
- Handles no score factors.
- Handles legacy fallback from `ScoreBreakdownJson`.

Regression tests:

- Existing analyzer tests still pass.
- Signal generation score values are unchanged.
- Signal persistence still writes `ScoreBreakdownJson`.

## Risks and Open Questions

- Should V1 include an explicit `isApproximate` field for reconstructed attribution?
- Should factor labels be converted to stable IDs immediately?
- Is base score always `50`, or should it be emitted by the analyzer?
- Some scoring flow may clamp before late factors, so attribution built after the fact may not perfectly represent true uncapped internal state.
- Should alert `ReasonJson` eventually include a score attribution reference or dominant factor?

## Rollback Plan

Backend rollback:

- Remove attribution models/service/repository methods.
- Remove endpoint.
- Remove `ScoreAttributionJson` property and mapping.
- Roll back migration if applied.

Data rollback:

- Drop `ScoreAttributionJson` column if no longer needed.

Frontend rollback:

- None expected in V1.
