# Persisted Alert Engine

## Problem

MarketAgent can ingest real market data, persist signals, evaluate outcomes, and display analytics, but it does not yet persist durable alert events. The existing dashboard alert center is derived in the frontend from the current signal set and is useful for live UI context, but it is not an auditable backend alert record.

Users need a backend layer that can decide when a generated signal is important enough to become an internal alert event, store it, and avoid creating the same alert repeatedly.

## Goal

Add a backend-only internal alert engine that evaluates persisted/generated signals and persists alert events in SQL Server.

Suggested endpoints:

```text
POST /api/alerts/evaluate
GET /api/alerts
```

V1 is manual and internal only:

- No Telegram.
- No email.
- No Discord.
- No background scheduler.
- No frontend changes.
- No delivery pipeline beyond persisted `InternalOnly` status.

## V1 Alert Rule

Create an alert when all required conditions are met:

- Signal action is `Candidate`.
- Score is `>= 85`.
- Confidence is `High` or `Medium`.
- `PriceAtSignal` exists.
- The same `SignalSnapshotId` has not already produced an alert.

Optional analytics-aware condition:

- If setup analytics are available for the signal setup, require setup average 15m return `> 0`.
- If setup analytics are unavailable or the setup has no partial sample yet, do not block the alert in V1 unless implementation explicitly chooses strict mode.

## Alert Event Fields

Persist an `AlertEvent` record with:

- `Id`
- `CreatedAtUtc`
- `SignalSnapshotId`
- `Symbol`
- `Setup`
- `Action`
- `Score`
- `Confidence`
- `PriceAtSignal`
- `AlertType`
- `Title`
- `Message`
- `ReasonJson`
- `DeliveryStatus`

Default delivery status:

```text
InternalOnly
```

## User Value

- Creates a durable audit trail of high-quality alert-worthy signals.
- Enables future notification channels without changing signal generation.
- Helps validate whether alert rules are noisy or useful using existing outcomes.
- Avoids duplicate alert records for the same persisted signal.
- Gives the backend a clear alerting boundary separate from dashboard-only alerts.

## Scope

- Add backend alert models/entities.
- Add EF Core persistence and migration.
- Add repository abstraction and SQL implementation.
- Add alert evaluator service.
- Add manual API endpoints.
- Keep changes additive.

## Out of Scope

- No frontend dashboard panel.
- No external delivery.
- No scheduler/background worker.
- No alert acknowledgement workflow.
- No alert rule editor.
- No signal generation changes.
- No outcome evaluation changes.
- No scanner behavior changes.

## Success Criteria

- `POST /api/alerts/evaluate` evaluates eligible persisted signals and creates alert events.
- Re-running evaluation does not duplicate alerts for the same `SignalSnapshotId`.
- `GET /api/alerts` returns persisted alerts.
- Alert reasons are explainable through `ReasonJson`.
- Existing signal generation, outcome evaluation, analytics endpoints, and dashboard panels continue to work unchanged.

## Risks

- Rule thresholds may be too strict and create few or no alerts.
- Setup analytics may be sparse early, making the setup performance filter hard to apply.
- Alert duplication rules need a database constraint or reliable upsert behavior.
- Future delivery systems may need additional status fields beyond `InternalOnly`.
- Pulling signal/outcome analytics in memory may need optimization as history grows.

## Rollback Plan

Backend rollback:

- Remove alert endpoints.
- Remove alert evaluator service.
- Remove alert repository and DI registration.
- Remove alert models/entities.
- Roll back the alert events migration/table if already applied.

Frontend rollback:

- None expected for V1 because no frontend changes should be made.

Data rollback:

- Drop the `AlertEvents` table only if alert history is not needed.
