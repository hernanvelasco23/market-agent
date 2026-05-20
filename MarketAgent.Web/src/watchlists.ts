import type { DashboardSignal, Watchlist } from "./types";

const WATCHLIST_STORAGE_KEY = "marketagent.customWatchlists";

export const allSignalsWatchlist: Watchlist = {
  id: "all",
  name: "All Signals",
  symbols: [],
  kind: "all"
};

export const predefinedWatchlists: Watchlist[] = [
  {
    id: "ai-leaders",
    name: "AI Leaders",
    symbols: ["NVDA", "MSFT", "GOOGL", "META", "AMZN", "TSLA", "PLTR", "AVGO"],
    kind: "predefined"
  },
  {
    id: "semiconductors",
    name: "Semiconductors",
    symbols: ["NVDA", "AMD", "AVGO", "MU", "TSM", "ASML", "INTC", "QCOM", "ARM", "SMCI"],
    kind: "predefined"
  },
  {
    id: "cedears",
    name: "CEDEARs",
    symbols: ["AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "MELI", "V", "KO", "JPM"],
    kind: "predefined"
  },
  {
    id: "crypto",
    name: "Crypto",
    symbols: ["BTC", "ETH", "SOL", "COIN", "MSTR", "MARA", "RIOT"],
    kind: "predefined"
  },
  {
    id: "swing-setups",
    name: "Swing Setups",
    symbols: ["NVDA", "AMD", "TSLA", "MU", "RKLB", "ABNB", "PLTR", "PATH"],
    kind: "predefined"
  }
];

export function getAllWatchlists(customWatchlists: Watchlist[]): Watchlist[] {
  return [allSignalsWatchlist, ...predefinedWatchlists, ...customWatchlists];
}

export function loadCustomWatchlists(): Watchlist[] {
  if (typeof window === "undefined") {
    return [];
  }

  try {
    const rawValue = window.localStorage.getItem(WATCHLIST_STORAGE_KEY);
    if (!rawValue) {
      return [];
    }

    const parsed = JSON.parse(rawValue) as Array<Partial<Watchlist>>;
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .map((item) => ({
        id: typeof item.id === "string" && item.id.trim() ? item.id : createCustomWatchlistId(),
        name: typeof item.name === "string" && item.name.trim() ? item.name.trim() : "Custom Watchlist",
        symbols: normalizeSymbols(item.symbols ?? []),
        kind: "custom" as const
      }))
      .filter((item) => item.kind === "custom");
  } catch {
    return [];
  }
}

export function saveCustomWatchlists(watchlists: Watchlist[]) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(
    WATCHLIST_STORAGE_KEY,
    JSON.stringify(watchlists.filter((watchlist) => watchlist.kind === "custom"))
  );
}

export function createCustomWatchlist(name: string, symbols: string[] | string): Watchlist {
  return {
    id: createCustomWatchlistId(),
    name: name.trim() || "Custom Watchlist",
    symbols: normalizeSymbols(symbols),
    kind: "custom"
  };
}

export function applyWatchlistFilter(signals: DashboardSignal[], watchlist: Watchlist | undefined): DashboardSignal[] {
  if (!watchlist || watchlist.kind === "all") {
    return signals;
  }

  const symbols = new Set(normalizeSymbols(watchlist.symbols));
  if (symbols.size === 0) {
    return [];
  }

  return signals.filter((signal) => symbols.has(signal.symbol.toUpperCase()));
}

export function normalizeSymbols(symbols: string[] | string): string[] {
  const values = Array.isArray(symbols) ? symbols : symbols.split(/[\s,;]+/);

  return Array.from(
    new Set(
      values
        .map((symbol) => symbol.trim().toUpperCase())
        .filter((symbol) => /^[A-Z0-9.-]{1,12}$/.test(symbol))
    )
  );
}

export function formatWatchlistSymbols(symbols: string[]) {
  return normalizeSymbols(symbols).join(", ");
}

function createCustomWatchlistId() {
  return `custom-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
