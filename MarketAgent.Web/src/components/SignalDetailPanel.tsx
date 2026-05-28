import { Sparkline } from "./Sparkline";
import { formatActionLabel, formatConfidenceLabel } from "../displayLabels";
import type { DashboardSignal } from "../types";

type SignalDetailPanelProps = {
  signal: DashboardSignal | null;
  sparklinePrices?: number[] | null;
};

export function SignalDetailPanel({ signal, sparklinePrices }: SignalDetailPanelProps) {
  if (!signal) {
    return (
      <article className="card detail-card">
        <span className="empty">No hay señal seleccionada</span>
      </article>
    );
  }

  const latestPrice = signal.currentPrice ?? getLatestPrice(sparklinePrices) ?? signal.entry;
  const ema20Extension = getEma20Extension(signal);

  return (
    <aside className="card detail-card signal-detail-panel">
      <div className="detail-header signal-detail-header">
        <div>
          <span className="eyebrow">{signal.setupType}</span>
          <h2>{signal.symbol}</h2>
        </div>
        <Score value={signal.score} large />
      </div>

      <div className="detail-status-row">
        <Pill value={signal.action} />
        {signal.openingRedReversalDetected ? <span className="signal-flag">ORR</span> : null}
        <span className="detail-chip">{formatConfidenceLabel(signal.confidence)}</span>
        <span className="detail-chip">{signal.timeframe}</span>
      </div>

      <section className="detail-section detail-trend-section">
        <div className="detail-section-heading">
          <span>Tendencia reciente</span>
          <b>{formatMoney(latestPrice)}</b>
        </div>
        <div className="detail-sparkline">
          <Sparkline prices={sparklinePrices} width={320} height={88} />
        </div>
      </section>

      <section className="detail-section">
        <div className="detail-section-heading">
            <span>Métricas clave</span>
        </div>
        <div className="detail-grid compact">
          <Metric label="RS vs SPY" value={formatPercent(signal.relativeStrengthVsSpy)} tone={metricTone(signal.relativeStrengthVsSpy, "rs")} />
          <Metric label="RVOL" value={formatNumber(signal.relativeVolume)} tone={metricTone(signal.relativeVolume, "rvol")} />
          <Metric label="EMA20 EXT" value={formatPercent(ema20Extension)} tone={metricTone(ema20Extension, "ext")} />
          <Metric label="RSI" value={formatNumber(signal.rsi14)} />
        </div>
      </section>

      {hasOpeningRedReversalData(signal) ? (
        <section className="detail-section">
          <div className="detail-section-heading">
            <span>Reversión desde apertura roja</span>
          </div>
          <div className="detail-grid compact">
            <Metric label="Gap apertura" value={formatPercent(signal.openGapPercent)} tone={signal.openGapPercent != null && signal.openGapPercent < 0 ? "metric-value-risk" : undefined} />
            <Metric label="Recup. mínimo" value={formatPercent(signal.openingRedReversalRecoveryFromLowPercent)} tone={signal.openingRedReversalDetected ? "metric-value-good" : undefined} />
            <Metric label="Recup. apertura" value={formatBoolean(signal.reclaimOpen)} tone={signal.reclaimOpen ? "metric-value-good" : "metric-value-muted"} />
            <Metric label="Recup. cierre prev." value={formatBoolean(signal.reclaimPreviousClose)} tone={signal.reclaimPreviousClose ? "metric-value-good" : "metric-value-muted"} />
          </div>
        </section>
      ) : null}

      <section className="detail-section">
        <div className="detail-section-heading">
          <span>Contexto de tendencia</span>
        </div>
        <div className="detail-grid compact">
          <Metric label="EMA9" value={formatMoney(signal.ema9)} />
          <Metric label="EMA20" value={formatMoney(signal.ema20)} />
          <Metric label="EMA50" value={formatMoney(signal.ema50)} />
          <Metric label="ATR14" value={formatNumber(signal.atr14)} />
        </div>
      </section>

      <section className="detail-section">
        <div className="detail-section-heading">
          <span>Plan de riesgo</span>
        </div>
        <div className="detail-grid compact">
          <Metric label="Entrada / último" value={formatMoney(signal.entry ?? latestPrice)} />
          <Metric label="Precio actual" value={formatMoney(latestPrice)} />
          <Metric label="Stop" value={formatMoney(signal.stop)} />
          <Metric label="Objetivo" value={formatMoney(signal.target ?? signal.takeProfit2)} />
          <Metric label="TP1" value={formatMoney(signal.takeProfit1)} />
          <Metric label="TP2" value={formatMoney(signal.takeProfit2)} />
          <Metric label="TP3" value={formatMoney(signal.takeProfit3)} />
          <Metric label="RR1" value={formatNumber(signal.riskReward1)} />
          <Metric label="RR2" value={formatNumber(signal.riskReward2)} />
          <Metric label="RR3" value={formatNumber(signal.riskReward3)} />
          <Metric label="Riesgo / acción" value={formatMoney(signal.riskPerShare)} />
          <Metric label="Tamaño posición" value={formatNumber(signal.suggestedPositionSize)} />
          <Metric label="Extensión" value={signal.extensionRisk ?? "n/a"} />
        </div>
      </section>

      <section className="detail-section">
        <div className="detail-section-heading">
          <span>Explicación</span>
        </div>
        <p className="reason">{signal.reason}</p>
      </section>

      <section className="detail-section breakdown">
        <div className="detail-section-heading">
          <span>Desglose de score</span>
          <b>{signal.scoreBreakdown?.length ?? 0}</b>
        </div>
        {(signal.scoreBreakdown ?? []).length === 0 ? <span className="empty">Sin factores disponibles</span> : null}
        {(signal.scoreBreakdown ?? []).map((factor) => (
          <div className="factor" key={`${factor.label}-${factor.points}`}>
            <span>{factor.label}</span>
            <b className={factor.points >= 0 ? "positive" : "negative"}>{factor.points > 0 ? "+" : ""}{formatNumber(factor.points)}</b>
          </div>
        ))}
      </section>
    </aside>
  );
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <b className={tone}>{value}</b>
    </div>
  );
}

function Score({ value, large = false }: { value: number; large?: boolean }) {
  const tone = value >= 60 ? "score-good" : value < 40 ? "score-risk" : "score-watch";
  return <span className={`score ${tone} ${large ? "large" : ""}`}>{formatNumber(value)}</span>;
}

function Pill({ value }: { value: string }) {
  const tone = value === "Candidate" ? "pill-good" : value === "Avoid / high risk" ? "pill-risk" : "pill-watch";
  return <span className={`pill ${tone}`}>{formatActionLabel(value)}</span>;
}

function getLatestPrice(prices?: number[] | null) {
  const values = prices?.filter(Number.isFinite) ?? [];
  return values.length > 0 ? values[values.length - 1] : null;
}

function getEma20Extension(signal: DashboardSignal) {
  return signal.extensionFromEma20Percent ?? signal.distanceFromEma20Percent;
}

function hasOpeningRedReversalData(signal: DashboardSignal) {
  return Boolean(signal.openingRedReversalDetected);
}

function metricTone(value: number | null | undefined, kind: "rs" | "rvol" | "ext") {
  if (value == null) {
    return "metric-value-neutral";
  }

  if (kind === "rs") {
    if (value > 3) return "metric-value-strong";
    if (value > 1) return "metric-value-good";
    if (value >= 0) return "metric-value-neutral";
    return "metric-value-risk";
  }

  if (kind === "rvol") {
    if (value > 3) return "metric-value-strong";
    if (value > 2) return "metric-value-good";
    if (value > 1) return "metric-value-neutral";
    return "metric-value-muted";
  }

  if (value > 7) return "metric-value-risk";
  if (value >= 3) return "metric-value-watch";
  if (value >= 0) return "metric-value-good";
  return "metric-value-muted";
}

function formatMoney(value?: number | null) {
  return value == null ? "n/a" : value.toFixed(2);
}

function formatNumber(value?: number | null) {
  return value == null ? "n/a" : Number(value).toFixed(2);
}

function formatPercent(value?: number | null) {
  return value == null ? "n/a" : `${formatNumber(value)}%`;
}

function formatBoolean(value?: boolean | null) {
  return value == null ? "n/a" : value ? "Sí" : "No";
}
