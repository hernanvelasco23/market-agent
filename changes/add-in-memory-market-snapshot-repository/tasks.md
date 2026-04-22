# Tasks

## Goal

Implement an in-memory repository for MarketSnapshot persistence.

## Tasks

### 1. Create repository class

- add InMemoryMarketSnapshotRepository in Infrastructure

### 2. Implement IMarketSnapshotRepository

- implement the current application contract
- keep the implementation simple and compilable

### 3. Add in-memory storage

- use a private in-memory collection to store snapshots
- preserve inserted snapshots during process lifetime

### 4. Keep architecture clean

- place implementation only in Infrastructure
- avoid leaking infrastructure concerns into Domain or Application

### 5. Ensure solution builds

- run dotnet build successfully

### 6. Add tests if appropriate

- add simple tests only if the current contract makes them clearly useful

## Out of scope

The following are intentionally excluded from this change:

- SQL Server persistence
- Entity Framework Core
- migrations
- ingestion orchestration
- signal detection
- AI-generated briefings