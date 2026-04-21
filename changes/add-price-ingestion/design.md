# Design

## Overview

The price ingestion workflow will retrieve daily market data for a fixed watchlist, transform external provider responses into a normalized internal model, and persist the resulting market snapshots.

The design prioritizes:

- simplicity
- replaceable providers
- traceable historical data
- clean separation between application logic and integrations

## Workflow

The daily ingestion flow will perform the following steps:

1. load configured watchlist
2. resolve the correct provider for each asset type
3. fetch latest market price data
4. normalize provider responses
5. validate required fields
6. persist market snapshots
7. return execution summary

## Supported asset categories

The initial version supports:

- Equity / ETF references (NVDA, MSFT, AMD, SPY)
- Crypto assets (BTC, ETH)
- MEP exchange rate

## Internal model

### MarketSnapshot

Represents the normalized stored market state for one asset at one point in time.

Suggested fields:

- Id
- Symbol
- AssetType
- Price
- Currency
- CapturedAtUtc
- Source
- Volume (optional)
- OpenPrice (optional)
- HighPrice (optional)
- LowPrice (optional)
- PreviousClose (optional)

## Application abstractions

### IWatchlistProvider

Returns the configured list of tracked assets.

### IMarketDataProvider

General contract for retrieving market data.

Methods may include:

- GetSnapshotAsync(symbol)

### IMarketSnapshotRepository

Persists normalized snapshots.

### IPriceIngestionService

Coordinates the end-to-end ingestion flow.

## Provider strategy

Different providers may be used depending on asset category.

Examples:

- equities / ETFs provider
- crypto provider
- MEP provider

The application layer should not know provider-specific details.

Use adapters in Infrastructure.

## Normalization rules

All providers must be converted to a common internal format.

Examples:

- timestamps converted to UTC
- decimals normalized
- symbol casing standardized
- null optional fields accepted when unavailable

## Persistence strategy

Each successful retrieval stores a new MarketSnapshot row.

This creates a historical timeline and enables:

- future signal detection
- debugging
- backfills
- historical briefing analysis

## Error handling

The ingestion workflow must be resilient.

Rules:

- one failed asset should not fail the full run
- partial failures should be reported
- errors must be logged with provider and symbol context

## Execution result

The ingestion run should return a summary such as:

- total assets requested
- successful ingestions
- failed ingestions
- failure reasons
- execution timestamp

## Scheduling

The first version will run once daily through .NET BackgroundService or manual trigger.

## Layer responsibilities

### Domain

Contains:

- AssetType enum
- MarketSnapshot entity rules if needed

### Application

Contains:

- orchestration service
- interfaces
- validation
- execution summary

### Infrastructure

Contains:

- external provider clients
- repositories
- SQL Server persistence

### API

Contains:

- manual trigger endpoint (optional)
- health endpoints

## Initial simplifications

To keep scope small:

- no retries in first version
- no streaming
- no batching optimization
- no parallel complexity unless needed
- no user-custom watchlists

## Future extensibility

The design should later support:

- additional providers
- richer fields
- intraday runs
- configurable watchlists
- retry policies
- caching