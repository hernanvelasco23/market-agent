# Add Custom Watchlists Design

## Current Architecture Findings

- `MarketAgent.Web` is a Vite React TypeScript dashboard.
- `MarketAgent.Web/src/App.tsx` owns dashboard state, data loading, selected symbol, alert derivation, filters, sorting, sparkline maps, and layout composition.
- Signal table rendering is currently inline in `App.tsx`.
- Signal detail is isolated in `MarketAgent.Web/src/components/SignalDetailPanel.tsx`.
- Signal filtering UI is isolated in `MarketAgent.Web/src/components/SignalFilterBar.tsx`.
- Filtering and sorting helpers live in `MarketAgent.Web/src/signalFilters.ts`.
- Current frontend types live in `MarketAgent.Web/src/types.ts`.
- Styling lives in `MarketAgent.Web/src/styles.css` using compact dark cards, chips, selects, and responsive wrapping.
- The current branch already has frontend watchlist primitives:
  - `MarketAgent.Web/src/watchlists.ts`
  - `MarketAgent.Web/src/components/WatchlistSelector.tsx`
  - `Watchlist` and `WatchlistKind` types in `types.ts`

## Existing Filter/Sort Flow

Current filtering works in memory:

1. `App.tsx` reads `briefing?.allSignals ?? []`.
2. `SignalFilters` state is stored in `App.tsx`.
3. `applySignalFilters(allSignals, filters)` returns a filtered and sorted signal array.
4. `SignalsTable` receives the filtered result.
5. `selectedSignal` is derived from the visible signals.
6. If filters hide the selected symbol, selection falls back to the first visible signal.

Watchlists should run before this filter/sort step:

```text
allSignals -> active watchlist symbol filter -> metric/setup filters -> sort -> table/detail
```

This keeps existing filter semantics intact and treats the active watchlist as the current signal universe.

## Watchlist Data Model

Suggested frontend model:

```ts
export type WatchlistKind = "all" | "predefined" | "custom";

export interface Watchlist {
  id: string;
  name: string;
  symbols: string[];
  kind: WatchlistKind;
}
```

Predefined watchlists:

- `All`: no symbol filtering
- `AI Leaders`
- `Semiconductors`
- `CEDEARs`
- `Crypto`
- `Swing Setups`

Custom watchlists:

- generated frontend `id`
- user-provided `name`
- normalized uppercase `symbols`
- `kind: "custom"`

Symbol normalization:

- Split by comma, whitespace, or semicolon.
- Trim whitespace.
- Uppercase symbols.
- De-duplicate.
- Ignore obviously invalid tokens.

## localStorage Persistence Approach

Persist only custom watchlists:

- Key: `marketagent.customWatchlists`
- Value: JSON array of custom `Watchlist` objects.

Do not persist:

- predefined watchlists
- signal data
- backend responses
- user accounts or identity

Load behavior:

- On initial frontend state creation, read from `localStorage`.
- If parsing fails, return an empty custom list.
- Validate and normalize stored data before use.

Save behavior:

- Save custom watchlists whenever the custom list changes.
- Ignore persistence when `window` is unavailable.

## State Handling Approach

Keep state local to `App.tsx`:

- `customWatchlists`
- `activeWatchlistId`
- existing `filters`
- existing `selectedSymbol`

Derived values:

- `watchlists = [All, ...predefined, ...customWatchlists]`
- `activeWatchlist`
- `watchlistSignals = applyWatchlistFilter(allSignals, activeWatchlist)`
- `filteredSignals = applySignalFilters(watchlistSignals, filters)`

Selection behavior:

- If active watchlist or filters hide the selected symbol, select the first visible signal.
- If no visible signals exist, allow detail panel empty state.
- If the active custom watchlist is removed, fall back to `All`.

Alert/group behavior:

- Prefer deriving alert center and dashboard signal groups from `watchlistSignals` so the dashboard focus is consistent with the selected watchlist.
- Existing filter/sort controls should still apply only to the table.

## UI/Component Design

Create or use:

- `MarketAgent.Web/src/components/WatchlistSelector.tsx`

Suggested props:

```ts
type WatchlistSelectorProps = {
  watchlists: Watchlist[];
  activeWatchlistId: string;
  visibleCount: number;
  totalCount: number;
  onSelect: (id: string) => void;
  onSaveCustom: (watchlist: Watchlist) => void;
  onRemoveCustom: (id: string) => void;
};
```

UI layout:

- Compact card placed near `SignalFilterBar`.
- Select control for active watchlist.
- Summary of active watchlist symbols.
- Inline fields for custom watchlist name and symbols.
- Small actions:
  - New
  - Save
  - Remove for selected custom lists

UX notes:

- Keep it non-modal.
- Keep controls wrapping on small screens.
- Use existing chip/select/card visual language.
- Unknown symbols simply produce no matching rows.
- Empty symbol lists are valid but show an empty result state.

## Files Expected To Change

Expected frontend files:

- `MarketAgent.Web/src/types.ts`
- `MarketAgent.Web/src/watchlists.ts`
- `MarketAgent.Web/src/components/WatchlistSelector.tsx`
- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/styles.css`

Related existing files:

- `MarketAgent.Web/src/signalFilters.ts`
- `MarketAgent.Web/src/components/SignalFilterBar.tsx`

Expected backend files:

- None.

Files intentionally not expected to change:

- backend API routes
- scoring logic
- AI briefing generation
- persistence/database code

## Risks

- Watchlist symbols may not exist in loaded signal data, producing empty results. The UI must make this safe and clear.
- `localStorage` can contain invalid or stale data. Parsing and normalization must be defensive.
- Too many controls near filters can clutter the dashboard. Keep the selector compact.
- Users may expect watchlists to affect backend ingestion. This first version only filters loaded frontend signals.
- Removing a selected custom watchlist can leave an invalid active ID unless handled.
- Custom watchlist names can duplicate; this is acceptable initially because IDs remain unique.
