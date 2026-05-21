# Persisted Alert Engine Design

## Current Architecture Context

Backend:

- `SignalSnapshots` are persisted in SQL Server.
- `SignalOutcomes` are persisted in SQL Server.
- Signal outcome summaries and setup analytics are available through `ISignalOutcomeService`.
- EF Core persistence is configured in `MarketAgentDbContext`.
- The API uses minimal endpoints in `Program.cs`.
- Repository abstractions live in Application and EF implementations live in Infrastructure.

Frontend:

- A separate in-app `AlertCenter` already derives current UI alerts from dashboard signals.
- V1 persisted alert engine should not modify frontend behavior.

## Data Model

Add a persisted `AlertEvent` entity.

Recommended domain/application shape:

```csharp
public sealed record AlertEventRecord(
    Guid Id,
    DateTime CreatedAtUtc,
    Guid SignalSnapshotId,
    string Symbol,
    string Setup,
    string Action,
    decimal Score,
    string Confidence,
    decimal PriceAtSignal,
    string AlertType,
    string Title,
    string Message,
    string ReasonJson,
    string DeliveryStatus);
```

Recommended constants:

```csharp
public static class AlertDeliveryStatuses
{
    public const string InternalOnly = "InternalOnly";
}

public static class AlertTypes
{
    public const string HighQualityCandidate = "HighQualityCandidate";
}
```

## Database Changes

Add table:

```text
AlertEvents
```

Columns:

- `Id uniqueidentifier` primary key
- `CreatedAtUtc datetime2`
- `SignalSnapshotId uniqueidentifier`
- `Symbol nvarchar(32)`
- `Setup nvarchar(128)`
- `Action nvarchar(128)`
- `Score decimal(18,4)`
- `Confidence nvarchar(64)`
- `PriceAtSignal decimal(18,4)`
- `AlertType nvarchar(128)`
- `Title nvarchar(256)`
- `Message nvarchar(max)`
- `ReasonJson nvarchar(max)`
- `DeliveryStatus nvarchar(64)`

Indexes:

- Unique index on `SignalSnapshotId` for V1 idempotency.
- Index on `CreatedAtUtc`.
- Optional index on `Symbol`.

If future versions allow multiple alert types per signal, change uniqueness to:

```text
SignalSnapshotId + AlertType
```

For V1, the user requirement is no duplicate alerts for the same signal, so unique `SignalSnapshotId` is simplest.

## Repository Design

Add Application abstraction:

```csharp
public interface IAlertEventRepository
{
    Task<IReadOnlyCollection<AlertEventRecord>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetExistingSignalSnapshotIdsAsync(
        IReadOnlyCollection<Guid> signalSnapshotIds,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        AlertEventRecord alertEvent,
        CancellationToken cancellationToken = default);
}
```

Infrastructure:

- Add `PersistedAlertEvent`.
- Add `DbSet<PersistedAlertEvent>` to `MarketAgentDbContext`.
- Add EF mapping and unique index.
- Add `EfAlertEventRepository`.
- Add `NoOpAlertEventRepository` for no SQL connection fallback if needed.

## Evaluator Service

Add Application service:

```csharp
public interface IAlertEvaluationService
{
    Task<AlertEvaluationResult> EvaluateAsync(
        int? limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AlertEventItem>> GetAlertsAsync(
        AlertEventQuery query,
        CancellationToken cancellationToken = default);
}
```

Suggested result model:

```csharp
public sealed record AlertEvaluationResult(
    DateTime EvaluatedAtUtc,
    int RequestedLimit,
    int CandidatesScanned,
    int CreatedCount,
    int SkippedDuplicateCount,
    int SkippedRuleCount,
    int FailedCount);
```

## Candidate Source

Preferred V1 candidate source:

- Existing persisted signal snapshots joined with outcome data where needed.
- Use `SignalSnapshot.Price` as `PriceAtSignal`.
- Use existing query/repository patterns where possible.

Implementation options:

Option A: Add alert candidate query to signal snapshot repository.

Pros:

- Direct access to persisted signals.
- No dependency on outcome rows existing.
- Alert rule can run immediately after signal persistence.

Cons:

- Requires a new repository method and model.

Option B: Use `SignalOutcomeService.GetOutcomesAsync`.

Pros:

- Already returns signal metadata plus `PriceAtSignal`.
- Easy to combine with setup analytics.

Cons:

- Requires outcome rows to exist; alerting would depend on outcome evaluation having run.

V1 recommendation:

- Prefer Option A if `SignalSnapshots.Price` is available through the persistence layer.
- Option B is acceptable only if the product accepts alerts after outcome rows exist.

## Rule Evaluation

Required checks:

```text
Action == Candidate
Score >= 85
Confidence in High, Medium
PriceAtSignal exists
No existing AlertEvent for SignalSnapshotId
```

Confidence normalization:

- Trim whitespace.
- Case-insensitive compare.
- Only `High` and `Medium` pass.

Setup analytics check:

- Load setup summary once per evaluation run if practical.
- Match setup names case-insensitively after trimming.
- If a matching setup has `AverageReturn15m`, require it to be `> 0`.
- If no setup analytics are available, do not block in V1 unless strict mode is later introduced.

Reason JSON should include every decision input, for example:

```json
{
  "score": 91.5,
  "scoreThreshold": 85,
  "confidence": "High",
  "action": "Candidate",
  "priceAtSignal": 123.45,
  "setup": "MomentumContinuation",
  "setupAverageReturn15m": 1.24,
  "setupAnalyticsApplied": true
}
```

## API Design

Add endpoints:

```text
POST /api/alerts/evaluate
GET /api/alerts
```

Suggested minimal API:

```csharp
app.MapPost(
    "/api/alerts/evaluate",
    async (IAlertEvaluationService alertEvaluationService, int? limit, CancellationToken cancellationToken) =>
    {
        var result = await alertEvaluationService.EvaluateAsync(limit, cancellationToken);
        return Results.Ok(result);
    });

app.MapGet(
    "/api/alerts",
    async (IAlertEvaluationService alertEvaluationService, int? limit, CancellationToken cancellationToken) =>
    {
        var result = await alertEvaluationService.GetAlertsAsync(
            new AlertEventQuery(limit),
            cancellationToken);

        return Results.Ok(result);
    });
```

## Error Handling

- A failure evaluating one signal should not fail the whole evaluation run when avoidable.
- Repository uniqueness should protect idempotency even if two evaluations run concurrently.
- API should return counts for created, skipped duplicate, skipped rule, and failed.

## Testing

Unit tests:

- Creates alert for eligible candidate.
- Skips low score.
- Skips unsupported confidence.
- Skips non-candidate action.
- Skips missing price.
- Skips duplicate `SignalSnapshotId`.
- Applies positive setup analytics when available.
- Blocks non-positive setup analytics when available.

Integration or EF tests if existing structure supports it:

- Alert event mapping.
- Unique index behavior.

## Risks and Open Questions

- Should setup analytics be strict or advisory when missing?
- Should uniqueness be only `SignalSnapshotId` or `SignalSnapshotId + AlertType`?
- Should `PriceAtSignal` come directly from `SignalSnapshot.Price` or existing outcome rows?
- Should alerts evaluate the latest signal run only or all persisted signals within a limit/window?
- Should `GET /api/alerts` support filters by symbol, type, delivery status, and date range in V1?

## Rollback Plan

- Remove alert API endpoints.
- Remove alert service and repository registrations.
- Remove `AlertEvents` DbSet/mapping/entity.
- Remove migration or apply a down migration to drop `AlertEvents`.
- No frontend rollback needed in V1.
