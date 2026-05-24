import type { DashboardAlert, DashboardAlertMetric, DashboardSignal } from "./types";

const MOMENTUM_BREAKOUT_SCORE = 75;
const MOMENTUM_BREAKOUT_RS = 3;
const MOMENTUM_BREAKOUT_RVOL = 2;
const NEAR_HIGH_RANGE_POSITION = 70;
const OVEREXTENDED_EMA20_PERCENT = 7;
const LOW_SCORE_RISK = 40;

export function deriveDashboardAlerts(signals: DashboardSignal[]): DashboardAlert[] {
  const alerts = signals.flatMap((signal) => [
    deriveMomentumBreakoutAlert(signal),
    deriveOpeningRedReversalAlert(signal),
    deriveEmaReclaimAlert(signal),
    deriveOverextendedWarningAlert(signal),
    deriveMomentumFailureAlert(signal)
  ]);

  return alerts
    .filter((alert): alert is DashboardAlert => alert != null)
    .sort(compareAlerts)
    .slice(0, 12);
}

function deriveMomentumBreakoutAlert(signal: DashboardSignal): DashboardAlert | null {
  const rs = signal.relativeStrengthVsSpy;
  const rvol = signal.relativeVolume;
  const rangePosition = signal.recoveryFromLowPercent;
  const hasNearHighProxy = rangePosition == null || rangePosition >= NEAR_HIGH_RANGE_POSITION;

  if (signal.score < MOMENTUM_BREAKOUT_SCORE || rs == null || rs <= MOMENTUM_BREAKOUT_RS || rvol == null || rvol < MOMENTUM_BREAKOUT_RVOL || !hasNearHighProxy) {
    return null;
  }

  return createAlert(signal, {
    type: "momentum-breakout",
    title: "Breakout de momentum en observación",
    description: "Score alto con RS fuerte y RVOL elevado. La posición dentro del rango se usa como proxy de cercanía a máximos cuando está disponible.",
    severity: "opportunity",
    metrics: [
      metric("Score", formatNumber(signal.score), "positive"),
      metric("RS vs SPY", formatPercent(rs), "positive"),
      metric("RVOL", formatMultiplier(rvol), rvol >= 3 ? "positive" : "neutral"),
      optionalMetric("Pos. rango", rangePosition, formatPercent, "neutral")
    ]
  });
}

function deriveOpeningRedReversalAlert(signal: DashboardSignal): DashboardAlert | null {
  if (!signal.openingRedReversalDetected) {
    return null;
  }

  return createAlert(signal, {
    type: "opening-red-reversal",
    title: signal.reclaimPreviousClose ? "Reversión desde apertura roja con recuperación del cierre previo" : "Reversión desde apertura roja",
    description: signal.reclaimPreviousClose
      ? "Abrió en rojo, recuperó sobre la apertura y recuperó el cierre previo."
      : "Abrió en rojo, recuperó desde el mínimo intradiario y recuperó la apertura.",
    severity: signal.reclaimPreviousClose ? "opportunity" : "info",
    metrics: [
      optionalMetric("Gap apertura", signal.openGapPercent, formatPercent, signal.openGapPercent != null && signal.openGapPercent < 0 ? "warning" : "neutral"),
      optionalMetric("Recuperación", signal.openingRedReversalRecoveryFromLowPercent, formatPercent, "positive"),
      metric("Recupera apertura", signal.reclaimOpen ? "Sí" : "No", signal.reclaimOpen ? "positive" : "neutral"),
      metric("Cierre previo", signal.reclaimPreviousClose ? "Recuperado" : "Pendiente", signal.reclaimPreviousClose ? "positive" : "neutral"),
      optionalMetric("RVOL", signal.relativeVolume, formatMultiplier, signal.relativeVolume != null && signal.relativeVolume >= 2 ? "positive" : "neutral")
    ]
  });
}

function deriveEmaReclaimAlert(signal: DashboardSignal): DashboardAlert | null {
  const price = signal.entry;
  const ema20 = signal.ema20;

  if (price == null || ema20 == null || price <= ema20 || !hasPriorWeaknessEvidence(signal)) {
    return null;
  }

  return createAlert(signal, {
    type: "ema-reclaim",
    title: "Recuperación sobre EMA20",
    description: "El precio está sobre EMA20 y la señal muestra debilidad previa o recuperación.",
    severity: "info",
    metrics: [
      metric("Precio", formatMoney(price), "positive"),
      metric("EMA20", formatMoney(ema20), "neutral"),
      optionalMetric("EXT", getEma20Extension(signal), formatPercent, "neutral")
    ]
  });
}

function deriveOverextendedWarningAlert(signal: DashboardSignal): DashboardAlert | null {
  const extension = getEma20Extension(signal);

  if (extension == null || extension <= OVEREXTENDED_EMA20_PERCENT) {
    return null;
  }

  return createAlert(signal, {
    type: "overextended-warning",
    title: "Extendida sobre EMA20",
    description: "La extensión sobre EMA20 supera el umbral de atención. El momentum puede seguir, pero aumenta el riesgo de pullback.",
    severity: "warning",
    metrics: [
      metric("EXT", formatPercent(extension), extension > 15 ? "risk" : "warning"),
      metric("Score", formatNumber(signal.score), signal.score >= 75 ? "positive" : "neutral"),
      optionalMetric("RS vs SPY", signal.relativeStrengthVsSpy, formatPercent, signal.relativeStrengthVsSpy != null && signal.relativeStrengthVsSpy < 0 ? "risk" : "neutral")
    ]
  });
}

function deriveMomentumFailureAlert(signal: DashboardSignal): DashboardAlert | null {
  const rs = signal.relativeStrengthVsSpy;
  const price = signal.entry;
  const ema20 = signal.ema20;
  const actionRisk = signal.action.toLowerCase().includes("avoid") || signal.action.toLowerCase().includes("high risk");
  const belowEma20WithWeakRs = rs != null && rs < 0 && price != null && ema20 != null && price < ema20;

  if (signal.score >= LOW_SCORE_RISK && !actionRisk && !belowEma20WithWeakRs) {
    return null;
  }

  return createAlert(signal, {
    type: "momentum-failure",
    title: "Riesgo de falla de momentum",
    description: "La señal muestra score débil, acción de riesgo o RS negativa con precio debajo de EMA20.",
    severity: "risk",
    metrics: [
      metric("Score", formatNumber(signal.score), signal.score < LOW_SCORE_RISK ? "risk" : "neutral"),
      optionalMetric("RS vs SPY", rs, formatPercent, rs != null && rs < 0 ? "risk" : "neutral"),
      optionalMetric("Precio", price, formatMoney, price != null && ema20 != null && price < ema20 ? "risk" : "neutral"),
      optionalMetric("EMA20", ema20, formatMoney, "neutral")
    ]
  });
}

function createAlert(
  signal: DashboardSignal,
  input: {
    type: string;
    title: string;
    description: string;
    severity: DashboardAlert["severity"];
    metrics: Array<DashboardAlertMetric | null>;
  }
): DashboardAlert {
  return {
    id: `${input.type}:${signal.symbol}`,
    symbol: signal.symbol,
    title: input.title,
    description: input.description,
    severity: input.severity,
    setupType: signal.setupType,
    action: signal.action,
    metrics: input.metrics.filter((item): item is DashboardAlertMetric => item != null)
  };
}

function hasPriorWeaknessEvidence(signal: DashboardSignal) {
  const text = [
    signal.setupType,
    signal.reason,
    ...((signal.scoreBreakdown ?? []).map((factor) => factor.label))
  ]
    .join(" ")
    .toLowerCase();

  return ["pullback", "weakness", "recovery", "recover", "reclaim", "gap-down", "below ema20"].some((token) => text.includes(token));
}

function getEma20Extension(signal: DashboardSignal) {
  return signal.extensionFromEma20Percent ?? signal.distanceFromEma20Percent;
}

function optionalMetric(
  label: string,
  value: number | null | undefined,
  formatter: (value: number) => string,
  tone?: DashboardAlertMetric["tone"]
): DashboardAlertMetric | null {
  return value == null ? null : metric(label, formatter(value), tone);
}

function metric(label: string, value: string, tone?: DashboardAlertMetric["tone"]): DashboardAlertMetric {
  return { label, value, tone };
}

function compareAlerts(left: DashboardAlert, right: DashboardAlert) {
  const severityDelta = severityRank(right.severity) - severityRank(left.severity);
  if (severityDelta !== 0) {
    return severityDelta;
  }

  return left.symbol.localeCompare(right.symbol);
}

function severityRank(severity: DashboardAlert["severity"]) {
  switch (severity) {
    case "risk":
      return 4;
    case "opportunity":
      return 3;
    case "warning":
      return 2;
    case "info":
      return 1;
  }
}

function formatMoney(value: number) {
  return value.toFixed(2);
}

function formatNumber(value: number) {
  return value.toFixed(2);
}

function formatPercent(value: number) {
  return `${formatNumber(value)}%`;
}

function formatMultiplier(value: number) {
  return `${formatNumber(value)}x`;
}
