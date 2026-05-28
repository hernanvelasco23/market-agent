import { Plus, RefreshCw, X } from "lucide-react";
import type { KeyboardEvent } from "react";
import { useEffect, useId, useMemo, useState } from "react";
import { maxUserWatchlistSymbols, watchlistTickerBySymbol, watchlistTickerUniverse } from "../watchlistMetadata";

export interface WatchlistSymbolState {
  label: string;
  tone: "active" | "monitoring" | "pending" | "no-data" | "error" | string;
  currentPrice?: number | null;
  description?: string | null;
}

interface MyWatchlistPanelProps {
  symbols: string[];
  symbolStates: Record<string, WatchlistSymbolState>;
  hydrating: boolean;
  hydrationSummary: string | null;
  onAdd: (symbol: string) => void;
  onRemove: (symbol: string) => void;
  onHydrate: () => void;
}

export function MyWatchlistPanel({
  symbols,
  symbolStates,
  hydrating,
  hydrationSummary,
  onAdd,
  onRemove,
  onHydrate
}: MyWatchlistPanelProps) {
  const availableSymbols = useMemo(
    () => watchlistTickerUniverse.filter((ticker) => !symbols.includes(ticker.symbol)),
    [symbols]
  );
  const listboxId = useId();
  const [query, setQuery] = useState("");
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  const isFull = symbols.length >= maxUserWatchlistSymbols;
  const filteredSymbols = useMemo(() => {
    const normalizedQuery = query.trim().toUpperCase();

    if (!normalizedQuery) {
      return availableSymbols;
    }

    return availableSymbols.filter((ticker) =>
      `${ticker.symbol} ${ticker.displayName} ${ticker.category}`.toUpperCase().includes(normalizedQuery)
    );
  }, [availableSymbols, query]);
  const selectedTicker = filteredSymbols[highlightedIndex] ?? filteredSymbols[0] ?? null;
  const canAdd = !isFull && selectedTicker != null;

  useEffect(() => {
    setHighlightedIndex(0);
  }, [query, availableSymbols.length]);

  function handleAdd() {
    if (!canAdd || selectedTicker == null) {
      return;
    }

    onAdd(selectedTicker.symbol);
    setQuery("");
    setHighlightedIndex(0);
    setIsPickerOpen(false);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setIsPickerOpen(true);
      setHighlightedIndex((current) => Math.min(current + 1, Math.max(filteredSymbols.length - 1, 0)));
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      setIsPickerOpen(true);
      setHighlightedIndex((current) => Math.max(current - 1, 0));
      return;
    }

    if (event.key === "Enter") {
      event.preventDefault();
      handleAdd();
      return;
    }

    if (event.key === "Escape") {
      setIsPickerOpen(false);
    }
  }

  return (
    <article className="card my-watchlist-panel">
      <div className="watchlist-panel-header">
        <div className="card-title">
          <span>Mi watchlist</span>
          <b>{symbols.length}/{maxUserWatchlistSymbols}</b>
        </div>
        <div className="watchlist-header-actions">
          {isFull ? <span className="watchlist-limit">Máximo 10 activos</span> : null}
          <button
            className="action-button compact watchlist-hydrate-button"
            type="button"
            onClick={onHydrate}
            disabled={hydrating || symbols.length === 0}
          >
            <RefreshCw className={hydrating ? "spin" : undefined} size={15} />
            <span>{hydrating ? "Actualizando watchlist..." : "Actualizar watchlist"}</span>
          </button>
        </div>
      </div>

      <div className="watchlist-add-row">
        <div className="watchlist-combobox">
          <label htmlFor="watchlist-ticker-search">Agregar ticker</label>
          <input
            id="watchlist-ticker-search"
            type="text"
            value={query}
            placeholder={availableSymbols.length === 0 ? "Sin tickers disponibles" : "Buscar ticker"}
            role="combobox"
            aria-autocomplete="list"
            aria-expanded={isPickerOpen}
            aria-controls={listboxId}
            aria-activedescendant={selectedTicker ? `${listboxId}-${selectedTicker.symbol}` : undefined}
            disabled={isFull || availableSymbols.length === 0}
            onChange={(event) => {
              setQuery(event.target.value);
              setIsPickerOpen(true);
            }}
            onFocus={() => setIsPickerOpen(true)}
            onBlur={() => window.setTimeout(() => setIsPickerOpen(false), 120)}
            onKeyDown={handleKeyDown}
          />
          {isPickerOpen && !isFull && availableSymbols.length > 0 ? (
            <div className="watchlist-option-list" id={listboxId} role="listbox">
              {filteredSymbols.length === 0 ? (
                <div className="watchlist-option empty-option">No se encontraron activos</div>
              ) : (
                filteredSymbols.map((ticker, index) => (
                  <button
                    id={`${listboxId}-${ticker.symbol}`}
                    className={`watchlist-option ${index === highlightedIndex ? "highlighted" : ""}`}
                    type="button"
                    role="option"
                    aria-selected={index === highlightedIndex}
                    key={ticker.symbol}
                    onMouseDown={(event) => {
                      event.preventDefault();
                      onAdd(ticker.symbol);
                      setQuery("");
                      setIsPickerOpen(false);
                    }}
                    onMouseEnter={() => setHighlightedIndex(index)}
                  >
                    <strong>{ticker.symbol}</strong>
                    <span>{ticker.displayName}</span>
                    {ticker.hasCedear ? <small>CEDEAR</small> : null}
                  </button>
                ))
              )}
            </div>
          ) : null}
        </div>
        <button className="action-button compact" type="button" onClick={handleAdd} disabled={!canAdd}>
          <Plus size={15} />
          <span>Agregar</span>
        </button>
      </div>

      <div className="watchlist-chip-grid">
        {symbols.map((symbol) => {
          const metadata = watchlistTickerBySymbol[symbol];
          const state = symbolStates[symbol] ?? {
            label: "Pendiente de actualizar",
            tone: "pending",
            currentPrice: null,
            description: "Seleccionado en tu watchlist. Ejecutá una actualización para hidratar datos."
          };

          return (
            <div className={`watchlist-chip ${state.tone}`} key={symbol}>
              <div className="watchlist-chip-main">
                <div className="watchlist-chip-title">
                  <strong>{symbol}</strong>
                  {metadata?.hasCedear ? <small className="cedear-badge">CEDEAR</small> : null}
                  <i className={`watchlist-state-badge ${state.tone}`} title={state.description ?? undefined}>
                    {state.label}
                  </i>
                </div>
                <span>{metadata?.displayName ?? symbol}</span>
                <em className="watchlist-chip-meta">{metadata?.category ?? "Watchlist"}</em>
                <strong className="watchlist-chip-price">
                  {state.currentPrice == null ? "Precio -" : `Precio ${formatPrice(state.currentPrice)}`}
                </strong>
              </div>
              <button className="watchlist-remove-button" type="button" aria-label={`Quitar ${symbol}`} onClick={() => onRemove(symbol)}>
                <X size={14} />
              </button>
            </div>
          );
        })}
      </div>
      {hydrationSummary ? <p className="watchlist-hydration-summary">{hydrationSummary}</p> : null}
    </article>
  );
}

function formatPrice(value: number) {
  return new Intl.NumberFormat("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}
