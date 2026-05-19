# Add Opening Red Reversal Signal Design

## Current Architecture Findings

- Signal logic lives in `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`.
- Signal output is represented by `src/MarketAgent.Domain/Entities/MarketSignal.cs`.
- API endpoints remain thin in `src/MarketAgent.Api/Program.cs`; `POST /api/signals/run` returns `MarketSignalRunResult`.
- AI briefing output models live in `src/MarketAgent.Application/Models/MarketBriefingResult.cs`.
- The dashboard consumes signals through `MarketAgent.Web/src/api.ts` and maps them into `DashboardSignal` in `MarketAgent.Web/src/types.ts`.
- The all-signals table is in `MarketAgent.Web/src/App.tsx`.
- The detail panel is now isolated in `MarketAgent.Web/src/components/SignalDetailPanel.tsx`.
- The reusable sparkline component is in `MarketAgent.Web/src/components/Sparkline.tsx`.
- Existing analyzer tests live in `tests/MarketAgent.UnitTests/TechnicalMarketSignalAnalyzerTests.cs`.

## Existing Signal Scoring Flow

- `TechnicalMarketSignalAnalyzer` starts score at `50`.
- It adds deterministic score factors for prior-close move, intraday move, range position, recovery, gap recovery, indicator state, extension risk, market regime, intraday weakness, relative strength, momentum continuation, and high-tight consolidation.
- Recent score-inflation fixes made EMA slope thresholds stricter and prevented strong EMA slope from stacking with individual slope bonuses.
- `ScoreBreakdown` is the source of explainability and should include any Opening Red Reversal bonus.
- The score is clamped to `0..100` near the end of analysis; the new feature must not introduce earlier clamping or cumulative inflation.

## Existing RS/RVOL/EXT Implementation

- `RelativeStrengthVsSpy` is calculated as asset percent change minus SPY percent change.
- `RelativeVolume` is calculated as current snapshot volume divided by `AverageVolume20`.
- `ExtensionFromEma20Percent` is exposed as `DistanceFromEma20Percent`.
- Null and zero denominators currently produce `null` for RS/RVOL/EXT rather than false positives.
- Opening Red Reversal should reuse the existing `relativeVolume` value after it is calculated, or the same calculation rules, to avoid duplicated volume semantics.

## Existing Sparkline and Detail View Integration

- `Sparkline` is generic and accepts only prices, width, height, and optional trend styling.
- `SignalDetailPanel` receives `DashboardSignal | null` and optional sparkline prices.
- The new reversal flag should be displayed as subtle metadata in the table and detail panel.
- No sparkline behavior is required for detection; it remains a visual aid only.

## Detection Calculations

Use decimal arithmetic and percent-point values:

```text
OpenGapPercent = ((OpenPrice - PreviousClose) / PreviousClose) * 100
RecoveryFromLowPercent = ((CurrentPrice - IntradayLow) / IntradayLow) * 100
ReclaimOpen = CurrentPrice >= OpenPrice
ReclaimPreviousClose = CurrentPrice >= PreviousClose
RelativeVolume = CurrentVolume / AverageVolume20
```

Important existing naming issue:

- Current `MarketSignal.RecoveryFromLowPercent` is populated from `rangePosition * 100`.
- The requested Opening Red Reversal definition uses true percent change from intraday low.
- Implementation should avoid silently changing the meaning of the existing field unless that contract is intentionally updated.
- Preferred approach: keep existing `RecoveryFromLowPercent` behavior stable for backward compatibility and add a new internally named calculation for true low recovery, then expose it as part of the Opening Red Reversal fields only if the API contract is updated accordingly.

## Initial Detection Rule

Opening Red Reversal is detected when all are true:

- `OpenGapPercent < -0.5`
- true low recovery percent is `>= 1.5`
- `CurrentPrice >= OpenPrice`
- `RelativeVolume >= 1.5`

Strong version:

- all initial rule criteria
- `ReclaimPreviousClose == true`

## Scoring Design

- Add `+6` for `OpeningRedReversalDetected`.
- Add `+4` extra if `ReclaimPreviousClose` is true.
- Add factors such as:
  - `Opening red reversal`
  - `Reclaimed previous close after red open`
- Keep these bonuses after core intraday/range/gap calculations but before final setup/action classification.
- Do not let the reversal bonus bypass risk filters or extension penalties.
- Do not make this signal create a high-confidence top opportunity by itself.
- Keep final clamping to `0..100`.

## API/DTO Impact

This feature requires additive fields so the UI can display the signal:

Backend domain/API serialized signal:

- `OpeningRedReversalDetected: bool`
- `OpenGapPercent: decimal?`
- `RecoveryFromLowPercent: decimal?` or a clearly named separate field if preserving the existing range-position meaning
- `ReclaimOpen: bool`
- `ReclaimPreviousClose: bool`

Frontend:

- Add optional fields to `DashboardSignal` and `ApiMarketSignal`.
- Show values safely with `n/a` when absent.

Briefing models:

- If briefing uses `MarketBriefingAllSignalItem` as the dashboard source, add the fields there too.
- Avoid adding the fields to AI prompts unless the briefing generator already mechanically maps all signal fields. The signal should remain deterministic, not AI-created.

API routes:

- No endpoint route changes.
- All changes should be additive and backward-compatible.

## Component/UI Design

All-signals table:

- Add a subtle badge column or compact flag.
- Suggested label: `ORR`.
- Only show the badge when `OpeningRedReversalDetected` is true.
- Keep the table readable and horizontally scrollable.

Signal detail panel:

- Add a small `Opening Red Reversal` badge near action/setup metadata.
- Add a small metric group or row for:
  - Open gap
  - Recovery from low
  - Reclaim open
  - Reclaim previous close
- Do not redesign the panel.

Styling:

- Use existing pill/chip style conventions.
- Keep dark mode readable.
- Make the badge subtle so it explains the setup without dominating score, action, or risk context.

## Files Expected To Change

Backend:

- `src/MarketAgent.Domain/Entities/MarketSignal.cs`
- `src/MarketAgent.Application/Signals/TechnicalMarketSignalAnalyzer.cs`
- `src/MarketAgent.Application/Models/MarketBriefingResult.cs`
- Any mapper/service that creates briefing signal items from `MarketSignal`
- `tests/MarketAgent.UnitTests/TechnicalMarketSignalAnalyzerTests.cs`

Frontend:

- `MarketAgent.Web/src/types.ts`
- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/components/SignalDetailPanel.tsx`
- `MarketAgent.Web/src/styles.css`
- `MarketAgent.Web/src/api.ts` only if mock data is updated to preview the new badge.

Likely not needed:

- `MarketAgent.Web/src/components/Sparkline.tsx`
- `src/MarketAgent.Api/Program.cs`
- Provider/infrastructure code
- New dependencies

## Testing Strategy

Add analyzer tests for:

- Opens red and recovers above open.
- Opens red and reclaims previous close.
- Opens red but fails to reclaim open.
- Opens green should not trigger.
- Zero/missing previous close or low should not crash.
- Missing or zero `AverageVolume20` should not trigger.
- Reversal bonus does not push score above 100.
- Score breakdown contains the new factors only when appropriate.

Frontend validation:

- `npm.cmd run build`.
- Confirm table badge renders only when flag is true.
- Confirm detail panel renders true/false/null values safely.

Backend validation:

- `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`.
- `dotnet build MarketAgent.sln --no-restore` if not blocked by running API processes.

## Risks

- Score inflation: this pattern can overlap with existing recovery, gap recovery, volume, and intraday strength bonuses. Keep the bonus small and test realistic overlap cases.
- Percentage scale confusion: current code uses percent points for many values, while current `RecoveryFromLowPercent` naming does not match the requested true low recovery formula.
- Contract confusion: changing the meaning of an existing `RecoveryFromLowPercent` field could break UI interpretation.
- False positives: default or missing previous close, open, low, or average volume must not produce a reversal.
- UI density: adding another table signal must remain subtle and not crowd already dense columns.
