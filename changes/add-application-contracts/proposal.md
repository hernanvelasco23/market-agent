# Proposal

## Title

Add application contracts for the price ingestion workflow

## Summary

Introduce the application-layer interfaces required to support the first market price ingestion workflow.

These contracts define the boundaries between the application layer and future infrastructure implementations such as watchlist loading, external market data retrieval, snapshot persistence, and ingestion orchestration.

## Problem

The project already has initial domain primitives, but the ingestion workflow still lacks application-level abstractions.

Without clear contracts, future implementation work risks coupling business flow directly to infrastructure concerns.

## Goals

- define application-layer contracts for price ingestion
- establish clear boundaries between orchestration and implementation details
- prepare the codebase for infrastructure providers and persistence
- keep the application layer independent from concrete external services

## Non-goals

This change does not include:

- provider implementations
- SQL Server repository implementations
- HTTP clients
- background scheduling
- signal detection
- AI-generated briefings

## Expected outcome

After this change, the application layer should expose the interfaces needed to implement the ingestion flow cleanly in later features.

These contracts will become the foundation for:

- fixed watchlist loading
- market data retrieval
- snapshot persistence
- ingestion workflow orchestration