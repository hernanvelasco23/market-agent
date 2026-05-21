# Manual System Cycle

## Problem

MarketAgent already exposes manual backend endpoints for each operational step:

- `POST /api/ingestion/run`
- `POST /api/signals/run`
- `POST /api/signals/outcomes/evaluate`
- `POST /api/alerts/evaluate`

To refresh the system manually, the user currently needs to call these endpoints in the right order. This is easy to forget, hard to repeat consistently, and makes manual validation slower.

## Goal

Add one backend-only endpoint that runs the full manual system cycle in sequence.

Suggested endpoint:

```text
POST /api/system/run-cycle
```

V1 sequence:

1. Ingestion
2. Signal generation
3. Outcome evaluation
4. Alert evaluation

The endpoint should return a structured result with cycle timing, per-step status, and any errors.

## User Value

- One manual action refreshes the core backend workflow.
- Reduces missed steps during local testing and operational use.
- Gives a single structured response showing what succeeded and what failed.
- Keeps existing individual endpoints available for targeted debugging.

## V1 Scope

- Backend only.
- Manual trigger only.
- No scheduler/background worker.
- No frontend changes.
- No changes to existing endpoints.
- No changes to signal generation logic.
- No changes to outcome evaluation logic.
- No changes to alert rules.
- Additive orchestration layer only.

## Proposed Response Shape

```json
{
  "startedAtUtc": "2026-05-21T15:00:00Z",
  "finishedAtUtc": "2026-05-21T15:00:12Z",
  "durationMs": 12000,
  "overallSuccess": true,
  "executedStepCount": 4,
  "successfulStepCount": 4,
  "failedStepName": null,
  "steps": [
    {
      "name": "ingestion",
      "startedAtUtc": "2026-05-21T15:00:00Z",
      "finishedAtUtc": "2026-05-21T15:00:04Z",
      "durationMs": 4000,
      "success": true,
      "errorMessage": null,
      "result": {}
    }
  ]
}
```

## Failure Policy

V1 should stop the cycle at the first failed step.

Reasoning:

- Signal generation depends on fresh or existing ingested market data.
- Outcome evaluation depends on persisted signals and market snapshots.
- Alert evaluation depends on persisted signals and optionally analytics.
- Continuing after a failed prerequisite can create confusing partial results.

The response should still include completed steps plus the failed step with its error message. Steps not started can be omitted or marked as skipped, depending on the implementation style chosen.

## Out of Scope

- No automatic scheduling.
- No retry policy.
- No distributed lock.
- No frontend button.
- No progress streaming.
- No alert delivery integrations.
- No recalibration of scoring or alert thresholds.
- No changes to scanner behavior.

## Success Criteria

- `POST /api/system/run-cycle` runs ingestion, signals, outcomes, and alerts in order.
- Existing manual endpoints continue to work unchanged.
- The response includes:
  - `startedAtUtc`
  - `finishedAtUtc`
  - `durationMs`
  - `executedStepCount`
  - `successfulStepCount`
  - `failedStepName`
  - per-step results
  - success/failure per step
  - error message per failed step
  - `overallSuccess`
- If a step fails, the cycle stops and reports the failure clearly.
- Existing tests and build continue to pass.

## Risks

- A long-running ingestion or signal step could make the endpoint slow.
- If the API process receives duplicate manual cycle requests, two cycles could overlap.
- Some existing services may return large result payloads that should not be embedded fully.
- Step result types may be inconsistent across existing endpoints/services.
- A first-failure stop policy is safe but may reduce diagnostic coverage in one run.

## Rollback Plan

Backend rollback:

- Remove `POST /api/system/run-cycle`.
- Remove the orchestration service and models.
- Remove DI registrations for the cycle service.
- Remove tests for the cycle service.

Database rollback:

- None expected for V1 because no schema changes should be required.

Frontend rollback:

- None expected because V1 has no frontend changes.
