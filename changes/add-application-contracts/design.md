# Design

## Overview

This change introduces the core application abstractions required by the price ingestion workflow.

The goal is to define contracts in the application layer before any infrastructure implementation is added.

This keeps orchestration logic independent from:

- market data providers
- persistence details
- scheduling details
- external APIs

## Contracts to introduce

### IWatchlistProvider

Provides the list of tracked assets for the ingestion flow.

Responsibilities:

- return the configured watchlist
- abstract away where the watchlist comes from

Initial implementations may use a fixed static list.

### IMarketDataProvider

Provides normalized market data retrieval for a specific asset or symbol.

Responsibilities:

- fetch the latest market data
- expose a provider-specific abstraction at the application boundary

This interface should remain simple in the MVP.

### IMarketSnapshotRepository

Persists normalized market snapshots.

Responsibilities:

- store snapshots
- prepare the system for future historical queries

This interface should not expose database-specific concerns.

### IPriceIngestionService

Coordinates the end-to-end ingestion flow.

Responsibilities:

- load watchlist
- obtain market data
- normalize results if needed
- persist snapshots
- return execution summary

## Supporting models

This change may also introduce lightweight application models for:

- tracked asset definition
- ingestion execution summary
- ingestion failure detail
- market data retrieval result

These models should live in the application layer and remain infrastructure-agnostic.

## Design principles

- interfaces belong to the application layer
- infrastructure will implement these contracts later
- keep signatures simple and async-friendly
- do not leak HTTP, SQL, or Azure concerns into contracts
- avoid premature abstraction beyond current workflow needs

## Folder placement

Suggested placement inside `src/MarketAgent.Application`:

- `Abstractions/`
- `MarketData/`
- `PriceIngestion/`
- `Models/` if needed

The exact folder layout can stay lightweight as long as naming is clear.

## Initial simplifications

To keep scope controlled:

- no retries
- no pagination
- no batching abstraction
- no provider registry yet unless clearly needed
- no user-specific watchlists

## Future extensibility

These contracts should later support:

- multiple market data providers
- richer snapshot fields
- ingestion summary reporting
- scheduled execution
- integration with signal detection