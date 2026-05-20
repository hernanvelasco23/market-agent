import { Activity, AlertTriangle, Gauge } from "lucide-react";
import type { SignalOutcomeScoreBucketSummary } from "../types";

export function ScoreConfidencePerformancePanel({
  loading,
  summary,
  unavailable
}: {
  loading: boolean;
  summary: SignalOutcomeScoreBucketSummary | null;
  unavailable: boolean;
}) {
  const confidenceItems = summary?.confidenceItems.filter((item) => item.count > 0) ?? [];
  const scoreBucketItems = summary?.scoreBucketItems.filter((item) => item.count > 0) ?? [];
  const hasItems = confidenceItems.length > 0 || scoreBucketItems.length > 0;

  return (
    <section className="card performance-preview score-confidence-performance">
      <div className="card-title">
        <Gauge size={17} />
        <span>Score & Confidence Performance</span>
        {summary ? <b>{confidenceItems.length + scoreBucketItems.length} groups</b> : null}
      </div>

      <p className="performance-note">Partial intraday returns grouped by confidence label and scanner score bucket.</p>

      {loading ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>Loading score and confidence performance...</span>
        </div>
      ) : null}

      {unavailable ? (
        <div className="performance-empty">
          <AlertTriangle size={16} />
          <span>Score and confidence performance unavailable. The scanner can still be used normally.</span>
        </div>
      ) : null}

      {!loading && !unavailable && summary && !hasItems ? (
        <div className="performance-empty neutral">
          <Activity size={16} />
          <span>No score or confidence outcome samples yet. Evaluate outcomes after partial checkpoints exist.</span>
        </div>
      ) : null}

      {summary && hasItems ? (
        <div className="performance-grid">
          {confidenceItems.map((item) => (
            <PerformanceCard
              key={`confidence-${item.confidence}`}
              label={item.confidence}
              eyebrow="confidence"
              count={item.count}
              countWith15m={item.countWith15m}
              countWith1h={item.countWith1h}
              averageReturn15m={item.averageReturn15m}
              averageReturn1h={item.averageReturn1h}
              bestSymbol15m={item.bestSymbol15m}
              worstSymbol15m={item.worstSymbol15m}
            />
          ))}

          {scoreBucketItems.map((item) => (
            <PerformanceCard
              key={`score-${item.bucket}`}
              label={item.bucket}
              eyebrow="score bucket"
              count={item.count}
              countWith15m={item.countWith15m}
              countWith1h={item.countWith1h}
              averageReturn15m={item.averageReturn15m}
              averageReturn1h={item.averageReturn1h}
              bestSymbol15m={item.bestSymbol15m}
              worstSymbol15m={item.worstSymbol15m}
            />
          ))}
        </div>
      ) : null}
    </section>
  );
}

function PerformanceCard({
  label,
  eyebrow,
  count,
  countWith15m,
  countWith1h,
  averageReturn15m,
  averageReturn1h,
  bestSymbol15m,
  worstSymbol15m
}: {
  label: string;
  eyebrow: string;
  count: number;
  countWith15m: number;
  countWith1h: number;
  averageReturn15m?: number | null;
  averageReturn1h?: number | null;
  bestSymbol15m?: string | null;
  worstSymbol15m?: string | null;
}) {
  return (
    <article className="performance-item">
      <div className="performance-item-header">
        <strong title={label}>{label}</strong>
        <span>{eyebrow}</span>
      </div>
      <div className="performance-values">
        <ScoreMetric label="Samples" value={count} />
        <ScoreMetric label="Avg 15m" value={averageReturn15m} suffix="%" tone />
        <ScoreMetric label="Avg 1h" value={averageReturn1h} suffix="%" tone />
        <ScoreMetric label="15m Count" value={countWith15m} />
        <ScoreMetric label="1h Count" value={countWith1h} />
        <ScoreMetric label="Best 15m" text={bestSymbol15m} />
        <ScoreMetric label="Worst 15m" text={worstSymbol15m} />
      </div>
    </article>
  );
}

function ScoreMetric({
  label,
  value,
  text,
  suffix,
  tone = false
}: {
  label: string;
  value?: number | null;
  text?: string | null;
  suffix?: string;
  tone?: boolean;
}) {
  return (
    <span className="performance-metric">
      <small>{label}</small>
      <b className={tone ? metricTone(value) : "neutral"} title={text ?? undefined}>
        {text ?? formatValue(value, suffix)}
      </b>
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
