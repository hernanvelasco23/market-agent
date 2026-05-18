# Market Signal Engine Tasks

- [x] Add `MarketSignal` domain model and signal type enum.
- [x] Add signal analyzer abstraction.
- [x] Implement technical signal analyzer using available snapshot data.
- [x] Add application service for running signal analysis.
- [x] Add `POST /api/signals/run`.
- [x] Pass calculated signals to AI briefing generation.
- [x] Add unit tests for bullish, risk, missing RSI, and scoring consistency cases.
- [x] Verify `dotnet build` and `dotnet test`.
- [x] Promote calculated signals to primary AI briefing input.
- [x] Add structured briefing fields for market regime, signal summary, top opportunities, and top risks.
- [x] Split briefing signals into opportunities, watchlist pullbacks, and risks.
- [x] Remove entry, stop, and target from risk items.
- [x] Add action semantics to calculated signals and briefing signal sections.
- [x] Add timeframe and confidence metadata to signals and briefing signal sections.
