# Signal Outcome Dashboard

## Problem

Signal Outcome Tracking now exposes partial performance metrics from the backend, but the frontend dashboard does not show them. Users can run ingestion, signals, and outcome evaluation, yet they cannot quickly see whether emitted signals have early follow-through.

The frontend is already dense and has several dashboard sections, so the first UI increment should be small and additive.

## Goal

Add a compact frontend section that reads:

```text
GET /api/signals/outcomes/summary
```

and displays partial Signal Outcome metrics:

- `totalCount`
- `pendingCount`
- `countWith15m`
- `averageReturn15m`
- `countWith1h`
- `averageReturn1h`
- `best15mSymbol`
- `best15mReturnPercent`
- `worst15mSymbol`
- `worst15mReturnPercent`

## User Value

- Shows whether pending signal outcomes already have 15m/1h follow-through.
- Makes the outcome evaluator visible without waiting for the full 1 day horizon.
- Gives a lightweight validation snapshot directly in the dashboard.
- Helps users notice whether ingestion cadence is producing enough outcome data.

## Scope

- Add backend summary consumption to the existing React frontend.
- Add a small outcome summary card/section.
- Load summary on dashboard refresh and after signal-related actions where safe.
- Show unavailable/empty states without breaking the rest of the dashboard.
- Reuse existing card and performance panel styling patterns.

## Out of Scope

- No frontend rewrite.
- No dashboard layout refactor.
- No table-level outcome annotations.
- No charts.
- No signal detail outcome panel.
- No ability to trigger outcome evaluation from the frontend in this first increment unless explicitly approved later.
- No changes to signal generation, ingestion behavior, or backend contracts.

## Success Criteria

- Dashboard renders normally if `/api/signals/outcomes/summary` succeeds.
- Dashboard renders normally if the summary endpoint fails.
- Summary displays partial metrics even when final `winRate` and `averageOutcomePercent` are null.
- Existing scanner, briefing, ingestion, filters, alerts, signal table, and performance preview remain unchanged.
- The change is additive and easy to remove.

## Risks

- The dashboard is already visually dense; adding another section could reduce scanability.
- The summary endpoint may fail when local SQL Server is unavailable.
- Users may confuse partial returns with final outcomes unless labels are clear.
- `best15mSymbol` and `worst15mSymbol` can be the same when only one symbol has 15m data.
- Metrics may be null until ingestion has produced future snapshots.

## Rollback Plan

- Remove the new summary type from `types.ts`.
- Remove the API function from `api.ts`.
- Remove the new component and import from `App.tsx`.
- Remove the new state/load call from `App.tsx`.
- Remove any new CSS classes.
- Backend remains unchanged because this is a frontend-only additive display.
