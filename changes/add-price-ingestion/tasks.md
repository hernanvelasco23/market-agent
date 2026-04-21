# Tasks

## Goal

Implement the first daily price ingestion workflow for the initial Market Agent watchlist.

## Tasks

### 1. Define domain primitives

- add `AssetType` enum
- define the initial `MarketSnapshot` entity or model shape
- define any required value objects only if clearly justified

### 2. Define application contracts

- add `IWatchlistProvider`
- add `IMarketDataProvider`
- add `IMarketSnapshotRepository`
- add `IPriceIngestionService`

### 3. Define ingestion result model

- create a result model for ingestion execution summary
- include total requested, succeeded, failed, and failure details

### 4. Implement fixed watchlist provider

- create an initial static watchlist provider
- include the first supported assets:
  - NVDA
  - MSFT
  - AMD
  - SPY
  - BTC
  - ETH
  - MEP

### 5. Implement provider resolution strategy

- decide how the system maps asset types to infrastructure providers
- keep provider-specific logic out of the application layer

### 6. Implement external market data providers

- implement one provider for equity and ETF reference assets
- implement one provider for crypto assets
- implement one provider for MEP
- return normalized raw data ready for mapping into `MarketSnapshot`

### 7. Implement snapshot normalization

- standardize timestamps to UTC
- standardize symbol format
- map provider-specific fields into the internal snapshot shape
- tolerate missing optional fields when data is unavailable

### 8. Implement persistence

- create persistence model and repository implementation
- store one snapshot per successful asset retrieval
- prepare the repository for historical queries later

### 9. Implement price ingestion service

- orchestrate:
  - load watchlist
  - fetch data
  - normalize responses
  - persist snapshots
  - build execution summary

### 10. Implement resilience and logging

- continue processing even if one asset fails
- log failures with symbol and provider context
- include failure details in the execution summary

### 11. Expose execution entry point

- add a manual trigger entry point
- this can be:
  - an application command
  - a minimal API endpoint
  - or a temporary startup/manual execution path

### 12. Add tests

- unit tests for normalization and ingestion orchestration
- unit tests for partial failure behavior
- integration tests for repository persistence if feasible

## Out of scope

The following are intentionally excluded from this change:

- technical signal detection
- AI-generated daily briefing
- retries and backoff strategies
- intraday frequency
- user-defined watchlists
- dashboards
- alert delivery