# Setup Performance by Confidence and Score Buckets Design

## Current Architecture Context

Backend:

- Signal snapshots are persisted in SQL Server as `SignalSnapshots`.
- Signal outcomes are persisted in `SignalOutcomes`.
- Existing outcome analytics endpoints:
  - `GET /api/signals/outcomes/summary`
  - `GET /api/signals/outcomes/setup-summary`
- `SignalOutcomeService` already computes partial return aggregates in memory.
- `SignalOutcomeItem` already includes signal metadata such as symbol, setup, action, score, confidence, baseline price, and checkpoint prices.

Frontend:

- `MarketAgent.Web/src/api.ts` contains API helpers.
- `MarketAgent.Web/src/types.ts` contains response types.
- `MarketAgent.Web/src/App.tsx` owns dashboard data loading and renders additive panels.
- `SignalOutcomeSummaryPanel` and `SetupPerformancePanel` provide the visual pattern to reuse.
- Existing performance card CSS supports compact metric grids and positive/negative return coloring.

## Backend Design

Add a new additive API endpoint:

```text
GET /api/signals/outcomes/score-buckets
```

Recommended Application models:

```csharp
public sealed record SignalOutcomeScoreBucketSummary(
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<SignalOutcomeConfidenceSummaryItem> ConfidenceItems,
    IReadOnlyCollection<SignalOutcomeScoreBucketSummaryItem> ScoreBucketItems);

public sealed record SignalOutcomeConfidenceSummaryItem(
    string Confidence,
    int Count,
    int CountWith15m,
    decimal? AverageReturn15m,
    int CountWith1h,
    decimal? AverageReturn1h,
    string? BestSymbol15m,
    string? WorstSymbol15m);

public sealed record SignalOutcomeScoreBucketSummaryItem(
    string Bucket,
    decimal MinScore,
    decimal MaxScore,
    int Count,
    int CountWith15m,
    decimal? AverageReturn15m,
    int CountWith1h,
    decimal? AverageReturn1h,
    string? BestSymbol15m,
    string? WorstSymbol15m);
```

The exact model names can follow existing naming conventions, but the endpoint should clearly separate confidence grouping from score bucket grouping.

## Return Calculation

Use the same partial return formula as Signal Outcome Summary:

```text
returnPct = ((checkpointPrice - priceAtSignal) / priceAtSignal) * 100
```

Use only rows where:

- `PriceAtSignal > 0`
- checkpoint price is non-null

For `bestSymbol15m`:

- Pick the symbol with the highest 15m return within the group.
- Tie-break by symbol alphabetically.

For `worstSymbol15m`:

- Pick the symbol with the lowest 15m return within the group.
- Tie-break by symbol alphabetically.

## Confidence Grouping

Group by normalized confidence:

- Trim whitespace.
- Preserve known labels: `Low`, `Medium`, `High`.
- Map null/empty values to `Unknown`.
- Treat grouping case-insensitively.

Suggested display order:

1. `High`
2. `Medium`
3. `Low`
4. `Unknown`
5. Any unexpected labels alphabetically

## Score Buckets

Use fixed buckets:

```text
0-20
21-40
41-60
61-80
81-100
```

Bucket rules:

- Clamp defensive display only if needed; do not mutate stored score values.
- Scores less than 0 can map to `0-20` or `Unknown/OutOfRange`; V1 should prefer defensive `OutOfRange` only if such data exists.
- Scores greater than 100 can map to `81-100` or `OutOfRange`; V1 should avoid surprising users by documenting whichever choice is implemented.
- Normal expected path is scores between 0 and 100.

Recommended V1 bucket function:

```text
0 <= score <= 20     -> 0-20
20 < score <= 40    -> 21-40
40 < score <= 60    -> 41-60
60 < score <= 80    -> 61-80
80 < score <= 100   -> 81-100
otherwise           -> OutOfRange
```

## Backend Implementation Approach

Add `GetScoreBucketSummaryAsync` to `ISignalOutcomeService`.

Implementation in `SignalOutcomeService`:

- Load outcomes with existing `GetOutcomesAsync(query, cancellationToken)`.
- Build confidence groups in memory.
- Build score bucket groups in memory.
- Reuse existing return calculation logic where possible.
- Ignore rows with missing baseline or checkpoint prices for averages.
- Do not modify outcome evaluation.
- Do not mark pending outcomes as evaluated.

Recommended helper methods:

- `NormalizeConfidence(string? confidence)`
- `GetScoreBucket(decimal score)`
- Shared group aggregation helper to avoid duplicating average/best/worst logic.

## API Design

Add to minimal API in `Program.cs`:

```csharp
app.MapGet(
    "/api/signals/outcomes/score-buckets",
    async (
        ISignalOutcomeService signalOutcomeService,
        string? symbol,
        string? status,
        bool? isSuccessful,
        int? days,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.GetScoreBucketSummaryAsync(
            new SignalOutcomeQuery(symbol, status, isSuccessful, days, limit),
            cancellationToken);

        return Results.Ok(result);
    });
```

Filters should mirror the existing summary and setup-summary endpoints.

## Frontend Design

Add API helper:

```ts
export async function loadSignalOutcomeScoreBuckets(): Promise<SignalOutcomeScoreBucketSummary> {
  return getJson<SignalOutcomeScoreBucketSummary>("/api/signals/outcomes/score-buckets");
}
```

Add types:

```ts
export interface SignalOutcomeScoreBucketSummary {
  generatedAtUtc: string;
  confidenceItems: SignalOutcomeConfidenceSummaryItem[];
  scoreBucketItems: SignalOutcomeScoreBucketSummaryItem[];
}

export interface SignalOutcomeConfidenceSummaryItem {
  confidence: string;
  count: number;
  countWith15m: number;
  averageReturn15m?: number | null;
  countWith1h: number;
  averageReturn1h?: number | null;
  bestSymbol15m?: string | null;
  worstSymbol15m?: string | null;
}

export interface SignalOutcomeScoreBucketSummaryItem {
  bucket: string;
  minScore: number;
  maxScore: number;
  count: number;
  countWith15m: number;
  averageReturn15m?: number | null;
  countWith1h: number;
  averageReturn1h?: number | null;
  bestSymbol15m?: string | null;
  worstSymbol15m?: string | null;
}
```

Add component:

```text
MarketAgent.Web/src/components/ScoreConfidencePerformancePanel.tsx
```

Recommended placement:

- Near `SetupPerformancePanel`.
- Prefer immediately after Setup Performance and before Signal Performance Preview.

Recommended UI:

- Title: `Score & Confidence Performance`
- Note: `Partial intraday returns grouped by confidence and score bucket.`
- Compact cards for:
  - confidence rows
  - score bucket rows
- Each row displays:
  - group label
  - sample count
  - avg 15m return
  - avg 1h return
  - best/worst 15m symbols if space allows

Reuse classes:

- `card`
- `performance-preview`
- `performance-grid`
- `performance-item`
- `performance-metric`
- positive/negative return colors

## Empty and Error States

If endpoint fails:

```text
Score and confidence performance unavailable. The scanner can still be used normally.
```

If no data:

```text
No score or confidence outcome samples yet. Evaluate outcomes after persisted signals have checkpoints.
```

If averages are missing:

- Show `n/a`.

## Risks

- Score bucket comparisons may be misleading with low sample counts.
- Confidence labels may not be evenly distributed.
- Score buckets may be empty if most signals cluster around the same range.
- Showing too many rows could crowd the dashboard.
- In-memory grouping may need repository-level aggregation later.

## Rollback Plan

Backend rollback:

- Remove score bucket DTOs.
- Remove `GetScoreBucketSummaryAsync`.
- Remove endpoint from `Program.cs`.
- Remove tests.

Frontend rollback:

- Remove `ScoreConfidencePerformancePanel`.
- Remove score bucket API helper and types.
- Remove App state/loader/render call.
- Remove optional scoped CSS.

Database rollback:

- None expected.
