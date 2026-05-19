# Add Alert Engine

## Problem

The dashboard now exposes richer signal context such as score, setup, confidence, RS vs SPY, RVOL, EMA20 extension, sparklines, a signal detail panel, and Opening Red Reversal flags. Users can inspect this information, but they still have to manually scan the table and detail view to notice the most important opportunities and risks.

There is no in-app alert center that summarizes why a symbol deserves attention right now.

## Goal

Add an internal Alert Center to the dashboard that derives explainable in-app alerts from existing signal data.

This feature is limited to deterministic, frontend-visible alerts. It is not an external notification system and does not change scoring behavior.

## User Value

- Helps users prioritize the symbols that need attention first.
- Makes opportunity and risk conditions easier to spot without reading every metric column.
- Explains each alert through the exact metrics that triggered it.
- Keeps the dashboard useful during fast scans while preserving detailed signal inspection.
- Creates a foundation for future notification channels without introducing them now.

## Scope

- Add frontend-derived alerts from existing `DashboardSignal` data.
- Support initial alert types:
  - Momentum Breakout
  - Opening Red Reversal
  - EMA Reclaim, only if available data makes the rule safe
  - Overextended Warning
  - Momentum Failure / Risk
- Add severity values:
  - `info`
  - `opportunity`
  - `warning`
  - `risk`
- Each alert should include:
  - symbol
  - title
  - description
  - severity
  - related setup/action when available
  - key metrics used to trigger it
- Add a compact Alert Center section to the existing dashboard.
- Keep alerts deterministic, explainable, and intentionally low-noise.
- Preserve current dark mode and dashboard visual style.

## Out of Scope

- No email, push, Telegram, Discord, Slack, or webhook notifications.
- No background jobs.
- No alert persistence.
- No user-configurable alert rules yet.
- No new dependencies.
- No charting library changes.
- No dashboard rewrite.
- No scoring changes.
- No API route changes unless implementation discovers an unavoidable data gap.
- No AI-generated alert explanations.

## Success Criteria

- Alerts are derived from existing signal data without changing backend scoring.
- Alert rules are centralized and easy to audit.
- Missing/null fields do not create false positives.
- Alerts show the triggering metrics clearly.
- Opening Red Reversal alerts reuse existing reversal fields.
- Overextended warnings trigger from EMA20 extension greater than `7%`.
- Momentum failure/risk alerts do not require unavailable historical state.
- EMA Reclaim is either implemented conservatively with available evidence or documented as limited until previous-state data exists.
- The dashboard remains readable and not noisy.
- `npm.cmd run build` passes.
