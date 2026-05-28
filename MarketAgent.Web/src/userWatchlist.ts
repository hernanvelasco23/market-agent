import {
  defaultUserWatchlist,
  maxUserWatchlistSymbols,
  watchlistTickerBySymbol
} from "./watchlistMetadata";

export const userWatchlistStorageKey = "marketagent.userWatchlist.v1";

const allowedSymbols = new Set(Object.keys(watchlistTickerBySymbol));

export function normalizeSymbol(value: string) {
  return value.trim().toUpperCase();
}

export function loadUserWatchlist() {
  if (typeof window === "undefined") {
    return defaultUserWatchlist;
  }

  try {
    const raw = window.localStorage.getItem(userWatchlistStorageKey);
    if (!raw) {
      return defaultUserWatchlist;
    }

    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return defaultUserWatchlist;
    }

    const symbols = normalizeWatchlist(parsed);
    return symbols.length > 0 ? symbols : defaultUserWatchlist;
  } catch {
    return defaultUserWatchlist;
  }
}

export function saveUserWatchlist(symbols: string[]) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(userWatchlistStorageKey, JSON.stringify(normalizeWatchlist(symbols)));
}

export function addWatchlistSymbol(symbols: string[], symbol: string) {
  const normalized = normalizeSymbol(symbol);
  if (!allowedSymbols.has(normalized) || symbols.includes(normalized) || symbols.length >= maxUserWatchlistSymbols) {
    return normalizeWatchlist(symbols);
  }

  return normalizeWatchlist([...symbols, normalized]);
}

export function removeWatchlistSymbol(symbols: string[], symbol: string) {
  const normalized = normalizeSymbol(symbol);
  const next = symbols.filter((item) => item !== normalized);
  return next.length > 0 ? normalizeWatchlist(next) : defaultUserWatchlist;
}

function normalizeWatchlist(values: unknown[]) {
  const result: string[] = [];

  for (const value of values) {
    if (typeof value !== "string") {
      continue;
    }

    const symbol = normalizeSymbol(value);
    if (!allowedSymbols.has(symbol) || result.includes(symbol)) {
      continue;
    }

    result.push(symbol);
    if (result.length >= maxUserWatchlistSymbols) {
      break;
    }
  }

  return result;
}
