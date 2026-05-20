# Add Custom Watchlists Tasks

## Incremental Tasks

### 1. Define watchlist types

- Add `WatchlistKind` and `Watchlist` frontend types.
- Keep them frontend-only.
- Do not change backend DTOs or API contracts.

### 2. Add watchlist helpers

- Create `MarketAgent.Web/src/watchlists.ts`.
- Define predefined watchlists:
  - All
  - AI Leaders
  - Semiconductors
  - CEDEARs
  - Crypto
  - Swing Setups
- Add helpers for:
  - loading custom watchlists from `localStorage`
  - saving custom watchlists to `localStorage`
  - normalizing symbols
  - creating custom watchlists
  - filtering signals by active watchlist

### 3. Add watchlist state in App

- Add `customWatchlists` state.
- Add `activeWatchlistId` state.
- Derive full watchlist list from predefined plus custom watchlists.
- Derive `watchlistSignals` before applying existing signal filters/sorting.
- Keep existing filters and sorting unchanged.

### 4. Add selection safety

- If the active watchlist disappears, fall back to All.
- If the selected signal is not visible under the active watchlist and filters, select the first visible signal.
- If no visible signal exists, allow the detail panel empty state.

### 5. Create watchlist UI

- Create `MarketAgent.Web/src/components/WatchlistSelector.tsx`.
- Add:
  - active watchlist selector
  - active watchlist summary
  - custom watchlist name input
  - custom symbol input
  - New action
  - Save action
  - Remove action for custom watchlists
- Keep it non-modal and compact.

### 6. Integrate with dashboard focus

- Render the watchlist selector near the filter bar.
- Apply active watchlist to:
  - all-signals table
  - selected signal/detail panel
  - alert center
  - top opportunity/pullback/risk groups
- Preserve briefing summary cards and backend data loading as-is.

### 7. Style the watchlist controls

- Update `MarketAgent.Web/src/styles.css`.
- Match existing dark card, chip, and select styles.
- Ensure controls wrap cleanly on mobile.

## Validation/Build Steps

- Run `npm.cmd run build` from `MarketAgent.Web`.
- If backend files are unexpectedly changed, also run:
  - `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`
  - `dotnet build MarketAgent.sln --no-restore`

## Manual QA Checklist

- Select All and confirm all loaded signals are visible.
- Select each predefined watchlist and confirm symbols are filtered safely.
- Create a custom watchlist with valid symbols and confirm it filters the dashboard.
- Create a custom watchlist with unknown symbols and confirm the UI shows an empty state without crashing.
- Create a custom watchlist with mixed lowercase, commas, spaces, or duplicate symbols and confirm normalization.
- Reload the page and confirm custom watchlists persist.
- Edit a custom watchlist and confirm the saved values update.
- Remove a custom watchlist and confirm the active watchlist falls back safely if needed.
- Confirm existing setup, score, RS, RVOL, EXT, risk/opportunity, and ORR filters still work inside the selected watchlist.
- Confirm sorting still works inside the selected watchlist.
- Confirm dark mode and responsive layout remain readable.

## Rollback Considerations

- This feature is frontend-only and additive.
- Rollback can remove:
  - `MarketAgent.Web/src/watchlists.ts`
  - `MarketAgent.Web/src/components/WatchlistSelector.tsx`
  - watchlist types from `types.ts`
  - watchlist state and derived `watchlistSignals` in `App.tsx`
  - watchlist CSS selectors
- `localStorage` data can remain harmless if the code no longer reads it.
- No backend, database, route, scoring, or API rollback is required.
