import type {
  ApiMarketSignal,
  BriefingResult,
  DashboardSignal,
  DashboardState,
  HistoricalCandle,
  HistoricalMarketDataResult,
  IngestionResult,
  SignalOutcomeScoreBucketSummary,
  SignalOutcomeSetupSummary,
  SignalOutcomeSummary,
  SignalPerformancePreviewResult,
  SignalRunResult,
  SparklinePricesBySymbol
} from "./types";

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  "https://marketagent-api-d6cqe0bncfhyhmh6.eastus-01.azurewebsites.net";

  if (import.meta.env.DEV) {
  console.info(`MarketAgent API base URL: ${API_BASE_URL}`);
}

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

export async function loadSignalPerformancePreview(days = 180): Promise<SignalPerformancePreviewResult> {
  return getJson<SignalPerformancePreviewResult>(`/api/signals/performance-preview?days=${days}`);
}

export async function loadSignalOutcomeSummary(): Promise<SignalOutcomeSummary> {
  return getJson<SignalOutcomeSummary>("/api/signals/outcomes/summary");
}

export async function loadSignalOutcomeSetupSummary(): Promise<SignalOutcomeSetupSummary> {
  return getJson<SignalOutcomeSetupSummary>("/api/signals/outcomes/setup-summary");
}

export async function loadSignalOutcomeScoreBuckets(): Promise<SignalOutcomeScoreBucketSummary> {
  return getJson<SignalOutcomeScoreBucketSummary>("/api/signals/outcomes/score-buckets");
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
    marketRegime: "Solo señales",
    summary: "Las señales se generaron correctamente. El briefing de IA no está disponible.",
    signalSummary: `${allSignals.length} señales calculadas devueltas por la API.`,
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
  marketRegime: "Vista previa offline",
  summary: "API no disponible. Se muestra una muestra simulada para revisar el layout del panel.",
  signalSummary: "Conectá el backend para reemplazar esta vista previa por señales calculadas de MarketAgent.",
  allSignals: [
    {
      symbol: "ABNB",
      score: 78.2,
      setupType: "BullishContinuation",
      action: "Candidate",
      confidence: "Medium",
      timeframe: "Intraday",
      reason: "precio cerca de máximos de la rueda; pendiente positiva de EMA20",
      recoveryFromLowPercent: 82,
      rsi14: 61,
      ema9: 132.4,
      ema20: 128.7,
      ema50: 121.6,
      atr14: 3.2,
      relativeStrengthVsSpy: 3.4,
      relativeVolume: 2.2,
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
        { label: "Precio sobre EMA20", points: 5 },
        { label: "Pendiente positiva de EMA20", points: 8 }
      ]
    },
    {
      symbol: "RKLB",
      score: 54.8,
      setupType: "MomentumContinuation",
      action: "Watch for confirmation",
      confidence: "Low",
      timeframe: "WatchOnly",
      reason: "fuerte recuperacion despues de gap-down; compradores absorbieron la presion vendedora inicial",
      recoveryFromLowPercent: 88,
      relativeStrengthVsSpy: 4.1,
      relativeVolume: 2.6,
      extensionFromEma20Percent: 1.4,
      strongIntradayRecovery: true,
      gapPercent: -8.5,
      gapRecovery: true,
      openingRedReversalDetected: true,
      openGapPercent: -8.5,
      openingRedReversalRecoveryFromLowPercent: 11.36,
      reclaimOpen: true,
      reclaimPreviousClose: false,
      scoreBreakdown: [
        { label: "Recuperación de gap-down", points: 10 },
        { label: "Reversión desde apertura roja", points: 6 },
        { label: "Debilidad intradiaria reducida por recuperacion", points: -4 }
      ]
    },
    {
      symbol: "PATH",
      score: 32.4,
      setupType: "Risk",
      action: "Avoid / high risk",
      confidence: "Low",
      timeframe: "WatchOnly",
      reason: "precio debajo de EMA20; precio debajo de EMA50",
      ema20: 13.8,
      ema50: 15.1,
      relativeStrengthVsSpy: -1.8,
      relativeVolume: 0.7,
      distanceFromEma20Percent: -27.5,
      extensionFromEma20Percent: -27.5,
      scoreBreakdown: [
        { label: "Precio debajo de EMA20", points: -6 },
        { label: "Precio debajo de EMA50", points: -8 }
      ]
    }
  ],
  topOpportunities: [],
  watchlistPullbacks: [],
  topRisks: [],
  highlights: ["Vista previa offline del layout"],
  risks: ["No se pudo conectar con la API backend"],
  watchItems: []
};

mockBriefing.topOpportunities = mockBriefing.allSignals.filter((signal) => signal.action === "Candidate");
mockBriefing.watchlistPullbacks = mockBriefing.allSignals.filter((signal) => signal.action.startsWith("Watch"));
mockBriefing.topRisks = mockBriefing.allSignals.filter((signal) => signal.action === "Avoid / high risk");
