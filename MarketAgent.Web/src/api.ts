import type {
  ApiMarketSignal,
  BriefingResult,
  DashboardSignal,
  DashboardState,
  HistoricalCandle,
  HistoricalMarketDataResult,
  IngestionResult,
  SignalRunResult,
  SparklinePricesBySymbol
} from "./types";

const API_BASE_URL = import.meta.env.VITE_MARKETAGENT_API_BASE_URL ?? "http://localhost:5215";

export async function runIngestion(): Promise<IngestionResult> {
  return postJson<IngestionResult>("/api/ingestion/run");
}

export async function runSignals(): Promise<SignalRunResult> {
  return postJson<SignalRunResult>("/api/signals/run");
}

export async function runBriefing(): Promise<BriefingResult> {
  return postJson<BriefingResult>("/api/briefing/run");
}

export async function loadHistoricalCandles(days = 60): Promise<HistoricalMarketDataResult> {
  return getJson<HistoricalMarketDataResult>(`/api/historical/candles?days=${days}`);
}

export async function loadDashboard(): Promise<DashboardState> {
  try {
    return {
      briefing: await runBriefing(),
      isMock: false
    };
  } catch {
    try {
      const signalRun = await runSignals();
      return {
        briefing: createBriefingFromSignals(signalRun),
        isMock: false
      };
    } catch {
      return {
        briefing: mockBriefing,
        isMock: true
      };
    }
  }
}

export function toDashboardSignal(signal: ApiMarketSignal): DashboardSignal {
  return {
    ...signal,
    rsi14: signal.rsi,
    extensionFromEma20Percent: signal.extensionFromEma20Percent ?? signal.distanceFromEma20Percent,
    scoreBreakdown: signal.scoreBreakdown ?? []
  };
}

export function buildSparklinePricesBySymbol(candles: HistoricalCandle[], limit = 20): SparklinePricesBySymbol {
  const grouped = candles.reduce<Record<string, HistoricalCandle[]>>((accumulator, candle) => {
    if (!Number.isFinite(candle.close)) {
      return accumulator;
    }

    const symbol = candle.symbol.toUpperCase();
    accumulator[symbol] = [...(accumulator[symbol] ?? []), candle];
    return accumulator;
  }, {});

  return Object.fromEntries(
    Object.entries(grouped).map(([symbol, symbolCandles]) => [
      symbol,
      symbolCandles
        .slice()
        .sort((left, right) => Date.parse(left.occurredAtUtc) - Date.parse(right.occurredAtUtc))
        .slice(-limit)
        .map((candle) => candle.close)
    ])
  );
}

function createBriefingFromSignals(result: SignalRunResult): BriefingResult {
  const allSignals = result.signals.map(toDashboardSignal);
  const topOpportunities = allSignals.filter(
    (signal) => signal.score >= 60 && signal.action === "Candidate" && ["Medium", "High"].includes(signal.confidence)
  );
  const watchlistPullbacks = allSignals.filter(
    (signal) => signal.setupType === "Pullback" || signal.action.startsWith("Watch")
  );
  const topRisks = allSignals.filter((signal) => signal.score < 40 || signal.action === "Avoid / high risk");

  return {
    generatedAtUtc: result.generatedAtUtc,
    marketRegime: "Signals only",
    summary: "Signals were generated successfully. AI briefing is unavailable.",
    signalSummary: `${allSignals.length} calculated signals returned from the API.`,
    allSignals,
    topOpportunities,
    watchlistPullbacks,
    topRisks,
    highlights: [],
    risks: [],
    watchItems: allSignals
      .filter((signal) => !topOpportunities.includes(signal) && !watchlistPullbacks.includes(signal) && !topRisks.includes(signal))
      .map((signal) => `${signal.symbol}: ${signal.reason}`)
  };
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      Accept: "application/json"
    }
  });

  if (!response.ok) {
    throw new Error(`${path} failed with ${response.status}`);
  }

  return response.json() as Promise<T>;
}

async function postJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: {
      Accept: "application/json"
    }
  });

  if (!response.ok) {
    throw new Error(`${path} failed with ${response.status}`);
  }

  return response.json() as Promise<T>;
}

const mockBriefing: BriefingResult = {
  generatedAtUtc: new Date().toISOString(),
  marketRegime: "Offline preview",
  summary: "API unavailable. Showing a small mock dataset so the dashboard layout can be reviewed.",
  signalSummary: "Connect the backend to replace this preview with calculated MarketAgent signals.",
  allSignals: [
    {
      symbol: "ABNB",
      score: 64.2,
      setupType: "BullishContinuation",
      action: "Candidate",
      confidence: "Medium",
      timeframe: "Intraday",
      reason: "price closed near the session high; positive EMA20 slope",
      rsi14: 61,
      ema9: 132.4,
      ema20: 128.7,
      ema50: 121.6,
      atr14: 3.2,
      relativeStrengthVsSpy: 2.4,
      relativeVolume: 1.8,
      distanceFromEma20Percent: 4.2,
      extensionFromEma20Percent: 4.2,
      entry: 134.1,
      stop: 130.9,
      takeProfit1: 138.9,
      takeProfit2: 142.1,
      takeProfit3: 145.3,
      riskReward1: 1.5,
      riskReward2: 2.5,
      riskReward3: 3.5,
      scoreBreakdown: [
        { label: "Price above EMA20", points: 5 },
        { label: "Positive EMA20 slope", points: 8 }
      ]
    },
    {
      symbol: "RKLB",
      score: 54.8,
      setupType: "MomentumContinuation",
      action: "Watch for confirmation",
      confidence: "Low",
      timeframe: "WatchOnly",
      reason: "strong recovery after gap-down; buyers absorbed early selling pressure",
      recoveryFromLowPercent: 88,
      relativeStrengthVsSpy: 4.1,
      relativeVolume: 2.6,
      extensionFromEma20Percent: 1.4,
      strongIntradayRecovery: true,
      gapPercent: -8.5,
      gapRecovery: true,
      scoreBreakdown: [
        { label: "Gap-down recovery", points: 10 },
        { label: "Intraday weakness reduced due to recovery", points: -4 }
      ]
    },
    {
      symbol: "PATH",
      score: 32.4,
      setupType: "Risk",
      action: "Avoid / high risk",
      confidence: "Low",
      timeframe: "WatchOnly",
      reason: "price below EMA20; price below EMA50",
      ema20: 13.8,
      ema50: 15.1,
      relativeStrengthVsSpy: -1.8,
      relativeVolume: 0.7,
      distanceFromEma20Percent: -27.5,
      extensionFromEma20Percent: -27.5,
      scoreBreakdown: [
        { label: "Price below EMA20", points: -6 },
        { label: "Price below EMA50", points: -8 }
      ]
    }
  ],
  topOpportunities: [],
  watchlistPullbacks: [],
  topRisks: [],
  highlights: ["Offline layout preview"],
  risks: ["Backend API could not be reached"],
  watchItems: []
};

mockBriefing.topOpportunities = mockBriefing.allSignals.filter((signal) => signal.action === "Candidate");
mockBriefing.watchlistPullbacks = mockBriefing.allSignals.filter((signal) => signal.action.startsWith("Watch"));
mockBriefing.topRisks = mockBriefing.allSignals.filter((signal) => signal.action === "Avoid / high risk");
