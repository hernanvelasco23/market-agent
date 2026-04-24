# Design

## Overview

This feature adds a simple read path for market snapshots.

The current in-memory repository can store snapshots, but it does not expose any read method yet.

## Contract change

Update `IMarketSnapshotRepository` with a read method.

Suggested method:

- `Task<IReadOnlyCollection<MarketSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)`

## Infrastructure

Update `InMemoryMarketSnapshotRepository` to return a snapshot copy of the internal collection.

Avoid exposing the mutable internal list.

## API endpoint

Add endpoint:

GET /api/ingestion/snapshots

The endpoint should:

- call the repository read method
- return stored snapshots as JSON
- keep API logic thin

## Design principles

- keep this as a demo/read-only endpoint
- avoid adding filtering or pagination for now
- avoid introducing DTOs unless clearly needed
- do not add database persistence yet

## Future extensibility

Later this endpoint may evolve into:

- filtered queries by symbol
- latest snapshot per asset
- persisted historical queries
- dashboard backend