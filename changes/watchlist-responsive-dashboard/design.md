# Design

## Current Shape

The dashboard currently loads persisted signal data from the frontend API helpers and renders multiple sections in a single page:

- Header actions.
- Status row.
- Market context cards.
- Signal groups.
- Top Profit Opportunities.
- Alert Center.
- Outcome and setup analytics panels.
- Watchlist/filter controls.
- Main signal table and detail panel.

The existing code already has watchlist-related pieces, but this change should harden the UX around a focused user watchlist and make the default dashboard feel less noisy.

## Architecture

Keep the implementation frontend-first:

- Add static ticker metadata.
- Add localStorage helpers for selected symbols and collapsed sections.
- Add a compact `MyWatchlistPanel`.
- Add a reusable `CollapsibleSection` wrapper.
- Apply the selected watchlist before rendering major signal-dependent panels.
- Surface current price and freshness metadata from existing signal/outcome data.
- Add responsive CSS without changing backend contracts.

## Future Evolution

Potential future improvements (out of V1 scope):
- Authenticated user watchlists
- Cloud-synced preferences
- Broker/import integration
- Dynamic CEDEAR universe
- Push/mobile alerts
- Multi-watchlist support

## Suggested Files

- `MarketAgent.Web/src/watchlistMetadata.ts`
  Static ticker universe with:
  - `symbol`
  - `displayName`
  - `hasCedear`
  - `category`

- `MarketAgent.Web/src/userWatchlist.ts`
  Pure helpers:
  - normalize symbols
  - enforce max 10
  - load/save localStorage
  - add/remove symbols
  - default watchlist

- `MarketAgent.Web/src/collapsibleSections.ts`
  Section IDs and localStorage helpers.

- `MarketAgent.Web/src/components/MyWatchlistPanel.tsx`
  Editable watchlist UI.

- `MarketAgent.Web/src/components/CollapsibleSection.tsx`
  Reusable section wrapper with chevron and persisted state.

- `MarketAgent.Web/src/App.tsx`
  Wire selected symbols into existing dashboard filtering and wrap sections.

- `MarketAgent.Web/src/styles.css`
  Mobile and collapsed-state layout improvements.

## Current Price and Freshness

Current price should be visible without extra frontend polling beyond existing read-only dashboard loads.

Preferred endpoint/source:

- Use the dashboard signal source already loaded from persisted outcomes.
- Map existing signal snapshot `Price` or outcome/source equivalent to a frontend `currentPrice`/`price` display value.
- If the currently used DTO does not expose current price, add the field to the persisted signal/outcome DTO only. Do not add a new endpoint in V1 unless unavoidable.

Display current price in:

- `Todas las señales`
- `Mejores oportunidades por upside`
- Signal detail panel if the field is available and layout allows.

Suggested row order for compact opportunity rows:

- `Precio`
- `Símbolo`
- `Setup`
- `Score`
- `Upside`
- `RR`

Formatting:

- Use 2 decimals for equities.
- Support large prices without layout overflow.
- Use `n/a` only when the API truly has no price value.
- Currency marker is optional and should only be shown if already available from existing data.

Freshness metadata:

- Show `Último snapshot` from the freshest signal/snapshot timestamp or market snapshot timestamp available in the current response.
- Keep existing market status visible.
- Show last dashboard update timestamp.

Optional price movement indicator:

- If previous snapshot/price is available in the response, compare current price to previous price.
- Positive movement: green.
- Negative movement: red.
- Unchanged or missing comparison: neutral.

Do not introduce additional expensive API calls solely for freshness or price movement in V1.

## localStorage Keys

Use stable keys:

- `marketagent.userWatchlist.v1`
- `marketagent.collapsedSections.v1`

Stored watchlist format:

```json
["NVDA", "MSFT", "AAPL", "TSLA"]
```

Stored collapsed sections format:

```json
{
  "alert-center": true,
  "signal-outcomes": true,
  "setup-performance": true
}
```

## Ticker Universe

Use static metadata in V1.

US mega cap / CEDEARs:

- `NVDA`
- `MSFT`
- `AAPL`
- `AMZN`
- `GOOGL`
- `META`
- `TSLA`
- `AMD`
- `MELI`
- `NU`

Argentina / ADR / CEDEAR-relevant:

- `GGAL`
- `YPF`
- `BMA`
- `PAM`
- `TGS`
- `VIST`

Crypto-related:

- `MSTR`
- `COIN`
- `IBIT`

Defensive / consumer:

- `KO`
- `PEP`
- `PG`
- `WMT`
- `DIS`
- `NFLX`

AI / semis / growth:

- `PLTR`
- `ASML`
- `TSM`
- `AVGO`
- `RGTI`

## Watchlist Filtering

Apply selected symbols to:

- `allSignals`
- Top Profit Opportunities input
- Alert Center input
- Signal groups if they depend on the active signal list

Do not change score, setup, alert, or profit-ranking calculations. Only filter the input data shown to the user.

If no signal exists for a selected symbol, show it in the watchlist as selected but inactive/no signal.

## Collapsible Sections

Section IDs:

- `market-context`
- `my-watchlist`
- `top-profit-opportunities`
- `alert-center`
- `signal-outcomes`
- `setup-performance`
- `score-confidence-performance`
- `signal-performance-preview`
- `all-signals`

Open by default:

- `market-context`
- `my-watchlist`
- `top-profit-opportunities`
- `all-signals`

Collapsed by default:

- `alert-center`
- `signal-outcomes`
- `setup-performance`
- `score-confidence-performance`
- `signal-performance-preview`

The wrapper should render the section title, count/summary if supplied, and a chevron button with labels:

- `Expandir`
- `Colapsar`

## Responsive Design

Mobile priorities:

- Header actions stack cleanly.
- Watchlist chips wrap.
- Ticker add controls remain tap-friendly.
- Main table scrolls horizontally or becomes compact enough to use.
- Current price remains visible on mobile, ideally before less critical technical columns.
- Detail panel moves below the table.
- Dense analytics can remain collapsed by default.

CSS guidance:

- Use `@media (max-width: 768px)` and `@media (max-width: 520px)`.
- Ensure buttons have touch-friendly height.
- Avoid fixed widths that overflow.
- Keep table wrapper horizontal scroll if full table remains.

## Risks

- Existing watchlist code may overlap with the new user watchlist. Prefer reusing what exists where safe, but avoid broad refactors.
- Too much layout churn could destabilize the dashboard. Keep wrappers additive.
- localStorage values may become malformed. Helpers should validate and fall back to defaults.
- Filtering all panels by watchlist may hide useful market-wide context. Keep market context unfiltered.

## Rollback

Rollback is a frontend revert only:

- Remove `MyWatchlistPanel`.
- Remove `CollapsibleSection`.
- Restore `App.tsx` section layout.
- Restore CSS changes.

No database or API rollback is needed.
