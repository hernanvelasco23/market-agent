# Setup Performance Analytics Tasks

## Backend Tasks

## V1 Scope Decisions

- Limit displayed setup rows to top 5 by count or average return.
- Best/worst setup ranking requires minimum sample size >= 3.
- Setup grouping is case-insensitive.
- Null/empty setup names map to "Unknown".
- Pending outcomes contribute to partial averages if checkpoint prices exist.
- Final evaluated outcomes are not required for setup analytics.
- Keep all calculations in-memory for V1.

### 1. Add Application models

- Add `SignalOutcomeSetupSummary`.
- Add `SignalOutcomeSetupSummaryItem`.
- Include:
  - generated timestamp
  - total setup count
  - best setup
  - best setup average 15m return
  - worst setup
  - worst setup average 15m return
  - item collection

### 2. Extend service abstraction

- Add `GetSetupSummaryAsync` to `ISignalOutcomeService`.
- Accept the existing `SignalOutcomeQuery`.
- Keep this additive.

### 3. Implement setup grouping

- Load outcomes through existing repository query.
- Group by setup.
- Calculate:
  - count
  - count with 15m
  - average 15m return
  - count with 1h
  - average 1h return
  - count with 4h
  - average 4h return
- Calculate best/worst setup by average 15m return.
- Ignore rows with missing baseline or checkpoint for return averages.
- Do not modify outcome evaluation.
- Do not mark outcomes as evaluated.

### Normalize setup names

- Trim setup names.
- Group case-insensitively.
- Map empty/null values to "Unknown".

### 4. Add API endpoint

- Add:

```text
GET /api/signals/outcomes/setup-summary
```

- Mirror filters from the existing summary endpoint where practical:
  - symbol
  - status
  - isSuccessful
  - days
  - limit

### 5. Add backend tests

- Test grouping by setup.
- Test average 15m return calculation.
- Test average 1h return calculation.
- Test best setup.
- Test worst setup.
- Test missing checkpoint values are ignored.
- Test no outcomes returns empty summary.

## Frontend Tasks

### UX safeguards

- Show sample count beside averages.
- Label all metrics as partial/intraday.
- Prevent layout overflow when setup names are long.

### 1. Add types

- Add `SignalOutcomeSetupSummary`.
- Add `SignalOutcomeSetupSummaryItem`.

### 2. Add API helper

- Add `loadSignalOutcomeSetupSummary`.
- Use `GET /api/signals/outcomes/setup-summary`.
- Do not change existing outcome summary helper.

### 3. Add component

- Add `SetupPerformancePanel`.
- Reuse Signal Outcome card/panel patterns.
- Display:
  - best setup
  - worst setup
  - top setup rows
  - count
  - avg 15m
  - avg 1h
- Use green for positive returns and red for negative returns.
- Show `n/a` for null averages.

### 4. Wire into `App.tsx`

- Add setup summary state.
- Add unavailable/loading state.
- Add refresh function.
- Load alongside Signal Outcome Summary.
- Render near Signal Outcome Summary.
- Keep failures isolated from global dashboard status.

### 5. Styling

- Reuse:
  - `performance-preview`
  - `performance-grid`
  - `performance-item`
  - `performance-metric`
- Add scoped class only if needed:
  - `setup-performance`

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
- Confirm Signal Outcomes section still works.
- Confirm Setup Performance section displays data when endpoint succeeds.
- Confirm dashboard still renders when endpoint fails.
- Confirm existing scanner actions still behave the same.

## Risks

- Setup averages may be misleading with low counts.
- UI may become crowded if too many setup rows are displayed.
- Backend in-memory grouping may need SQL aggregation later.
- Endpoint failures should not break dashboard refresh.
- Existing setup labels may be inconsistent.

## Rollback Plan

Backend:

- Remove setup summary models.
- Remove `GetSetupSummaryAsync`.
- Remove endpoint.
- Remove tests.

Frontend:

- Remove `SetupPerformancePanel`.
- Remove setup summary API helper and types.
- Remove setup summary state and loader from `App.tsx`.
- Remove render call.
- Remove optional CSS.

Database:

- No rollback expected because V1 should not add schema.
