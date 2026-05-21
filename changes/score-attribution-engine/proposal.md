# Score Attribution Engine

## Problem

MarketAgent already exposes `scoreBreakdown` factors, but many generated signals are reaching score `100`, especially `MomentumContinuation`. When several signals saturate at the cap, the final score alone no longer explains why one signal ranked highly or which factors drove the result.

The system needs clearer score attribution metadata so each persisted signal can explain:

- where the score started
- which factors added points
- which factors subtracted points
- whether the score was capped
- which factor dominated the final score

## Goal

Add backend score attribution metadata for generated and persisted signals.

Expected output for a signal:

```json
{
  "baseScore": 50,
  "finalScore": 100,
  "wasCapped": true,
  "dominantPositiveFactor": "RelativeStrengthVsSpy",
  "dominantNegativeFactor": "OverextensionRisk",
  "positiveContributions": [
    {
      "factor": "RelativeStrengthVsSpy",
      "points": 20,
      "reason": "Outperformed SPY intraday"
    },
    {
      "factor": "PositiveEmaStack",
      "points": 15,
      "reason": "Price above EMA9/20/50"
    }
  ],
  "negativeContributions": [
    {
      "factor": "OverextensionRisk",
      "points": -5,
      "reason": "RSI elevated"
    }
  ]
}
```

## V1 Scope

- Backend only.
- Add explainability, not recalibration.
- Do not change scoring behavior.
- Do not change alert rules.
- Do not modify frontend.
- Prefer capturing attribution at signal generation time.
- Persist or expose attribution for `SignalSnapshots`.

## Proposed API

Preferred:

```text
GET /api/signals/{signalSnapshotId}/score-attribution
```

Alternative:

- Include attribution in an existing or future signal snapshot detail endpoint.

The dedicated endpoint is cleaner for V1 because it avoids widening existing dashboard responses.

## Existing System Fit

MarketAgent already has:

- `MarketSignal.Score`
- `MarketSignal.ScoreBreakdown`
- `PersistedSignalSnapshot.ScoreBreakdownJson`
- persisted `SignalSnapshots`
- persisted `SignalOutcomes`
- score/confidence analytics
- persisted `AlertEvents`
- `ReasonJson` for alerts

V1 should reuse the existing score breakdown as much as possible, then add richer attribution metadata where needed.

## User Value

- Explains why scores hit `100`.
- Makes score saturation visible through `wasCapped`.
- Identifies dominant positive and negative drivers.
- Supports future score recalibration work with evidence.
- Helps debug alert decisions without changing alert thresholds in V1.

## Out of Scope

- No score recalibration.
- No changes to `MomentumContinuation` scoring.
- No alert rule changes.
- No frontend panel.
- No ML feature importance.
- No normalized score-factor table in V1 unless needed.
- No migration of old historical rows required in V1.

## Success Criteria

- New signals can expose structured attribution metadata.
- Persisted signal snapshots can return attribution by `SignalSnapshotId`.
- `wasCapped` is true when the uncapped score exceeded the final score cap.
- Positive and negative contributions are separated.
- Dominant factor is computed consistently.
- Existing score values and existing signal generation behavior remain unchanged.
- Existing tests keep passing.

## Risks

- Existing score flow clamps partway through the analyzer, so exact uncapped score may require careful capture.
- Current `ScoreBreakdown` labels are human-readable labels, not stable factor IDs.
- Adding detailed attribution directly inside the analyzer could make already complex scoring logic harder to maintain.
- Historical rows may only have legacy `ScoreBreakdownJson`, not full attribution metadata.

## Rollback Plan

- Remove attribution models and endpoint.
- Remove `ScoreAttributionJson` persistence if added.
- Remove related service/repository methods.
- Remove migration if a new column is added.
- Leave existing `ScoreBreakdownJson`, scoring behavior, alerts, outcomes, and dashboard untouched.
