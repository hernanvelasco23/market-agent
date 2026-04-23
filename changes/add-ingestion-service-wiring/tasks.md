# Tasks

## Goal

Wire the existing ingestion components into the application host using dependency injection.

## Tasks

### 1. Register watchlist provider

- register StaticWatchlistProvider as IWatchlistProvider

### 2. Register market data provider

- register EquityMarketDataProvider as IMarketDataProvider
- configure HttpClient registration for the provider

### 3. Register snapshot repository

- register InMemoryMarketSnapshotRepository as IMarketSnapshotRepository

### 4. Register ingestion service

- register PriceIngestionService as IPriceIngestionService

### 5. Keep the host clean

- keep registration explicit
- avoid adding unrelated runtime behavior

### 6. Ensure solution builds

- run dotnet build successfully

## Out of scope

The following are intentionally excluded from this change:

- actually triggering ingestion at startup
- API endpoints
- background jobs
- crypto provider wiring
- MEP provider wiring
- SQL Server
- briefing generation