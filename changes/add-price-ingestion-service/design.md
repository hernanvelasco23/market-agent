# Design

## Overview

This change adds the first implementation of IPriceIngestionService inside the Application layer.

The service will orchestrate the ingestion workflow using existing abstractions:

- IWatchlistProvider
- IMarketDataProvider
- IMarketSnapshotRepository

The service must remain infrastructure-agnostic and rely only on contracts.

## Responsibilities

The service will:

1. load the watchlist
2. iterate through tracked assets
3. request latest market data
4. map market data into MarketSnapshot
5. persist snapshots
6. capture failures without failing the whole run
7. return an execution summary

## Placement

Suggested placement:

- src/MarketAgent.Application/PriceIngestion/

Suggested class name:

- PriceIngestionService

## Provider usage

For this MVP, the service may use a single injected IMarketDataProvider if the currently supported watchlist is compatible with that provider.

If provider selection logic becomes necessary later, it should be introduced as a separate abstraction or strategy.

## Mapping

The service should map MarketDataResult into MarketSnapshot.

Expected fields include:

- Symbol
- AssetType
- Price
- Currency
- CapturedAtUtc
- Source
- Volume
- OpenPrice
- HighPrice
- LowPrice

## Failure handling

The ingestion flow must be resilient.

Rules:

- one failed asset must not fail the whole ingestion run
- failures must be captured in PriceIngestionFailure
- successful assets must still be persisted

## Execution result

The service should return PriceIngestionResult including:

- total assets processed
- successful ingestions
- failed ingestions
- failure details

## Design principles

- orchestration belongs in Application
- no HTTP code in Application
- no SQL code in Application
- keep the workflow simple and explicit
- avoid premature abstractions beyond current needs

## Initial simplifications

- no retries
- no batching
- no parallel execution
- no logging abstraction unless already required
- no provider registry yet unless current contracts force it

## Future extensibility

The service should later support:

- multiple provider selection by asset type
- crypto and MEP ingestion
- background scheduling
- richer execution summaries
- alerting and briefing generation