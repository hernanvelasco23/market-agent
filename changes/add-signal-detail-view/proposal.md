# Add Signal Detail View

## Problem

The dashboard already lets a user select a market signal and see a compact detail card, but the current implementation is embedded directly in `App.tsx` and presents the selected signal as a dense metric grid. As more signal fields are added, this makes the dashboard harder to maintain and makes it harder for users to understand why a symbol matters at a glance.

## Goal

Add a professional detail view for the selected market signal that explains the setup using existing deterministic signal data, highlights important metrics, and shows a larger lightweight trend chart.

## User Value

- Users can inspect a selected signal without leaving the dashboard.
- Users can quickly understand score, setup, confidence, trend, RS, RVOL, extension, RSI, EMA context, and risk levels.
- The dashboard becomes more useful as a monitoring and review tool while preserving the explainable, non-trading-bot product philosophy.
- The UI becomes easier to maintain by moving detail-panel rendering out of `App.tsx`.

## Scope

- Create a reusable frontend detail component, preferably `MarketAgent.Web/src/components/SignalDetailPanel.tsx`.
- Keep selection behavior driven by the existing selected symbol state.
- Reuse existing `DashboardSignal` fields wherever possible.
- Reuse the existing `Sparkline` component for a larger trend chart.
- Show:
  - Symbol
  - Current price when available from existing fields
  - Score
  - Action/setup
  - Confidence
  - RS vs SPY
  - RVOL
  - EMA20 extension
  - RSI
  - EMA9/EMA20/EMA50
  - Score breakdown
  - Target/stop/risk-reward fields that already exist
  - Explanation based on existing fields
- Preserve dark mode and the current dashboard visual style.
- Handle null, missing, and empty data safely.

## Out of Scope

- No new dependencies.
- No charting library.
- No scoring changes.
- No trading execution, prediction, or recommendation workflow.
- No new AI call.
- No backend rewrite.
- No API route changes unless implementation discovers an unavoidable gap.
- No dashboard rewrite.
- No persistence changes.

## Success Criteria

- Selecting any signal shows a polished, readable detail panel.
- The panel renders safely when optional fields are missing.
- The panel uses existing API data and sparkline prices.
- The existing table, signal groups, action buttons, mock fallback, and dashboard refresh behavior still work.
- `npm.cmd run build` passes.
- No backend API contract is broken.
