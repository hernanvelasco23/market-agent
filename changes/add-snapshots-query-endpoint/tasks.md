# Tasks

## Goal

Add a simple API read path for market snapshots.

## Tasks

### 1. Update repository contract

- add `GetAllAsync` to `IMarketSnapshotRepository`

### 2. Update in-memory implementation

- return a read-only copy of stored snapshots
- keep internal collection private

### 3. Add API endpoint

- expose `GET /api/ingestion/snapshots`

### 4. Keep API thin

- no business logic in the endpoint

### 5. Ensure solution builds

- run `dotnet build`

## Out of scope

The following are intentionally excluded:

- SQL Server
- pagination
- filtering
- authentication
- dashboards
- briefing generation