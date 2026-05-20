# Add Custom Watchlists

## Problem

The dashboard now has richer signal rows, alerts, detail views, and frontend filtering/sorting. Users can narrow the table by setup and metrics, but they cannot easily focus the entire dashboard on a preferred group of symbols.

This makes recurring workflows harder, such as monitoring AI leaders, semiconductors, crypto-linked names, CEDEARs, or a personal swing trading list.

## Goal

Add lightweight custom watchlists to the signal dashboard so users can select a watchlist and focus visible signals, alerts, groups, filters, sorting, and detail navigation on that symbol set.

The first version should be frontend-only and persist custom watchlists in `localStorage`.

## User Value

- Users can focus the dashboard on the symbols they care about most.
- Preset watchlists provide quick entry points for common market themes.
- Custom watchlists support personal workflows without accounts or backend persistence.
- Existing filtering and sorting become more useful because they operate within the selected universe.
- The feature keeps MarketAgent lightweight and usable locally.

## Scope

- Add predefined watchlists:
  - All
  - AI Leaders
  - Semiconductors
  - CEDEARs
  - Crypto
  - Swing Setups
- Allow creating custom watchlists with:
  - name
  - symbols
- Allow selecting an active watchlist.
- Filter dashboard signals by symbols in the active watchlist.
- Persist custom watchlists in `localStorage`.
- Handle empty watchlists and unknown symbols safely.
- Keep existing filters/sorting working after the watchlist filter is applied.
- Keep the UI compact, non-modal, and consistent with the current dark visual style.

## Out of Scope

- No authentication.
- No user accounts.
- No database persistence.
- No backend watchlist API.
- No server-side filtering.
- No Redux or global state overhaul.
- No new dependencies.
- No dashboard rewrite.
- No change to scoring behavior.
- No API contract changes.

## Success Criteria

- Users can switch between predefined watchlists and All.
- Users can create, select, edit, and remove custom watchlists.
- Custom watchlists survive page reload through `localStorage`.
- Existing filters and sorting continue to work within the selected watchlist.
- Empty watchlists show a safe empty state.
- Unknown or invalid symbols do not crash the UI.
- Selecting a watchlist updates table rows and selected signal safely.
- The dashboard remains readable in dark mode.
- `npm.cmd run build` passes.
