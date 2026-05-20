# Add Signal Filters Design

## Current Architecture Findings

- `MarketAgent.Web` is a Vite React TypeScript dashboard with no state management dependency beyond React state/hooks.
- `MarketAgent.Web/src/App.tsx` owns dashboard state, data loading, selected symbol, sparkline maps, alert derivation, and layout composition.
- The all-signals table is currently an inline `SignalsTable` component inside `App.tsx`.
- `SignalDetailPanel` is isolated in `MarketAgent.Web/src/components/SignalDetailPanel.tsx`.
- `AlertCenter` is isolated in `MarketAgent.Web/src/components/AlertCenter.tsx`, while alert derivation lives in `MarketAgent.Web/src/alerts.ts`.
- Styling lives in `MarketAgent.Web/src/styles.css` with compact dark cards, pills, metric chips, and responsive grid behavior.
- Signal types live in `MarketAgent.Web/src/types.ts`.
- Backend signal generation and scoring are not needed for filtering because the dashboard already receives a complete signal list.

## Existing Data Already Available

Available on `DashboardSignal` and suitable for filtering/sorting:

- `symbol`
- `score`
- `setupType`
- `action`
- `confidence`
- `timeframe`
- `relativeStrengthVsSpy`
- `relativeVolume`
- `extensionFromEma20Percent`
- `distanceFromEma20Percent`
- `openingRedReversalDetected`
- `extensionRisk`

Derived values already used by the UI:

- EMA20 extension can use `extensionFromEma20Percent ?? distanceFromEma20Percent`.
- Risk signals can use score/action and the same broad semantics already used by top risks.
- Opportunity signals can use candidate action and/or score thresholds, matching existing dashboard conventions.

No backend fields are required for the initial filtering feature.

## Frontend Filtering Approach

Use a small frontend-only filter state object:

```ts
type SignalFilters = {
  setupType: string;
  minScore: number | null;
  minRs: number | null;
  minRvol: number | null;
  riskOnly: boolean;
  opportunityOnly: boolean;
  overextendedOnly: boolean;
  openingRedReversalOnly: boolean;
  sortBy: SignalSortKey;
};
```

Recommended thresholds:

- Score: `60+`, `75+`, `90+`
- RS: `> 0`, `> 1`, `> 3`
- RVOL: `> 1`, `> 2`, `> 3`

Filtering rules:

- Setup type filter matches exact `setupType`, with `All setups` as default.
- Score threshold requires `signal.score >= minScore`.
- RS threshold requires non-null RS and `relativeStrengthVsSpy >= minRs`.
- RVOL threshold requires non-null RVOL and `relativeVolume >= minRvol`.
- Risk-only matches `score < 40` or action containing avoid/high risk.
- Opportunity-only matches candidate/high-score conventions already used in dashboard summaries.
- Overextended-only matches EMA20 extension greater than `7`.
- Opening Red Reversal only matches `openingRedReversalDetected === true`.

Null handling:

- Missing numeric fields should not pass threshold filters.
- Missing optional flags should be treated as false.

## Sorting Approach

Supported sort keys:

- `scoreDesc`
- `rsDesc`
- `rvolDesc`
- `extDesc`
- `symbolAsc`

Sorting should:

- Work on the filtered array, not mutate `allSignals`.
- Treat null metric values as lowest priority for descending metric sorts.
- Use symbol as a stable tie-breaker.
- Default to `scoreDesc`, matching the dashboard's ranking-oriented behavior.

## State Handling Approach

- Keep filter state in `App.tsx` because it controls the all-signals table and selected signal behavior.
- Derive `filteredSignals` with `useMemo` from `allSignals` and `filters`.
- Pass `filteredSignals` into `SignalsTable`.
- Keep `AlertCenter` derived from the full `allSignals` in the initial implementation so alerts do not disappear unexpectedly when users filter the table.
- Keep signal groups unchanged in the first version.
- When filters change:
  - If the selected symbol is still visible, keep it selected.
  - If not, select the first visible signal.
  - If no signals are visible, allow the detail panel to show its empty state.

This keeps filtering local to table navigation and avoids broader dashboard behavior changes.

## UI Component Design

Prefer creating:

- `MarketAgent.Web/src/components/SignalFilterBar.tsx`

Suggested props:

```ts
type SignalFilterBarProps = {
  filters: SignalFilters;
  setupTypes: string[];
  visibleCount: number;
  totalCount: number;
  onChange: (filters: SignalFilters) => void;
  onReset: () => void;
};
```

Controls:

- Setup type select or compact segmented chip group if setup count stays small.
- Score threshold chip group.
- RS threshold chip group.
- RVOL threshold chip group.
- Toggle chips for:
  - Risk
  - Opportunity
  - Overextended
  - ORR
- Sort select or compact button group.
- Clear/reset button.
- Visible count such as `8 / 20`.

Style:

- Use existing card/pill/metric-chip visual language.
- Keep the filter bar above the signals table, not inside table rows.
- Keep controls compact and wrapping.
- Avoid large panels or nested cards.
- Maintain responsive wrapping on mobile.

## Files Expected To Change

Expected frontend files:

- `MarketAgent.Web/src/types.ts`
- `MarketAgent.Web/src/signalFilters.ts`
- `MarketAgent.Web/src/components/SignalFilterBar.tsx`
- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/styles.css`

Potential frontend files:

- `MarketAgent.Web/src/api.ts` only if mock data needs small updates to exercise filters.

Expected backend files:

- None.

Files intentionally not expected to change:

- `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`
- `src/MarketAgent.Domain/Entities/MarketSignal.cs`
- `src/MarketAgent.Application/Models/MarketBriefingResult.cs`
- `src/MarketAgent.Infrastructure/AI/SemanticKernelMarketBriefingGenerator.cs`
- Existing backend tests.

## Risks

- Filter complexity: too many controls can make the dashboard feel heavier than the table itself. Keep the first version compact and threshold-based.
- UI clutter: the dashboard already has hero summary cards, signal groups, Alert Center, table, and detail panel. The filter bar should be one concise row that wraps.
- Selection confusion: filtering can hide the selected symbol. The implementation must handle fallback selection clearly.
- Conflicting toggles: risk-only and opportunity-only can logically conflict. The implementation should either allow intersections intentionally or make the behavior obvious.
- Null metrics: missing RS/RVOL/EXT must not accidentally pass threshold filters.
- User expectation: frontend-only filters operate on loaded data only, not the entire market universe.
