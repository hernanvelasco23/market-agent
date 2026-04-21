# Architecture

## Overview

Market Agent is designed as a layered .NET application that combines deterministic market signal detection with AI-generated daily briefings.

The architecture separates:

- market data ingestion
- signal computation
- AI orchestration
- persistence
- delivery

This separation is intentional. Market logic must remain deterministic and auditable, while AI is used only to synthesize and explain already computed findings.

## Architectural style

The system follows clean architecture principles:

- Domain contains core market concepts and signal logic.
- Application coordinates use cases and abstractions.
- Infrastructure implements external integrations.
- API exposes entry points and hosts the application.

## Project structure

### `src/MarketAgent.Api`

Responsibilities:

- host the application
- expose HTTP endpoints
- configure dependency injection
- configure middleware
- trigger hosted services when needed

This layer must remain thin and should not contain market logic or AI prompting logic.

### `src/MarketAgent.Application`

Responsibilities:

- define use cases
- orchestrate workflows
- define interfaces for providers and services
- coordinate signal generation and briefing generation

Examples of responsibilities:

- run daily market briefing flow
- request market snapshots from providers
- invoke signal detectors
- call briefing service
- persist results

### `src/MarketAgent.Domain`

Responsibilities:

- define entities
- define value objects
- define enums
- hold deterministic signal logic
- enforce business rules

Examples of domain concepts:

- Asset
- MarketSnapshot
- SignalDetection
- DailyBriefing
- SignalType
- TechnicalCondition

The domain layer must not depend on Azure, Semantic Kernel, SQL Server, or HTTP APIs.

### `src/MarketAgent.Infrastructure`

Responsibilities:

- implement market data providers
- implement persistence
- implement Azure OpenAI integration
- implement Semantic Kernel orchestration support
- implement background jobs
- implement repositories and external clients

Examples:

- Yahoo Finance or other market data provider client
- MEP data provider client
- SQL Server repositories
- Semantic Kernel briefing service
- scheduled job runner

### `src/MarketAgent.Shared`

Responsibilities:

- cross-cutting shared primitives
- result types
- constants
- lightweight abstractions shared across layers when justified

## Core workflow

The main daily workflow is:

1. load configured watchlist
2. retrieve latest market data from external providers
3. normalize and store snapshots
4. run deterministic signal detectors
5. rank relevant findings
6. send structured findings to the briefing service
7. generate daily AI briefing
8. persist briefing and related signal results
9. expose results through API or future delivery channels

## AI architecture

AI is not embedded directly in the domain.

The application layer should depend on an abstraction such as:

- `IBriefingGenerator`
- or `IMarketBriefingService`

The infrastructure layer will provide the implementation using:

- Semantic Kernel
- Azure OpenAI

This keeps the LLM provider replaceable and prevents leakage of infrastructure concerns into core logic.

## Signal architecture

Signal detection should be rule-based and transparent.

Recommended shape:

- one detector per signal type
- shared interface such as `ISignalDetector`
- each detector receives normalized market data
- each detector returns structured findings, not prose

Example outputs may include:

- signal type
- asset
- severity
- confidence based on deterministic conditions
- supporting metrics
- timestamp

The briefing layer can later transform these findings into natural language.

## Persistence model

The system should persist at least:

- tracked assets
- market snapshots
- detected signals
- generated briefings
- briefing-to-signal relationships

This allows:

- traceability
- debugging
- historical analysis
- future dashboards

## Scheduling

The MVP will use a .NET `BackgroundService` to run scheduled workflows.

This keeps the first version simple while remaining compatible with future migration to:

- Azure Functions
- Azure WebJobs
- Azure Container Apps jobs
- external schedulers

## Configuration

Configuration should be externalized through app settings and environment variables.

Examples:

- Azure OpenAI endpoint
- Azure OpenAI deployment name
- SQL Server connection string
- market data provider settings
- watchlist settings
- schedule settings

Secrets must never be hardcoded.

## Testing strategy

### Unit tests

Focus on:

- signal detectors
- domain rules
- ranking logic
- briefing input builders

### Integration tests

Focus on:

- provider integrations
- persistence
- end-to-end application flows

### Architecture tests

Focus on:

- layer boundaries
- forbidden references
- dependency direction

## Key design principles

- deterministic first
- AI for synthesis, not fact generation
- external integrations isolated behind interfaces
- small, composable services
- auditable outputs
- production-style structure even for a portfolio project

## Future extensibility

The current architecture should support future additions such as:

- richer signal detectors
- multiple briefing modes
- configurable watchlists
- email delivery
- dashboards
- historical trend analysis
- contextual enrichment with news or macro data