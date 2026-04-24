# Proposal

## Title

Add market data provider routing

## Summary

Introduce provider routing so the ingestion workflow can select the correct market data provider for each tracked asset.

## Problem

The ingestion workflow currently uses a single IMarketDataProvider for every asset. This causes crypto and exchange-rate assets to be sent to the equity provider.

## Goals

- support multiple market data providers
- route assets by supported type or symbol
- update PriceIngestionService to use provider routing
- wire equity and crypto providers through dependency injection
- return clear failures when no provider exists

## Non-goals

This change does not include:

- MEP provider implementation
- SQL Server persistence
- retries
- background scheduling
- signal detection
- AI briefing generation

## Expected outcome

The ingestion workflow should process equity, ETF, and crypto assets with the correct provider while returning a clear failure for unsupported assets such as MEP.