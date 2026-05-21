# Manual System Cycle Tasks

## V1 Scope Decisions

- Backend only.
- Manual trigger only.
- No scheduler/background worker.
- Do not modify existing endpoints.
- Do not modify signal generation logic.
- Do not modify outcome evaluation logic.
- Do not modify alert rules.
- Keep implementation additive.
- Stop at the first failed step.
- No database migration expected.

## Backend Tasks

### 1. Inspect existing endpoint wiring

- Find the current handlers for:
  - `POST /api/ingestion/run`
  - `POST /api/signals/run`
  - `POST /api/signals/outcomes/evaluate`
  - `POST /api/alerts/evaluate`
- Identify the underlying services and result models.
- Confirm whether endpoint logic is already service-backed or partially inline.

### 2. Add cycle models

- Add `ManualSystemCycleRequest`.
- Add `ManualSystemCycleResult`.
- Add `ManualSystemCycleStepResult`.
- Include:
  - `StartedAtUtc`
  - `FinishedAtUtc`
  - `DurationMs`
  - `OverallSuccess`
  - `ExecutedStepCount`
  - `SuccessfulStepCount`
  - `FailedStepName`
  - per-step name
  - per-step success flag
  - per-step error message
  - compact per-step result

### 3. Add cycle service abstraction

- Add `IManualSystemCycleService`.
- Define:

```csharp
Task<ManualSystemCycleResult> RunAsync(
    ManualSystemCycleRequest request,
    CancellationToken cancellationToken = default);
```

### 4. Implement cycle service

- Add `ManualSystemCycleService`.
- Inject existing application services for:
  - ingestion
  - signal generation
  - outcome evaluation
  - alert evaluation
  - logging
- Execute steps in order:
  1. ingestion
  2. signals
  3. outcomes
  4. alerts
- Capture timing per step.
- Capture compact step result payloads.
- Convert step exceptions into failed step results.
- Stop at the first failed step.
- Set `OverallSuccess` only when all steps succeed.

### 5b. Add minimal cycle summary

- Add compact cycle summary fields:
  - `ExecutedStepCount`
  - `SuccessfulStepCount`
  - `FailedStepName`
- Keep summary lightweight.
- Count only attempted steps as executed.
- Set `FailedStepName` to `null` when the full cycle succeeds.

### 5. Add optional overlap protection

- Consider an in-process `SemaphoreSlim` to prevent overlapping cycles in the same API process.
- If already running, return a structured failure or map the endpoint to `409 Conflict`.
- Keep this minimal and avoid distributed locking in V1.

### 6. Register service in DI

- Register `IManualSystemCycleService`.
- Follow existing DI registration style and folder conventions.

### 7. Add API endpoint

- Add:

```text
POST /api/system/run-cycle
```

- Accept optional query parameters if useful:
  - `outcomeLimit`
  - `alertLimit`
- Return `ManualSystemCycleResult`.
- Keep existing endpoints unchanged.

### 8. Add logging

- Log cycle start and finish.
- Log step start and finish.
- Log failed step exception details.
- Keep logs concise and operationally useful.

### 9. Add tests

Unit tests:

- Successful cycle runs all steps in order.
- Ingestion failure stops the cycle.
- Signal failure stops before outcomes and alerts.
- Outcome failure stops before alerts.
- Alert failure returns overall failure after prior successful steps.
- Step error messages are populated.
- Overall timing fields are populated.

Endpoint tests if existing structure supports them:

- `POST /api/system/run-cycle` returns structured result.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Manual validation:

```text
POST /api/system/run-cycle
```

Verify:

- ingestion runs
- signals run after ingestion
- outcomes evaluate after signals
- alerts evaluate after outcomes
- response contains all step statuses and timings
- existing individual endpoints still work

## Risks

- Existing endpoint logic may need small extraction if it is not already service-backed.
- Step result objects may be too large and need compact mapping.
- Long-running cycles may hit client or proxy timeouts later.
- Overlapping manual cycles could create confusing state without a guard.
- Returning `200 OK` for a cycle with failed steps may surprise API clients unless documented clearly.

## Open Questions

- Should failed step results return HTTP `200 OK` with `overallSuccess=false` or an HTTP failure code?
- Should skipped steps be omitted or included explicitly?
- Should `outcomeLimit` and `alertLimit` be exposed in V1?
- Should cycle run history be persisted in a later version?
- Should overlap protection be required in V1?

## Rollback Plan

Backend:

- Remove `POST /api/system/run-cycle`.
- Remove `ManualSystemCycleService`.
- Remove `IManualSystemCycleService`.
- Remove cycle request/result models.
- Remove DI registration.
- Remove tests.

Database:

- None expected.

Frontend:

- None expected.
