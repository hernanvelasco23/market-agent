# Signal Outcome Dashboard Tasks

## Discovery

- Inspect `MarketAgent.Web/src/App.tsx`.
- Inspect `MarketAgent.Web/src/api.ts`.
- Inspect `MarketAgent.Web/src/types.ts`.
- Inspect `MarketAgent.Web/src/components/SignalPerformancePreviewPanel.tsx`.
- Inspect `MarketAgent.Web/src/styles.css` for reusable card/performance classes.

## Type/API Tasks

- Add `SignalOutcomeSummary` interface to `src/types.ts`.
- Add `loadSignalOutcomeSummary` to `src/api.ts`.
- Import the new type/API helper where needed.
- Do not change existing API helpers.
- Do not change existing signal, briefing, ingestion, or performance preview types.

## Component Tasks

- Add `src/components/SignalOutcomeSummaryPanel.tsx`.
- Reuse existing card/performance styling patterns.
- Display:
  - `totalCount`
  - `pendingCount`
  - `countWith15m`
  - `averageReturn15m`
  - `countWith1h`
  - `averageReturn1h`
  - `best15mSymbol`
  - `best15mReturnPercent`
  - `worst15mSymbol`
  - `worst15mReturnPercent`
- Add unavailable and empty states.
- Add local formatting helpers for count and percent values.

## App Wiring Tasks

- Add `outcomeSummary` state to `App.tsx`.
- Add `outcomeSummaryUnavailable` state to `App.tsx`.
- Add `refreshOutcomeSummary` function.
- Call `refreshOutcomeSummary` during `refreshDashboard`.
- Call it after `handleRunSignals` and `handleGenerateBriefing`.
- Consider calling it after `handleRunIngestion`, but keep failures isolated.
- Render `SignalOutcomeSummaryPanel` near `SignalPerformancePreviewPanel`.

## Styling Tasks

- First try existing classes:
  - `card`
  - `performance-preview`
  - `performance-note`
  - `performance-empty`
  - `performance-grid`
  - `performance-item`
  - `performance-metric`
- Add small scoped classes only if needed:
  - `outcome-summary`
  - `outcome-extreme`
- Avoid global dashboard restyling.
- Avoid changing table/workspace layout.

## Validation Tasks

- Run frontend typecheck/build:

```text
cd MarketAgent.Web
npm run build
```

- Run backend locally and confirm:
  - dashboard loads if summary endpoint succeeds
  - dashboard loads if summary endpoint fails
  - metrics show `n/a` for null returns
  - positive and negative returns are visually distinct
  - existing signal table and filters still work

## Risks

- API endpoint may fail due to local SQL configuration; UI must degrade gracefully.
- Summary metrics may be null or zero for a while, which can look empty.
- Adding another panel can make the dashboard long; keep it compact.
- Existing CSS class reuse may need minor spacing adjustment.
- Users may mistake partial returns for completed 1D outcomes.

## Rollback Plan

- Remove the new component.
- Remove new state and loader from `App.tsx`.
- Remove `loadSignalOutcomeSummary`.
- Remove `SignalOutcomeSummary` type.
- Remove optional CSS additions.
- No backend rollback needed.
