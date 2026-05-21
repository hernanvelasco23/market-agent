# Persisted Alert Engine Tasks

## V1 Scope Decisions

- Backend only.
- Manual API trigger only.
- Persist alert events in SQL.
- No Telegram/email/Discord.
- No background scheduler.
- Do not modify signal generation.
- Do not modify outcome evaluation.
- Do not modify existing dashboard panels.
- Keep implementation additive.
- Avoid duplicate alerts for the same `SignalSnapshotId`.

## Backend Tasks

### 1. Add alert models

- Add alert constants:
  - `AlertDeliveryStatuses.InternalOnly`
  - `AlertTypes.HighQualityCandidate`
- Add `AlertEventRecord`.
- Add `AlertEventItem`.
- Add `AlertEventQuery`.
- Add `AlertEvaluationResult`.
- Add alert candidate model if querying signal snapshots directly.

### 2. Add persisted entity

- Add `PersistedAlertEvent` in Infrastructure persistence.
- Include fields:
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

### 3. Add DbContext mapping

- Add `DbSet<PersistedAlertEvent>`.
- Configure table `AlertEvents`.
- Configure decimal precision for score and price.
- Configure required string lengths.
- Add unique index on `SignalSnapshotId`.
- Add index on `CreatedAtUtc`.
- Add optional index on `Symbol`.

### 4. Add EF migration

- Create migration for `AlertEvents`.
- Verify `Down` drops the table.
- Do not modify existing tables unless strictly necessary.

### 5. Add repository abstraction

- Add `IAlertEventRepository`.
- Include:
  - query recent alerts
  - fetch existing alert signal snapshot IDs
  - save alert event

### 6. Add repository implementations

- Add `EfAlertEventRepository`.
- Add `NoOpAlertEventRepository` if needed for no SQL connection fallback.
- Register repository in DI alongside existing SQL/no-SQL repository selection.

### 7. Add alert candidate source

Preferred:

- Add a signal snapshot repository query for alert candidates.
- Return persisted signal fields needed by alert rules.
- Support limit.

Alternative:

- Use existing outcome query if candidate source from signal snapshots would be too invasive.
- Document that alerts require outcome rows in that case.

### 8. Add alert evaluator service

- Add `IAlertEvaluationService`.
- Add `AlertEvaluationService`.
- Inject:
  - alert repository
  - signal candidate source
  - setup analytics source if used
  - logger
- Evaluate candidates in batches with normalized limit.

### 9. Implement V1 alert rules

Create alert only when:

- Action is `Candidate`.
- Score is `>= 85`.
- Confidence is `High` or `Medium`.
- Price at signal exists.
- No alert exists for the same `SignalSnapshotId`.

Setup analytics rule:

- If setup analytics are available for the setup and contain `AverageReturn15m`, require it to be `> 0`.
- If setup analytics are unavailable or missing for the setup, allow the alert in V1 unless a strict-mode decision is made before implementation.

### 10. Build alert content

- `AlertType`: `HighQualityCandidate`.
- `Title`: concise symbol/setup title.
- `Message`: explain why the alert was created.
- `ReasonJson`: include rule inputs and setup analytics details.
- `DeliveryStatus`: `InternalOnly`.
- `CreatedAtUtc`: UTC.

### 11. Add API endpoints

- Add:

```text
POST /api/alerts/evaluate
GET /api/alerts
```

- `POST /api/alerts/evaluate` should return:
  - evaluated timestamp
  - requested limit
  - candidates scanned
  - created count
  - skipped duplicate count
  - skipped rule count
  - failed count

- `GET /api/alerts` should support at least:
  - `limit`

### 12. Add tests

Unit tests:

- Eligible candidate creates alert.
- Low score is skipped.
- Non-candidate action is skipped.
- Low confidence is skipped.
- Missing price is skipped.
- Duplicate signal snapshot ID is skipped.
- Positive setup analytics allows alert.
- Non-positive setup analytics blocks alert when available.
- Reason JSON contains key rule inputs.

Repository/EF tests if existing test structure supports it:

- Alert persistence mapping.
- Unique index/idempotency behavior.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Manual:

- Run migration.
- Run signal generation.
- Run outcome evaluation if setup analytics are needed.
- Run:

```text
POST /api/alerts/evaluate
GET /api/alerts
```

- Re-run `POST /api/alerts/evaluate` and confirm duplicates are skipped.

## Risks

- Setup analytics dependency may make alerts too sparse if partial outcome data is not available yet.
- A unique index on only `SignalSnapshotId` blocks future multiple alert types per signal unless changed later.
- If signal candidate query scans too much history, evaluation may need date filters.
- `ReasonJson` should stay small and stable enough for future debugging.
- Manual-only V1 means alerts are only as fresh as the last API trigger.

## Open Questions

- Should setup analytics be strict when missing or advisory?
- Should `GET /api/alerts` include symbol/type/status filters in V1?
- Should the evaluator scan only latest run signals or all persisted signals by newest first?
- Should idempotency be `SignalSnapshotId` or `SignalSnapshotId + AlertType` for future compatibility?
- Should alert events include a `RunId` field for easier tracing?

## Rollback Plan

Backend:

- Remove `POST /api/alerts/evaluate`.
- Remove `GET /api/alerts`.
- Remove alert service and repository.
- Remove alert persistence entity and DbContext mapping.
- Remove alert migration or apply migration down.
- Remove alert tests.

Frontend:

- No rollback expected in V1.

Database:

- Drop `AlertEvents` if alert history can be discarded.
