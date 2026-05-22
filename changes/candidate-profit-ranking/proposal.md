# Candidate Profit Ranking

## Problem

MarketAgent shows many candidate signals in the dashboard, but users cannot quickly identify which candidates offer the highest estimated upside from entry to take-profit levels.

Scores, setup types, confidence, and risk/reward are useful, but they do not directly answer:

```text
Which candidates have the largest potential profit from current entry?
```

## Goal

Add a compact ranking that orders `Candidate` signals by estimated upside from `Entry Point` to `Take Profit`.

Core metric:

```text
potentialProfitPct = ((takeProfit - entryPoint) / entryPoint) * 100
```

## Current Data Availability

The existing backend `MarketSignal` model already exposes:

- `Entry`
- `Stop`
- `TakeProfit1`
- `TakeProfit2`
- `TakeProfit3`
- `RiskReward1`
- `RiskReward2`
- `RiskReward3`

The frontend `DashboardSignal` type already includes:

- `entry`
- `stop`
- `takeProfit1`
- `takeProfit2`
- `takeProfit3`
- `riskReward1`
- `riskReward2`
- `riskReward3`

Because the required values are already available in the signal API response, V1 can be frontend-only.

## V1 Scope

- Frontend-only ranking panel.
- No backend endpoint required.
- No signal generation changes.
- No scoring logic changes.
- No outcome evaluation changes.
- No database changes.
- No frontend rewrite.
- Keep the panel compact and additive.

## Ranking Rules

Include only signals where:

- `action === "Candidate"`
- `entry > 0`
- selected take-profit is present
- selected take-profit is greater than entry

V1 take-profit selection:

- Prefer `takeProfit2` as the main target if available.
- Fallback to `takeProfit1`.
- Fallback to `takeProfit3`.
- Exclude the signal if no valid take-profit is greater than entry.

Sort:

1. `potentialProfitPct` descending
2. selected `riskReward` descending
3. `score` descending

Missing values should exclude the signal from ranking, not become zero.

## Suggested Display

Panel title:

```text
Top Profit Opportunities
```

Fields:

- symbol
- setup
- score
- confidence
- entryPoint
- takeProfit
- stopLoss
- potentialProfitPct
- riskReward
- action

Display top 5 in V1.

## Optional Future Ranking

Later versions can add:

```text
expectedOpportunityScore =
potentialProfitPct * confidenceMultiplier * riskRewardMultiplier
```

V1 should avoid this and start with the simpler, explainable `potentialProfitPct` ranking.

## User Value

- Quickly surfaces candidates with the highest estimated upside.
- Separates upside potential from calibrated score.
- Makes existing entry/target/stop calculations easier to act on.
- Helps prioritize candidates when the dashboard has many valid signals.

## Out of Scope

- No backend ranking endpoint in V1.
- No scanner behavior changes.
- No signal generation changes.
- No score calibration changes.
- No alert rule changes.
- No outcome evaluation changes.
- No position sizing optimization.
- No expected-value model.

## Success Criteria

- Dashboard shows a compact `Top Profit Opportunities` panel.
- Panel includes only valid `Candidate` signals.
- Potential profit percent is calculated from entry and selected take-profit.
- Signals with missing or invalid values are excluded.
- Sorting uses potential profit, then risk/reward, then score.
- Existing dashboard sections continue to work unchanged.

## Risks

- A high upside target can still be low probability; V1 ranking is not expected value.
- Using `takeProfit2` may exclude otherwise valid candidates if only TP1 exists unless fallback is implemented.
- Users may mistake potential profit ranking for a buy recommendation.
- Very long setup names or symbols could affect compact layout if not constrained.
- A frontend-only implementation ranks only currently loaded dashboard signals.

## Rollback Plan

Frontend rollback:

- Remove the `Top Profit Opportunities` panel/component.
- Remove any helper function used to calculate potential profit.
- Remove related styles.

Backend rollback:

- None expected for V1.

Database rollback:

- None expected.
