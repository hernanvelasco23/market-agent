# Market Signal Engine

## Goal

Add deterministic market signal calculation before AI briefing generation so Market Agent can explain computed signals instead of relying on generic AI summaries.

## Scope

- Add a domain model for market signals.
- Add an application analyzer that derives signals from available market snapshots.
- Expose a signal generation endpoint.
- Pass calculated signals into AI briefing generation.
- Add unit tests for core scoring behavior.

## Non-goals

- Do not add trading execution.
- Do not invent indicators when historical data is unavailable.
- Do not persist signals beyond the existing in-memory workflow.
