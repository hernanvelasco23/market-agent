import type {
  ApiMarketSignal,
  BriefingResult,
  DashboardSignal,
  DashboardState,
  HistoricalCandle,
  HistoricalMarketDataResult,
  IngestionResult,
  MarketSnapshotDto,
  SignalOutcomeScoreBucketSummary,
  SignalOutcomeSetupSummary,
  SignalOutcomeSummary,
  SignalOutcomeItem,
  SignalPerformancePreviewResult,
  SignalRunResult,
  SparklinePricesBySymbol,
  SystemStatus,
  WatchlistHydrationRequest,
  WatchlistHydrationResult
} from "./types";
import { API_BASE_URL, buildApiUrl } from "./config/api";

console.info("[MarketAgent] API base URL:", API_BASE_URL);

export async function runIngestion(): Promise<IngestionResult> {
  return postJson<IngestionResult>("ingestion/run");
}

export async function runSignals(): Promise<SignalRunResult> {
  return postJson<SignalRunResult>("signals/run");
}

export async function hydrateWatchlist(request: WatchlistHydrationRequest): Promise<WatchlistHydrationResult> {
  return postJson<WatchlistHydrationResult>("watchlist/hydrate", request);
}

export async function loadLatestSignals(): Promise<SignalRunResult> {
  return getJson<SignalRunResult>("signals/latest");
}

export async function loadMarketSnapshots(): Promise<MarketSnapshotDto[]> {
  return getJson<MarketSnapshotDto[]>("ingestion/snapshots");
}

export async function loadSignalOutcomes(limit = 200): Promise<SignalOutcomeItem[]> {
  return getJson<SignalOutcomeItem[]>(`signals/outcomes?limit=${limit}`);
}

export async function runBriefing(): Promise<BriefingResult> {
  return postJson<BriefingResult>("briefing/run");
}

export async function loadSystemStatus(): Promise<SystemStatus> {
  return getJson<SystemStatus>("system/status");
}

export async function loadHistoricalCandles(days = 60): Promise<HistoricalMarketDataResult> {
  return getJson<HistoricalMarketDataResult>(`historical/candles?days=${days}`);
}

export async function loadSignalPerformancePreview(days = 180): Promise<SignalPerformancePreviewResult> {
  return getJson<SignalPerformancePreviewResult>(`signals/performance-preview?days=${days}`);
}

export async function loadSignalOutcomeSummary(): Promise<SignalOutcomeSummary> {
  return getJson<SignalOutcomeSummary>("signals/outcomes/summary");
}

export async function loadSignalOutcomeSetupSummary(): Promise<SignalOutcomeSetupSummary> {
  return getJson<SignalOutcomeSetupSummary>("signals/outcomes/setup-summary");
}

export async function loadSignalOutcomeScoreBuckets(): Promise<SignalOutcomeScoreBucketSummary> {
  return getJson<SignalOutcomeScoreBucketSummary>("signals/outcomes/score-buckets");
}

export async function loadDashboard(): Promise<DashboardState> {
  const systemStatus = await loadSystemStatus().catch(() => null);

  try {
    const outcomes = await loadSignalOutcomes();
    return {
      briefing: createBriefingFromOutcomes(outcomes),
      systemStatus,
      isMock: false
    };
  } catch {
    if (systemStatus) {
      return {
        briefing: createBriefingFromOutcomes([]),
        systemStatus,
        isMock: false
      };
    }

    return {
      briefing: mockBriefing,
      systemStatus,
      isMock: true
    };
  }
}

export function toDashboardSignal(signal: ApiMarketSignal): DashboardSignal {
  return {
    ...signal,
    currentPrice: signal.currentPrice ?? signal.entry ?? null,
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

function createBriefingFromOutcomes(outcomes: SignalOutcomeItem[]): BriefingResult {
  const latestBySymbol = Array.from(
    outcomes
      .slice()
      .sort((left, right) => Date.parse(right.signalCreatedAtUtc) - Date.parse(left.signalCreatedAtUtc))
      .reduce<Map<string, SignalOutcomeItem>>((items, outcome) => {
        const symbol = outcome.symbol.toUpperCase();
        if (!items.has(symbol)) {
          items.set(symbol, outcome);
        }

        return items;
      }, new Map())
      .values()
  );
  const allSignals = latestBySymbol.map(toDashboardSignalFromOutcome);
  const generatedAtUtc = latestBySymbol
    .map((outcome) => outcome.signalCreatedAtUtc)
    .sort((left, right) => Date.parse(right) - Date.parse(left))[0] ?? new Date().toISOString();
  const topOpportunities = allSignals.filter(
    (signal) => signal.score >= 60 && signal.action === "Candidate" && ["Medium", "High"].includes(signal.confidence)
  );
  const watchlistPullbacks = allSignals.filter(
    (signal) => signal.setupType === "Pullback" || signal.action.startsWith("Watch")
  );
  const topRisks = allSignals.filter((signal) => signal.score < 40 || signal.action === "Avoid / high risk");

  return {
    generatedAtUtc,
    marketRegime: "API connected",
    summary: allSignals.length === 0
      ? "API conectada. Todavía no hay señales persistidas para mostrar."
      : "Señales persistidas cargadas desde outcomes. La generación de IA es manual.",
    signalSummary: `${allSignals.length} señales persistidas cargadas desde outcomes.`,
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

function toDashboardSignalFromOutcome(outcome: SignalOutcomeItem): DashboardSignal {
  return {
    symbol: outcome.symbol,
    score: outcome.score,
    setupType: outcome.setup || "Unknown",
    action: outcome.action,
    timeframe: "Persisted",
    confidence: outcome.confidence,
    reason: buildOutcomeReason(outcome),
    currentPrice: outcome.currentPrice ?? outcome.priceAtSignal ?? outcome.entry ?? null,
    relativeStrengthVsSpy: outcome.relativeStrengthVsSpy ?? null,
    relativeVolume: outcome.relativeVolume ?? null,
    extensionFromEma20Percent: outcome.extensionFromEma20Percent ?? null,
    rsi14: outcome.rsi ?? null,
    ema9: outcome.ema9 ?? null,
    ema20: outcome.ema20 ?? null,
    ema50: outcome.ema50 ?? null,
    entry: outcome.entry ?? outcome.priceAtSignal ?? null,
    stop: outcome.stop ?? null,
    target: outcome.target ?? null,
    takeProfit1: outcome.takeProfit1 ?? outcome.target ?? null,
    takeProfit2: outcome.takeProfit2 ?? null,
    takeProfit3: outcome.takeProfit3 ?? null,
    riskReward1: outcome.riskReward1 ?? null,
    riskReward2: outcome.riskReward2 ?? null,
    riskReward3: outcome.riskReward3 ?? null,
    recoveryFromLowPercent: outcome.maxRunupPercent ?? null,
    distanceFromEma20Percent: outcome.extensionFromEma20Percent ?? null,
    scoreBreakdown: []
  };
}

function buildOutcomeReason(outcome: SignalOutcomeItem) {
  const pieces = [
    `Persisted signal from ${new Date(outcome.signalCreatedAtUtc).toLocaleString()}`,
    outcome.evaluationStatus ? `status ${outcome.evaluationStatus}` : null,
    outcome.priceAfter15Minutes != null ? `15m ${outcome.priceAfter15Minutes.toFixed(2)}` : null,
    outcome.priceAfter1Hour != null ? `1h ${outcome.priceAfter1Hour.toFixed(2)}` : null,
    outcome.outcomePercent != null ? `outcome ${outcome.outcomePercent.toFixed(2)}%` : null
  ].filter(Boolean);

  return pieces.join("; ");
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(buildApiUrl(path), {
    headers: {
      Accept: "application/json"
    }
  });

  if (!response.ok) {
    throw new Error(`${path} failed with ${response.status}`);
  }

  return response.json() as Promise<T>;
}

async function postJson<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(buildApiUrl(path), {
    method: "POST",
    headers: {
      Accept: "application/json",
      ...(body == null ? {} : { "Content-Type": "application/json" })
    },
    body: body == null ? undefined : JSON.stringify(body)
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
