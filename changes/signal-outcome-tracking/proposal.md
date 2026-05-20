# Signal Outcome Tracking

## Problem

MarketAgent can ingest real market data, persist market and signal snapshots, generate signal rankings, and display signals in the frontend dashboard. However, the system does not yet measure what happened after a signal was emitted.

Without post-signal outcome tracking, there is no quantitative feedback loop to validate whether generated signals were useful, which setups worked, which symbols followed through, or where the scoring model needs improvement.

## Business Goal

Add a quantitative validation layer for persisted signals that evaluates forward price action after each generated signal.

The goal is to turn persisted signal history into measurable performance data, enabling MarketAgent to answer:

- Did this signal work after it was generated?
- How far did price move in favor of the signal?
- How much drawdown occurred after entry?
- Which setups, actions, confidence levels, and score ranges perform best?
- Are current signal rules producing repeatable value?

## User Value

- Gives users objective feedback on generated signals instead of relying on visual review.
- Enables future performance dashboards based on actual emitted signals.
- Helps compare setups, symbols, score ranges, and timeframes.
- Creates the foundation for improving signal scoring with real outcome data.
- Supports auditability by preserving both the signal and what happened afterward.

## Scope

- Track outcomes for persisted signal snapshots.
- Evaluate forward prices at fixed checkpoints:
  - 15 minutes after signal
  - 1 hour after signal
  - 4 hours after signal
  - 1 day after signal
- Track realized movement metrics:
  - max runup percent
  - max drawdown percent
  - outcome percent
  - success/failure
  - evaluated timestamp
- Add database support for storing outcome fields.
- Add an evaluator approach that can process unevaluated persisted signals.
- Add API support for reading outcome-enhanced signal history or performance summaries.
- Keep signal generation behavior unchanged.

## Out of Scope

- No changes to signal scoring rules in the first implementation.
- No ML model or adaptive scoring.
- No full backtesting engine.
- No broker integration or trade execution.
- No user portfolio accounting.
- No P&L with position sizing.
- No frontend dashboard rewrite in the first backend-oriented increment.
- No distributed job system unless explicitly chosen later.
- No real-time streaming evaluator.

## Success Criteria

- Persisted signals can be evaluated after sufficient future price data exists.
- Each evaluated signal stores checkpoint prices and outcome metrics.
- Evaluation is idempotent and safe to rerun.
- Existing signal generation and dashboard contracts continue to work.
- The system can distinguish pending, successful, failed, and unevaluable outcomes.
- Outcome data can be queried by symbol, run, date range, setup, action, score, or confidence in future API work.
- The implementation remains additive and does not rewrite existing signal persistence.

## Risks/Open Questions

- The current historical candle source must provide enough intraday coverage for 15 minute, 1 hour, and 4 hour checkpoints.
- Market hours, weekends, holidays, and after-hours behavior need clear rules.
- Some symbols may not have candles exactly at checkpoint timestamps, requiring nearest-after or interval-close logic.
- Long, short, buy, sell, hold, and watchlist-only actions may need different success rules.
- Outcome percent needs a clear price basis: signal price, entry price, or nearest candle close.
- The evaluator should avoid marking a 1 day outcome too early.
- Large signal history may require batching and indexes to keep evaluation efficient.
