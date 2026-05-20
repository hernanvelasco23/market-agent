# Setup Performance Analytics Design

## Current Architecture Context

Backend:

- Signal snapshots are persisted in SQL Server as `SignalSnapshots`.
- Signal outcomes are persisted in `SignalOutcomes`.
- Outcome summary exists at:
  - `GET /api/signals/outcomes/summary`
- Outcome records can be queried through existing outcome repository/service patterns.
- `SignalSnapshots.Setup` is already persisted and is the grouping key for this feature.

Frontend:

- `MarketAgent.Web/src/api.ts` contains API helpers.
- `MarketAgent.Web/src/types.ts` contains response types.
- `MarketAgent.Web/src/App.tsx` owns dashboard state and renders sections.
- `SignalOutcomeSummaryPanel` displays compact Signal Outcome cards.
- Existing card/performance classes can be reused.

## Backend Design

Add a new additive API endpoint:

```text
GET /api/signals/outcomes/setup-summary
```

Recommended Application model:

```csharp
public sealed record SignalOutcomeSetupSummary(
    DateTime GeneratedAtUtc,
    int TotalSetupCount,
    string? BestSetup,
    decimal? BestSetupAverageReturn15m,
    string? WorstSetup,
    decimal? WorstSetupAverageReturn15m,
    IReadOnlyCollection<SignalOutcomeSetupSummaryItem> Items);

public sealed record SignalOutcomeSetupSummaryItem(
    string Setup,
    int Count,
    int CountWith15m,
    decimal? AverageReturn15m,
    int CountWith1h,
    decimal? AverageReturn1h,
    int CountWith4h,
    decimal? AverageReturn4h);
```

Return calculation:

```text
returnPct = ((checkpointPrice - priceAtSignal) / priceAtSignal) * 100
```

Use only outcomes where:

- `PriceAtSignal > 0`
- checkpoint price is non-null

Grouping:

- Group by `SignalSnapshot.Setup`.
- Normalize empty/null setup to `Unknown` only if defensive handling is needed.
- Sort items by `AverageReturn15m` descending, then setup name.

Best/worst setup:

- Best setup is the setup with the highest average 15m return among setups with at least one 15m checkpoint.
- Worst setup is the setup with the lowest average 15m return among setups with at least one 15m checkpoint.
- If only one setup has 15m data, best and worst may be the same.

## Backend Implementation Options

Option A: Compute in `SignalOutcomeService` using `GetOutcomesAsync`.

Pros:

- Smallest change.
- Reuses existing query and mapping.
- Easy to test with existing fake repository.

Cons:

- Pulls more rows into memory.
- May need optimization later.

Option B: Add repository aggregation method.

Pros:

- More efficient for large history.
- SQL can group directly.

Cons:

- More implementation surface.
- More EF query complexity.

V1 recommendation:

- Use Option A unless performance becomes an issue.
- Keep the endpoint additive and service-level first.

## API Design

Add to minimal API in `Program.cs`:

```csharp
app.MapGet(
    "/api/signals/outcomes/setup-summary",
    async (
        ISignalOutcomeService signalOutcomeService,
        string? symbol,
        string? status,
        bool? isSuccessful,
        int? days,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.GetSetupSummaryAsync(
            new SignalOutcomeQuery(symbol, status, isSuccessful, days, limit),
            cancellationToken);

        return Results.Ok(result);
    });
```

The filters should mirror `/api/signals/outcomes/summary` where practical.

## Frontend Design

Add API helper:

```ts
export async function loadSignalOutcomeSetupSummary(): Promise<SignalOutcomeSetupSummary> {
  return getJson<SignalOutcomeSetupSummary>("/api/signals/outcomes/setup-summary");
}
```

Add types:

```ts
export interface SignalOutcomeSetupSummary {
  generatedAtUtc: string;
  totalSetupCount: number;
  bestSetup?: string | null;
  bestSetupAverageReturn15m?: number | null;
  worstSetup?: string | null;
  worstSetupAverageReturn15m?: number | null;
  items: SignalOutcomeSetupSummaryItem[];
}

export interface SignalOutcomeSetupSummaryItem {
  setup: string;
  count: number;
  countWith15m: number;
  averageReturn15m?: number | null;
  countWith1h: number;
  averageReturn1h?: number | null;
}
```

Add component:

```text
MarketAgent.Web/src/components/SetupPerformancePanel.tsx
```

Recommended placement:

- Near `SignalOutcomeSummaryPanel`.
- Prefer immediately after Signal Outcomes and before Signal Performance Preview.

Recommended UI:

- Title: `Setup Performance`
- Note: `Partial outcome returns grouped by emitted signal setup.`
- Compact cards:
  - best setup
  - worst setup
  - top setup rows with setup, count, avg 15m, avg 1h

Reuse existing classes:

- `card`
- `performance-preview`
- `performance-grid`
- `performance-item`
- `performance-metric`
- positive/negative classes

## Empty and Error States

If endpoint fails:

- Show compact warning:
  - `Setup performance unavailable. The dashboard can still be used normally.`

If no setup data:

- Show:
  - `No setup performance data yet. Evaluate signal outcomes to populate this section.`

If a setup has no checkpoint data:

- Show `n/a` for averages.

## Risks

- Setup-level averages can be noisy with low sample counts.
- Pulling all outcomes into Application memory may become slow with larger data.
- UI may get too long if all setups are displayed; cap visible items in V1.
- Setup naming consistency affects usefulness.
- The endpoint can fail when SQL Server is unavailable; UI must degrade safely.

## Rollback Plan

Backend rollback:

- Remove setup summary DTOs.
- Remove service method.
- Remove endpoint from `Program.cs`.
- Remove tests.

Frontend rollback:

- Remove `SetupPerformancePanel`.
- Remove setup summary type and API helper.
- Remove App state/loader/render call.
- Remove optional CSS.

No database rollback should be needed because this feature should not add schema in V1.
