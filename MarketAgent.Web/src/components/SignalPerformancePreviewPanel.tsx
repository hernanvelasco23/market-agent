import { Activity, AlertTriangle } from "lucide-react";
import type { SignalPerformancePreviewResult } from "../types";

export function SignalPerformancePreviewPanel({
  preview,
  unavailable
}: {
  preview: SignalPerformancePreviewResult | null;
  unavailable: boolean;
}) {
  return (
    <section className="card performance-preview">
      <div className="card-title">
        <Activity size={17} />
        <span>Signal Performance Preview</span>
        {preview ? <b>{preview.requestedDays}d</b> : null}
      </div>

      <p className="performance-note">
        Reconstructed historical samples from OHLCV candles. Diagnostic only, not a prediction or trading advice.
      </p>

      {unavailable ? (
        <div className="performance-empty">
          <AlertTriangle size={16} />
          <span>Performance preview unavailable. The dashboard can still be used normally.</span>
        </div>
      ) : null}

      {preview ? (
        <>
          <div className="performance-grid">
            {preview.items.map((item) => (
              <article key={item.signalType} className="performance-item">
                <div className="performance-item-header">
                  <strong>{formatSignalType(item.signalType)}</strong>
                  <span>{item.sampleCount} samples</span>
                </div>
                <div className="performance-values">
                  <PerformanceMetric label="Avg 1D" value={item.averageForwardReturn1Day} suffix="%" />
                  <PerformanceMetric label="Avg 3D" value={item.averageForwardReturn3Day} suffix="%" />
                  <PerformanceMetric label="Avg 5D" value={item.averageForwardReturn5Day} suffix="%" />
                  <PerformanceMetric label="Win 1D" value={item.winRate1Day} suffix="%" />
                  <PerformanceMetric label="Win 3D" value={item.winRate3Day} suffix="%" />
                  <PerformanceMetric label="Win 5D" value={item.winRate5Day} suffix="%" />
                </div>
                {item.isInsufficientData ? <span className="performance-warning">Insufficient data</span> : null}
                {!item.isInsufficientData && item.hasLowSampleWarning ? (
                  <span className="performance-warning subtle">Low sample size</span>
                ) : null}
              </article>
            ))}
          </div>
          <div className="performance-cautions">
            {(preview.warnings ?? []).slice(0, 4).map((warning) => (
              <span key={warning}>{warning}</span>
            ))}
          </div>
        </>
      ) : null}
    </section>
  );
}

function PerformanceMetric({
  label,
  value,
  suffix
}: {
  label: string;
  value?: number | null;
  suffix?: string;
}) {
  return (
    <span className="performance-metric">
      <small>{label}</small>
      <b className={metricTone(value)}>{formatValue(value, suffix)}</b>
    </span>
  );
}

function metricTone(value?: number | null) {
  if (value == null) {
    return "neutral";
  }

  if (value > 0) {
    return "positive";
  }

  if (value < 0) {
    return "negative";
  }

  return "neutral";
}

function formatValue(value?: number | null, suffix = "") {
  return value == null ? "n/a" : `${value.toFixed(2)}${suffix}`;
}

function formatSignalType(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}
