# Manual System Cycle Design

## Current Architecture Context

MarketAgent already has separate manual API endpoints for the core backend workflow:

- Ingestion persists `MarketSnapshots`.
- Signal generation persists `SignalSnapshots`.
- Outcome evaluation updates or creates `SignalOutcomes`.
- Alert evaluation persists `AlertEvents`.

The new system cycle should orchestrate existing application services directly rather than making HTTP calls back into the same API.

## Endpoint

Add:

```text
POST /api/system/run-cycle
```

Optional V1 query parameters:

- `outcomeLimit`
- `alertLimit`

If omitted, use the same safe defaults already used by the existing outcome and alert evaluators.

Avoid adding many knobs in V1. The endpoint should be a simple manual refresh command.

## Service Design

Add an application service:

```csharp
public interface IManualSystemCycleService
{
    Task<ManualSystemCycleResult> RunAsync(
        ManualSystemCycleRequest request,
        CancellationToken cancellationToken = default);
}
```

Implementation:

```csharp
public sealed class ManualSystemCycleService : IManualSystemCycleService
{
    public Task<ManualSystemCycleResult> RunAsync(
        ManualSystemCycleRequest request,
        CancellationToken cancellationToken = default);
}
```

The service should depend on existing application abstractions used by the current endpoints, for example:

- ingestion service
- signal generation service
- signal outcome service
- alert evaluation service
- logger
- time provider if the project already has one

Do not duplicate endpoint logic if the logic already lives in services. If any endpoint currently contains orchestration directly, move only the smallest safe shared call into a service or call the same underlying service method.

## Models

Add request model:

```csharp
public sealed record ManualSystemCycleRequest(
    int? OutcomeLimit,
    int? AlertLimit);
```

Add result model:

```csharp
public sealed record ManualSystemCycleResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long DurationMs,
    bool OverallSuccess,
    int ExecutedStepCount,
    int SuccessfulStepCount,
    string? FailedStepName,
    IReadOnlyCollection<ManualSystemCycleStepResult> Steps);
```

Summary fields should stay lightweight:

- `ExecutedStepCount`: number of steps actually attempted.
- `SuccessfulStepCount`: number of attempted steps that succeeded.
- `FailedStepName`: failed step name, or `null` when the full cycle succeeds.

Add step result:

```csharp
public sealed record ManualSystemCycleStepResult(
    string Name,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long DurationMs,
    bool Success,
    string? ErrorMessage,
    object? Result);
```

Recommended step names:

```text
ingestion
signals
outcomes
alerts
```

If skipped steps are represented explicitly, add:

```csharp
bool Skipped
```

or use `Success = false` with `ErrorMessage = "Skipped because a previous step failed."`. Prefer an explicit `Skipped` flag if this does not add unnecessary complexity.

## Step Execution

Run steps in this order:

1. Ingestion
2. Signal generation
3. Outcome evaluation
4. Alert evaluation

Each step should:

- record `StartedAtUtc`
- execute the existing service method
- record `FinishedAtUtc`
- calculate `DurationMs`
- capture the result object or a compact summary
- catch exceptions and convert them to a failed step result

## Failure Policy

V1 should stop at the first failed step.

Implementation behavior:

- If ingestion fails, do not run signals, outcomes, or alerts.
- If signals fail, do not run outcomes or alerts.
- If outcomes fail, do not run alerts.
- If alerts fail, return the previous successful steps plus failed alerts step.

`OverallSuccess` should be `true` only when every executed required step succeeds.

When the cycle stops early, `ExecutedStepCount` should include the failed step and `FailedStepName` should match that step name.

## Result Payload Size

Step `Result` should be useful but not enormous.

Recommended approach:

- Include existing service result objects if they are already compact.
- If a result contains large collections, map it to a small summary before embedding it.
- Avoid embedding full lists of snapshots, signals, outcomes, or alerts.

## API Mapping

Suggested minimal API shape:

```csharp
app.MapPost(
    "/api/system/run-cycle",
    async (
        IManualSystemCycleService cycleService,
        int? outcomeLimit,
        int? alertLimit,
        CancellationToken cancellationToken) =>
    {
        var result = await cycleService.RunAsync(
            new ManualSystemCycleRequest(outcomeLimit, alertLimit),
            cancellationToken);

        return Results.Ok(result);
    });
```

V1 can return `200 OK` even when `OverallSuccess` is false, because the cycle request itself was handled and the response contains structured failure information. If the project prefers HTTP failure codes for failed operations, use `500` only for unexpected outer failures before a structured result can be created.

## Concurrency

V1 can avoid distributed locking.

Recommended minimal guard:

- Use an in-process `SemaphoreSlim` in the cycle service to prevent overlapping cycles in the same API process.
- If a cycle is already running, return a structured failure or `409 Conflict`.

Open question:

- Whether this is needed in V1 depends on how likely manual double-clicks or parallel API calls are.

## Logging

Log:

- cycle start
- each step start
- each step success with duration
- each step failure with exception
- cycle finish with `overallSuccess` and duration

Use UTC timestamps.

## Testing

Unit tests should cover:

- Runs all steps in order when all succeed.
- Stops after ingestion failure.
- Stops after signal generation failure.
- Stops after outcome evaluation failure.
- Returns alert failure while preserving earlier successful step results.
- Computes duration and timestamps.
- Preserves step result payloads or summaries.

Endpoint-level tests can be added if the existing test structure makes this easy.

## Risks and Open Questions

- Should the endpoint return `200 OK` with `overallSuccess=false` or an HTTP error on step failure?
- Should skipped steps be omitted or included as skipped?
- Should outcome and alert limits be exposed in V1?
- Should cycle runs be persisted for audit history in a later version?
- Should overlapping cycle requests be rejected in V1?

## Rollback Plan

- Remove API mapping for `POST /api/system/run-cycle`.
- Remove manual cycle service, models, and DI registration.
- Remove tests for the cycle service.
- No database rollback should be required.
