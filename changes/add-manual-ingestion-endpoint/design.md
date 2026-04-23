# Design

## Overview

This change adds a minimal API endpoint in the API layer to trigger the current ingestion workflow.

## Endpoint

Suggested endpoint:

POST /api/ingestion/run

## Responsibilities

The endpoint should:

- receive no body
- call IPriceIngestionService.ExecuteAsync
- return the PriceIngestionResult
- preserve cancellation token support

## Placement

Suggested placement:

- src/MarketAgent.Api/Controllers/IngestionController.cs

or minimal API mapping in Program.cs if the project currently uses minimal APIs.

## Design principles

- keep controller thin
- no market logic in API
- no provider-specific logic in API
- no scheduling
- no background jobs

## Future extensibility

Later this endpoint may be complemented by:

- background scheduled ingestion
- authentication
- persisted ingestion history
- dashboard display