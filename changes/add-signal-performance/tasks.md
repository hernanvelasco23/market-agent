# Signal Performance Preview Tasks

## Incremental Tasks

### 1. Define preview models

- Add application result/DTO records:
  - `SignalPerformancePreviewResult`
  - `SignalPerformancePreviewItem`
- Include:
  - generated timestamp
  - requested days
  - signal family/type
  - sample count
  - insufficient-data flag
  - low-sample warning flag
  - average forward returns for 1D/3D/5D
  - win rates for 1D/3D/5D only when sample count is sufficient
  - warnings explaining reconstruction limits
  - failures if historical data is missing or provider errors occur
- Do not model this as a full backtest result.

### 2. Add preview service abstraction

- Add `ISignalPerformancePreviewService`.
- Keep it in the Application layer.
- Do not add a historical signal repository.
- Do not add database persistence.
- Do not add scoring changes to `TechnicalMarketSignalAnalyzer`.

### 3. Implement deterministic preview service

- Create `SignalPerformancePreviewService`.
- Fetch historical candles through `IHistoricalMarketDataService`.
- Group and order candles by symbol.
- Iterate candidate dates with enough lookback and forward candles.
- Reconstruct daily snapshots from OHLCV candles.
- Call existing analyzer using only historical data available up to each candidate date.
- Map generated signals into supported preview families.
- Calculate forward returns after 1, 3, and 5 candles.
- Treat missing horizons as null, not zero.
- Mark insufficient data when sample count is unavailable or too low.
- Mark low sample warning below the selected threshold.
- Include warnings that reconstructed samples may differ from real-time emitted signals.

### 4. Add API endpoint

- Register the preview service in DI.
- Add a new additive endpoint, for example:
  - `GET /api/signals/performance-preview?days=180`
- Clamp/normalize requested days.
- Do not change existing `/api/signals/run` or briefing endpoints.
- Do not add persistence or background jobs.

### 5. Add backend tests

- Add `SignalPerformancePreviewServiceTests`.
- Test:
  - 1D/3D/5D forward return calculation.
  - win rate calculation when sample count is sufficient.
  - win rate omitted/null when sample count is insufficient.
  - average forward return calculation.
  - sample count.
  - missing forward horizon handling.
  - insufficient-data flag.
  - low-sample warning flag.
  - no crash when candles are missing.
  - no lookahead in analyzer input.
  - reconstructed samples are mapped to the intended signal family.

### 6. Add frontend API/types

- Add frontend types for preview result/items.
- Add API client method to load performance preview.
- Keep API changes additive.
- Preserve existing signal, briefing, alert, filter, and watchlist types.

### 7. Add compact dashboard UI

- Create `MarketAgent.Web/src/components/SignalPerformancePreviewPanel.tsx`.
- Show signal family rows/cards.
- Show sample count, average forward returns, win rates when available, and insufficient/low-sample status.
- Include concise caution copy:
  - reconstructed historical samples may differ from real-time signals
  - small samples are not statistically reliable
  - educational/diagnostic only
  - no guarantee of future performance
- Keep wording non-recommendational.
- Keep dark mode style consistent.

### 8. Integrate frontend state

- Load preview data from the dashboard or via a small explicit action.
- Prefer not blocking the main dashboard if preview loading fails.
- Show a clear unavailable/insufficient-data state.
- Do not change existing filters, watchlists, alerts, sparklines, detail panel, or scoring behavior.

## Validation/Build Steps

- Run `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`.
- Run `dotnet build MarketAgent.sln --no-restore`.
- Run `npm.cmd run build` from `MarketAgent.Web`.

## Manual QA Checklist

- Run the API and dashboard.
- Confirm existing signal generation still works.
- Confirm the new performance preview endpoint returns reconstructed sample data or insufficient-data states.
- Confirm missing historical candles do not crash the endpoint.
- Confirm dashboard still loads when preview loading fails.
- Confirm performance preview UI shows sample counts.
- Confirm small sample sizes are visibly flagged.
- Confirm insufficient data is visibly flagged.
- Confirm 1D/3D/5D values are formatted clearly.
- Confirm no wording implies trading advice, prediction, or guaranteed future performance.
- Confirm UI states that samples are reconstructed and may differ from real-time signals.
- Confirm filters, watchlists, alerts, sparklines, and detail panel still work.

## Rollback Considerations

- This feature should be additive.
- Rollback backend by removing:
  - preview service abstraction
  - preview service implementation
  - preview models
  - DI registration
  - preview endpoint
  - preview tests
- Rollback frontend by removing:
  - preview types
  - preview API client
  - preview panel component
  - App integration
  - preview styles
- No database migration should be required in the initial version.
- No historical signal repository should be created in the initial version.
- Existing signal, briefing, filter, watchlist, alert, and scoring APIs should remain untouched.
