export interface WatchlistTickerMetadata {
  symbol: string;
  displayName: string;
  hasCedear: boolean;
  category: string;
}

export const maxUserWatchlistSymbols = 10;

export const defaultUserWatchlist = [
  "NVDA",
  "MSFT",
  "AAPL",
  "TSLA",
  "MELI",
  "AMD",
  "GGAL",
  "YPF",
  "VIST",
  "RGTI"
];

const popularSymbols = new Set([
  "NVDA",
  "MSFT",
  "AAPL",
  "AMZN",
  "GOOGL",
  "META",
  "TSLA",
  "AMD",
  "MELI",
  "GGAL",
  "YPF",
  "VIST",
  "RGTI",
  "SPY",
  "QQQ"
]);

const rawTickerUniverse: WatchlistTickerMetadata[] = [
  { symbol: "NVDA", displayName: "NVIDIA", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "AMD", displayName: "Advanced Micro Devices", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "AVGO", displayName: "Broadcom", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "ASML", displayName: "ASML", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "TSM", displayName: "Taiwan Semiconductor", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "QCOM", displayName: "Qualcomm", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "MRVL", displayName: "Marvell Technology", hasCedear: true, category: "AI / Semiconductors" },
  { symbol: "PLTR", displayName: "Palantir", hasCedear: true, category: "AI / Growth" },
  { symbol: "RGTI", displayName: "Rigetti Computing", hasCedear: false, category: "AI / Growth" },
  { symbol: "RKLB", displayName: "Rocket Lab", hasCedear: false, category: "AI / Growth" },
  { symbol: "ORCL", displayName: "Oracle", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "IBM", displayName: "IBM", hasCedear: true, category: "Mega Cap Tech" },

  { symbol: "MSFT", displayName: "Microsoft", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "AAPL", displayName: "Apple", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "AMZN", displayName: "Amazon", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "GOOGL", displayName: "Alphabet Class A", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "GOOG", displayName: "Alphabet Class C", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "META", displayName: "Meta Platforms", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "NFLX", displayName: "Netflix", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "SHOP", displayName: "Shopify", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "PYPL", displayName: "PayPal", hasCedear: true, category: "Mega Cap Tech" },
  { symbol: "SE", displayName: "Sea Limited", hasCedear: true, category: "Mega Cap Tech" },

  { symbol: "MELI", displayName: "MercadoLibre", hasCedear: true, category: "Argentina / Latam" },
  { symbol: "NU", displayName: "Nu Holdings", hasCedear: true, category: "Argentina / Latam" },
  { symbol: "GGAL", displayName: "Grupo Financiero Galicia", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "YPF", displayName: "YPF", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "YPFD", displayName: "YPF local", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "PAM", displayName: "Pampa Energia ADR", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "PAMP", displayName: "Pampa Energia local", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "VIST", displayName: "Vista Energy", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "BMA", displayName: "Banco Macro", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "BHIP", displayName: "Banco Hipotecario", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "BYMA", displayName: "Bolsas y Mercados Argentinos", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "CAPX", displayName: "Capex", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "CEPU", displayName: "Central Puerto", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "LOMA", displayName: "Loma Negra", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "SUPV", displayName: "Grupo Supervielle", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "TGS", displayName: "Transportadora de Gas del Sur ADR", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "TGSU2", displayName: "Transportadora de Gas del Sur local", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "TGNO4", displayName: "Transportadora de Gas del Norte", hasCedear: false, category: "Argentina / Latam" },
  { symbol: "PBR", displayName: "Petrobras", hasCedear: true, category: "Argentina / Latam" },
  { symbol: "VALE", displayName: "Vale", hasCedear: true, category: "Argentina / Latam" },
  { symbol: "BBD", displayName: "Banco Bradesco", hasCedear: true, category: "Argentina / Latam" },
  { symbol: "ITUB", displayName: "Itau Unibanco", hasCedear: true, category: "Argentina / Latam" },

  { symbol: "XOM", displayName: "Exxon Mobil", hasCedear: true, category: "Energy" },
  { symbol: "CVX", displayName: "Chevron", hasCedear: true, category: "Energy" },
  { symbol: "TTE", displayName: "TotalEnergies", hasCedear: true, category: "Energy" },
  { symbol: "PBR", displayName: "Petrobras", hasCedear: true, category: "Energy" },
  { symbol: "YPF", displayName: "YPF", hasCedear: false, category: "Energy" },
  { symbol: "VIST", displayName: "Vista Energy", hasCedear: false, category: "Energy" },

  { symbol: "JPM", displayName: "JPMorgan Chase", hasCedear: true, category: "Financials" },
  { symbol: "BAC", displayName: "Bank of America", hasCedear: true, category: "Financials" },
  { symbol: "GS", displayName: "Goldman Sachs", hasCedear: true, category: "Financials" },
  { symbol: "AXP", displayName: "American Express", hasCedear: true, category: "Financials" },
  { symbol: "BRK.B", displayName: "Berkshire Hathaway", hasCedear: true, category: "Financials" },

  { symbol: "KO", displayName: "Coca-Cola", hasCedear: true, category: "Consumer Defensive" },
  { symbol: "PEP", displayName: "PepsiCo", hasCedear: true, category: "Consumer Defensive" },
  { symbol: "PG", displayName: "Procter & Gamble", hasCedear: true, category: "Consumer Defensive" },
  { symbol: "WMT", displayName: "Walmart", hasCedear: true, category: "Consumer Defensive" },
  { symbol: "COST", displayName: "Costco", hasCedear: true, category: "Consumer Defensive" },

  { symbol: "TSLA", displayName: "Tesla", hasCedear: true, category: "Consumer Cyclical" },
  { symbol: "NIO", displayName: "NIO", hasCedear: true, category: "Consumer Cyclical" },
  { symbol: "DIS", displayName: "Disney", hasCedear: true, category: "Consumer Cyclical" },
  { symbol: "PDD", displayName: "PDD Holdings", hasCedear: true, category: "Consumer Cyclical" },
  { symbol: "TM", displayName: "Toyota", hasCedear: true, category: "Consumer Cyclical" },
  { symbol: "UBER", displayName: "Uber", hasCedear: true, category: "Consumer Cyclical" },

  { symbol: "CVS", displayName: "CVS Health", hasCedear: true, category: "Healthcare" },
  { symbol: "GLOB", displayName: "Globant", hasCedear: true, category: "Industrial" },
  { symbol: "MRVL", displayName: "Marvell Technology", hasCedear: true, category: "Industrial" },
  { symbol: "QCOM", displayName: "Qualcomm", hasCedear: true, category: "Telecom" },
  { symbol: "CEPU", displayName: "Central Puerto", hasCedear: false, category: "Utilities" },
  { symbol: "VALE", displayName: "Vale", hasCedear: true, category: "Materials" },

  { symbol: "MSTR", displayName: "MicroStrategy", hasCedear: true, category: "Crypto / Bitcoin" },
  { symbol: "COIN", displayName: "Coinbase", hasCedear: true, category: "Crypto / Bitcoin" },
  { symbol: "IBIT", displayName: "iShares Bitcoin Trust", hasCedear: false, category: "Crypto / Bitcoin" },
  { symbol: "ETHA", displayName: "iShares Ethereum Trust ETF", hasCedear: false, category: "Crypto / Bitcoin" },

  { symbol: "SPY", displayName: "SPDR S&P 500 ETF", hasCedear: false, category: "ETFs" },
  { symbol: "QQQ", displayName: "Invesco QQQ ETF", hasCedear: false, category: "ETFs" },
  { symbol: "IBIT", displayName: "iShares Bitcoin Trust", hasCedear: false, category: "ETFs" },
  { symbol: "ETHA", displayName: "iShares Ethereum Trust ETF", hasCedear: false, category: "ETFs" }
];

export const watchlistTickerUniverse: WatchlistTickerMetadata[] = Object.values(
  rawTickerUniverse.reduce<Record<string, WatchlistTickerMetadata>>((items, item) => {
    const symbol = item.symbol.trim().toUpperCase();
    if (!items[symbol]) {
      items[symbol] = { ...item, symbol };
    }

    return items;
  }, {})
).sort((left, right) => {
  const leftPopular = popularSymbols.has(left.symbol) ? 0 : 1;
  const rightPopular = popularSymbols.has(right.symbol) ? 0 : 1;

  if (leftPopular !== rightPopular) {
    return leftPopular - rightPopular;
  }

  const categoryOrder = left.category.localeCompare(right.category);
  return categoryOrder !== 0 ? categoryOrder : left.symbol.localeCompare(right.symbol);
});

export const watchlistTickerBySymbol = Object.fromEntries(
  watchlistTickerUniverse.map((item) => [item.symbol, item])
);
