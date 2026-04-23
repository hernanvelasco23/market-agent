# Proposal

## Title

Add crypto market data provider

## Summary

Introduce a market data provider for crypto assets in the initial watchlist.

This provider will retrieve latest price data for supported crypto symbols and return normalized market data results.

## Problem

The current ingestion flow supports only equity and ETF assets.

The initial watchlist also includes crypto assets, so the system still lacks full coverage of the intended monitored assets.

## Goals

- support crypto assets in the ingestion pipeline
- implement IMarketDataProvider for BTC and ETH
- return normalized MarketDataResult values
- keep the implementation simple and isolated in Infrastructure

## Non-goals

This change does not include:

- MEP data
- retries
- caching
- advanced error handling
- provider selection strategy redesign

## Expected outcome

After this change, the project should support retrieving latest market data for BTC and ETH through a dedicated crypto provider.