# Add Signal Detail View Tasks

## Goal

Add a professional selected-signal detail view using existing dashboard data, without changing scoring or API contracts.

## Incremental Implementation Tasks

### 1. Prepare the component boundary

- Create `MarketAgent.Web/src/components/SignalDetailPanel.tsx`.
- Define props for `signal: DashboardSignal | null` and `sparklinePrices?: number[] | null`.
- Keep the component presentational and free of API calls.

### 2. Move current detail rendering out of App

- Replace the inline `SignalDetail` function in `App.tsx` with `SignalDetailPanel`.
- Pass the selected signal and selected symbol's sparkline prices from `App.tsx`.
- Preserve current selection behavior from groups and table rows.

### 3. Build the professional detail layout

- Add a header with symbol, setup, score, action, and confidence.
- Add a larger `Sparkline` chart using existing prices.
- Show current price using existing fields with safe fallback.
- Show key metrics:
  - RS vs SPY
  - RVOL
  - EMA20 extension
  - RSI
- Show moving averages:
  - EMA9
  - EMA20
  - EMA50
  - ATR14 if useful.

### 4. Add risk and target sections

- Show entry/latest, stop, target, TP1, TP2, TP3 when available.
- Show RR1, RR2, RR3 when available.
- Show risk per share and suggested position size when available.
- Render `n/a` for missing optional values.

### 5. Add explanation section

- Use `signal.reason` as the deterministic explanation.
- Render `scoreBreakdown` when available.
- Preserve positive/negative score-factor styling.
- Do not add a new AI-generated explanation.

### 6. Style within the existing design system

- Update `MarketAgent.Web/src/styles.css`.
- Preserve dark mode, compact dashboard density, and current card styling.
- Avoid nested cards.
- Ensure the detail panel remains readable on desktop and mobile.

## Validation/Build Steps

- Run `npm.cmd run build` from `MarketAgent.Web`.
- If backend files are unexpectedly changed, run:
  - `dotnet build MarketAgent.sln --no-restore`
  - `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`

## Manual QA Checklist

- Refresh dashboard with live API data.
- Confirm selecting a table row updates the detail panel.
- Confirm selecting top opportunity, pullback, and risk cards updates the detail panel.
- Confirm null selected signal renders an empty state.
- Confirm missing optional fields render as `n/a`.
- Confirm score breakdown renders correctly when present.
- Confirm empty score breakdown renders a clear empty state.
- Confirm sparkline renders in the detail panel for symbols with historical candles.
- Confirm sparkline placeholder renders when historical data is missing.
- Confirm mobile layout remains readable.
- Confirm no new API request is triggered from the detail component itself.

## Rollback Considerations

- The change should be isolated to frontend presentation files.
- Rollback can remove `SignalDetailPanel.tsx`, restore the inline `SignalDetail` usage in `App.tsx`, and remove any new CSS selectors.
- Since no backend contract changes are planned, rollback should not require database, API, scoring, or AI prompt changes.
