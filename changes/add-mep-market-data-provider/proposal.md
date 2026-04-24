# Proposal

## Title

Add MEP market data provider

## Summary

Introduce a market data provider for the MEP exchange rate in the initial watchlist.

## Problem

The ingestion workflow currently supports equity, ETF, and crypto assets, but MEP still fails because no provider exists for exchange-rate assets.

## Goals

- support the MEP asset in the ingestion pipeline
- implement IMarketDataProvider for AssetType.ExchangeRate
- return normalized MarketDataResult values
- complete the initial watchlist ingestion with 10/10 successful assets

## Non-goals

This change does not include:

- historical MEP data
- CCL data
- provider fallback
- retries
- caching
- SQL Server persistence

## Expected outcome

After this change, the ingestion endpoint should process the full initial watchlist successfully, including MEP.