# Tasks

## Goal

Add market data provider routing so ingestion can use the correct provider for each asset type.

## Tasks

### 1. Update market data provider contract

- add a provider capability method such as CanHandle(TrackedAsset asset)
- update existing providers to implement it

### 2. Add provider resolver contract

- create IMarketDataProviderResolver in Application

### 3. Implement provider resolver

- create MarketDataProviderResolver in Infrastructure
- use injected providers to find one that can handle the asset
- fail clearly when no provider exists

### 4. Update PriceIngestionService

- inject IMarketDataProviderResolver instead of a single IMarketDataProvider
- resolve provider per asset
- keep partial failure handling

### 5. Update dependency injection

- register equity provider
- register crypto provider
- register provider resolver
- keep HttpClient registration valid

### 6. Keep MEP unsupported for now

- MEP should return a clear ingestion failure until a MEP provider is implemented

### 7. Ensure solution builds

- run dotnet build successfully

## Out of scope

The following are intentionally excluded:

- MEP provider implementation
- retries
- caching
- SQL Server
- background jobs
- AI-generated briefings