# Proposal

## Title

Add equity market data provider

## Summary

Introduce the first real external market data provider for equity and ETF assets.

This provider will retrieve latest price data for supported symbols in the watchlist and return normalized application models.

## Goals

- support equity and ETF assets
- retrieve real external market prices
- integrate with IMarketDataProvider
- prepare the ingestion pipeline

## Initial supported symbols

- NVDA
- MSFT
- AMD
- SPY
- MELI
- TSLA
- NU

## Non-goals

- crypto data
- MEP data
- retries
- advanced caching
- full ingestion workflow