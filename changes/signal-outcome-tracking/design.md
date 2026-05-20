# Signal Outcome Tracking Design

## Current Architecture Context

- The backend is organized around API, Application, Domain, and Infrastructure projects.
- Real market data ingestion already creates current market snapshots.
- Signal generation already emits signal snapshots from the scanner flow.
- SQL Server persistence now stores generated signal snapshots.
- The frontend dashboard displays generated signals, but outcome validation should begin as backend data first.

## Technical Design

Add a signal outcome evaluation layer that reads persisted signal snapshots, finds future market prices, calculates forward performance metrics, and stores the result.

Recommended shape:

- Keep signal creation append-only.
- Store outcome data separately or as an additive outcome section tied to each persisted signal.
- Introduce an Application-level evaluator service that does not depend directly on EF Core.
- Implement SQL Server persistence and market price lookup in Infrastructure.
- Run evaluation through an explicit API trigger first, with a background job path available later.

## Database Changes

Recommended first schema:

- Add a new `SignalOutcomes` table keyed by signal snapshot id.

Suggested columns:

- `Id uniqueidentifier primary key`
- `SignalSnapshotId uniqueidentifier not null`
- `EvaluatedAtUtc datetime2 not null`
- `EvaluationStatus nvarchar(32) not null`
- `PriceAtSignal decimal(18,6) null`
- `PriceAfter15Minutes decimal(18,6) null`
- `PriceAfter1Hour decimal(18,6) null`
- `PriceAfter4Hours decimal(18,6) null`
- `PriceAfter1Day decimal(18,6) null`
- `MaxRunupPercent decimal(9,2) null`
- `MaxDrawdownPercent decimal(9,2) null`
- `OutcomePercent decimal(9,2) null`
- `IsSuccessful bit null`
- `FailureReason nvarchar(256) null`

Suggested indexes:

- Unique index on `SignalSnapshotId`
- Index on `EvaluatedAtUtc`
- Index on `EvaluationStatus`
- Index on `IsSuccessful`

Alternative:

- Add nullable outcome columns directly to `SignalSnapshots`.

Recommendation:

- Prefer a separate `SignalOutcomes` table. Outcomes are derived data, may be recalculated, and have their own lifecycle. Keeping them separate preserves the immutability of emitted signals.

## Outcome Calculation Rules

Use the persisted signal price as the baseline when available. If signal price is missing, use the nearest candle close at or after the signal timestamp.

Checkpoint prices:

- `PriceAfter15Minutes`: first available candle close at or after `CreatedAtUtc + 15 minutes`.
- `PriceAfter1Hour`: first available candle close at or after `CreatedAtUtc + 1 hour`.
- `PriceAfter4Hours`: first available candle close at or after `CreatedAtUtc + 4 hours`.
- `PriceAfter1Day`: first available candle close at or after `CreatedAtUtc + 1 day`, adjusted for market availability.

Runup/drawdown:

- Evaluate candles between signal timestamp and the configured outcome horizon.
- `MaxRunupPercent` is the best favorable move from baseline during the window.
- `MaxDrawdownPercent` is the worst adverse move from baseline during the window.
- `OutcomePercent` is the final checkpoint move, likely based on the 1 day price for the initial implementation.

Success/failure:

- Define success per actionable direction.
- For bullish actions, success means positive `OutcomePercent` or hitting a configured threshold.
- For bearish actions, success means negative price movement in the expected direction.
- For neutral/watch actions, use `IsSuccessful = null` until product rules define a clear expected direction.

## API Changes

Initial additive endpoints:

- `POST /api/signals/outcomes/evaluate`
  - Evaluates eligible persisted signals.
  - Optional inputs later: date range, symbol, limit, force reevaluation.

- `GET /api/signals/outcomes`
  - Returns outcome records with filters such as symbol, days, setup, action, status, success.

- `GET /api/signals/outcomes/summary`
  - Returns aggregate metrics such as win rate, average outcome, average runup, average drawdown, and counts by setup/action.

Existing endpoints should remain unchanged:

- `POST /api/signals/run`
- `GET /api/signals/performance-preview`
- existing dashboard reads

## Background Job/Evaluator Approach

Start with an explicit evaluator service and API-triggered execution.

Application abstractions:

- `ISignalOutcomeEvaluator`
- `ISignalOutcomeRepository`
- `ISignalOutcomePriceProvider` or reuse an existing historical candle service if it supports the needed windows.

Evaluator flow:

1. Load persisted signals that do not have outcomes or are still pending.
2. Skip signals whose full evaluation horizon has not elapsed.
3. Load future candles for each symbol from signal timestamp through the 1 day horizon.
4. Calculate checkpoint prices, runup, drawdown, outcome percent, status, and success flag.
5. Upsert the outcome record by `SignalSnapshotId`.
6. Log failures per signal without failing the entire batch when safe.

Future background options:

- Hosted service that runs every few minutes.
- Scheduled job triggered by deployment infrastructure.
- Queue-based evaluation if scanner volume grows.

Initial recommendation:

- Implement the evaluator as a batch service with `limit` support.
- Add an API trigger for local/manual operation.
- Move to `IHostedService` only after the calculation rules and database shape are proven.

## Failure Handling

- Treat missing future price data as `Pending` if the horizon has not elapsed.
- Treat missing data after the horizon as `Unevaluable` with a reason.
- Do not block signal generation if outcome evaluation fails.
- Do not overwrite emitted signal fields.
- Make reevaluation explicit or idempotent.
- Log per-symbol and per-signal evaluation errors.

## Risks/Open Questions

- Which candle interval is authoritative for 15 minute evaluation?
- Should 1 day mean 24 hours, next trading day close, or next available regular-session candle?
- How should premarket and after-hours signals be handled?
- What threshold defines success: any favorable move, risk/reward target, or setup-specific threshold?
- How should `Action` values map to bullish, bearish, or neutral direction?
- Should max runup/drawdown use high/low candles or close-only candles?
- Should outcome values be recalculated when better historical data arrives?
- Should frontend display outcomes immediately or wait for a summary endpoint first?

## V1 Implementation Decisions

To keep the first implementation small and reliable:

- Use `SignalSnapshot.Price` as the baseline price.
- If `SignalSnapshot.Price` is null, mark the outcome as `Unevaluable`.
- Do not implement fallback baseline lookup in V1.
- Treat `1 day` as `CreatedAtUtc + 24 hours`, using the first available candle close at or after that timestamp.
- Use candle `High` and `Low` for max runup/drawdown when available.
- If high/low are unavailable, fall back to close-only movement.
- Evaluate only clearly actionable bullish/candidate signals in the first summary.
- Neutral, watch-only, avoid, and high-risk actions should be stored but not counted as wins/losses in V1 summary.
- Do not add hosted background jobs in V1.
- Evaluation is triggered manually through the API.
