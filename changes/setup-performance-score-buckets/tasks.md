# Setup Performance by Confidence and Score Buckets Tasks

## V1 Scope Decisions

- Keep implementation additive.
- Keep calculations in memory for V1.
- Do not modify signal generation.
- Do not modify outcome evaluation.
- Do not modify scanner behavior.
- Reuse existing Signal Outcome and Setup Performance patterns.
- Pending outcomes may contribute to partial averages when checkpoint prices exist.
- Final evaluated outcomes are not required.
- Ignore rows with missing baseline or checkpoint prices for return averages.
- Keep the frontend panel compact and near Setup Performance.

## Backend Tasks

### 1. Add Application models

- Add `SignalOutcomeScoreBucketSummary`.
- Add `SignalOutcomeConfidenceSummaryItem`.
- Add `SignalOutcomeScoreBucketSummaryItem`.
- Include:
  - generated timestamp
  - confidence item collection
  - score bucket item collection

### 2. Extend service abstraction

- Add `GetScoreBucketSummaryAsync` to `ISignalOutcomeService`.
- Accept existing `SignalOutcomeQuery`.
- Keep this additive.

### 3. Implement confidence grouping

- Load outcomes through the existing outcome repository/service query.
- Normalize confidence:
  - trim whitespace
  - group case-insensitively
  - map null/empty to `Unknown`
- Calculate per confidence:
  - count
  - count with 15m
  - average 15m return
  - count with 1h
  - average 1h return
  - best 15m symbol
  - worst 15m symbol
- Use existing return calculation logic where practical.

### 4. Implement score bucket grouping

- Use fixed buckets:
  - `0-20`
  - `21-40`
  - `41-60`
  - `61-80`
  - `81-100`
- Calculate per bucket:
  - count
  - count with 15m
  - average 15m return
  - count with 1h
  - average 1h return
  - best 15m symbol
  - worst 15m symbol
- Decide and document handling for out-of-range scores.
- Preserve score values; do not mutate persisted data.

### 5. Add shared aggregation helper if useful

- Consider a small private helper in `SignalOutcomeService` that accepts:
  - group label
  - outcome rows
  - shape factory or internal aggregate result
- Avoid over-abstracting if direct code is clearer.

### 6. Add API endpoint

- Add:

```text
GET /api/signals/outcomes/score-buckets
```

- Mirror filters from the existing summary endpoints where practical:
  - symbol
  - status
  - isSuccessful
  - days
  - limit

### 7. Add backend tests

- Test confidence grouping.
- Test score bucket grouping.
- Test average 15m return calculation.
- Test average 1h return calculation.
- Test best 15m symbol.
- Test worst 15m symbol.
- Test missing checkpoint values are ignored.
- Test empty/null confidence maps to `Unknown`.
- Test no outcomes returns empty collections.

## Frontend Tasks

### 1. Add types

- Add `SignalOutcomeScoreBucketSummary`.
- Add `SignalOutcomeConfidenceSummaryItem`.
- Add `SignalOutcomeScoreBucketSummaryItem`.

### 2. Add API helper

- Add `loadSignalOutcomeScoreBuckets`.
- Use:

```text
GET /api/signals/outcomes/score-buckets
```

- Do not change existing outcome summary or setup summary helpers.

### 3. Add component

- Add:

```text
MarketAgent.Web/src/components/ScoreConfidencePerformancePanel.tsx
```

- Reuse Signal Outcome and Setup Performance card patterns.
- Display:
  - confidence groups
  - score buckets
  - count
  - avg 15m
  - avg 1h
  - best 15m symbol
  - worst 15m symbol
- Use green for positive returns and red for negative returns.
- Show `n/a` for null averages or missing symbols.
- Label metrics clearly as partial/intraday.

### 4. Wire into `App.tsx`

- Add score bucket summary state.
- Add loading state.
- Add unavailable state.
- Add refresh function.
- Load alongside Signal Outcome Summary and Setup Performance.
- Render near Setup Performance.
- Keep failures isolated from global dashboard status.

### 5. Styling

- Reuse:
  - `performance-preview`
  - `performance-grid`
  - `performance-item`
  - `performance-metric`
- Add scoped class only if needed:
  - `score-confidence-performance`
- Prevent label overflow for long or unexpected group names.
- Keep mobile responsive behavior consistent with existing performance panels.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Frontend:

```text
cd MarketAgent.Web
npm.cmd run build
```

Manual:

- Run backend.
- Run frontend.
- Confirm Signal Outcomes still works.
- Confirm Setup Performance still works.
- Confirm Score & Confidence Performance renders when endpoint succeeds.
- Confirm dashboard still renders when endpoint fails.
- Confirm scanner actions still behave the same.

## Risks

- Low sample counts may make bucket comparisons noisy.
- Score distribution may be uneven across buckets.
- Confidence labels may be sparse.
- Too many cards could crowd the analytics area.
- In-memory aggregation may need SQL optimization later.

## Rollback Plan

Backend:

- Remove score bucket summary models.
- Remove `GetScoreBucketSummaryAsync`.
- Remove score bucket endpoint.
- Remove tests.

Frontend:

- Remove `ScoreConfidencePerformancePanel`.
- Remove score bucket API helper and types.
- Remove score bucket state and loader from `App.tsx`.
- Remove render call.
- Remove optional CSS.

Database:

- No rollback expected because V1 should not add schema.

## V1 UX Decision

Hide empty confidence groups and empty score buckets in the frontend by default.
Only render groups with at least one sample count.

### 8. Add score spread diagnostics

- Compute:
  - top 10 raw score range
  - top 10 calibrated score range
- This helps validate whether calibration improves top candidate separation.

## V1 Safety Decision

Do not modify sorting/ranking behavior anywhere in V1.
Calibration is diagnostics-only until dispersion improvements are reviewed manually.