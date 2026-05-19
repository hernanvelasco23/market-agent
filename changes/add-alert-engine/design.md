# Add Alert Engine Design

## Current Architecture Findings

- The dashboard is implemented in `MarketAgent.Web`, a Vite React TypeScript app.
- `MarketAgent.Web/src/App.tsx` owns dashboard state, data loading, row selection, and layout composition.
- `MarketAgent.Web/src/api.ts` maps backend signal responses into dashboard-facing data and includes mock fallback data.
- `MarketAgent.Web/src/types.ts` defines `DashboardSignal`, API signal shapes, historical candle shapes, and sparkline maps.
- `MarketAgent.Web/src/components/Sparkline.tsx` is a generic reusable SVG component that accepts price arrays and does not depend on signal models.
- `MarketAgent.Web/src/components/SignalDetailPanel.tsx` presents selected signal detail using existing signal fields and sparkline prices.
- Signal scoring lives in `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`.
- Signal output is represented by `src/MarketAgent.Domain/Entities/MarketSignal.cs`.
- Opening Red Reversal is already calculated and exposed through additive signal fields.
- Existing analyzer tests are in `tests/MarketAgent.UnitTests/TechnicalMarketSignalAnalyzerTests.cs`.

## Existing Data Available

Available on `DashboardSignal`:

- `symbol`
- `score`
- `setupType`
- `action`
- `confidence`
- `timeframe`
- `reason`
- `entry`
- `ema9`
- `ema20`
- `ema50`
- `rsi14`
- `relativeStrengthVsSpy`
- `relativeVolume`
- `extensionFromEma20Percent`
- `distanceFromEma20Percent`
- `extensionRisk`
- `recoveryFromLowPercent`
- `gapPercent`
- `gapRecovery`
- `openingRedReversalDetected`
- `openGapPercent`
- `openingRedReversalRecoveryFromLowPercent`
- `reclaimOpen`
- `reclaimPreviousClose`
- `scoreBreakdown`

Available from sparkline integration:

- Recent close prices by symbol through `GET /api/historical/candles`.
- The sparkline data currently exposes closes only, not intraday highs/lows.

Important limitations:

- The frontend does not have a dedicated current price field; `entry` currently acts as the latest/entry price in the UI.
- The frontend does not have true prior signal state, such as "previously below EMA20" or score deterioration over time.
- The historical candle client currently provides close prices only, so "near/new intraday high" cannot be confirmed directly from frontend candle data.

## Alert Derivation Approach

Prefer a frontend-only additive implementation:

- Add a small pure alert derivation module, for example `MarketAgent.Web/src/alerts.ts`.
- Derive alerts from `DashboardSignal[]` and optionally `SparklinePricesBySymbol`.
- Keep rule logic deterministic and side-effect free.
- Keep `AlertCenter` presentational and free of API calls.
- Sort alerts by severity and strength.
- Cap or constrain displayed alerts to avoid noisy output.
- Generate stable alert IDs from alert type and symbol.
- Avoid alerts when required metrics are missing.

Suggested frontend model:

```ts
export type AlertSeverity = "info" | "opportunity" | "warning" | "risk";

export type DashboardAlert = {
  id: string;
  symbol: string;
  title: string;
  description: string;
  severity: AlertSeverity;
  setupType?: string;
  action?: string;
  metrics: Array<{
    label: string;
    value: string;
    tone?: "positive" | "neutral" | "warning" | "risk";
  }>;
};
```

## Alert Rule Definitions

### Momentum Breakout

Purpose:

- Highlight strong opportunity candidates with broad confirmation.

Suggested rule:

- `score >= 75`
- `relativeStrengthVsSpy > 3`
- `relativeVolume >= 2`
- If `recoveryFromLowPercent` is available, require it to be at least `70` as a proxy for price near the upper intraday range.

Severity:

- `opportunity`

Metrics:

- Score
- RS vs SPY
- RVOL
- Recovery/range position when available

Limitation:

- The UI cannot currently confirm a new intraday high because high-price data is not exposed to the dashboard. The first implementation should describe this as "strong momentum confirmation" rather than "new high" unless high data is added later.

### Opening Red Reversal

Purpose:

- Surface symbols that opened weak and recovered above the open.

Suggested rule:

- `openingRedReversalDetected === true`

Severity:

- `opportunity` when `reclaimPreviousClose === true`
- `info` otherwise

Metrics:

- Open gap
- Recovery from low
- Reclaim open
- Reclaim previous close
- RVOL

### EMA Reclaim

Purpose:

- Highlight a possible price reclaim of EMA20 after weakness.

Safe initial rule:

- `entry > ema20`
- and existing text or score factors indicate prior weakness/recovery, such as setup/reason/breakdown containing `pullback`, `weakness`, `recovery`, or `reclaim`.

Severity:

- `info`

Limitation:

- A true EMA reclaim requires previous price state, for example a prior close below EMA20 followed by current price above EMA20. That state is not currently available in `DashboardSignal`. If implementation cannot find explicit existing evidence of prior weakness, the EMA Reclaim alert should be skipped rather than guessed.

Future backend/API improvement:

- Add an additive field such as `WasBelowEma20` or `ReclaimedEma20` if the scanner needs a reliable deterministic reclaim alert.

### Overextended Warning

Purpose:

- Warn when a signal is stretched above EMA20.

Suggested rule:

- `extensionFromEma20Percent > 7`
- Fall back to `distanceFromEma20Percent` if needed.

Severity:

- `warning`

Metrics:

- EMA20 extension
- Score
- Setup/action when available

### Momentum Failure / Risk

Purpose:

- Surface weak or deteriorating candidates already visible in signal data.

Suggested rule:

- Trigger when one of these conservative conditions is true:
  - `score < 40`
  - action text indicates avoid/high risk
  - `relativeStrengthVsSpy < 0` and `entry < ema20`

Severity:

- `risk`

Metrics:

- Score
- RS vs SPY
- Price versus EMA20 when available
- Action/setup

Limitation:

- True deterioration over time is not available without previous signal snapshots. The initial version should avoid claiming deterioration unless the current signal fields support it.

## Frontend Component Design

Create:

- `MarketAgent.Web/src/components/AlertCenter.tsx`

Suggested props:

```ts
type AlertCenterProps = {
  alerts: DashboardAlert[];
  onSelectSymbol?: (symbol: string) => void;
};
```

Component behavior:

- Render a compact empty state when no alerts are active.
- Render alert rows or compact cards grouped visually by severity.
- Keep each alert explainable by showing its metric chips.
- Let alert selection call `onSelectSymbol(alert.symbol)` so the detail panel updates.
- Keep styling subtle and consistent with existing dashboard cards, pills, and muted text.
- Avoid nested card-heavy layouts.

Integration in `App.tsx`:

- Derive alerts with `useMemo` from `allSignals`.
- Render `AlertCenter` near the main signal workspace, likely above the table/detail panel.
- Preserve existing signal selection behavior.

## Backend/API Impact

Preferred initial impact:

- No backend changes.
- No API/DTO changes.
- No scoring changes.
- No Semantic Kernel or briefing prompt changes.

Reasoning:

- The initial alert set can be derived from fields already returned to the dashboard.
- Opening Red Reversal data is already exposed.
- Overextension, RS, RVOL, score, action, setup, and EMA values are already exposed.

Potential future additive backend fields:

- `currentPrice` to avoid relying on `entry` as latest price.
- `intradayHigh` or a near-high boolean for more precise Momentum Breakout alerts.
- `reclaimedEma20` or previous close/EMA state for reliable EMA Reclaim alerts.
- Persisted previous signal state if "deteriorating" alerts become a requirement.

## Files Expected To Change

Expected frontend files for implementation:

- `MarketAgent.Web/src/types.ts`
- `MarketAgent.Web/src/alerts.ts`
- `MarketAgent.Web/src/components/AlertCenter.tsx`
- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/styles.css`

Potential frontend files:

- `MarketAgent.Web/src/api.ts` if mock data needs small updates to demonstrate alert states.

Expected backend files:

- None for the preferred frontend-only implementation.

Files intentionally not expected to change:

- `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`
- `src/MarketAgent.Domain/Entities/MarketSignal.cs`
- `src/MarketAgent.Application/Models/MarketBriefingResult.cs`
- `src/MarketAgent.Infrastructure/AI/SemanticKernelMarketBriefingGenerator.cs`
- Existing backend tests, unless an unexpected backend gap is intentionally addressed.

## Risks

- Alert noise: broad conditions could create too many alerts. Use conservative thresholds, stable sorting, and a display cap.
- False positives: missing metrics must not be treated as bullish or bearish defaults.
- EMA Reclaim ambiguity: previous state is not reliably available, so the initial rule must be conservative or skipped.
- Intraday high ambiguity: the dashboard does not currently expose intraday high data to the alert rules.
- UI density: the dashboard already has signal groups, table, and detail view; the Alert Center must stay compact.
- User trust: alert descriptions must say what data triggered them and avoid implying external notifications or AI judgment.
- Future backend expansion: adding persisted or background alerts later should be separate from this in-app deterministic layer.
