# Tasks

## Goal

Implement the first real equity market data provider.

## Tasks

### 1. Create provider class

- add EquityMarketDataProvider in Infrastructure

### 2. Implement IMarketDataProvider

- support equity and ETF assets

### 3. Integrate external source

- fetch latest market data

### 4. Normalize output

Return:

- Symbol
- AssetType
- Price
- Currency
- CapturedAtUtc
- Source

### 5. Validate unsupported symbols

Return safe failures or exceptions as appropriate.

### 6. Ensure solution builds

- dotnet build passes