# Tasks

## Goal

Define the application-layer contracts needed to support the price ingestion workflow.

## Tasks

### 1. Add watchlist contract

- create `IWatchlistProvider`
- define the method required to retrieve tracked assets
- keep the interface simple and async-friendly

### 2. Add market data provider contract

- create `IMarketDataProvider`
- define the method required to retrieve latest market data for an asset
- keep the interface independent from concrete APIs

### 3. Add snapshot repository contract

- create `IMarketSnapshotRepository`
- define the method required to persist market snapshots
- avoid database-specific details in the interface

### 4. Add ingestion orchestration contract

- create `IPriceIngestionService`
- define the method required to execute the ingestion workflow
- return an execution summary model

### 5. Add supporting models if needed

- tracked asset model
- ingestion execution summary
- ingestion failure detail
- provider result model

Only add the minimum required to support clean contracts.

### 6. Keep the solution compilable

- place all interfaces in the application project
- align namespaces with project structure
- ensure `dotnet build` passes

## Out of scope

The following are intentionally excluded from this change:

- concrete providers
- repository implementations
- SQL Server code
- API endpoints
- background jobs
- signal detection
- AI-generated briefings