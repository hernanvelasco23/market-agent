import { Target } from "lucide-react";
import { getTopProfitOpportunities } from "../candidateProfitRanking";
import { formatActionLabel, formatConfidenceLabel } from "../displayLabels";
import type { DashboardSignal } from "../types";

export function TopProfitOpportunitiesPanel({
  signals,
  onSelectSymbol
}: {
  signals: DashboardSignal[];
  onSelectSymbol: (symbol: string) => void;
}) {
  const opportunities = getTopProfitOpportunities(signals);

  return (
    <section className="card profit-ranking">
      <div className="card-title">
        <Target size={17} />
        <span>Mejores oportunidades por upside</span>
        <b>{opportunities.length}</b>
      </div>

      {opportunities.length === 0 ? (
        <div className="performance-empty neutral">
          <Target size={16} />
          <span>No hay candidatos con entrada y take profit calculados.</span>
        </div>
      ) : (
        <div className="profit-ranking-list">
          {opportunities.map((opportunity) => (
            <button
              key={opportunity.signal.symbol}
              className="profit-ranking-row"
              type="button"
              onClick={() => onSelectSymbol(opportunity.signal.symbol)}
            >
              <span className="profit-symbol">{opportunity.signal.symbol}</span>
              <span className="profit-setup" title={opportunity.signal.setupType}>
                {opportunity.signal.setupType}
              </span>
              <span className="profit-score">{formatNumber(opportunity.signal.score)}</span>
              <span className="profit-confidence">{formatConfidenceLabel(opportunity.signal.confidence)}</span>
              <span>{formatMoney(opportunity.entryPoint)}</span>
              <span>{formatMoney(opportunity.takeProfit)}</span>
              <span>{formatMoney(opportunity.stopLoss)}</span>
              <span className="profit-upside">
                {formatPercent(opportunity.potentialProfitPct)}
                {getUpsideBadge(opportunity.potentialProfitPct) ? (
                  <small>{getUpsideBadge(opportunity.potentialProfitPct)}</small>
                ) : null}
              </span>
              <span>{formatNumber(opportunity.riskReward)}</span>
              <span className="profit-action">{formatActionLabel(opportunity.signal.action)}</span>
            </button>
          ))}
        </div>
      )}
    </section>
  );
}

function getUpsideBadge(value: number) {
  if (value >= 20) {
    return "Upside alto";
  }

  if (value >= 10) {
    return "Buen upside";
  }

  return null;
}

function formatMoney(value?: number | null) {
  return value == null ? "n/a" : value.toFixed(2);
}

function formatNumber(value?: number | null) {
  return value == null ? "n/a" : value.toFixed(2);
}

function formatPercent(value: number) {
  return `${value.toFixed(2)}%`;
}
