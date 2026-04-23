# Proposal

## Title

Add ingestion service wiring

## Summary

Register the ingestion-related services in the application host so the first end-to-end ingestion workflow can be resolved through dependency injection.

This change connects the existing application and infrastructure pieces without introducing new business behavior.

## Problem

The project already contains the required ingestion components:

- PriceIngestionService
- StaticWatchlistProvider
- EquityMarketDataProvider
- InMemoryMarketSnapshotRepository

However, these components are not yet wired into the application host, so the ingestion workflow cannot be resolved and executed as a composed system.

## Goals

- register ingestion-related services in dependency injection
- register HttpClient for EquityMarketDataProvider
- keep wiring simple and explicit
- prepare the application for manual execution of the ingestion flow

## Non-goals

This change does not include:

- background scheduling
- API endpoints
- crypto provider wiring
- MEP provider wiring
- SQL Server persistence
- signal detection
- AI-generated briefings

## Expected outcome

After this change, the application host should be able to resolve IPriceIngestionService and its current dependencies successfully.