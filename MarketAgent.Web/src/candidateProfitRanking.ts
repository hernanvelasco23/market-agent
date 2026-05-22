import type { DashboardSignal } from "./types";

export interface CandidateProfitOpportunity {
  signal: DashboardSignal;
  entryPoint: number;
  takeProfit: number;
  stopLoss?: number | null;
  riskReward?: number | null;
  potentialProfitPct: number;
}

export function getTopProfitOpportunities(
  signals: DashboardSignal[],
  limit = 5
): CandidateProfitOpportunity[] {
  return signals
    .map(toProfitOpportunity)
    .filter((opportunity): opportunity is CandidateProfitOpportunity => opportunity !== null)
    .sort(compareOpportunities)
    .slice(0, limit);
}

function toProfitOpportunity(signal: DashboardSignal): CandidateProfitOpportunity | null {
  if (signal.action !== "Candidate" || !isPositiveFinite(signal.entry)) {
    return null;
  }

  const target = selectTakeProfit(signal, signal.entry);

  if (target === null) {
    return null;
  }

  return {
    signal,
    entryPoint: signal.entry,
    takeProfit: target.takeProfit,
    stopLoss: isFiniteNumber(signal.stop) ? signal.stop : null,
    riskReward: target.riskReward,
    potentialProfitPct: ((target.takeProfit - signal.entry) / signal.entry) * 100
  };
}

function selectTakeProfit(
  signal: DashboardSignal,
  entryPoint: number
): { takeProfit: number; riskReward?: number | null } | null {
  const candidates = [
    { takeProfit: signal.takeProfit2, riskReward: signal.riskReward2 },
    { takeProfit: signal.takeProfit1, riskReward: signal.riskReward1 },
    { takeProfit: signal.takeProfit3, riskReward: signal.riskReward3 }
  ];

  for (const candidate of candidates) {
    if (!isFiniteNumber(candidate.takeProfit) || candidate.takeProfit <= entryPoint) {
      continue;
    }

    if (candidate.riskReward != null && (!isFiniteNumber(candidate.riskReward) || candidate.riskReward < 1)) {
      continue;
    }

    return {
      takeProfit: candidate.takeProfit,
      riskReward: candidate.riskReward ?? null
    };
  }

  return null;
}

function compareOpportunities(left: CandidateProfitOpportunity, right: CandidateProfitOpportunity) {
  const profitDifference = right.potentialProfitPct - left.potentialProfitPct;
  if (profitDifference !== 0) {
    return profitDifference;
  }

  const riskRewardDifference = riskRewardSortValue(right.riskReward) - riskRewardSortValue(left.riskReward);
  if (riskRewardDifference !== 0) {
    return riskRewardDifference;
  }

  return right.signal.score - left.signal.score;
}

function riskRewardSortValue(value?: number | null) {
  return value == null ? Number.NEGATIVE_INFINITY : value;
}

function isPositiveFinite(value?: number | null): value is number {
  return isFiniteNumber(value) && value > 0;
}

function isFiniteNumber(value?: number | null): value is number {
  return typeof value === "number" && Number.isFinite(value);
}
