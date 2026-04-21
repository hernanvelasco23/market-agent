# AGENTS

## Mission

Build Market Agent as a production-style portfolio project that demonstrates agentic AI workflows in the Microsoft ecosystem.

The project must combine:

- real backend engineering
- external data integration
- deterministic market logic
- AI orchestration with Semantic Kernel
- grounded daily briefings
- professional software delivery standards

## Non-negotiables

- Use English in code, names, comments, commits, and technical documents.
- Respect clean architecture boundaries.
- Keep controllers thin.
- Keep business logic out of API and infrastructure layers.
- Prefer small, reviewable changes.
- Every non-trivial feature must start inside `changes/` with:
  - proposal.md
  - design.md
  - tasks.md
- Do not invent APIs, tables, contracts, or providers without documenting them first.
- Add tests for critical signal logic.
- Do not hardcode secrets, keys, or endpoints.
- Use dependency injection consistently.
- Prefer explicit and readable code over clever abstractions.

## Product philosophy

Market Agent is **not**:

- a chatbot
- a trading bot
- a prediction engine
- a random indicator playground

Market Agent **is**:

- a focused market monitoring system
- a signal detection engine
- a daily market briefing assistant
- an example of practical agentic AI

## AI usage philosophy

AI must be used for:

- summarization
- prioritization
- explanation
- synthesis
- daily briefing generation

AI must **not** be the source of truth for market signals.

Signals must come from deterministic logic, rules, and verified data.

## Architecture principles

- Domain layer must not depend on Azure, Semantic Kernel, SQL Server, or APIs.
- Application layer coordinates use cases and abstractions.
- Infrastructure implements providers and integrations.
- API layer exposes endpoints only.
- Keep LLM providers behind interfaces.

## Delivery style

- Break work into small steps.
- Explain tradeoffs when making design choices.
- Prefer maintainability over premature optimization.
- Keep classes focused and cohesive.
- Name things clearly.

## Commit style

Use conventional commits when possible:

- feat:
- fix:
- docs:
- refactor:
- test:
- chore:

## Long-term goal

Build a portfolio-quality system that can be confidently presented in interviews as a serious AI engineering project.

## Repository bootstrap guardrails

Before adding any C# production code:

- A valid .NET solution file (`.sln`) must exist.
- Each source layer must be a real `.csproj` project.
- Project references must reflect intended architecture boundaries.
- The repository must build successfully before adding feature code.

Do not create standalone `.cs` files inside `src/` without an associated project.

If the solution does not exist yet, prioritize bootstrapping the .NET structure first.