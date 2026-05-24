import { ListPlus, Save, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import type { Watchlist } from "../types";
import { createCustomWatchlist, formatWatchlistSymbols, normalizeSymbols } from "../watchlists";

type WatchlistSelectorProps = {
  watchlists: Watchlist[];
  activeWatchlistId: string;
  visibleCount: number;
  totalCount: number;
  onSelect: (id: string) => void;
  onSaveCustom: (watchlist: Watchlist) => void;
  onRemoveCustom: (id: string) => void;
};

export function WatchlistSelector({
  watchlists,
  activeWatchlistId,
  visibleCount,
  totalCount,
  onSelect,
  onSaveCustom,
  onRemoveCustom
}: WatchlistSelectorProps) {
  const activeWatchlist = watchlists.find((watchlist) => watchlist.id === activeWatchlistId) ?? watchlists[0];
  const [draftId, setDraftId] = useState<string | null>(activeWatchlist?.kind === "custom" ? activeWatchlist.id : null);
  const [draftName, setDraftName] = useState(activeWatchlist?.kind === "custom" ? activeWatchlist.name : "");
  const [draftSymbols, setDraftSymbols] = useState(activeWatchlist?.kind === "custom" ? formatWatchlistSymbols(activeWatchlist.symbols) : "");

  useEffect(() => {
    if (activeWatchlist?.kind === "custom") {
      setDraftId(activeWatchlist.id);
      setDraftName(activeWatchlist.name);
      setDraftSymbols(formatWatchlistSymbols(activeWatchlist.symbols));
      return;
    }

    setDraftId(null);
  }, [activeWatchlist]);

  const draftNormalizedSymbols = normalizeSymbols(draftSymbols);
  const canSave = draftName.trim().length > 0 || draftNormalizedSymbols.length > 0;

  function handleNewCustom() {
    setDraftId(null);
    setDraftName("");
    setDraftSymbols("");
  }

  function handleSave() {
    if (!canSave) {
      return;
    }

    const watchlist: Watchlist = draftId
      ? {
          id: draftId,
          name: draftName.trim() || "Custom Watchlist",
          symbols: draftNormalizedSymbols,
          kind: "custom"
        }
      : createCustomWatchlist(draftName, draftNormalizedSymbols);

    onSaveCustom(watchlist);
    onSelect(watchlist.id);
  }

  return (
    <section className="card watchlist-panel">
      <div className="watchlist-header">
        <div className="card-title">
          <ListPlus size={17} />
          <span>Watchlist</span>
          <b>{visibleCount}/{totalCount}</b>
        </div>
        <button className="filter-chip" type="button" onClick={handleNewCustom}>
          Nueva
        </button>
      </div>

      <div className="watchlist-controls">
        <label className="filter-select watchlist-select">
          <span>Activa</span>
          <select value={activeWatchlistId} onChange={(event) => onSelect(event.target.value)}>
            {watchlists.map((watchlist) => (
              <option key={watchlist.id} value={watchlist.id}>
                {watchlist.name}
              </option>
            ))}
          </select>
        </label>

        <div className="watchlist-summary">
          <span>{activeWatchlist?.name ?? "Todas las señales"}</span>
          <small>
            {activeWatchlist?.kind === "all"
              ? "Sin filtro de símbolos"
              : activeWatchlist?.symbols.length
                ? activeWatchlist.symbols.join(", ")
                : "Watchlist vacía"}
          </small>
        </div>
      </div>

      <div className="watchlist-editor">
        <input
          aria-label="Nombre de watchlist"
          placeholder="Nombre de watchlist"
          value={draftName}
          onChange={(event) => setDraftName(event.target.value)}
        />
        <input
          aria-label="Símbolos de watchlist"
          placeholder="Símbolos: NVDA, AMD, TSLA"
          value={draftSymbols}
          onChange={(event) => setDraftSymbols(event.target.value)}
        />
        <button className="filter-chip active" type="button" onClick={handleSave} disabled={!canSave}>
          <Save size={14} />
          Guardar
        </button>
        {draftId ? (
          <button className="filter-chip danger" type="button" onClick={() => onRemoveCustom(draftId)}>
            <Trash2 size={14} />
            Eliminar
          </button>
        ) : null}
      </div>
    </section>
  );
}
