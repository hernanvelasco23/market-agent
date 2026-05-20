# Setup Performance by Confidence and Score Buckets

## Problem

MarketAgent now tracks persisted signal outcomes and exposes partial performance by setup type, but it still cannot answer whether higher confidence and higher score signals actually perform better.

The scanner emits confidence labels and numeric scores, yet those fields are not currently validated against observed partial outcome returns. This makes it difficult to know whether a `High` confidence signal or an `81-100` score bucket is meaningfully better than lower-ranked signals.

## Goal

Add additive Signal Outcome analytics grouped by:

- confidence: `Low`, `Medium`, `High`
- score buckets:
  - `0-20`
  - `21-40`
  - `41-60`
  - `61-80`
  - `81-100`

Backend target:

```text
GET /api/signals/outcomes/score-buckets
```

Frontend target:

- Add a compact `Score & Confidence Performance` panel near `Setup Performance`.

## Metrics

For each confidence group:

- confidence
- count
- count with 15m checkpoint
- average 15m return
- count with 1h checkpoint
- average 1h return
- best 15m symbol
- worst 15m symbol

For each score bucket:

- score bucket
- count
- count with 15m checkpoint
- average 15m return
- count with 1h checkpoint
- average 1h return
- best 15m symbol
- worst 15m symbol

Return calculation:

```text
returnPct = ((checkpointPrice - priceAtSignal) / priceAtSignal) * 100
```

## User Value

- Validates whether scanner ranking quality is improving.
- Shows whether high confidence labels are earning better short-term follow-through.
- Helps tune scoring logic later without changing signal generation in this increment.
- Gives users a compact way to compare signal quality tiers using persisted outcome data.

## Scope

- Add a backend summary endpoint for confidence and score bucket analytics.
- Reuse existing Signal Outcome query/service patterns.
- Reuse existing partial return calculation patterns.
- Add a small frontend panel near existing outcome analytics.
- Keep dashboard behavior, scanner behavior, signal generation, and outcome evaluation unchanged.

## Out of Scope

- No signal scoring changes.
- No confidence classification changes.
- No outcome evaluation changes.
- No database schema changes.
- No frontend dashboard rewrite.
- No charts in V1.
- No predictive modeling.
- No automatic score calibration.

## Success Criteria

- Backend returns confidence-level and score-bucket partial performance.
- Frontend displays the new analytics without breaking Signal Outcomes, Setup Performance, or scanner sections.
- Empty data renders gracefully.
- Endpoint failures are isolated from the main dashboard state.
- Existing signal generation, ingestion, filters, watchlists, and scanner actions remain unchanged.

## Risks

- Small sample sizes can make high/low bucket comparisons misleading.
- Pending outcomes may dominate partial analytics.
- Confidence labels may be sparse or inconsistent.
- Score buckets may be skewed if most signals cluster in one range.
- In-memory grouping may need SQL optimization later if outcome history grows.

## Rollback Plan

Backend rollback:

- Remove score bucket response models.
- Remove service method.
- Remove endpoint from `Program.cs`.
- Remove tests.

Frontend rollback:

- Remove the `Score & Confidence Performance` component.
- Remove frontend response types and API helper.
- Remove App state, loader, and render call.
- Remove optional scoped CSS.

Database rollback:

- No rollback expected because V1 should not add schema.

## V1 Decisions

- Keep calculations in memory.
- Reuse the existing Signal Outcome return calculation approach.
- Ignore rows with missing baseline or checkpoint prices.
- Pending outcomes can contribute to partial averages when checkpoint prices exist.
- Final evaluated outcomes are not required.
- Use fixed score buckets: `0-20`, `21-40`, `41-60`, `61-80`, `81-100`.
- Normalize confidence defensively; unknown or empty confidence labels should map to `Unknown` if encountered.
- Keep the frontend compact and observational, not prescriptive.

## UX Note

Score and confidence analytics are validation diagnostics. The UI should label metrics as partial/intraday and avoid implying that current partial returns are final performance.
