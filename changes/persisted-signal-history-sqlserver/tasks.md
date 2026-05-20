# Persisted Signal History SQL Server Tasks

## Incremental Implementation Tasks

### 1. Add Application persistence abstraction

- Create `ISignalSnapshotHistoryRepository`.
- Define a method for appending signal snapshots for one scanner run.
- Pass enough data to persist:
  - run id
  - created timestamp
  - generated signals
  - optional market regime
  - optional triggered alerts JSON
  - source
- Keep the abstraction independent of EF Core.

### 2. Add EF Core SQL Server packages

- Add `Microsoft.EntityFrameworkCore.SqlServer`.
- Add `Microsoft.EntityFrameworkCore.Design` for migrations.
- Keep packages scoped to Infrastructure/API as appropriate.
- Do not add PostgreSQL, Supabase, or SQLite packages.

### 3. Add persistence entity and DbContext

- Add `PersistedSignalSnapshot`.
- Add `MarketAgentDbContext`.
- Configure:
  - table name `SignalSnapshots`
  - required fields
  - decimal precision
  - max lengths
  - nullable JSON/text columns
  - indexes

### 4. Add SQL Server repository implementation

- Add `EfSignalSnapshotHistoryRepository`.
- Map `MarketSignal` output to persisted rows.
- Use one `RunId` for all signals from the same scanner execution.
- Persist append-only rows.
- Do not update or delete prior snapshots.

### 5. Add optional no-op fallback

- If no SQL Server connection string is configured, register `NoOpSignalSnapshotHistoryRepository`.
- This keeps local development usable before SQL Server is configured.
- Log that signal history persistence is disabled.

### 6. Wire persistence into scanner flow

- Inject `ISignalSnapshotHistoryRepository` into `MarketSignalService`.
- Generate `RunId` per scanner run.
- Persist after signal analysis succeeds.
- Catch and log persistence exceptions.
- Do not swallow cancellation exceptions.
- Return the existing `MarketSignalRunResult` unchanged.
- Do not change scoring behavior.

### 7. Configure SQL Server connection string

- Add placeholder to `src/MarketAgent.Api/appsettings.json`:
  - `ConnectionStrings:MarketAgentSqlServer`
- Add development placeholder or comment-friendly empty value to `appsettings.Development.json`.
- Document environment variable:
  - `ConnectionStrings__MarketAgentSqlServer`
- Do not hardcode real secrets.

### 8. Add EF migration

- Add initial migration:
  - `AddSignalSnapshots`
- Verify migration creates:
  - `SignalSnapshots` table
  - `CreatedAtUtc` index
  - `Symbol` index
  - `RunId` index
  - `Symbol + CreatedAtUtc` index

### 9. Add tests

- Unit test `MarketSignalService` persists generated signals when scanner runs.
- Unit test persistence failure does not prevent scanner response.
- Unit test cancellation exceptions are not swallowed.
- Unit test repository mapping includes core fields:
  - symbol
  - setup/action
  - score
  - confidence
  - RS/RVOL/EXT
  - EMA/RSI
  - run id
- Add integration tests later only if a SQL Server test environment is available.

## SQL Server Configuration Steps

- Create or choose a SQL Server database, for example `MarketAgent`.
- Configure local connection through user secrets or environment variables:

```text
ConnectionStrings__MarketAgentSqlServer=Server=localhost;Database=MarketAgent;Trusted_Connection=True;TrustServerCertificate=True;
```

- For deployed environments, configure the same key through deployment secrets or platform configuration.
- Do not commit real credentials.

## EF Core Migration Steps

- Ensure EF CLI is available.
- From repo root, run:

```text
dotnet ef migrations add AddSignalSnapshots --project src/MarketAgent.Infrastructure --startup-project src/MarketAgent.Api
dotnet ef database update --project src/MarketAgent.Infrastructure --startup-project src/MarketAgent.Api
```

- If `dotnet ef` is unavailable, add a documented local tool manifest in a separate tooling step or install the CLI outside this feature implementation.
- Review generated migration before applying to shared environments.

## Validation/Build Steps

- Run `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`.
- Run `dotnet build MarketAgent.sln --no-restore`.
- Run API locally with a configured SQL Server connection string.
- Run `POST /api/signals/run`.
- Confirm rows are inserted into `SignalSnapshots`.
- Confirm scanner response shape is unchanged.
- Confirm app still runs without SQL Server if no-op fallback is implemented.

## Manual QA Checklist

- Scanner returns current signals as before.
- Signal scores, setup labels, actions, confidence, and UI behavior are unchanged.
- One scanner run creates a single shared `RunId`.
- One row is inserted per generated signal.
- `CreatedAtUtc` values are UTC.
- `Symbol`, `Score`, `Setup`, `Action`, `Confidence`, RS, RVOL, EXT, EMA values, and RSI are persisted where available.
- Persistence failure is logged.
- Persistence failure does not crash scanner execution when safe.
- Existing dashboard, alerts, filters, watchlists, and performance preview still work.
- No real secrets are committed.

## Rollback Considerations

- This feature should be additive.
- Rollback code by removing:
  - `ISignalSnapshotHistoryRepository`
  - EF entity/DbContext/repository
  - DI registrations
  - `MarketSignalService` persistence call
  - EF package references
  - migration files
  - connection string placeholders if desired
- Rollback database by dropping `SignalSnapshots` only after confirming no needed history exists.
- Existing scanner API contracts should not require rollback changes.
- Since records are append-only, partial historical rows can be safely ignored by old app versions if the app no longer reads them.
