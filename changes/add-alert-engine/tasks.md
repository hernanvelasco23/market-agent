# Add Alert Engine Tasks

## Incremental Tasks

### 1. Define alert types

- Add `AlertSeverity` and `DashboardAlert` types in `MarketAgent.Web/src/types.ts` or a dedicated alert module.
- Include symbol, title, description, severity, optional setup/action, and metric chips.
- Keep the shape frontend-only unless backend support becomes necessary.

### 2. Add pure alert derivation

- Create `MarketAgent.Web/src/alerts.ts`.
- Implement a pure function such as `deriveDashboardAlerts(signals)`.
- Avoid API calls and side effects.
- Do not mutate `DashboardSignal` objects.
- Ensure null/missing values do not trigger false positives.

### 3. Implement Momentum Breakout rule

- Require high score, strong RS, elevated RVOL, and optional near-high proxy when available.
- Use conservative thresholds to avoid noisy alerts.
- Include metrics that explain the trigger.

### 4. Implement Opening Red Reversal rule

- Reuse `openingRedReversalDetected`.
- Use `reclaimPreviousClose` to distinguish the stronger version.
- Include open gap, recovery from low, reclaim flags, and RVOL when available.

### 5. Implement EMA Reclaim rule conservatively

- Use current price/EMA20 only when both are available.
- Require existing evidence of prior weakness or recovery from setup, reason, or score breakdown text.
- If that evidence is not available, skip the alert and document the limitation in code comments or tests.
- Do not infer previous state from missing data.

### 6. Implement Overextended Warning rule

- Trigger when EMA20 extension is greater than `7%`.
- Use `extensionFromEma20Percent` first and fall back to `distanceFromEma20Percent`.
- Include extension, score, and setup/action metrics.

### 7. Implement Momentum Failure / Risk rule

- Trigger on conservative current-state weakness:
  - low score;
  - action indicating avoid/high risk;
  - negative RS plus price below EMA20 when available.
- Avoid claiming deterioration unless previous-state data exists.

### 8. Add Alert Center component

- Create `MarketAgent.Web/src/components/AlertCenter.tsx`.
- Keep the component presentational.
- Render an empty state safely.
- Show severity, symbol, title, description, setup/action, and metrics.
- Support optional symbol selection callback.

### 9. Integrate in App

- Derive alerts from `allSignals` with `useMemo`.
- Render `AlertCenter` in the dashboard without rewriting existing layout.
- Wire alert clicks to `setSelectedSymbol`.
- Keep signal table, detail panel, sparkline, and run buttons unchanged.

### 10. Style the Alert Center

- Update `MarketAgent.Web/src/styles.css`.
- Match current dark dashboard style.
- Keep severity colors readable and subtle.
- Avoid clutter and nested card-heavy layout.
- Ensure mobile layout remains readable.

### 11. Update mock data if useful

- Adjust `MarketAgent.Web/src/api.ts` mock signals only if needed to demonstrate each alert state.
- Keep mock changes small and consistent with existing fields.

## Validation/Build Steps

- Run `npm.cmd run build` from `MarketAgent.Web`.
- If backend files are unexpectedly changed, also run:
  - `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`
  - `dotnet build MarketAgent.sln --no-restore`

## Manual QA Checklist

- Load the dashboard with mock data and live data when available.
- Confirm the Alert Center appears without disrupting existing layout.
- Confirm no-alert state is readable.
- Confirm clicking an alert selects the matching signal and updates the detail panel.
- Confirm Momentum Breakout alerts require score, RS, and RVOL confirmation.
- Confirm Opening Red Reversal alerts appear only when the existing ORR flag is true.
- Confirm Overextended Warning triggers for EMA20 extension above `7%`.
- Confirm Momentum Failure / Risk alerts do not trigger from missing RS or EMA values.
- Confirm EMA Reclaim is conservative and does not appear when previous weakness cannot be inferred.
- Confirm alert descriptions list the exact metrics used.
- Confirm the dashboard remains readable in dark mode.
- Confirm table, sparkline, signal detail, run briefing, run signals, and run ingestion flows still work.

## Rollback Considerations

- The preferred implementation is frontend-only and additive.
- Rollback can remove:
  - `MarketAgent.Web/src/alerts.ts`
  - `MarketAgent.Web/src/components/AlertCenter.tsx`
  - Alert-related type additions
  - `App.tsx` Alert Center integration
  - Alert Center CSS selectors
- Since no backend, persistence, route, scoring, or external notification changes are planned, rollback should not require database migration or API changes.
