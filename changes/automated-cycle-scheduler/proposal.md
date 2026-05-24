# Automated Cycle Scheduler Proposal

## Business Goal

MarketAgent currently requires the user to manually trigger the system refresh flow through `POST /api/system/run-cycle` and then manually deliver emails. This creates operational friction and makes the dashboard and alerts stale unless the user remembers to run the workflow.

The goal is to add a backend scheduler that automatically runs the existing market cycle on a configurable interval so MarketAgent can keep market snapshots, signals, outcomes, alerts, and optional email delivery fresh without manual intervention.

## Scope

V1 is backend only.

Add:

- a hosted scheduler service
- configuration-driven enablement
- configurable interval in minutes
- optional market-hours-only execution
- optional email delivery after a successful cycle
- overlap protection
- structured logging for each run

Do not add:

- frontend controls
- distributed scheduling
- persisted scheduler run history
- new alert rules
- new signal generation behavior
- new outcome evaluation behavior

## Proposed Configuration

```json
{
  "MarketAgentScheduler": {
    "Enabled": false,
    "IntervalMinutes": 5,
    "RunEmailDelivery": false,
    "MarketHoursOnly": true
  }
}
```

Defaults:

- `Enabled`: `false`
- `IntervalMinutes`: `5`
- `RunEmailDelivery`: `false`
- `MarketHoursOnly`: `true`

Keeping the scheduler disabled by default is safest for local development and avoids surprise API/provider usage.

## User Value

- Keeps the app updated without manual API calls.
- Allows alert emails to be sent automatically after fresh signals are produced.
- Reduces stale dashboard and stale alert risk.
- Preserves the manual endpoint for explicit refreshes and troubleshooting.

## Constraints

- Do not modify signal generation.
- Do not modify alert rules.
- Do not modify outcome evaluation.
- Do not modify frontend.
- Do not call internal HTTP endpoints.
- Reuse `IManualSystemCycleService` directly.
- Keep implementation additive and reversible.
- Avoid overlapping cycles in a single process.

## Success Criteria

- When enabled, the scheduler runs the manual system cycle every configured interval.
- When disabled, no automatic cycles run.
- Scheduler never starts a second cycle while one is already running.
- Optional email delivery runs only when configured.
- Logs clearly show scheduler start, skip, success, failure, and duration.
- Existing manual endpoints continue working unchanged.

## Rollback Plan

- Set `MarketAgentScheduler:Enabled` to `false`.
- Remove hosted service registration.
- Remove scheduler options and service files.
- No database rollback should be required.
