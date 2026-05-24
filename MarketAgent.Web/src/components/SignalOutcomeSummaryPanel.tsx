import { Activity, AlertTriangle } from "lucide-react";
import type { SignalOutcomeSummary } from "../types";

export function SignalOutcomeSummaryPanel({
  loading,
  summary,
  unavailable
}: {
  loading: boolean;
  summary: SignalOutcomeSummary | null;
  unavailable: boolean;
}) {
  return (
    <section className="card performance-preview outcome-summary">
      <div className="card-title">
        <Activity size={17} />
        <span>Resultados de señales</span>
        {summary ? <b>{summary.pendingCount} pendientes</b> : null}
      </div>

      <p className="performance-note">
        Resultados persistidos de señales emitidas. Los retornos parciales se actualizan antes del cierre a 1D.
      </p>

      {loading ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>Cargando resumen de resultados...</span>
        </div>
      ) : null}

      {unavailable ? (
        <div className="performance-empty">
          <AlertTriangle size={16} />
          <span>Resumen de resultados no disponible. El panel puede usarse normalmente.</span>
        </div>
      ) : null}

      {!loading && !unavailable && summary?.totalCount === 0 ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>Todavía no hay resultados persistidos. Generá señales, ingesta datos y evaluá outcomes para poblar esta sección.</span>
        </div>
      ) : null}

      {summary && summary.totalCount > 0 ? (
        <div className="performance-grid outcome-summary-grid">
          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Cobertura</strong>
              <span>{formatCount(summary.totalCount)} total</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Pendientes" value={summary.pendingCount} />
              <OutcomeMetric label="Cant. 15m" value={summary.countWith15m} />
              <OutcomeMetric label="Cant. 1h" value={summary.countWith1h} />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Retorno promedio</strong>
              <span>parcial</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Prom. 15m" value={summary.averageReturn15m} suffix="%" tone />
              <OutcomeMetric label="Prom. 1h" value={summary.averageReturn1h} suffix="%" tone />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Mejor 15m</strong>
              <span>{summary.best15mSymbol ?? "n/a"}</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Retorno" value={summary.best15mReturnPercent} suffix="%" tone />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Peor 15m</strong>
              <span>{summary.worst15mSymbol ?? "n/a"}</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Retorno" value={summary.worst15mReturnPercent} suffix="%" tone />
            </div>
          </article>
        </div>
      ) : null}
    </section>
  );
}

function OutcomeMetric({
  label,
  value,
  suffix,
  tone = false
}: {
  label: string;
  value?: number | null;
  suffix?: string;
  tone?: boolean;
}) {
  return (
    <span className="performance-metric">
      <small>{label}</small>
      <b className={tone ? metricTone(value) : "neutral"}>{formatValue(value, suffix)}</b>
    </span>
  );
}

function metricTone(value?: number | null) {
  if (value == null || value === 0) {
    return "neutral";
  }

  return value > 0 ? "positive" : "negative";
}

function formatValue(value?: number | null, suffix = "") {
  return value == null ? "n/a" : `${value.toFixed(2)}${suffix}`;
}

function formatCount(value: number) {
  return new Intl.NumberFormat().format(value);
}
