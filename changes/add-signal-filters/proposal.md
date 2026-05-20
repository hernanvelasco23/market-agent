# Add Signal Filters

## Problem

The signal dashboard now combines ranked signals, sparklines, a detail panel, Opening Red Reversal flags, and an internal Alert Center. As the number of signals grows, users need a faster way to focus on the setups that matter for the current session.

Without filtering and sorting, users must manually scan a dense table to find high-RS names, unusual volume, risk-only signals, overextended setups, or Opening Red Reversal candidates.

## Goal

Add lightweight frontend filtering and sorting capabilities to the signal dashboard using signal data already loaded in memory.

The first version should improve scanning without changing backend APIs, scoring behavior, or dashboard architecture.

## User Value

- Quickly focus on opportunities, risks, and specific setup types.
- Sort signals by the metrics users already care about: score, RS, RVOL, EXT, or symbol.
- Reduce table noise while keeping the full signal list available.
- Make the existing Alert Center and detail panel easier to use because selecting from a filtered list is faster.
- Preserve the current explainable scanner workflow without adding server-side complexity.

## Scope

- Add frontend-only filtering over `briefing.allSignals`.
- Add a compact filter bar above the all-signals table.
- Support initial filters:
  - setup type
  - score thresholds
  - RS thresholds
  - RVOL thresholds
  - risk-only
  - opportunity-only
  - overextended-only
  - Opening Red Reversal only
- Support sorting:
  - score descending
  - RS descending
  - RVOL descending
  - EXT descending
  - alphabetical
- Add a clear/reset filters action.
- Keep filter controls compact, readable, and consistent with the dark dashboard style.
- Reuse existing signal fields and existing UI patterns.

## Out of Scope

- No server-side filtering.
- No backend query infrastructure.
- No API contract changes.
- No scoring changes.
- No saved filters.
- No user accounts.
- No advanced query language.
- No Redux/global state overhaul.
- No new dependencies.
- No dashboard rewrite.

## Success Criteria

- Users can filter and sort the all-signals table without reloading data.
- Filters use only already-loaded `DashboardSignal` data.
- Clearing filters restores the full signal list with the default sort.
- Null/missing RS, RVOL, EXT, or ORR fields are handled safely.
- The selected signal remains valid when filters change, or falls back to the first visible signal.
- The Alert Center remains derived from the full signal set unless intentionally changed later.
- The UI remains compact and readable in dark mode.
- `npm.cmd run build` passes.
