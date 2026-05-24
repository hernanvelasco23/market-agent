import { BellRing } from "lucide-react";
import { formatActionLabel, formatSeverityLabel } from "../displayLabels";
import type { DashboardAlert } from "../types";

type AlertCenterProps = {
  alerts: DashboardAlert[];
  onSelectSymbol?: (symbol: string) => void;
};

export function AlertCenter({ alerts, onSelectSymbol }: AlertCenterProps) {
  return (
    <article className="card alert-center">
      <div className="card-title">
        <BellRing size={17} />
        <span>Centro de alertas</span>
        <b>{alerts.length}</b>
      </div>

      {alerts.length === 0 ? (
        <p className="empty alert-empty">No hay alertas activas para el set actual de señales.</p>
      ) : (
        <div className="alert-list">
          {alerts.map((alert) => (
            <button
              key={alert.id}
              className={`alert-item alert-${alert.severity}`}
              type="button"
              onClick={() => onSelectSymbol?.(alert.symbol)}
            >
              <div className="alert-heading">
                <span className="alert-symbol">{alert.symbol}</span>
                <span className={`alert-severity alert-severity-${alert.severity}`}>{formatSeverityLabel(alert.severity)}</span>
              </div>
              <strong>{alert.title}</strong>
              <p>{alert.description}</p>
              <div className="alert-meta">
                {alert.setupType ? <span>{alert.setupType}</span> : null}
                {alert.action ? <span>{formatActionLabel(alert.action)}</span> : null}
              </div>
              <div className="alert-metrics">
                {alert.metrics.map((metric) => (
                  <span key={`${alert.id}:${metric.label}`} className={`alert-metric alert-metric-${metric.tone ?? "neutral"}`}>
                    <small>{metric.label}</small>
                    <b>{metric.value}</b>
                  </span>
                ))}
              </div>
            </button>
          ))}
        </div>
      )}
    </article>
  );
}
