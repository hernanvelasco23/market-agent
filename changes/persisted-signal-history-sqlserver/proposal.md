# Persisted Signal History SQL Server

## Problem

MarketAgent generates useful real-time signal snapshots, alerts, dashboard rankings, and daily briefing inputs, but the platform does not persist generated signals. Once a scanner run finishes, there is no durable memory of what the system saw, scored, or flagged.

This limits historical signal review, alert history, future real backtesting, performance analytics, and daily-use workflows.

## Goal

Persist generated signal snapshots to SQL Server whenever the scanner runs, using a simple append-only schema that captures the current signal output without changing scoring behavior or existing API contracts.

The first implementation should be production-oriented but incremental: add durable signal history, isolate persistence concerns, and avoid broader platform rewrites.

## User Value

- Enables historical review of scanner output by symbol and date.
- Creates a durable foundation for future alert history and signal history UI.
- Makes future performance analytics and real backtesting possible with actual emitted signals instead of reconstructed samples only.
- Supports daily briefing traceability and debugging.
- Increases product stickiness by giving the platform historical memory.

## Scope

- Add SQL Server persistence for scanner-generated signal snapshots.
- Persist snapshots whenever `MarketSignalService.GenerateAsync` runs.
- Use a simple append-only `SignalSnapshots` table.
- Capture a scanner run identifier so records from the same run can be grouped.
- Persist at minimum:
  - `Id`
  - `CreatedAtUtc`
  - `RunId` or `ScannerRunId`
  - `Symbol`
  - `Setup`
  - `Action`
  - `Score`
  - `Confidence`
  - `Price`
  - `RelativeStrengthVsSpy`
  - `RelativeVolume`
  - `ExtensionFromEma20Percent`
  - `MarketRegime`
  - `TriggeredAlertsJson` or simple alert text/json
  - `Ema9`
  - `Ema20`
  - `Ema50`
  - `Rsi`
  - `Source`
- Add SQL Server connection string configuration through `appsettings` and environment variables.
- Prefer EF Core SQL Server provider for the persistence implementation.
- Keep failure handling non-blocking where safe: log persistence failures and still return the scanner response.

## Out of Scope

- No full backtesting engine.
- No ML or adaptive scoring.
- No scoring behavior changes.
- No dashboard rewrite.
- No PostgreSQL.
- No Supabase.
- No SQLite except possibly later for isolated tests if explicitly needed.
- No Kafka, queues, event sourcing, distributed architecture, or microservices.
- No authentication.
- No user-specific persistence.
- No user portfolios.
- No broker integrations.
- No subscriptions.
- No advanced alert delivery such as email, push, Telegram, or Discord.

## Success Criteria

- Running the scanner persists one row per generated signal snapshot.
- Persisted records share a `RunId` for the scanner execution.
- Persistence is append-only and does not overwrite prior signal history.
- SQL Server schema includes the planned `SignalSnapshots` table and indexes.
- Existing `/api/signals/run` response remains unchanged.
- Existing scoring behavior remains unchanged.
- If SQL Server persistence fails, the scanner logs the error and still returns the current signal response when safe.
- Configuration supports local and environment-variable connection strings without hardcoded secrets.
- The solution builds and tests pass after implementation.
