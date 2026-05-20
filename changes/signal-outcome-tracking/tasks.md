# Signal Outcome Tracking Tasks

## V1 Execution Scope

For this first implementation:

- Implement the database entity/table.
- Implement manual API-triggered evaluation only.
- Implement a simple summary endpoint.
- Do not add background jobs.
- Do not modify signal generation.
- Do not modify frontend in this PR.
- Use `SignalSnapshot.Price` as baseline.
- If baseline price is missing, mark as `Unevaluable`.
- Use `CreatedAtUtc + 24 hours` for the 1 day horizon.
- Use existing historical candle infrastructure only if available.
- If reliable future candles are not available yet, still implement the outcome table, repository, evaluator skeleton, and mark records as `Pending` or `Unevaluable` safely.
- Keep the implementation additive.

## Documentation

- Create proposal, design, and task documents under `changes/signal-outcome-tracking`.
- Confirm outcome definitions before implementation.
- Confirm database strategy: separate `SignalOutcomes` table vs nullable columns on `SignalSnapshots`.

## Database Tasks

- Add `PersistedSignalOutcome` entity.
- Add `SignalOutcomes` DbSet to `MarketAgentDbContext`.
- Configure one-to-one relationship from `SignalOutcomes.SignalSnapshotId` to `SignalSnapshots.Id`.
- Configure decimal precision for prices and percent fields.
- Configure max lengths for status and failure reason.
- Add indexes for signal id, evaluated timestamp, status, and success.
- Add EF migration for the outcome table.
- Apply migration locally and verify schema.

## Application Tasks

- Add outcome models for evaluation input/result.
- Add `ISignalOutcomeRepository`.
- Add `ISignalOutcomeEvaluator`.
- Add or reuse a price/candle provider abstraction capable of loading future candles by symbol and timestamp window.
- Define evaluation statuses:
  - `Pending`
  - `Evaluated`
  - `Unevaluable`
  - `Failed`
- Define direction mapping from signal action/setup to bullish, bearish, or neutral.
- Define success calculation rules.

## Infrastructure Tasks

- Implement SQL Server outcome repository.
- Implement query for persisted signals eligible for evaluation.
- Implement upsert by `SignalSnapshotId`.
- Implement future candle lookup using existing historical market data infrastructure where possible.
- Add batching and limit support.
- Add logging around data gaps and per-signal failures.

## Evaluator Tasks

- Load eligible persisted signals.
- Determine whether the 1 day horizon has elapsed.
- Fetch future candles per symbol.
- Select checkpoint prices at 15 minutes, 1 hour, 4 hours, and 1 day.
- Calculate max runup percent.
- Calculate max drawdown percent.
- Calculate outcome percent.
- Determine success/failure.
- Store evaluated timestamp.
- Mark missing-data cases as pending or unevaluable according to horizon rules.
- Ensure evaluator is idempotent.

## API Tasks

- Add manual evaluation endpoint:
  - `POST /api/signals/outcomes/evaluate`
- Add query endpoint for outcomes:
  - `GET /api/signals/outcomes`
- Add summary endpoint if needed in first implementation:
  - `GET /api/signals/outcomes/summary`
- Keep existing scanner and dashboard endpoints unchanged.
- Add request parameters for optional filters only after the core evaluator works.

## Testing Tasks

- Unit test checkpoint price selection.
- Unit test bullish outcome success/failure.
- Unit test bearish outcome success/failure.
- Unit test neutral/watch actions produce nullable success or unevaluable status.
- Unit test max runup and max drawdown calculations.
- Unit test pending behavior when horizon has not elapsed.
- Unit test unevaluable behavior when required future candles are missing.
- Unit test repository/evaluator idempotency.
- Add integration tests for EF mapping if a SQL Server test environment is available.

## Validation Tasks

- Run `dotnet build MarketAgent.sln --no-restore`.
- Run unit tests.
- Apply EF migration locally.
- Run scanner to create persisted signals.
- Run outcome evaluator after enough candle data is available.
- Verify one outcome row per evaluated signal.
- Verify repeated evaluator runs do not duplicate outcomes.
- Verify existing dashboard still loads.

## Risks/Open Questions

- The app may not currently persist enough historical candle data to evaluate all horizons reliably.
- External market data APIs may not provide intraday history far enough back for all symbols.
- Timezone and market-session handling can materially change results.
- Some signals may be generated outside regular trading hours.
- Success criteria may need to be setup-specific rather than global.
- Close-only calculations are simpler, but high/low calculations may better represent runup and drawdown.
- Outcome evaluation could become slow without batching and proper indexes.
- Recalculation policy must be clear before exposing analytics as authoritative.
