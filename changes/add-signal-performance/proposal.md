# Signal Performance Preview

## Problem

MarketAgent now produces deterministic signal scores, setup labels, alert flags, detail views, filters, and watchlists. Users can understand why a signal is ranked highly, but they still lack a lightweight way to inspect how similar reconstructed setups behaved in recent historical candles.

The current architecture has OHLCV historical candles and a reusable signal analyzer, but it does not persist historical signal events. Because of that, this feature must be framed as a diagnostic preview, not as a full production-grade backtesting engine.

## Goal

Add a Historical Signal Outcome Preview that reconstructs historical signal samples from existing OHLCV candles, then calculates simple forward returns after those reconstructed samples.

The first version should be simple, deterministic, cautious, and transparent. It should reuse existing historical candle data and analyzer logic without adding persistence, external data sources, trading recommendations, or overconfident statistical claims.

## User Value

- Helps users inspect whether reconstructed signal categories had useful recent forward outcomes.
- Adds historical context for current setups without requiring an external research tool.
- Makes signal engine development more measurable.
- Highlights insufficient or low sample sizes instead of implying false confidence.
- Provides a foundation for later, richer research workflows if real signal persistence is added.

## Scope

- Reconstruct historical signal samples by running or reusing analyzer logic over historical OHLCV candles.
- Measure simple forward returns after reconstructed signal appearances:
  - 1 day
  - 3 days
  - 5 days
- Aggregate when enough samples exist:
  - sample count
  - average 1D / 3D / 5D forward return
  - win rate when sample count is sufficient
- Show explicit warnings:
  - insufficient data
  - low sample count
  - reconstructed samples may differ from real-time signals
- Evaluate initial signal families:
  - `MomentumContinuation`
  - `OpeningRedReversal`
  - `Pullback`
  - `OverextendedWarning` / risk warning when represented by existing setup or extension-risk fields
- Use existing historical candles only.
- Add a backend Application service for candle-based reconstruction and calculations.
- Add a small, cautious dashboard UI section.
- Keep API changes additive only.

## Out of Scope

- No full production-grade backtesting engine.
- No historical signal repository.
- No database persistence.
- No external data sources.
- No new dependencies.
- No trading recommendations.
- No execution simulation.
- No position sizing backtest.
- No PnL ledger.
- No walk-forward optimization.
- No parameter optimization.
- No AI-generated performance claims.
- No guarantee of future performance.
- No scoring behavior changes.
- No dashboard rewrite.

## Success Criteria

- Missing historical candles do not break signal generation or the dashboard.
- Historical samples are clearly labeled as reconstructed.
- Performance output clearly shows insufficient data when samples are too few.
- Low sample sizes are visibly warned as statistically unreliable.
- Forward returns are calculated deterministically from candle closes.
- Aggregates include sample count, average 1D/3D/5D forward returns, and win rates only when enough samples exist.
- UI copy states that results are educational/diagnostic, not trading advice.
- API changes are additive and do not break current dashboard consumers.
- Current filters, watchlists, signal table, alerts, and detail panel continue to work.
- Unit tests cover return calculation, missing horizon data, sample aggregation, insufficient-data behavior, low-sample warnings, and no-crash missing candle handling.
- `dotnet test` and frontend build pass after implementation.
