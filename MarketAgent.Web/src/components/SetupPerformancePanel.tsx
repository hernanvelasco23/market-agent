import { Activity, AlertTriangle, BarChart3 } from "lucide-react";
import type { SignalOutcomeSetupSummary } from "../types";

export function SetupPerformancePanel({
  loading,
  summary,
  unavailable
}: {
  loading: boolean;
  summary: SignalOutcomeSetupSummary | null;
  unavailable: boolean;
}) {
  const topItems = summary?.items.slice(0, 5) ?? [];

  return (
    <section className="card performance-preview setup-performance">
      <div className="card-title">
        <BarChart3 size={17} />
        <span>Setup Performance</span>
        {summary ? <b>{summary.totalSetupCount} setups</b> : null}
      </div>

      <p className="performance-note">Partial intraday returns grouped by emitted setup type.</p>

      {loading ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>Loading setup performance...</span>
        </div>
      ) : null}

      {unavailable ? (
        <div className="performance-empty">
          <AlertTriangle size={16} />
          <span>Setup performance unavailable. The scanner can still be used normally.</span>
        </div>
      ) : null}

      {!loading && !unavailable && summary?.totalSetupCount === 0 ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>No setup outcome samples yet. Evaluate outcomes after persisted signals have partial checkpoints.</span>
        </div>
      ) : null}

      {summary && summary.totalSetupCount > 0 ? (
        <div className="performance-grid">
          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Best Setup</strong>
              <span title={summary.bestSetup ?? undefined}>{summary.bestSetup ?? "n/a"}</span>
            </div>
            <div className="performance-values">
              <SetupMetric label="Avg 15m" value={summary.bestSetupAverageReturn15m} suffix="%" tone />
            </div>
          </article>

          <article className="performance-item">
            <div className="performance-item-header">
              <strong>Worst Setup</strong>
              <span title={summary.worstSetup ?? undefined}>{summary.worstSetup ?? "n/a"}</span>
            </div>
            <div className="performance-values">
              <SetupMetric label="Avg 15m" value={summary.worstSetupAverageReturn15m} suffix="%" tone />
            </div>
          </article>

          {topItems.map((item) => (
            <article className="performance-item" key={item.setup}>
              <div className="performance-item-header">
                <strong title={item.setup}>{item.setup}</strong>
                <span>{formatCount(item.count)} samples</span>
              </div>
              <div className="performance-values">
                <SetupMetric label="Avg 15m" value={item.averageReturn15m} suffix="%" tone />
                <SetupMetric label="Avg 1h" value={item.averageReturn1h} suffix="%" tone />
                <SetupMetric label="1h Count" value={item.countWith1h} />
              </div>
            </article>
          ))}
        </div>
      ) : null}
    </section>
  );
}

function SetupMetric({
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
