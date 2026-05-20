# Signal Outcome Dashboard Design

## Current Frontend Findings

- The frontend lives under `MarketAgent.Web`.
- It is a Vite React app using TypeScript.
- Main files:
  - `src/App.tsx` owns dashboard state, actions, and layout.
  - `src/api.ts` centralizes API calls with `getJson` and `postJson`.
  - `src/types.ts` owns API and UI types.
  - `src/styles.css` owns dashboard styling.
- Existing relevant component:
  - `src/components/SignalPerformancePreviewPanel.tsx`
- Existing dashboard layout order:
  - topbar actions
  - status row
  - hero grid
  - signal groups
  - alert center
  - signal performance preview panel
  - watchlist selector
  - filters
  - workspace table/detail
  - briefing lists

## Safest Insertion Point

The safest first insertion point is directly near `SignalPerformancePreviewPanel`, likely immediately before or after it.

Rationale:

- It is already a performance/validation section.
- It uses a full-width card-like section.
- It is separate from the scanner table and filter workflow.
- Adding a sibling component avoids touching table rows, signal detail, alerts, or watchlist logic.

Recommended placement:

```tsx
<AlertCenter alerts={alerts} onSelectSymbol={setSelectedSymbol} />

<SignalOutcomeSummaryPanel summary={outcomeSummary} unavailable={outcomeSummaryUnavailable} />

<SignalPerformancePreviewPanel preview={performancePreview} unavailable={performancePreviewUnavailable} />
```

Alternative placement:

- After `SignalPerformancePreviewPanel` if we want reconstructed backtest preview to remain above real outcome tracking.

## API Design

Add a new API helper in `src/api.ts`:

```ts
export async function loadSignalOutcomeSummary(): Promise<SignalOutcomeSummary> {
  return getJson<SignalOutcomeSummary>("/api/signals/outcomes/summary");
}
```

Add a type in `src/types.ts`:

```ts
export interface SignalOutcomeSummary {
  generatedAtUtc: string;
  totalCount: number;
  evaluatedCount: number;
  pendingCount: number;
  countWith15m: number;
  averageReturn15m?: number | null;
  countWith1h: number;
  averageReturn1h?: number | null;
  best15mSymbol?: string | null;
  best15mReturnPercent?: number | null;
  worst15mSymbol?: string | null;
  worst15mReturnPercent?: number | null;
}
```

The backend exposes more fields, but V1 frontend should only model fields it displays plus harmless context fields.

## State Design

In `App.tsx`, add:

```ts
const [outcomeSummary, setOutcomeSummary] = useState<SignalOutcomeSummary | null>(null);
const [outcomeSummaryUnavailable, setOutcomeSummaryUnavailable] = useState(false);
```

Add a loader similar to `refreshPerformancePreview`:

```ts
async function refreshOutcomeSummary() {
  try {
    const summary = await loadSignalOutcomeSummary();
    setOutcomeSummary(summary);
    setOutcomeSummaryUnavailable(false);
  } catch {
    setOutcomeSummary(null);
    setOutcomeSummaryUnavailable(true);
  }
}
```

Load it in `refreshDashboard` using existing `Promise.all` pattern:

```ts
await Promise.all([
  refreshSparklines(),
  refreshPerformancePreview(),
  refreshOutcomeSummary()
]);
```

Also refresh it after `handleRunSignals` and `handleGenerateBriefing` because those paths currently refresh performance data. Optionally refresh after `handleRunIngestion`, since ingestion creates future snapshots that may affect partial outcomes after the evaluator has run.

## Component Design

Add a new component:

```text
src/components/SignalOutcomeSummaryPanel.tsx
```

Suggested display:

- Card title: `Signal Outcomes`
- Small note: `Persisted signal outcomes from emitted signals. Partial returns update before 1D outcomes close.`
- Metrics:
  - total outcomes
  - pending outcomes
  - 15m coverage and average return
  - 1h coverage and average return
  - best 15m
  - worst 15m

Use existing visual language:

- `card`
- `performance-preview`
- `performance-grid`
- `performance-item`
- `performance-metric`
- positive/negative tone classes from `SignalPerformancePreviewPanel` patterns

Potential structure:

```tsx
<section className="card performance-preview outcome-summary">
  <div className="card-title">...</div>
  <p className="performance-note">...</p>
  <div className="performance-grid">...</div>
</section>
```

This avoids creating a new visual system.

## Formatting Rules

- Percent values should use two decimals and `%`.
- Missing values should render as `n/a`.
- Positive returns should use existing positive styling.
- Negative returns should use existing negative styling.
- Counts should be plain integers.
- If `best15mSymbol` and `worst15mSymbol` are the same, show both as-is; this can be correct with a single sample.

## Empty/Unavailable States

If endpoint fails:

- Render the card with a small warning:
  - `Outcome summary unavailable. The dashboard can still be used normally.`

If endpoint succeeds but `totalCount === 0`:

- Render:
  - `No persisted outcomes yet. Run signals, ingestion, and outcome evaluation to populate this section.`

If partial counts are zero:

- Show `0` count and `n/a` average.

## Risks

- New state in `App.tsx` can add clutter; keep it parallel to `performancePreview`.
- Endpoint failure should not change global dashboard status to error during refresh.
- Reusing `performance-preview` classes may visually blur reconstructed preview vs actual outcome tracking; clear title and note are important.
- Calling summary on every refresh adds one API request, but payload should be small.

## Rollback Plan

Rollback is isolated:

- Delete `SignalOutcomeSummaryPanel.tsx`.
- Remove `SignalOutcomeSummary` type.
- Remove `loadSignalOutcomeSummary`.
- Remove `outcomeSummary` state and loader from `App.tsx`.
- Remove panel render line.
- Remove any optional CSS additions.
