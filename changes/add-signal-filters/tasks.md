# Add Signal Filters Tasks

## Incremental Tasks

### 1. Define filter and sort types

- Add `SignalFilters` and `SignalSortKey` types in `MarketAgent.Web/src/types.ts` or a dedicated filtering module.
- Define default filters with `scoreDesc` sorting.
- Keep the model frontend-only.

### 2. Add pure filtering and sorting helpers

- Create `MarketAgent.Web/src/signalFilters.ts`.
- Implement:
  - `defaultSignalFilters`
  - `applySignalFilters(signals, filters)`
  - `getAvailableSetupTypes(signals)`
  - helper for EMA20 extension fallback
- Ensure helpers do not mutate the original signal array.
- Treat null numeric values as failing threshold filters.

### 3. Implement filter semantics

- Setup type exact match.
- Score thresholds.
- RS thresholds.
- RVOL thresholds.
- Risk-only.
- Opportunity-only.
- Overextended-only.
- Opening Red Reversal only.
- Sorting by score, RS, RVOL, EXT, and symbol.

### 4. Create the filter bar component

- Create `MarketAgent.Web/src/components/SignalFilterBar.tsx`.
- Keep the component presentational.
- Render compact controls above the all-signals table.
- Include visible/total count.
- Include reset filters button.
- Use existing dark chip/button visual patterns.

### 5. Integrate filters in App

- Add filter state to `App.tsx`.
- Derive `filteredSignals` from `allSignals`.
- Pass `filteredSignals` to `SignalsTable`.
- Keep Alert Center and signal group summaries based on the full signal set for the first version.
- Keep selected symbol stable when possible and fall back safely when the selected symbol is filtered out.

### 6. Update styles

- Add filter bar styles to `MarketAgent.Web/src/styles.css`.
- Keep controls compact and wrapping.
- Preserve dark mode readability.
- Keep mobile behavior reasonable.

### 7. Update mock data if useful

- Adjust `MarketAgent.Web/src/api.ts` only if the existing mock data does not exercise enough filter states.
- Keep mock changes small.

## Validation/Build Steps

- Run `npm.cmd run build` from `MarketAgent.Web`.
- If backend files are unexpectedly changed, also run:
  - `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`
  - `dotnet build MarketAgent.sln --no-restore`

## Manual QA Checklist

- Load the dashboard with mock data and live data when available.
- Confirm default table shows all loaded signals sorted by score descending.
- Confirm setup type filter narrows table correctly.
- Confirm score threshold filters work.
- Confirm RS threshold filters exclude missing RS values.
- Confirm RVOL threshold filters exclude missing RVOL values.
- Confirm risk-only and opportunity-only filters work.
- Confirm overextended-only uses EMA20 extension fallback safely.
- Confirm ORR-only shows only Opening Red Reversal signals.
- Confirm sorting by score, RS, RVOL, EXT, and alphabetical works.
- Confirm reset returns the full table and default sort.
- Confirm selected signal updates safely when the selected row is filtered out.
- Confirm empty result state is clear and does not break the detail panel.
- Confirm Alert Center remains visible and functional.
- Confirm dark mode and responsive wrapping remain readable.

## Rollback Considerations

- The preferred implementation is frontend-only and additive.
- Rollback can remove:
  - `MarketAgent.Web/src/signalFilters.ts`
  - `MarketAgent.Web/src/components/SignalFilterBar.tsx`
  - filter-related type additions
  - filter state and `filteredSignals` integration in `App.tsx`
  - filter bar CSS selectors
- Since no backend, persistence, route, or scoring changes are planned, rollback should not require database migration or API changes.
