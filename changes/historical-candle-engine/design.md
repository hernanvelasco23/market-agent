# Technical Design

## New Components

### Domain
- MarketCandle

### Application
- IHistoricalMarketDataProvider
- ITechnicalIndicatorService

### Infrastructure
- HistoricalMarketDataProvider
- TechnicalIndicatorService

## Indicators

The following indicators will be calculated:
- EMA9
- EMA20
- EMA50
- RSI14
- ATR14
- AverageVolume10
- AverageVolume20

## Signal Engine Changes

TechnicalMarketSignalAnalyzer will:
- use historical candles,
- calculate indicators,
- classify setups,
- and apply market regime filters.

## Architecture Notes

- Business logic stays outside API layer.
- Providers remain abstracted through interfaces.
- Signal engine remains independent from AI generation.