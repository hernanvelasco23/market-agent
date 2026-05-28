# Watchlist Responsive Dashboard

## Business Goal

MarketAgent is now usable as a deployed market dashboard, but the current dashboard is still too broad for demo and product use. Users need a cleaner first view focused on the symbols they actually follow, especially CEDEAR-related tickers and Argentina-market names.

This change adds a lightweight, frontend-first personalization layer:

- A user-editable watchlist with up to 10 symbols.
- A cleaner default layout with secondary analytics collapsed.
- Better mobile responsiveness for demo and public usage.
- Current market price visibility in key tables and cards.
- Local persistence using `localStorage`, without auth or backend storage in V1.

## User Value

Users can quickly answer:

- What is happening in my selected tickers?
- What is the current price and how fresh is the market data?
- Which watched candidates have the best upside?
- Are there alerts or risks for symbols I care about?
- Can I use the dashboard comfortably from a phone?

## V1 Scope

- Frontend only unless an issue makes a backend change unavoidable.
- No API contract changes.
- No database changes.
- No auth or user accounts.
- Persist watchlist and collapsed section state in `localStorage`.
- Keep scoring, signal generation, scheduler, outcome evaluation, and ranking logic unchanged.

## Watchlist Behavior

The user can:

- Select up to 10 tickers.
- Add tickers from a predefined static universe.
- Remove selected tickers.
- See a CEDEAR badge when available.
- Keep the selection after refresh.

The selected watchlist filters:

- Main signals list.
- Top Profit Opportunities.
- Alert Center, when applicable.

## Current Price Visibility

The dashboard should expose current asset price prominently so users can validate market freshness, scheduler ingestion, signal scale, and upside context.

Add current price to:

- `Todas las señales`
- `Mejores oportunidades por upside`

Suggested compact order for dense rows:

- `Precio`
- `Símbolo`
- `Setup`
- `Score`
- `Upside`
- `RR`

Price formatting:

- Use standard financial formatting.
- Show 2 decimals for equities.
- Support larger prices cleanly.
- Include a currency indicator only if the existing API/model already provides one.

Freshness metadata should be visible near the status row or market context:

- `Último snapshot`
- last update timestamp
- market status

Optional but preferred visual indicator:

- Green when current price is above the previous snapshot.
- Red when current price is below the previous snapshot.
- Neutral when unchanged or unavailable.

Avoid additional expensive frontend API calls. First reuse fields already present in persisted signal/outcome responses. If current price is missing from the dashboard source, expose it from signal snapshot DTOs in the smallest compatible way.

Default watchlist should be populated when no stored value exists, using a compact relevant set such as:

- `NVDA`
- `MSFT`
- `AAPL`
- `TSLA`
- `MELI`
- `AMD`
- `GGAL`
- `YPF`
- `VIST`
- `RGTI`

## Collapsed Sections

Open by default:

- Contexto de mercado / briefing.
- Mi watchlist.
- Mejores oportunidades por upside.
- Todas las señales.

Collapsed by default:

- Centro de alertas.
- Resultados de señales.
- Performance por setup.
- Performance por score y confianza.
- Preview de performance de señales.

## Spanish Copy

Use Spanish for visible labels:

- `Mi watchlist`
- `Agregar ticker`
- `Quitar`
- `CEDEAR`
- `Máximo 10 activos`
- `Expandir`
- `Colapsar`
- `Sin resultados para la watchlist seleccionada`

Keep trading terms when natural:

- `setup`
- `upside`
- `score`
- `watchlist`
- `pullback`

## Success Criteria

- User can customize up to 10 symbols.
- Watchlist persists after refresh.
- Dashboard is less noisy on first load.
- User can immediately see current price and market freshness.
- Secondary analytics remain accessible.
- Mobile layout is usable.
- `npm.cmd run build` passes.
- No API contract changes.

## Rollback Plan

Revert the frontend files for the watchlist UI, collapsible section wrapper, and CSS changes. Since V1 uses only `localStorage`, rollback does not require data migration or backend cleanup.
