import type { DashboardSignal, SignalFilters, SignalSortKey } from "./types";

export const defaultSignalFilters: SignalFilters = {
  setupType: "all",
  minScore: null,
  minRs: null,
  minRvol: null,
  riskOnly: false,
  opportunityOnly: false,
  overextendedOnly: false,
  openingRedReversalOnly: false,
  sortBy: "scoreDesc"
};

export const scoreThresholds = [60, 75, 90] as const;
export const rsThresholds = [0, 1, 3] as const;
export const rvolThresholds = [1, 2, 3] as const;

export const sortOptions: Array<{ label: string; value: SignalSortKey }> = [
  { label: "Score", value: "scoreDesc" },
  { label: "RS", value: "rsDesc" },
  { label: "RVOL", value: "rvolDesc" },
  { label: "EXT", value: "extDesc" },
  { label: "A-Z", value: "symbolAsc" }
];

export function applySignalFilters(signals: DashboardSignal[], filters: SignalFilters): DashboardSignal[] {
  return signals
    .filter((signal) => matchesFilters(signal, filters))
    .slice()
    .sort((left, right) => compareSignals(left, right, filters.sortBy));
}

export function getAvailableSetupTypes(signals: DashboardSignal[]): string[] {
  return Array.from(new Set(signals.map((signal) => signal.setupType).filter(Boolean))).sort((left, right) =>
    left.localeCompare(right)
  );
}

export function hasActiveSignalFilters(filters: SignalFilters): boolean {
  return (
    filters.setupType !== defaultSignalFilters.setupType ||
    filters.minScore !== defaultSignalFilters.minScore ||
    filters.minRs !== defaultSignalFilters.minRs ||
    filters.minRvol !== defaultSignalFilters.minRvol ||
    filters.riskOnly !== defaultSignalFilters.riskOnly ||
    filters.opportunityOnly !== defaultSignalFilters.opportunityOnly ||
    filters.overextendedOnly !== defaultSignalFilters.overextendedOnly ||
    filters.openingRedReversalOnly !== defaultSignalFilters.openingRedReversalOnly ||
    filters.sortBy !== defaultSignalFilters.sortBy
  );
}

export function getEma20Extension(signal: DashboardSignal): number | null | undefined {
  return signal.extensionFromEma20Percent ?? signal.distanceFromEma20Percent;
}

function matchesFilters(signal: DashboardSignal, filters: SignalFilters) {
  if (filters.setupType !== "all" && signal.setupType !== filters.setupType) {
    return false;
  }

  if (filters.minScore != null && signal.score < filters.minScore) {
    return false;
  }

  if (filters.minRs != null && !passesThreshold(signal.relativeStrengthVsSpy, filters.minRs)) {
    return false;
  }

  if (filters.minRvol != null && !passesThreshold(signal.relativeVolume, filters.minRvol)) {
    return false;
  }

  if (filters.riskOnly && !isRiskSignal(signal)) {
    return false;
  }

  if (filters.opportunityOnly && !isOpportunitySignal(signal)) {
    return false;
  }

  if (filters.overextendedOnly && !passesThreshold(getEma20Extension(signal), 7)) {
    return false;
  }

  if (filters.openingRedReversalOnly && !signal.openingRedReversalDetected) {
    return false;
  }

  return true;
}

function compareSignals(left: DashboardSignal, right: DashboardSignal, sortBy: SignalSortKey) {
  if (sortBy === "symbolAsc") {
    return left.symbol.localeCompare(right.symbol);
  }

  const leftValue = getSortValue(left, sortBy);
  const rightValue = getSortValue(right, sortBy);
  const valueDelta = rightValue - leftValue;

  if (valueDelta !== 0) {
    return valueDelta;
  }

  return left.symbol.localeCompare(right.symbol);
}

function getSortValue(signal: DashboardSignal, sortBy: SignalSortKey) {
  switch (sortBy) {
    case "scoreDesc":
      return signal.score;
    case "rsDesc":
      return signal.relativeStrengthVsSpy ?? Number.NEGATIVE_INFINITY;
    case "rvolDesc":
      return signal.relativeVolume ?? Number.NEGATIVE_INFINITY;
    case "extDesc":
      return getEma20Extension(signal) ?? Number.NEGATIVE_INFINITY;
    case "symbolAsc":
      return 0;
  }
}

function passesThreshold(value: number | null | undefined, threshold: number) {
  return value != null && value >= threshold;
}

function isRiskSignal(signal: DashboardSignal) {
  const action = signal.action.toLowerCase();
  return signal.score < 40 || action.includes("avoid") || action.includes("high risk");
}

function isOpportunitySignal(signal: DashboardSignal) {
  return signal.action === "Candidate" || signal.score >= 75;
}
