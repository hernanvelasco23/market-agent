# Candidate Profit Ranking Tasks

## V1 Scope Decisions

- Frontend-only.
- Use existing signal fields.
- Do not add backend endpoint in V1.
- Do not change signal generation logic.
- Do not change scoring logic.
- Do not change outcome evaluation.
- Do not change scanner behavior.
- Keep implementation additive and compact.

## Frontend Tasks

### 1. Inspect current signal data mapping

- Confirm `ApiMarketSignal` includes:
  - `entry`
  - `stop`
  - `takeProfit1`
  - `takeProfit2`
  - `takeProfit3`
  - `riskReward1`
  - `riskReward2`
  - `riskReward3`
- Confirm `toDashboardSignal` maps these fields.
- Confirm `DashboardSignal` exposes these fields.

### 2. Add ranking helper

- Add a pure helper function, for example:

```ts
getTopProfitOpportunities(signals: DashboardSignal[], limit = 5)
```

- Return compact opportunity items containing:
  - signal
  - entryPoint
  - takeProfit
  - stopLoss
  - potentialProfitPct
  - riskReward

### 3. Implement filtering rules

- Include only `action === "Candidate"`.
- Require `entry > 0`.
- Require valid selected take-profit.
- Require selected take-profit greater than entry.
- Exclude missing values instead of treating them as zero.

### 4. Implement take-profit selection

- Prefer:
  1. `takeProfit2` / `riskReward2`
  2. `takeProfit1` / `riskReward1`
  3. `takeProfit3` / `riskReward3`
- Use only targets greater than entry.

### 5. Implement sorting and limiting

- Sort descending by:
  1. `potentialProfitPct`
  2. `riskReward`
  3. `score`
- Treat missing `riskReward` as lower than numeric values.
- Limit to top 5 in V1.

### 6. Add panel component

- Add `TopProfitOpportunitiesPanel`.
- Display:
  - symbol
  - setup
  - score
  - confidence
  - entry
  - take profit
  - stop loss
  - potential profit %
  - risk/reward
  - action
- Add empty state when no valid opportunities exist.
- Use existing dark card styling.
- Prevent overflow for long setup names.

### 7. Insert panel safely

- Add panel near existing signal discovery sections.
- Recommended location:
  - after `signal-groups`
  - before `AlertCenter`
- Use `watchlistSignals` as input so the panel respects active watchlist.
- Do not modify existing scanner behavior.

### 8. Add styles

- Reuse existing card and row styling where possible.
- Add only minimal new CSS.
- Use green/positive formatting for `potentialProfitPct`.
- Keep compact responsive layout.

### 9. Add tests if frontend test structure supports it

- Test helper calculation.
- Test filtering exclusions.
- Test sorting tie-breaks.
- Test top 5 limit.

## Validation Tasks

Frontend:

```text
npm.cmd run build
```

Manual validation:

- Run signals.
- Confirm `Top Profit Opportunities` appears.
- Confirm only candidates appear.
- Confirm candidates without entry/target are excluded.
- Confirm potential profit percent matches:

```text
((takeProfit - entryPoint) / entryPoint) * 100
```

- Confirm existing signal groups, table, filters, and detail panel still work.

## Backend Tasks

None expected for V1.

Backend should remain unchanged because required fields already exist.

## Risks

- Panel may duplicate information from `Top Opportunities` unless label and metrics are clear.
- Frontend-only ranking only considers currently loaded signals.
- Potential profit does not account for probability or market context.
- Ranking by TP2 may be too optimistic for some users.
- Missing risk/reward should not hide otherwise valid opportunities unless product later requires it.

## Open Questions

- Should the panel use active watchlist signals or fully filtered table signals?
- Should users be able to switch TP1/TP2/TP3 ranking mode?
- Should risk/reward become a required filter later?
- Should a backend endpoint be added later for persisted historical candidate ranking?

## Rollback Plan

Frontend:

- Remove `TopProfitOpportunitiesPanel`.
- Remove ranking helper.
- Remove related styles.
- Remove panel insertion from `App.tsx`.

Backend:

- None expected.

Database:

- None expected.
