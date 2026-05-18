# Market Signal Engine Design

## Architecture

Signal analysis lives behind `IMarketSignalAnalyzer` in the Application layer and returns Domain `MarketSignal` objects. The API calls an application service and does not contain scoring logic.

Briefing generation receives both snapshots and calculated signals. The Semantic Kernel prompt instructs AI to treat calculated signals as the primary input, explain the provided data, and avoid inventing prices, indicators, targets, stops, or recommendations.

The briefing response includes structured fields for:

- market regime
- signal summary
- top opportunities
- watchlist pullbacks
- top risks
- highlights
- risks
- watch items

Signal sections are bucketed deterministically from calculated signals:

- top opportunities: bullish signals with score >= 55
- watchlist pullbacks: pullback setups with score >= 40 and < 55
- top risks: weak/risky assets with score < 40

Risk items do not expose entry, stop, or target fields.

Every signal includes timeframe and confidence metadata. Intraday opportunity observations with score >= 60 near the session high are Medium confidence. Watchlist pullbacks and risks are Low confidence and WatchOnly.

## Scoring Inputs

The analyzer uses only available snapshot fields:

- current price
- open price
- high price
- low price
- previous close
- historical snapshot prices when enough data exists for RSI

RSI remains null unless at least 15 ordered snapshots are available for a symbol.

## Endpoint

`POST /api/signals/run` returns a generated timestamp and structured signal collection.
