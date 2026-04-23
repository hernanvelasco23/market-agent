# Design

## Overview

This change adds a dedicated Infrastructure provider for crypto assets.

The provider will implement IMarketDataProvider and return normalized MarketDataResult values for supported crypto symbols.

## Responsibilities

- validate supported crypto symbols
- fetch latest crypto market data from a public source
- normalize provider data into MarketDataResult
- remain isolated from Application and Domain concerns beyond the existing contracts

## Placement

Suggested location:

- src/MarketAgent.Infrastructure/MarketData/

Suggested type:

- CryptoMarketDataProvider

## Supported assets

Initial support:

- BTC
- ETH

## Design principles

- keep provider-specific logic in Infrastructure
- keep symbol mapping explicit when useful
- use simple parsing and normalization
- avoid redesigning existing abstractions

## Initial simplifications

- support only BTC and ETH
- no retries
- no batching
- no caching
- no provider registry changes unless required

## Future extensibility

This provider should later support:

- more crypto assets
- retry policies
- provider fallback
- richer market fields if available