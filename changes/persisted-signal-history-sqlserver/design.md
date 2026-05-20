# Persisted Signal History SQL Server Design

## Current Architecture Findings

- The backend follows clean architecture:
  - `src/MarketAgent.Api/Program.cs` hosts the app, configures DI, and exposes thin endpoints.
  - `src/MarketAgent.Application` owns use cases and abstractions.
  - `src/MarketAgent.Domain` owns entities such as `MarketSignal`, `MarketSnapshot`, and `MarketCandle`.
  - `src/MarketAgent.Infrastructure` implements market data providers, AI integration, indicators, watchlists, and persistence adapters.
- Current scanner flow:
  - `POST /api/signals/run` calls `IMarketSignalService.GenerateAsync`.
  - `MarketSignalService` loads current snapshots from `IMarketSnapshotRepository`.
  - It enriches with historical candles through `IHistoricalMarketDataService`.
  - It calls `IMarketSignalAnalyzer.Analyze`.
  - It returns `MarketSignalRunResult` with generated signals.
- Current briefing flow also analyzes signals but should not be the first persistence trigger unless explicitly required later.
- The dashboard uses the current scanner/briefing responses and derives alerts in the frontend from signal fields.
- The alert center is currently frontend-only and does not have persisted alert records.
- Signal Performance Preview currently reconstructs samples from candles because there is no historical signal store.

## Existing Persistence Findings

- Existing persistence implementations are in-memory:
  - `InMemoryMarketSnapshotRepository`
  - `InMemoryHistoricalCandleRepository`
- Existing persistence abstractions:
  - `IMarketSnapshotRepository`
  - `IHistoricalCandleRepository`
- There is no EF Core setup today.
- There is no `DbContext`.
- There are no migrations.
- There are no SQL Server package references in the current `.csproj` files.
- `README.md` and `specs/architecture.md` identify SQL Server as the intended database target, but the current implementation has not wired SQL Server yet.

## SQL Server Approach

Use SQL Server as the durable store for signal history.

Initial approach:

- Add a SQL Server-backed persistence adapter in Infrastructure.
- Keep the first persisted table focused on generated signal snapshots.
- Use append-only inserts.
- Avoid changing existing in-memory snapshot and candle repositories in this feature.
- Do not introduce background jobs or distributed infrastructure.

Suggested table:

- `SignalSnapshots`

This table should represent the scanner output at a point in time, not mutable current state.

## EF Core Approach

EF Core is not currently present, so this feature should add it deliberately and narrowly.

Expected packages:

- Add `Microsoft.EntityFrameworkCore.SqlServer` to `src/MarketAgent.Infrastructure`.
- Add `Microsoft.EntityFrameworkCore.Design` where migrations are created, likely `src/MarketAgent.Api` or `src/MarketAgent.Infrastructure` depending on the chosen startup/migrations setup.

Suggested Infrastructure additions:

- `MarketAgentDbContext`
- `PersistedSignalSnapshot` entity
- `EfSignalSnapshotRepository`
- EF configuration for table name, precision, required fields, JSON/text columns, and indexes

Suggested Application abstraction:

- `ISignalSnapshotHistoryRepository`

Suggested Application service integration:

- Inject the repository into `MarketSignalService`.
- Generate a `RunId` per scanner run.
- Persist generated `MarketSignal` records after analysis.
- Catch and log persistence failures without crashing the scanner response when safe.

## Connection String/Configuration Approach

Add a SQL Server connection string to API configuration without hardcoding secrets.

Suggested `appsettings.json` shape:

```json
{
  "ConnectionStrings": {
    "MarketAgentSqlServer": ""
  }
}
```

Environment variable shape:

```text
ConnectionStrings__MarketAgentSqlServer=Server=...;Database=MarketAgent;...
```

Development guidance:

- `appsettings.Development.json` may include an empty placeholder or local non-secret example.
- Local secrets should use .NET user secrets or environment variables.
- Production should use environment variables, Azure App Configuration, Key Vault, or deployment secrets.

DI guidance:

- In `Program.cs`, read `builder.Configuration.GetConnectionString("MarketAgentSqlServer")`.
- Register `MarketAgentDbContext` with `UseSqlServer`.
- Register `ISignalSnapshotHistoryRepository` with the EF implementation only when a connection string is configured, or provide a no-op fallback if the app should run without SQL Server during local development.

## Signal Snapshot Entity Design

Prefer a simple persisted entity:

```text
PersistedSignalSnapshot
  Id uniqueidentifier primary key
  CreatedAtUtc datetime2 not null
  RunId uniqueidentifier not null
  Symbol nvarchar(32) not null
  Setup nvarchar(128) not null
  Action nvarchar(128) not null
  Score decimal(9,2) not null
  Confidence nvarchar(64) not null
  Price decimal(18,6) null
  RelativeStrengthVsSpy decimal(9,2) null
  RelativeVolume decimal(9,2) null
  ExtensionFromEma20Percent decimal(9,2) null
  MarketRegime nvarchar(64) null
  TriggeredAlertsJson nvarchar(max) null
  Ema9 decimal(18,6) null
  Ema20 decimal(18,6) null
  Ema50 decimal(18,6) null
  Rsi decimal(9,2) null
  Source nvarchar(64) not null
```

Additional useful fields may be included if still small and additive:

- `Timeframe`
- `Reason`
- `SignalType`
- `Entry`
- `Stop`
- `Target`
- `ScoreBreakdownJson`
- `OpeningRedReversalDetected`
- `OpenGapPercent`
- `RecoveryFromLowPercent`

Price mapping:

- Current `MarketSignal` does not expose a `CurrentPrice` property directly.
- It does expose `Entry`, which is currently calculated from latest price and used in the detail UI as price-like context.
- The implementation should either:
  - persist `Price` from `signal.Entry`, documenting it as scanner price/entry basis, or
  - add an internal persistence mapper that receives latest snapshot prices without changing the public API.

Market regime mapping:

- `TechnicalMarketSignalAnalyzer` calculates market regime internally but does not expose it on `MarketSignal`.
- Smallest safe options:
  - persist `MarketRegime` as null initially, or
  - introduce an Application-level market regime provider/result if needed later.
- Do not change scoring just to persist market regime.

## Alert Persistence Approach

Current alerts are frontend-derived from returned signal fields.

Initial persistence options:

- Prefer storing `TriggeredAlertsJson` as null or a small deterministic JSON generated by backend rules only if equivalent backend alert rules already exist.
- Do not call frontend alert derivation from backend.
- Do not persist external notification state.

Recommended initial approach:

- Add `TriggeredAlertsJson` nullable.
- Leave it null in the first implementation unless a backend alert derivation service is introduced as a separate, deterministic Application service.
- Future alert history can populate this field or move to a separate `AlertSnapshots` table.

## DB/Schema Approach

Use one append-only table first:

- `SignalSnapshots`

Schema characteristics:

- All rows are immutable historical records.
- Each scanner run has a shared `RunId`.
- `CreatedAtUtc` is the persisted timestamp for the snapshot row.
- Signal timestamps should use UTC.
- JSON fields should be text columns (`nvarchar(max)`) for now.
- Decimal precision should be explicit for score, indicators, and price values.

Avoid in the first version:

- separate normalized score factor tables
- separate alert tables
- user-specific watchlist tables
- event sourcing tables
- partitioning

## Index Strategy

Suggested indexes:

- `IX_SignalSnapshots_CreatedAtUtc`
- `IX_SignalSnapshots_Symbol`
- `IX_SignalSnapshots_RunId`
- `IX_SignalSnapshots_Symbol_CreatedAtUtc`

Rationale:

- `CreatedAtUtc` supports recent history queries.
- `Symbol` supports symbol detail history.
- `RunId` supports grouping scanner outputs.
- `Symbol + CreatedAtUtc` supports future chart/detail/history views.

## Migration Strategy

Use EF Core migrations.

Initial migration:

- Add `SignalSnapshots` table.
- Add indexes listed above.

Expected commands:

```text
dotnet ef migrations add AddSignalSnapshots --project src/MarketAgent.Infrastructure --startup-project src/MarketAgent.Api
dotnet ef database update --project src/MarketAgent.Infrastructure --startup-project src/MarketAgent.Api
```

If the EF CLI tool is not installed locally, document the installation or use `dotnet tool restore` only if a tool manifest is introduced.

Do not run destructive database commands as part of normal app startup.

## Failure Handling

Persistence should not make scanner execution fragile.

Recommended behavior:

- If signal analysis succeeds but SQL Server insert fails:
  - log the exception with `ILogger`
  - still return the current `MarketSignalRunResult`
  - include no API contract changes in the response
- Do not swallow cancellation exceptions.
- Do not hide analyzer/scanner failures that are unrelated to persistence.
- Consider a no-op repository for missing connection string during development if the team wants local app startup without SQL Server.

## API Impact

Initial API impact should be none for existing routes:

- Do not change `/api/signals/run` response shape.
- Do not change `/api/briefing/run`.
- Do not change dashboard contracts.

Potential additive future endpoints:

- `GET /api/signals/history?symbol=NVDA&days=30`
- `GET /api/signals/runs`
- `GET /api/signals/runs/{runId}`
- `GET /api/alerts/history`

These should be separate follow-up features.

## Future Extensibility

Persisted signal snapshots enable:

- real historical signal history UI
- alert history
- symbol detail timelines
- daily briefing traceability
- real performance analytics using emitted signals
- comparison between reconstructed Signal Performance Preview and actual historical scanner output
- future SQL-backed watchlists or user-specific workflows

The first schema should remain simple but leave room for:

- separate `ScannerRuns` table later
- separate `AlertSnapshots` table later
- normalized score factors later
- retention policies or archiving later

## Files Expected To Change

Expected backend files:

- `src/MarketAgent.Application/Abstractions/ISignalSnapshotHistoryRepository.cs`
- `src/MarketAgent.Application/Signals/MarketSignalService.cs`
- `src/MarketAgent.Infrastructure/MarketAgent.Infrastructure.csproj`
- `src/MarketAgent.Infrastructure/Persistence/MarketAgentDbContext.cs`
- `src/MarketAgent.Infrastructure/Persistence/PersistedSignalSnapshot.cs`
- `src/MarketAgent.Infrastructure/Persistence/EfSignalSnapshotHistoryRepository.cs`
- `src/MarketAgent.Infrastructure/Persistence/NoOpSignalSnapshotHistoryRepository.cs` if using a no-op fallback
- `src/MarketAgent.Api/Program.cs`
- `src/MarketAgent.Api/appsettings.json`
- `src/MarketAgent.Api/appsettings.Development.json`
- EF migration files under `src/MarketAgent.Infrastructure/Migrations/`
- tests for `MarketSignalService` persistence behavior and repository mapping

Potential files:

- `src/MarketAgent.Application/Models/PersistSignalSnapshotRequest.cs` if a request DTO keeps mapping out of the repository interface.
- `tests/MarketAgent.IntegrationTests/*` if SQL Server integration testing is added later.

Files intentionally not expected to change:

- `TechnicalMarketSignalAnalyzer.cs` scoring behavior.
- `MarketSignal.cs` public API contract unless a strictly additive internal persistence need is approved.
- AI briefing generator.
- Frontend dashboard for the initial persistence-only feature.

## Risks

- EF Core is a new dependency and must be added carefully.
- Missing SQL Server connection strings could break local startup if no fallback is provided.
- Persistence failures could make scanner runs brittle unless isolated and logged.
- Market regime is not currently exposed on `MarketSignal`; persisting it may require a separate design decision or nullable initial field.
- Alerts are currently frontend-derived; persisting alerts now could duplicate frontend logic unless a backend alert derivation service is added later.
- JSON fields are flexible but less queryable; this is acceptable initially but may need normalization later.
- Append-only data grows over time; retention and archiving are future concerns.
- Decimal precision and UTC handling must be explicit to avoid analytics issues later.
