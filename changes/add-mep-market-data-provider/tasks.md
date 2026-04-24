# Tasks

## Goal

Implement a MEP market data provider for the initial watchlist.

## Tasks

### 1. Create provider class

- add MepMarketDataProvider in Infrastructure

### 2. Implement IMarketDataProvider

- support AssetType.ExchangeRate only
- support MEP symbol only

### 3. Integrate public source

- fetch latest MEP value from a public API

### 4. Normalize output

Return MarketDataResult with:

- Symbol = MEP
- AssetType = ExchangeRate
- Price
- Currency = ARS
- CapturedAtUtc
- Source

### 5. Update dependency injection

- register MepMarketDataProvider so provider routing can resolve it

### 6. Keep solution building

- ensure dotnet build passes

## Out of scope

The following are intentionally excluded:

- CCL support
- official exchange rate support
- historical FX
- retries
- caching
- SQL Server
- AI-generated briefings