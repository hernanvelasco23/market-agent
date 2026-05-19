# Add Opening Red Reversal Signal Tasks

## Goal

Detect, score, expose, and display a deterministic Opening Red Reversal signal with minimal score impact and safe null handling.

## Incremental Implementation Tasks

### 1. Confirm data semantics

- Identify where `OpenPrice`, `PreviousClose`, `LowPrice`, `Price`, `Volume`, and `AverageVolume20` are available in `TechnicalMarketSignalAnalyzer`.
- Confirm current `RecoveryFromLowPercent` is range-position based and decide whether to add a separate true low recovery field or preserve the existing contract.
- Document the chosen field naming in code through clear method names and tests.

### 2. Add domain/API fields

- Extend `MarketSignal` with additive properties:
  - `OpeningRedReversalDetected`
  - `OpenGapPercent`
  - `ReclaimOpen`
  - `ReclaimPreviousClose`
- Add the requested recovery-from-low exposure carefully:
  - prefer a distinct field if preserving existing `RecoveryFromLowPercent` semantics;
  - only reuse `RecoveryFromLowPercent` if intentionally changing the existing contract.
- Update constructor arguments and property assignments.
- Keep all new numeric fields nullable where denominators can be missing or zero.

### 3. Implement detection helper

- Add a small private helper in `TechnicalMarketSignalAnalyzer`.
- Calculate:
  - `OpenGapPercent`
  - true low recovery percent
  - `ReclaimOpen`
  - `ReclaimPreviousClose`
- Require positive non-zero denominators.
- Require `RelativeVolume >= 1.5`.
- Return a small result record to keep `AnalyzeLatest` readable.

### 4. Apply small scoring bonus

- Add `+6` when Opening Red Reversal is detected.
- Add `+4` when previous close is reclaimed.
- Add score breakdown labels for both.
- Apply bonus in a deterministic location before final classification.
- Do not add early clamping.
- Preserve final score clamp to `0..100`.

### 5. Update briefing/dashboard models

- Add the new additive fields to `MarketBriefingAllSignalItem` and any other briefing item type used by dashboard sections if needed.
- Ensure mappings from `MarketSignal` populate the new fields.
- Avoid AI-generated signal interpretation.
- Do not change endpoint routes.

### 6. Update frontend types and rendering

- Add optional fields to `ApiMarketSignal` and `DashboardSignal`.
- Add a subtle `ORR` badge or flag in the all-signals table.
- Add a subtle badge and supporting metrics in `SignalDetailPanel`.
- Render missing values as `n/a`.
- Keep dark mode readable and dashboard layout stable.

### 7. Add regression tests

- Opens red and recovers above open.
- Opens red and reclaims previous close.
- Opens red but fails to reclaim open.
- Opens green should not trigger.
- Zero previous close should not crash.
- Missing or zero low should not crash.
- Missing or zero average volume should not trigger.
- Reversal bonus does not push score above 100.
- Score breakdown includes the reversal factors only when triggered.

## Validation/Build Steps

- Run `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`.
- Run `dotnet build MarketAgent.sln --no-restore`.
- Run `npm.cmd run build` from `MarketAgent.Web`.

## Manual QA Checklist

- Run signals with data that includes a known opening red reversal candidate.
- Confirm the table shows the subtle ORR badge only for detected signals.
- Select a detected signal and confirm the detail panel shows:
  - Open gap
  - Recovery from low
  - Reclaim open
  - Reclaim previous close
- Confirm non-detected signals do not show a misleading badge.
- Confirm missing values render as `n/a`.
- Confirm scores do not broadly saturate at 100.
- Confirm score breakdown shows no duplicate or oversized reversal bonus.
- Confirm existing RS, RVOL, EXT, sparkline, and detail panel behavior still works.

## Rollback Considerations

- This is an additive feature, but it touches signal model constructor signatures and mappings.
- Rollback requires removing the new fields from domain/frontend models, analyzer helper/scoring, UI badges, and tests.
- Because no route or persistence change is planned, rollback should not require database or API route migration.
