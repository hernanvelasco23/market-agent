export type SignalAction =
  | "Candidate"
  | "Watch for confirmation"
  | "Watch for pullback or breakout confirmation"
  | "Avoid / high risk"
  | string;

export interface ScoreFactor {
  label: string;
  points: number;
}

export interface DashboardSignal {
  symbol: string;
  score: number;
  setupType: string;
  action: SignalAction;
  timeframe: string;
  confidence: string;
  reason: string;
  ema9?: number | null;
  ema20?: number | null;
  ema50?: number | null;
  rsi14?: number | null;
  atr14?: number | null;
  aboveVwap?: boolean | null;
  relativeStrengthVsSpy?: number | null;
  relativeVolume?: number | null;
  recoveryFromLowPercent?: number | null;
  strongIntradayRecovery?: boolean;
  gapPercent?: number | null;
  gapRecovery?: boolean;
  openingRedReversalDetected?: boolean;
  openGapPercent?: number | null;
  openingRedReversalRecoveryFromLowPercent?: number | null;
  reclaimOpen?: boolean;
  reclaimPreviousClose?: boolean;
  ema20Slope?: number | null;
  ema50Slope?: number | null;
  strongTrendSlope?: boolean;
  distanceFromEma20Percent?: number | null;
  extensionFromEma20Percent?: number | null;
  extensionRisk?: string | null;
  momentumContinuation?: boolean;
  entry?: number | null;
  stop?: number | null;
  target?: number | null;
  takeProfit1?: number | null;
  takeProfit2?: number | null;
  takeProfit3?: number | null;
  riskReward1?: number | null;
  riskReward2?: number | null;
  riskReward3?: number | null;
  riskPerShare?: number | null;
  suggestedPositionSize?: number | null;
  scoreBreakdown?: ScoreFactor[];
}

export interface OpportunitySignal extends DashboardSignal {}

export interface PullbackSignal extends DashboardSignal {
  confirmationNeeded?: boolean;
}

export interface RiskSignal extends DashboardSignal {
  riskType?: string;
}

export interface BriefingResult {
  generatedAtUtc: string;
  marketRegime: string;
  summary: string;
  signalSummary: string;
  allSignals: DashboardSignal[];
  topOpportunities: OpportunitySignal[];
  watchlistPullbacks: PullbackSignal[];
  topRisks: RiskSignal[];
  highlights: string[];
  risks: string[];
  watchItems: string[];
  warning?: string | null;
}

export interface SignalRunResult {
  generatedAtUtc: string;
  signals: ApiMarketSignal[];
}

export interface ApiMarketSignal {
  symbol: string;
  score: number;
  setupType: string;
  action: SignalAction;
  timeframe: string;
  confidence: string;
  reason: string;
  ema9?: number | null;
  ema20?: number | null;
  ema50?: number | null;
  rsi?: number | null;
  atr14?: number | null;
  aboveVwap?: boolean | null;
  relativeStrengthVsSpy?: number | null;
  relativeVolume?: number | null;
  recoveryFromLowPercent?: number | null;
  strongIntradayRecovery?: boolean;
  gapPercent?: number | null;
  gapRecovery?: boolean;
  openingRedReversalDetected?: boolean;
  openGapPercent?: number | null;
  openingRedReversalRecoveryFromLowPercent?: number | null;
  reclaimOpen?: boolean;
  reclaimPreviousClose?: boolean;
  ema20Slope?: number | null;
  ema50Slope?: number | null;
  strongTrendSlope?: boolean;
  distanceFromEma20Percent?: number | null;
  extensionFromEma20Percent?: number | null;
  extensionRisk?: string | null;
  momentumContinuation?: boolean;
  entry?: number | null;
  stop?: number | null;
  target?: number | null;
  takeProfit1?: number | null;
  takeProfit2?: number | null;
  takeProfit3?: number | null;
  riskReward1?: number | null;
  riskReward2?: number | null;
  riskReward3?: number | null;
  riskPerShare?: number | null;
  suggestedPositionSize?: number | null;
  scoreBreakdown?: ScoreFactor[];
}

export interface IngestionResult {
  totalRequested: number;
  succeeded: number;
  failed: number;
  executedAtUtc: string;
  failures: Array<{
    symbol: string;
    reason: string;
    source?: string | null;
  }>;
}

export interface HistoricalCandle {
  symbol: string;
  occurredAtUtc: string;
  close: number;
}

export interface HistoricalMarketDataResult {
  generatedAtUtc: string;
  requestedDays: number;
  candles: HistoricalCandle[];
  failures: Array<{
    symbol: string;
    reason: string;
    source?: string | null;
  }>;
}

export type SparklinePricesBySymbol = Record<string, number[]>;

export interface DashboardState {
  briefing: BriefingResult;
  isMock: boolean;
}

export type AlertSeverity = "info" | "opportunity" | "warning" | "risk";

export type AlertMetricTone = "positive" | "neutral" | "warning" | "risk";

export interface DashboardAlertMetric {
  label: string;
  value: string;
  tone?: AlertMetricTone;
}

export interface DashboardAlert {
  id: string;
  symbol: string;
  title: string;
  description: string;
  severity: AlertSeverity;
  setupType?: string;
  action?: string;
  metrics: DashboardAlertMetric[];
}

export type SignalSortKey = "scoreDesc" | "rsDesc" | "rvolDesc" | "extDesc" | "symbolAsc";

export interface SignalFilters {
  setupType: string;
  minScore: number | null;
  minRs: number | null;
  minRvol: number | null;
  riskOnly: boolean;
  opportunityOnly: boolean;
  overextendedOnly: boolean;
  openingRedReversalOnly: boolean;
  sortBy: SignalSortKey;
}

export type WatchlistKind = "all" | "predefined" | "custom";

export interface Watchlist {
  id: string;
  name: string;
  symbols: string[];
  kind: WatchlistKind;
}
