# Add Signal Sparklines

## 1. Problem

The signal dashboard ranks symbols by score and exposes explanatory metrics such as RS, RVOL, EXT, EMA values, RSI, action, confidence, and score breakdown. A user can inspect why a symbol ranked highly, but the all-signals table does not provide a quick visual cue for recent price movement. Users must infer movement from numeric metrics or leave the dashboard to inspect charts elsewhere.

## 2. Goal

Add lightweight sparkline charts to the signal dashboard so each signal row shows recent price movement at a glance, while keeping the dashboard fast, readable in dark mode, and consistent with the existing minimal React/TypeScript UI.

## 3. Scope

- Show a compact sparkline in each row of the all-signals table.
- Use a small custom SVG component.
- Use recent close prices only, not a full charting experience.
- Handle null, empty, one-point, and flat data safely.
- Preserve the existing dashboard layout and visual style.
- Keep the implementation incremental and dependency-free.

## 4. Out of Scope

- No new charting libraries.
- No heavy data visualization framework.
- No interactive chart tooltips, zooming, crosshairs, or indicators.
- No rewrite of the dashboard table or signal engine.
- No trading or recommendation changes.
- No changes to scoring behavior.

## 5. Current Architecture Findings

- Backend follows clean architecture:
  - API exposes thin endpoints in `src/MarketAgent.Api/Program.cs`.
  - Application orchestrates signal generation and historical data retrieval.
  - Domain holds entities such as `MarketSignal`, `MarketSnapshot`, and `MarketCandle`.
  - Infrastructure implements market data and historical data providers.
- The dashboard lives in `MarketAgent.Web`, a Vite React TypeScript app.
- The dashboard currently consumes:
  - `POST /api/signals/run`
  - `POST /api/briefing/run`
  - `POST /api/ingestion/run`
  - fallback mock data in `MarketAgent.Web/src/api.ts`
- The all-signals table is implemented directly in `MarketAgent.Web/src/App.tsx`.
- Existing frontend dependencies are limited to React, React DOM, Vite, TypeScript, and `lucide-react`.

## 6. Data Availability Findings

- Historical price data already exists in the backend as `MarketCandle`.
- `MarketCandle` includes `Symbol`, `OccurredAtUtc`, `Open`, `High`, `Low`, `Close`, `Volume`, and `Source`.
- Historical data can be fetched through `GET /api/historical/candles`.
- Signal generation already fetches historical candles internally for indicators and scoring.
- Current signal DTO/API responses do not expose a per-signal price series.
- The frontend currently has no historical candle client or sparkline-friendly data shape.

## 7. Proposed Approach

Use a minimal dashboard-focused approach that avoids the AI briefing generator:

- Prefer using existing historical candle data through `GET /api/historical/candles` and mapping recent closes by symbol in the dashboard.
- Keep the sparkline series small, such as the most recent 20 closes per symbol.
- In the frontend, add a `Sparkline` SVG component that accepts `number[] | null | undefined`.
- Place the reusable component at `MarketAgent.Web/src/components/Sparkline.tsx`.
- Keep `Sparkline` generic and independent from `MarketSignal`.
- Component inputs should be limited to:
  - `prices`
  - `width`
  - `height`
  - optional trend styling
- Render a compact sparkline column in the all-signals table.
- For empty/null data, render a muted placeholder.
- For one-point or flat data, render a stable horizontal line/dot instead of failing or producing invalid SVG paths.

This keeps sparklines as a dashboard visualization concern and avoids touching AI/briefing generation. The frontend should make at most one historical-candles request per dashboard refresh, not one request per row.

Alternative if the extra dashboard request becomes undesirable later:

- Add an optional compact close-price series to `POST /api/signals/run` signal output only.
- Populate it from the same historical candles already fetched during signal generation.
- Keep it additive and optional.
- Do not add sparkline data to AI prompts or briefing text.

## 8. Files Expected To Change

Expected backend files:

- None required if the dashboard consumes `GET /api/historical/candles`.

Expected frontend files:

- `MarketAgent.Web/src/types.ts`
- `MarketAgent.Web/src/api.ts`
- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/styles.css`
- `MarketAgent.Web/src/components/Sparkline.tsx`

Potential optional files:

- Additional unit tests if a frontend test setup is introduced later. None exists now.
- Backend signal DTO/model files only if the reviewed implementation chooses the optional `sparklinePrices` field on `POST /api/signals/run`.

## 9. API/DTO Impact

No signal or briefing API change is required if the dashboard uses the existing historical candles endpoint.

Preferred initial API impact:

- Reuse `GET /api/historical/candles`.
- Do not change `POST /api/signals/run`.
- Do not change `POST /api/briefing/run`.
- Do not touch AI prompt construction, briefing DTOs, or `SemanticKernelMarketBriefingGenerator.cs` unless a later requirement explicitly makes sparkline data part of briefing output.

Optional later API change, only if needed:

- Add optional sparkline data to signal objects.
- Do not remove or rename existing fields.
- Do not alter endpoint routes.
- Do not change score, action, confidence, or briefing bucket contracts.

Candidate field:

```text
sparklinePrices?: number[] | null
```

The backend should serialize decimals as numbers through the existing JSON behavior. The frontend can type these as `number[]`.

## 10. Testing Plan

Backend tests:

- Not required for the preferred frontend-only mapping approach, because no backend behavior changes.
- If adding `sparklinePrices` to signal DTOs later, then add backend tests for ordering, cap length, missing candles, one candle, and flat candles.

Frontend verification:

- `npm.cmd run build`
- Verify the historical candles client maps candles by symbol.
- Visual check in dark dashboard:
  - normal rising/falling series
  - empty/null placeholder
  - flat series
  - one-point series
- Confirm table remains horizontally scrollable and readable.

Full verification:

- `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`
- `dotnet build` or API project build when the API process is not locking output files.

## 11. Risks

- Adding arrays to every signal increases response size, though a 20-point close-only series should remain small.
- If historical data is missing or provider calls fail, sparklines may be unavailable for some symbols.
- Reusing `GET /api/historical/candles` adds one dashboard request, but avoids expanding signal/briefing payloads.
- If the historical endpoint is slow, dashboard loading may need a separate loading state for sparklines.
- The table is already dense; adding a sparkline column may require careful width and responsive behavior.
- SVG rendering per row is lightweight for the current watchlist size, but should remain simple to avoid table performance issues.
- AI briefing generation should remain untouched unless a future requirement explicitly asks for sparkline data in briefing output.
