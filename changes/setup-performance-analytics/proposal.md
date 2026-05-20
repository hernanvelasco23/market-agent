# Setup Performance Analytics

## Problem

MarketAgent now persists emitted signals and evaluates partial outcomes, but the dashboard only shows aggregate outcome performance. Users still cannot see which setup types are driving early follow-through.

For example, `MomentumContinuation`, `Pullback`, `BullishContinuation`, and `Risk` signals may behave very differently. Without setup-level grouping, the system cannot answer which signal setups are currently working.

## Goal

Add setup-level Signal Outcome analytics that group partial performance by `SignalSnapshot.Setup`.

Backend target:

```text
GET /api/signals/outcomes/setup-summary
```

Frontend target:

- Add a compact `Setup Performance` card/section near the existing `Signal Outcomes` section.

## Metrics

For each setup type:

- setup type
- count
- count with 15m checkpoint
- average 15m return
- count with 1h checkpoint
- average 1h return
- count with 4h checkpoint if useful in backend response
- average 4h return if useful in backend response

Top-level summary:

- best setup
- best setup average 15m return
- worst setup
- worst setup average 15m return
- total setup count

Examples:

- `MomentumContinuation avg 15m return`
- `Pullback avg 15m return`
- `BullishContinuation avg 15m return`
- `Risk avg 15m return`

## User Value

- Makes outcome data actionable by setup type.
- Helps identify which signal logic is performing best in current market conditions.
- Supports future scanner tuning without changing signal generation behavior.
- Gives users a quick way to compare setup families.

## Scope

- Add an additive backend setup summary endpoint.
- Reuse existing Signal Outcome data and return calculations.
- Add a small frontend card/section.
- Keep dashboard layout stable.
- Keep existing signal generation, outcome evaluation, and scanner behavior unchanged.

## Out of Scope

- No scoring changes.
- No setup classification rewrite.
- No frontend table refactor.
- No charts in the first increment.
- No per-symbol drilldown.
- No editing or filtering setup definitions.
- No ML/adaptive ranking.
- No background jobs.

## Success Criteria

- Backend returns setup-level partial performance grouped by setup type.
- Frontend displays setup-level 15m/1h performance without breaking existing dashboard sections.
- Missing or empty outcome data renders gracefully.
- Existing Signal Outcomes cards remain unchanged.
- Existing scanner, ingestion, briefing, filters, watchlists, and signal table behavior remain unchanged.

## Risks

- Setup names may be inconsistent if signal generation emits multiple labels for similar concepts.
- Small sample sizes can make best/worst setup misleading.
- Pending outcomes may dominate early data.
- Users may interpret partial 15m returns as final performance.
- API query could become expensive if outcome history grows without indexes.

## Rollback Plan

- Remove the new backend endpoint and setup summary models.
- Remove the frontend API helper, type, state, and component.
- Remove the `Setup Performance` render line from `App.tsx`.
- Leave existing `SignalOutcomes`, `SignalSnapshots`, and Signal Outcome Summary untouched.

## V1 Decisions

- Setup grouping is case-insensitive.
- Empty/null setup names should be grouped as "Unknown".
- Partial returns should use the same calculation logic already used in Signal Outcome Summary.
- Setup ranking should require a minimum sample size of at least 3 records before being eligible for best/worst setup calculations.
- Pending outcomes may still contribute to partial 15m/1h averages if checkpoint prices exist.
- Final evaluated outcomes are not required for V1 setup analytics.

## UX Note

Setup analytics are intended as observational diagnostics, not trading recommendations.
Partial 15m/1h returns should be clearly labeled as incomplete/intraday metrics.