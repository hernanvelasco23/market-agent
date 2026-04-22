# Design

## Overview

This change adds a simple in-memory implementation of IMarketSnapshotRepository in the Infrastructure layer.

The repository will store MarketSnapshot instances in a private in-memory collection for the lifetime of the application process.

## Responsibilities

- accept MarketSnapshot instances from the application layer
- store snapshots in memory
- expose only the contract required by IMarketSnapshotRepository

## Placement

The implementation should live inside:

- src/MarketAgent.Infrastructure/Persistence/

Suggested class name:

- InMemoryMarketSnapshotRepository

## Design principles

- keep the implementation minimal
- do not introduce database concerns
- do not introduce Entity Framework
- keep the repository suitable for local development and early integration testing
- respect clean architecture boundaries

## Storage model

The repository may use:

- a private List<MarketSnapshot>
- or another simple in-memory collection if clearly justified

The collection should remain internal to the repository implementation.

## Concurrency

The MVP can keep concurrency handling simple.

A lightweight lock is acceptable if needed, but avoid overengineering.

## Initial simplifications

- no persistence across process restarts
- no query API yet unless required by the existing contract
- no pagination
- no deduplication logic
- no indexing

## Future extensibility

This repository is a temporary implementation that will later be replaced or complemented by a SQL Server implementation.

The key goal is to unblock the ingestion service while preserving the repository abstraction.