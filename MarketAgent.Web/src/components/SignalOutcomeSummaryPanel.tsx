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
        <span>Signal Outcomes</span>
        {summary ? <b>{summary.pendingCount} pending</b> : null}
      </div>

      <p className="performance-note">
        Persisted signal outcomes from emitted signals. Partial returns update before 1D outcomes close.
      </p>

      {loading ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>Loading outcome summary...</span>
        </div>
      ) : null}

      {unavailable ? (
        <div className="performance-empty">
          <AlertTriangle size={16} />
          <span>Outcome summary unavailable. The dashboard can still be used normally.</span>
        </div>
      ) : null}

      {!loading && !unavailable && summary?.totalCount === 0 ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>No persisted outcomes yet. Run signals, ingestion, and outcome evaluation to populate this section.</span>
        </div>
      ) : null}

      {summary && summary.totalCount > 0 ? (
        <div className="performance-grid outcome-summary-grid">
          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Coverage</strong>
              <span>{formatCount(summary.totalCount)} total</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Pending" value={summary.pendingCount} />
              <OutcomeMetric label="15m Count" value={summary.countWith15m} />
              <OutcomeMetric label="1h Count" value={summary.countWith1h} />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Average Return</strong>
              <span>partial</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Avg 15m" value={summary.averageReturn15m} suffix="%" tone />
              <OutcomeMetric label="Avg 1h" value={summary.averageReturn1h} suffix="%" tone />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Best 15m</strong>
              <span>{summary.best15mSymbol ?? "n/a"}</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Return" value={summary.best15mReturnPercent} suffix="%" tone />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Worst 15m</strong>
              <span>{summary.worst15mSymbol ?? "n/a"}</span>
            </div>
            <div className="performance-values">
              <OutcomeMetric label="Return" value={summary.worst15mReturnPercent} suffix="%" tone />
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
