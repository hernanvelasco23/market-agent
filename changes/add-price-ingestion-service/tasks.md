# Tasks

## Goal

Implement the first application service that orchestrates the price ingestion workflow.

## Tasks

### 1. Create service class

- add PriceIngestionService in the Application layer

### 2. Implement IPriceIngestionService

- implement the current application contract
- keep the service asynchronous and explicit

### 3. Load watchlist

- use IWatchlistProvider to retrieve tracked assets

### 4. Fetch market data

- use IMarketDataProvider to retrieve latest data for each asset
- process assets sequentially for now

### 5. Map market data to snapshots

- create MarketSnapshot instances from MarketDataResult

### 6. Persist snapshots

- use IMarketSnapshotRepository to save successful snapshots

### 7. Handle partial failures

- capture failures in PriceIngestionFailure
- continue processing remaining assets

### 8. Return execution summary

- build and return PriceIngestionResult

### 9. Keep architecture clean

- do not add infrastructure code into Application
- do not add API endpoints
- do not add scheduling

### 10. Ensure solution builds

- run dotnet build successfully

### 11. Add tests if appropriate

- add simple unit tests for orchestration behavior if clearly useful

## Out of scope

The following are intentionally excluded from this change:

- provider selection strategies for multiple providers
- crypto ingestion
- MEP ingestion
- database persistence
- background execution
- signal detection
- AI-generated briefings