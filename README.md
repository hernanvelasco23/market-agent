# Market Agent

Market Agent is an AI-powered market monitoring system built with .NET, Semantic Kernel, and Azure OpenAI.

It monitors a curated watchlist of assets such as CEDEAR-related tickers, MEP, and crypto, detects technical signals and anomalies, and generates a grounded daily market briefing with structured reasoning.

## Why this project exists

Most market tools either provide raw prices or generic dashboards. This project focuses on a narrower and more useful workflow:

- monitor a fixed list of assets
- compute transparent signal detectors
- identify notable events
- rank the most relevant findings
- generate a daily briefing that explains what happened and what to watch next

The goal is not to predict markets or automate trading. The goal is to build a practical agentic AI system that combines tool use, orchestration, structured outputs, and external data sources.

## Core capabilities

### 1. Market monitoring

The system tracks a fixed watchlist of selected assets, initially including:

- CEDEAR-related tickers and reference assets
- MEP exchange rate
- crypto assets such as BTC and ETH

### 2. Signal detection

The MVP focuses on transparent and explainable detectors, such as:

- price above or below EMA 9
- short-term breakout or breakdown
- abnormal daily movement
- unusual volume when available
- simple volatility expansion or compression

### 3. Daily AI briefing

After signals are computed, the system uses Semantic Kernel with Azure OpenAI to produce a daily briefing that includes:

- summary of the day
- most relevant assets
- detected signals
- initial interpretation
- risk or confirmation points to watch next

## Tech stack

- **Backend:** .NET / ASP.NET Core
- **AI orchestration:** Semantic Kernel
- **LLM provider:** Azure OpenAI
- **Cloud target:** Azure
- **Database:** SQL Server
- **Scheduling:** .NET BackgroundService
- **Architecture style:** Clean Architecture with spec-driven development

## Engineering approach

This project follows a spec-first workflow inspired by AI-assisted engineering practices.

Each relevant feature starts as a change package with:

- `proposal.md`
- `design.md`
- `tasks.md`

The repository also includes AI collaboration rules under `.ai/` so coding agents can work under shared standards.

## Project structure

- `src/MarketAgent.Api` exposes endpoints and hosts the application
- `src/MarketAgent.Application` contains use cases, orchestration, and abstractions
- `src/MarketAgent.Domain` contains the core business model and signal logic
- `src/MarketAgent.Infrastructure` implements data providers, persistence, Semantic Kernel integration, and background execution
- `specs/` contains product and architecture documentation
- `changes/` contains feature-by-feature spec packages

## Example output

A daily briefing may include sections like:

- market summary
- top monitored assets
- detected technical signals
- contextual interpretation
- next-day watchpoints

## What this project demonstrates

This project is designed as a portfolio piece for agentic AI and backend engineering. It demonstrates:

- external tool use
- orchestrated workflows
- structured AI outputs
- grounded reasoning over deterministic data
- Microsoft ecosystem integration with Azure OpenAI and Semantic Kernel
- spec-driven development with AI coding collaboration

## Current scope

Current demo scope includes:

- market data ingestion
- calibrated signal detection and score attribution
- persisted market snapshots, signal snapshots, signal outcomes, and alert events
- dashboard panels for signals, candidate upside ranking, partial outcome metrics, setup analytics, and score/confidence buckets
- manual and scheduled system cycles
- email alert digest delivery
- Spanish user-facing copy for an Argentina / CEDEAR-oriented audience
- AI briefing generation when Azure OpenAI is configured

Future extensions may include:

- richer technical detectors
- full i18n support
- richer CEDEAR-local market data enrichment
- holiday-aware market-hours scheduling
- historical briefing analysis

## Demo quick start

1. Configure `src/MarketAgent.Api/appsettings.Development.json` or user secrets:
   - `ConnectionStrings:DefaultConnection`
   - optional `AzureOpenAI` values for AI briefings
   - optional `EmailDelivery` SMTP values for email digest delivery
2. Apply EF migrations against SQL Server.
3. Start the API and the Vite frontend.
4. Run a manual cycle with `POST /api/system/run-cycle`, or run the individual endpoints:
   - `POST /api/ingestion/run`
   - `POST /api/signals/run`
   - `POST /api/signals/outcomes/evaluate`
   - `POST /api/alerts/evaluate`
5. Open the dashboard and verify:
   - signals load without a page refresh
   - candidate upside ranking is populated when entry/take-profit data exists
   - outcome/setup/score panels show partial intraday metrics when historical snapshots exist
   - `GET /api/system/status` reports scheduler and email configuration state
6. For email demo, run `POST /api/alerts/deliver/email` after alerts exist.

## Deployment checklist

- Keep secrets in environment variables, Azure App Configuration, Key Vault, or user secrets. Do not commit SMTP passwords, API keys, or production connection strings.
- Set `Cors:AllowedOrigins` to the deployed frontend URL.
- Set `MarketAgentScheduler:Enabled` only when scheduled cycles are intended.
- Use `MarketAgentScheduler:MarketHoursOnly=true` for normal demo operation.
- Configure `EmailDelivery` before enabling scheduled email delivery.
- Confirm pending migrations are applied before starting the production app.
- Verify Swagger is only exposed in Development.

## Disclaimer

This project is for market monitoring and research assistance only. It does not provide investment advice and does not execute trades.
