# Design

## Overview

This change adds dependency injection wiring for the current ingestion workflow components.

The goal is not to add new behavior, but to connect the existing pieces into a resolvable runtime graph.

## Components to wire

The following services should be registered:

- IWatchlistProvider -> StaticWatchlistProvider
- IMarketDataProvider -> EquityMarketDataProvider
- IMarketSnapshotRepository -> InMemoryMarketSnapshotRepository
- IPriceIngestionService -> PriceIngestionService

## HttpClient

EquityMarketDataProvider depends on HttpClient.

Use the built-in HttpClient registration pattern so the provider can be constructed cleanly through dependency injection.

## Placement

Wiring should be added in the application host startup composition.

Suggested location:

- src/MarketAgent.Api/Program.cs

If helpful, registration may be extracted into an Infrastructure or Application service collection extension method, but only if it keeps the wiring clearer and still simple.

## Design principles

- keep composition root concerns in the host layer
- do not move business logic into Program.cs
- keep registrations explicit
- avoid premature modularization if the registration list remains small

## Initial simplifications

- single market data provider registration
- no provider selection registry yet
- no hosted service execution yet
- no configuration binding yet unless required for build clarity

## Future extensibility

This wiring should later support:

- additional market data providers
- repository replacement with SQL Server
- background execution
- manual execution endpoints
- runtime configuration