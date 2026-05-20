# Signal Performance Preview Design

## Current Architecture Findings

- Backend follows clean architecture:
  - API routes are thin in `src/MarketAgent.Api/Program.cs`.
  - Application services coordinate use cases.
  - Domain contains entities such as `MarketSignal`, `MarketSnapshot`, and `MarketCandle`.
  - Infrastructure implements historical data providers, indicators, and in-memory repositories.
- Signal scoring lives in `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`.
- Current signal generation in `MarketSignalService` loads recent historical candles, analyzes latest snapshots, and returns `MarketSignalRunResult`.
- Historical candle access is available through `IHistoricalMarketDataService` and `IHistoricalCandleRepository`.
- The signal analyzer is reusable and can be called with reconstructed historical snapshots plus historical candles.
- Frontend dashboard consumes:
  - `POST /api/signals/run`
  - `POST /api/briefing/run`
  - `POST /api/ingestion/run`
  - `GET /api/historical/candles`
- Frontend filters and watchlists are currently in-memory and operate on already-loaded `DashboardSignal` data.
- The current frontend historical candle shape exposes `symbol`, `occurredAtUtc`, and `close` for sparklines only.
- There is no persisted historical signal repository today.
- Because historical signals are not persisted, this feature should be presented as a reconstructed outcome preview, not a production backtest.

## Historical Data Availability

Available historical data:

- `MarketCandle` includes:
  - `Symbol`
  - `AssetType`
  - `OccurredAtUtc`
  - `Open`
  - `High`
  - `Low`
  - `Close`
  - `Volume`
  - `Source`
- `HistoricalMarketDataService` can fetch/cache watchlist candles.
- `HistoricalMarketDataService.DefaultDays` is currently `90`.
- Candle fetch is normalized to a maximum of `300` days.
- The API exposes `GET /api/historical/candles?days=...`.

Important gaps:

- There is no durable historical signal table.
- There is no historical real-time signal event stream.
- There is no historical intraday snapshot series equivalent to current `MarketSnapshot` data.
- The current frontend candle DTO does not expose high/low/open/volume.
- Opening Red Reversal is intraday by definition, so daily candles can only approximate it unless historical intraday data is later added.

## Signal Sampling Approach

Preferred first implementation should stay deterministic and modest:

1. Create an Application service, for example `SignalPerformancePreviewService`.
2. Fetch historical daily candles for tracked assets.
3. For each symbol, iterate through candles where enough lookback and forward horizon exist.
4. Reconstruct a daily `MarketSnapshot` from each candidate candle:
   - `Price = Close`
   - `OpenPrice = Open`
   - `HighPrice = High`
   - `LowPrice = Low`
   - `PreviousClose = previous candle Close`
   - `Volume = Volume`
   - `CapturedAtUtc = candle OccurredAtUtc`
5. Pass reconstructed snapshots plus lookback candles into the existing analyzer.
6. Ensure analyzer input only includes candles available up to the candidate date.
7. Collect reconstructed samples whose resulting signal matches the target signal family.
8. Measure forward returns from the candidate candle close to future candle closes.

Signal family mapping:

- `MomentumContinuation`: `signal.SetupType == "MomentumContinuation"` or `signal.MomentumContinuation == true`.
- `OpeningRedReversal`: `signal.OpeningRedReversalDetected == true`.
- `Pullback`: `signal.SetupType == "Pullback"`.
- `OverextendedWarning`: `signal.ExtensionRisk is not null` or `signal.Action` indicates pullback/breakout confirmation due to extension.

This approach reuses current deterministic logic while acknowledging daily-candle and reconstruction limitations.

## Performance Calculation Approach

Per reconstructed sample:

- Entry basis: close of the candidate signal candle.
- 1-day forward return: `(close[t+1] - close[t]) / close[t] * 100`.
- 3-day forward return: `(close[t+3] - close[t]) / close[t] * 100`.
- 5-day forward return: `(close[t+5] - close[t]) / close[t] * 100`.
- Missing future candles produce null for that horizon, not zero.
- Sample is included only for horizons that are available.

Aggregates per signal family:

- `SampleCount`
- `AverageForwardReturn1Day`
- `AverageForwardReturn3Day`
- `AverageForwardReturn5Day`
- `WinRate1Day`
- `WinRate3Day`
- `WinRate5Day`
- `IsInsufficientData`
- `HasLowSampleWarning`

Insufficient and low-sample handling:

- Treat zero available samples as insufficient data.
- Display insufficient data when there are too few samples to calculate useful output.
- Display a low sample warning below a conservative threshold, for example fewer than `10` samples.
- Only show win rate when sample count is sufficient for that horizon.
- Continue returning raw sample count so users understand why confidence is low.
- Avoid ranking, recommending, or implying expected future returns from tiny samples.

## API/DTO Impact

Preferred API change is additive:

- Add a new endpoint, for example:
  - `GET /api/signals/performance-preview?days=180`
  - or `POST /api/signals/performance-preview/run`

Candidate response model:

```text
SignalPerformancePreviewResult
  GeneratedAtUtc
  RequestedDays
  Items[]
  Warnings[]
  Failures[]

SignalPerformancePreviewItem
  SignalType
  SampleCount
  IsInsufficientData
  HasLowSampleWarning
  AverageForwardReturn1Day
  AverageForwardReturn3Day
  AverageForwardReturn5Day
  WinRate1Day
  WinRate3Day
  WinRate5Day
```

Do not change existing signal or briefing contracts for the initial version.

Application additions:

- `ISignalPerformancePreviewService`
- `SignalPerformancePreviewService`
- preview result models under `src/MarketAgent.Application/Models`

Dependency usage:

- Reuse `IHistoricalMarketDataService`.
- Reuse `IMarketSignalAnalyzer`.
- Reuse `ITechnicalIndicatorService` indirectly through the analyzer.
- Do not add a historical signal repository yet.
- Do not add database persistence.

## UI/Component Design

Frontend should be small, read-only, and cautious:

- Add API client method for signal performance preview.
- Add frontend types for preview result/items.
- Add a compact component, for example:
  - `MarketAgent.Web/src/components/SignalPerformancePreviewPanel.tsx`

Display:

- Signal family name.
- Sample count.
- 1D / 3D / 5D average forward return.
- 1D / 3D / 5D win rate when sample count is sufficient.
- Insufficient-data badge/message.
- Low sample warning.
- General caution note that samples are reconstructed and educational/diagnostic.

Placement options:

- Small panel below Alert Center and above watchlist/filter controls.
- Or small section inside `SignalDetailPanel` filtered to the selected signal's setup family.

Preferred first placement:

- Dashboard-level compact panel grouped by signal family, because the initial endpoint aggregates by family rather than per selected symbol.

Copy constraints:

- Use language like "reconstructed historical samples" and "forward returns".
- Avoid "backtest", "expected return", "prediction", or "recommendation".
- State that reconstructed samples may differ from real-time signals.
- State that small sample sizes are not statistically reliable.
- State that results are educational/diagnostic, not trading advice.
- State that historical outcomes do not guarantee future performance.

## Files Expected To Change

Expected backend files:

- `src/MarketAgent.Application/Abstractions/ISignalPerformancePreviewService.cs`
- `src/MarketAgent.Application/Models/SignalPerformancePreviewResult.cs`
- `src/MarketAgent.Application/Signals/SignalPerformancePreviewService.cs`
- `src/MarketAgent.Api/Program.cs`
- `tests/MarketAgent.UnitTests/SignalPerformancePreviewServiceTests.cs`

Potential backend files:

- `src/MarketAgent.Application/Abstractions/IHistoricalMarketDataService.cs` only if additional access shape is needed.
- `src/MarketAgent.Application/Models/*` for smaller DTO records.

Expected frontend files:

- `MarketAgent.Web/src/types.ts`
- `MarketAgent.Web/src/api.ts`
- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/styles.css`
- `MarketAgent.Web/src/components/SignalPerformancePreviewPanel.tsx`

Files intentionally not expected to change:

- `TechnicalMarketSignalAnalyzer.cs` scoring behavior.
- `MarketSignal.cs` contract, unless a strictly additive field is later required.
- AI briefing generator.
- Watchlist/filter semantics.
- Any database or persistent repository implementation.

## Risks

- Misleading statistics: small sample sizes can look authoritative. The UI must show sample count, insufficient-data state, and low-sample warnings prominently.
- Reconstructed sample mismatch: results may differ from real-time signals because historical signal events were not persisted.
- Survivorship and watchlist bias: the current tracked universe may not represent the broader market.
- Daily-candle approximation: Opening Red Reversal is intraday, so daily OHLC data may only approximate the pattern.
- No signal persistence: this is a preview reconstructed from candles, not evidence of actual emitted historical signals.
- Lookahead bias: analyzer input must only use candles available up to the candidate date. Future candles must be used only for return measurement.
- Missing candles: holidays, provider failures, unsupported symbols, and partial history must produce null/insufficient data rather than errors.
- Performance cost: iterating analyzer logic across many symbols/days should start with conservative day limits.
- False confidence: UI copy must state that results are educational/diagnostic, not trading advice, and no guarantee of future performance.
