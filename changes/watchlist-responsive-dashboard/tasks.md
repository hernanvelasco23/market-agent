# Tasks

## 1. Inspect Current Frontend

- [ ] Review `MarketAgent.Web/src/App.tsx`.
- [ ] Review existing watchlist helpers/components.
- [ ] Identify current signal filtering path.
- [ ] Identify which frontend field currently represents current market price.
- [ ] Confirm whether persisted outcomes/signal snapshots expose current price.
- [ ] Identify available timestamp for `Último snapshot`.
- [ ] Identify all major dashboard sections to wrap or collapse.
- [ ] Confirm API calls remain unchanged.

## 2. Add Static Watchlist Metadata

- [ ] Add ticker metadata file.
- [ ] Include `symbol`, `displayName`, `hasCedear`, and `category`.
- [ ] Include suggested CEDEAR, ADR, crypto-related, defensive, AI, and growth tickers.
- [ ] Add default watchlist of up to 10 symbols.

## 3. Add Watchlist Persistence Helpers

- [ ] Add `marketagent.userWatchlist.v1` localStorage key.
- [ ] Load selected symbols from localStorage.
- [ ] Validate symbols against static universe.
- [ ] Fall back to default watchlist when storage is missing or invalid.
- [ ] Enforce maximum of 10 selected symbols.
- [ ] Add pure add/remove helpers.

## 4. Build Watchlist UI

- [ ] Add `MyWatchlistPanel`.
- [ ] Show selected symbols as removable chips.
- [ ] Show CEDEAR badge when metadata has `hasCedear`.
- [ ] Add symbol picker/dropdown from static universe.
- [ ] Disable or message when max 10 is reached.
- [ ] Use Spanish labels:
  - `Mi watchlist`
  - `Agregar ticker`
  - `Quitar`
  - `CEDEAR`
  - `Máximo 10 activos`

## 5. Wire Watchlist Filtering

- [ ] Apply selected watchlist to main signal list.
- [ ] Apply selected watchlist to Top Profit Opportunities.
- [ ] Apply selected watchlist to Alert Center.
- [ ] Keep market context and global system status unfiltered.
- [ ] Show `Sin resultados para la watchlist seleccionada` when relevant.
- [ ] Preserve existing filters and watchlist behavior where already present.

## 6. Add Collapsible Sections

- [ ] Add `CollapsibleSection` component.
- [ ] Add `marketagent.collapsedSections.v1` localStorage key.
- [ ] Persist expand/collapse state.
- [ ] Add chevron/visual indicator.
- [ ] Use Spanish labels:
  - `Expandir`
  - `Colapsar`
- [ ] Wrap major dashboard sections.

## 7. Add Current Price Visibility

- [ ] Add current price to `Todas las señales`.
- [ ] Add current price to `Mejores oportunidades por upside`.
- [ ] Prefer row order:
  - `Precio`
  - `Símbolo`
  - `Setup`
  - `Score`
  - `Upside`
  - `RR`
- [ ] Format equity prices with 2 decimals.
- [ ] Ensure larger prices do not overflow.
- [ ] Show `n/a` only when no price exists in the API response.
- [ ] Add `Último snapshot` freshness metadata near status or market context.
- [ ] Keep existing market status visible.
- [ ] Avoid additional expensive frontend API calls.
- [ ] If current price is missing from the response, expose it from signal snapshot DTOs with a minimal additive field.
- [ ] Optional: add price movement color if previous snapshot comparison is available:
  - green for up
  - red for down
  - neutral for unchanged/unavailable

## 8. Default Section State

- [ ] Open by default:
  - Contexto de mercado / briefing
  - Mi watchlist
  - Mejores oportunidades por upside
  - Todas las señales
- [ ] Collapsed by default:
  - Centro de alertas
  - Resultados de señales
  - Performance por setup
  - Performance por score y confianza
  - Preview de performance de señales

## 9. Mobile Responsiveness

- [ ] Stack header actions on narrow screens.
- [ ] Make action buttons tap-friendly.
- [ ] Ensure watchlist chips wrap.
- [ ] Keep dense table horizontally scrollable or compact.
- [ ] Keep current price visible on mobile.
- [ ] Move detail panel below table on mobile.
- [ ] Confirm no cards overflow.
- [ ] Confirm long setup names do not break layout.

## 10. Copy Polish

- [ ] Use Spanish user-facing labels.
- [ ] Use `Precio` and `Último snapshot` labels for price/freshness.
- [ ] Keep natural trading terms:
  - setup
  - upside
  - score
  - watchlist
  - pullback
- [ ] Keep wording concise for mobile.
- [ ] Avoid changing backend/API names.

## 11. Validation

- [ ] Run `npm.cmd run build`.
- [ ] Test initial load with empty localStorage.
- [ ] Test watchlist persistence after refresh.
- [ ] Test max 10 enforcement.
- [ ] Test removing and re-adding symbols.
- [ ] Test selected watchlist filtering.
- [ ] Test collapsed section persistence after refresh.
- [ ] Confirm current price appears in `Todas las señales`.
- [ ] Confirm current price appears in `Mejores oportunidades por upside`.
- [ ] Confirm `Último snapshot`/freshness metadata is visible.
- [ ] Confirm price formatting uses 2 decimals for equities.
- [ ] Test mobile/narrow viewport.
- [ ] Confirm no API contract changes.
- [ ] Confirm no backend or DB changes.

## 12. Rollback Checklist

- [ ] Revert watchlist metadata/helper files.
- [ ] Revert `MyWatchlistPanel`.
- [ ] Revert `CollapsibleSection`.
- [ ] Revert `App.tsx` layout wiring.
- [ ] Revert current price/freshness UI changes.
- [ ] Revert responsive CSS changes.
- [ ] No localStorage cleanup required, but keys can be ignored safely.
