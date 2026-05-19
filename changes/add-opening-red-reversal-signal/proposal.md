# Add Opening Red Reversal Signal

## Problem

The current signal engine recognizes broad intraday strength, gap-down recovery, recovery from the session low, relative strength, relative volume, momentum continuation, and extension risk. It does not explicitly identify the common intraday pattern where a symbol opens red versus the previous close, sells off, then recovers above the open and sometimes reclaims the previous close.

Because this pattern is currently folded into generic recovery and gap logic, the dashboard cannot clearly explain when a selected symbol is showing an opening red reversal.

## Goal

Detect and expose an `OpeningRedReversal` signal using deterministic intraday data and existing volume context, while keeping the score impact small and preventing score inflation.

## User Value

- Users can quickly spot symbols that opened weak but attracted buyers intraday.
- The dashboard can explain why a symbol has constructive reversal characteristics.
- The signal remains auditable because the UI exposes the exact supporting fields.
- The feature improves scanner explainability without adding AI-generated assumptions.

## Scope

- Add deterministic detection in the current signal analyzer.
- Calculate and expose:
  - `OpeningRedReversalDetected`
  - `OpenGapPercent`
  - `RecoveryFromLowPercent`
  - `ReclaimOpen`
  - `ReclaimPreviousClose`
- Add a small scoring bonus:
  - `+6` for opening red reversal.
  - `+4` extra when previous close is reclaimed.
- Add score breakdown entries for the new bonus.
- Add subtle UI badges/flags in the all-signals table and signal detail panel.
- Add regression tests for detection, non-detection, null/zero safety, and score cap behavior.

## Out of Scope

- No new dependencies.
- No dashboard redesign.
- No new charting behavior.
- No trading execution or recommendation workflow.
- No AI call or AI-generated explanation.
- No broad scoring rewrite.
- No change to endpoint routes.
- No change to existing signal labels unless directly required to expose this flag.

## Success Criteria

- A symbol opening red, recovering at least `1.5%` from intraday low, reclaiming open, and trading at least `1.5x` average 20-day volume is flagged.
- A stronger case also flags `ReclaimPreviousClose`.
- Opens green do not trigger.
- Red opens that fail to reclaim open do not trigger.
- Zero or missing previous close, open, low, or volume denominator does not throw and does not create false positives.
- The new bonus is visible in `scoreBreakdown`.
- The new bonus remains small and does not by itself make a symbol top-ranked.
- Scores remain clamped only at the final stage and cannot exceed 100.
- `dotnet test` and frontend build pass after implementation.
