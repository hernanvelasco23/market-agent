# Design

## Overview

This change adds a dedicated Infrastructure provider for the MEP exchange rate.

The provider will implement IMarketDataProvider and return a normalized MarketDataResult for the MEP symbol.

## Responsibilities

- validate exchange-rate asset type
- support the MEP symbol
- fetch latest MEP value from a public source
- normalize provider response into MarketDataResult
- remain isolated in Infrastructure

## Placement

Suggested location:

- src/MarketAgent.Infrastructure/MarketData/

Suggested type:

- MepMarketDataProvider

## Supported asset

Initial support:

- MEP

## Design principles

- keep provider-specific logic in Infrastructure
- keep symbol mapping explicit
- keep parsing logic isolated
- avoid redesigning provider routing

## Initial simplifications

- support only MEP
- no historical data
- no CCL
- no retries
- no caching

## Future extensibility

This provider may later support:

- CCL
- official exchange rate
- historical FX rates
- provider fallback
- richer FX metadata