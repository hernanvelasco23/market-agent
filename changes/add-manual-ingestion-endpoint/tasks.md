# Tasks

## Goal

Add the first visible manual execution path for the ingestion workflow.

## Tasks

### 1. Add endpoint

- expose POST /api/ingestion/run

### 2. Use application service

- inject IPriceIngestionService
- call ExecuteAsync

### 3. Return result

- return PriceIngestionResult as JSON

### 4. Keep API thin

- do not add business logic in the endpoint

### 5. Ensure solution builds

- run dotnet build successfully

## Out of scope

The following are intentionally excluded:

- background scheduling
- authentication
- UI
- signal detection
- AI briefing generation