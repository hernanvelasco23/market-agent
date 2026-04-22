# Design

## Overview

The provider will implement IMarketDataProvider for equity and ETF symbols.

It will call an external public data source, map results into MarketDataResult, and remain isolated inside Infrastructure.

## Responsibilities

- validate supported symbols
- fetch latest price
- normalize values
- return MarketDataResult

## Architecture

Application depends on IMarketDataProvider.

Infrastructure provides:

EquityMarketDataProvider

## Initial simplifications

- one provider only
- no retry policy
- one request per symbol if needed
- minimal logging

## Future extensions

- batching
- retries
- caching
- multiple providers
- fallback sources

## Maintainability expectations

- isolate provider-specific mapping logic
- keep parsing logic explicit
- centralize external format assumptions