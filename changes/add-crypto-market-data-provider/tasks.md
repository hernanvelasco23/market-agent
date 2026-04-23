# Tasks

## Goal

Implement a crypto market data provider for the initial watchlist.

## Tasks

### 1. Create provider class

- add CryptoMarketDataProvider in Infrastructure

### 2. Implement IMarketDataProvider

- support crypto assets only
- validate supported symbols

### 3. Integrate external source

- fetch latest crypto market data from a public provider

### 4. Normalize output

Return MarketDataResult with the expected fields.

### 5. Keep the solution building

- ensure dotnet build passes

## Out of scope

The following are intentionally excluded from this change:

- MEP support
- provider registry redesign
- retries
- caching
- scheduling
- AI-generated briefings