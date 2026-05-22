# Candidate Profit Ranking Design

## Current Architecture Context

The dashboard already receives signal fields from `POST /api/signals/run` and briefing/dashboard loading flows.

Relevant frontend types already include:

```ts
entry?: number | null;
stop?: number | null;
takeProfit1?: number | null;
takeProfit2?: number | null;
takeProfit3?: number | null;
riskReward1?: number | null;
riskReward2?: number | null;
riskReward3?: number | null;
```

The signal detail panel already displays these values, which confirms they are available without changing backend APIs.

## V1 Architecture Decision

Implement V1 as frontend-only.

Reasoning:

- Required fields already exist in the API response.
- Ranking is derived presentation logic over currently loaded dashboard signals.
- Avoids adding a backend endpoint that would duplicate existing signal payloads.
- Keeps implementation additive and easy to roll back.

If later the ranking needs persisted signals, server-side paging, or query windows, add a backend endpoint in V2.

## Ranking Model

Add a small frontend model or helper shape:

```ts
type CandidateProfitOpportunity = {
  signal: DashboardSignal;
  entryPoint: number;
  takeProfit: number;
  stopLoss?: number | null;
  riskReward?: number | null;
  potentialProfitPct: number;
};
```

## Take-Profit Selection

V1 should prefer the most representative target:

1. `takeProfit2` with `riskReward2`
2. `takeProfit1` with `riskReward1`
3. `takeProfit3` with `riskReward3`

Only accept a target if:

```text
takeProfit > entryPoint
```

If no valid target is available, exclude the signal.

## Calculation

```ts
potentialProfitPct = ((takeProfit - entryPoint) / entryPoint) * 100;
```

Rules:

- Require `entryPoint > 0`.
- Require finite numeric values.
- Do not treat missing values as zero.
- Round only for display; sort using the raw calculated value.

## Filtering

Include only:

```ts
signal.action === "Candidate"
```

Use exact match for V1 because this is the existing canonical action string.

## Sorting

Sort descending by:

1. `potentialProfitPct`
2. selected `riskReward`
3. `signal.score`

Null `riskReward` should sort below numeric values.

Limit:

```text
top 5
```

V1 can later expose top 10 if the panel has enough room.

## UI Placement

Safest insertion point:

- Near the existing signal group/cards area.
- Prefer below the current `Top Opportunities`, `Watchlist Pullbacks`, and `Top Risks` cards, or near `All Signals`.

Recommended:

- Add a compact full-width panel between the existing signal groups and `AlertCenter`.
- This keeps it close to candidate discovery without disturbing the main table or detail panel.

## UI Design

Panel:

```text
Top Profit Opportunities
```

Rows should show:

- symbol
- setup
- score
- confidence
- entry
- target
- stop
- potential profit %
- risk/reward

Behavior:

- Use existing dark card styling.
- Use green/positive formatting for `potentialProfitPct`.
- Use compact rows.
- Truncate long setup names.
- Clicking a row should select the signal if this fits existing dashboard behavior.
- Show an empty state when no valid candidates exist.

## Data Flow

Inputs:

- `watchlistSignals` or `filteredSignals`

Recommendation:

- Use `watchlistSignals` so the ranking respects the active watchlist but does not disappear because of table filters.
- If users expect filters to affect every panel, switch to `filteredSignals`.

V1 recommendation:

- Use `watchlistSignals`.

## Testing

Frontend tests if available:

- Excludes non-candidate signals.
- Excludes missing entry.
- Excludes target less than or equal to entry.
- Calculates `potentialProfitPct`.
- Sorts by potential profit, risk/reward, then score.
- Limits to top 5.

Manual testing:

- Run signals.
- Confirm panel appears.
- Confirm rows match candidate signals.
- Confirm percent calculation matches entry/target values.
- Confirm selecting a row updates signal detail if row click is implemented.

## Risks and Open Questions

- Should the ranking use `takeProfit2`, `target`, or the best available take-profit?
- Should filters affect the panel or only active watchlist?
- Should bearish/risk setups ever be included in future versions?
- Should risk/reward be mandatory or only a tie-breaker?
- Should the panel rank by TP1 for conservative traders and TP2 for balanced traders?

## Rollback Plan

- Remove the panel/component.
- Remove ranking helper.
- Remove styles.
- No backend or database rollback required.
