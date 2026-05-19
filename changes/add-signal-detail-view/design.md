# Add Signal Detail View Design

## Current Architecture Findings

- Market Agent follows clean architecture:
  - `src/MarketAgent.Api/Program.cs` exposes thin endpoints.
  - `src/MarketAgent.Application` coordinates use cases and signal generation.
  - `src/MarketAgent.Domain` owns deterministic market entities such as `MarketSignal`.
  - `src/MarketAgent.Infrastructure` implements providers, indicators, persistence, and Semantic Kernel integration.
- `MarketAgent.Web` is a Vite React TypeScript dashboard.
- Frontend dependencies are intentionally small: React, React DOM, Vite, TypeScript, and `lucide-react`.
- The dashboard implementation currently lives mostly in `MarketAgent.Web/src/App.tsx`.
- A compact selected-signal detail card already exists as an inline `SignalDetail` function in `App.tsx`.
- The recent sparkline feature added `MarketAgent.Web/src/components/Sparkline.tsx` as a generic SVG component and added historical candle loading in `MarketAgent.Web/src/api.ts`.

## UI/Data Flow

1. The dashboard loads briefing or signal data through existing API client functions.
2. `App.tsx` stores the current `BriefingResult`, selected symbol, ingestion status, mock state, and sparkline prices.
3. `selectedSignal` is derived from `briefing.allSignals` and `selectedSymbol`.
4. Signal groups and the all-signals table call `setSelectedSymbol`.
5. The detail panel receives the derived selected signal and renders the signal details.
6. Sparkline prices are loaded separately from `GET /api/historical/candles` and mapped by symbol.
7. The new detail panel should receive the selected signal plus the selected symbol's sparkline prices.

## Existing Signal/Dashboard Flow

- `POST /api/briefing/run` returns `MarketBriefingResult`, including `allSignals`, top opportunities, pullbacks, risks, highlights, risks, and watch items.
- `POST /api/signals/run` returns `MarketSignalRunResult`, and the frontend maps it into a briefing-shaped dashboard fallback.
- `MarketAgent.Web/src/api.ts` maps backend `rsi` into frontend `rsi14`.
- The all-signals table currently displays symbol, trend sparkline, score, setup, action, confidence, timeframe, RS, RVOL, EXT, RSI, EMA9, EMA20, EMA50, and ATR14.
- The existing inline detail view displays action, confidence, timeframe, entry, stop, take profits, risk/reward, RS, RVOL, EMA20 extension, recovery, gap, EMA slopes, extension risk, reason, and score breakdown.

## Existing Sparkline Integration

- `MarketAgent.Web/src/components/Sparkline.tsx` is generic and accepts:
  - `prices`
  - `width`
  - `height`
  - optional `trend`
- The component handles null, empty, one-point, flat, rising, and falling data safely.
- `MarketAgent.Web/src/api.ts` has `loadHistoricalCandles()` and `buildSparklinePricesBySymbol()`.
- `App.tsx` stores `sparklinePrices` as `SparklinePricesBySymbol` and passes row-level prices into the all-signals table.
- The detail view should reuse this same map and render the selected symbol with larger dimensions.

## Data Already Available

Available from `DashboardSignal` / backend signal models:

- `symbol`
- `score`
- `setupType`
- `action`
- `timeframe`
- `confidence`
- `reason`
- `ema9`
- `ema20`
- `ema50`
- `rsi14`
- `atr14`
- `relativeStrengthVsSpy`
- `relativeVolume`
- `distanceFromEma20Percent`
- `extensionFromEma20Percent`
- `extensionRisk`
- `entry`
- `stop`
- `target`
- `takeProfit1`
- `takeProfit2`
- `takeProfit3`
- `riskReward1`
- `riskReward2`
- `riskReward3`
- `riskPerShare`
- `suggestedPositionSize`
- `scoreBreakdown`
- `recoveryFromLowPercent`
- `gapPercent`
- `ema20Slope`
- `ema50Slope`

Current price is not exposed as a dedicated frontend field. The analyzer currently sets `entry` to the latest rounded snapshot price, so the initial implementation can label it as current price only if this assumption remains acceptable. A safer UI label is `Entry / Last` or `Latest price` with fallback to `n/a`.

Available from sparkline integration:

- Recent close prices by symbol from `GET /api/historical/candles`.

## API/DTO Impact

Preferred implementation has no backend API or DTO changes.

- Reuse `DashboardSignal`.
- Reuse `SparklinePricesBySymbol`.
- Do not change `POST /api/signals/run`.
- Do not change `POST /api/briefing/run`.
- Do not change scoring behavior.
- Do not add data to Semantic Kernel prompts.

Potential future improvement:

- Add a dedicated `currentPrice` field to the signal DTO if the UI needs to distinguish latest market price from proposed entry. This should be additive only and documented separately if needed.

## Component Design

Create:

- `MarketAgent.Web/src/components/SignalDetailPanel.tsx`

Suggested props:

```ts
type SignalDetailPanelProps = {
  signal: DashboardSignal | null;
  sparklinePrices?: number[] | null;
};
```

The component should:

- Render an empty state when `signal` is null.
- Keep formatting helpers local or accept formatted values through small helper functions if moved from `App.tsx`.
- Reuse `Sparkline` with larger dimensions than the table row.
- Group content into readable sections:
  - Header: symbol, setup, score, action/confidence.
  - Trend: large sparkline and latest/entry price.
  - Key metrics: RS, RVOL, extension, RSI.
  - Moving averages: EMA9, EMA20, EMA50, ATR if useful.
  - Risk plan: entry/latest, stop, targets, risk/reward.
  - Explanation: existing reason plus score breakdown.
- Use existing CSS conventions such as cards, metric blocks, pills, score badges, and muted empty states.
- Avoid nested card-on-card visual patterns.

## State Handling Approach

- Keep `selectedSymbol` and `selectedSignal` in `App.tsx`.
- Keep `sparklinePrices` in `App.tsx` because it is already shared between table rows and the detail view.
- Pass `sparklinePrices[selectedSignal.symbol.toUpperCase()]` into `SignalDetailPanel`.
- Keep the component presentational and deterministic.
- Do not trigger fetching or mutate dashboard state inside `SignalDetailPanel`.

## Files Expected To Change

Expected frontend files:

- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/components/SignalDetailPanel.tsx`
- `MarketAgent.Web/src/styles.css`

Potential frontend files:

- `MarketAgent.Web/src/types.ts` only if a small presentational prop type is shared.
- `MarketAgent.Web/src/api.ts` only if mock sparkline data or client shaping needs a tiny improvement.

Expected backend files:

- None for the initial implementation.

Files intentionally not expected to change:

- `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`
- `src/MarketAgent.Domain/Entities/MarketSignal.cs`
- `src/MarketAgent.Application/Models/MarketBriefingResult.cs`
- `src/MarketAgent.Infrastructure/AI/SemanticKernelMarketBriefingGenerator.cs`

## Risks

- The UI may imply `entry` is the current price even though the API does not expose a dedicated `currentPrice` field.
- The detail panel could become too dense if every available field is shown with equal visual weight.
- Moving formatting helpers out of `App.tsx` could create accidental churn if not kept scoped.
- The dashboard table and detail panel share sparkline data; missing historical candles should show a graceful placeholder.
- The current layout is already dense, so responsive behavior needs a quick visual QA pass.
