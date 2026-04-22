# Proposal

## Title

Add price ingestion service

## Summary

Introduce the first application service that orchestrates the end-to-end price ingestion flow for the initial watchlist.

This service will coordinate watchlist retrieval, market data fetching, snapshot creation, persistence, and execution summary reporting.

## Problem

The project already has:

- domain primitives
- application contracts
- a fixed watchlist provider
- an equity market data provider
- an in-memory snapshot repository

However, there is still no service that coordinates these pieces into a single ingestion workflow.

## Goals

- implement IPriceIngestionService
- execute the first end-to-end ingestion flow
- persist generated market snapshots
- return a useful execution summary
- keep orchestration inside the application layer

## Non-goals

This change does not include:

- crypto ingestion
- MEP ingestion
- background scheduling
- API endpoints
- SQL Server persistence
- signal detection
- AI-generated briefings

## Expected outcome

After this change, the system should be able to run an end-to-end ingestion flow for the currently supported watchlist assets and persist snapshots through the configured repository.