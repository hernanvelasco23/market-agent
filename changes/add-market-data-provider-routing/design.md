# Design

## Overview

This change adds a routing layer between PriceIngestionService and the available market data providers.

Instead of injecting a single IMarketDataProvider directly into PriceIngestionService, the service will depend on a resolver that selects the correct provider for each asset.

## New abstraction

Add an application contract:

- IMarketDataProviderResolver

Suggested method:

- Resolve(TrackedAsset asset)

The resolver should return the provider capable of handling the asset or fail clearly when no provider is available.

## Provider capability

Each IMarketDataProvider should expose whether it can handle a given asset.

Suggested method:

- CanHandle(TrackedAsset asset)

This allows routing to stay simple and avoids hardcoding provider-specific logic in the ingestion service.

## Updated ingestion flow

PriceIngestionService should:

1. load tracked assets
2. resolve provider for each asset
3. fetch latest market data using selected provider
4. map result into MarketSnapshot
5. persist snapshot
6. capture provider resolution failures as ingestion failures

## Infrastructure implementation

Add:

- MarketDataProviderResolver

Suggested location:

- src/MarketAgent.Infrastructure/MarketData/

The resolver can receive IEnumerable<IMarketDataProvider> through dependency injection.

## Dependency injection

Register:

- EquityMarketDataProvider
- CryptoMarketDataProvider
- MarketDataProviderResolver

Use HttpClient registration for providers that need it.

## Design principles

- keep provider selection out of PriceIngestionService
- keep provider-specific logic in Infrastructure
- keep Application dependent only on abstractions
- keep failure behavior explicit and easy to debug

## Initial simplifications

- no provider priority
- no fallback provider
- no MEP provider yet
- no caching
- no retry logic

## Future extensibility

This routing layer should later support:

- MEP provider
- fallback providers
- provider priority
- richer capability checks