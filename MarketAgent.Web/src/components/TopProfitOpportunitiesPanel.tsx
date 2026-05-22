import { Target } from "lucide-react";
import { getTopProfitOpportunities } from "../candidateProfitRanking";
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
        <span>Top Profit Opportunities</span>
        <b>{opportunities.length}</b>
      </div>

      {opportunities.length === 0 ? (
        <div className="performance-empty neutral">
          <Target size={16} />
          <span>No candidate signals with valid entry, target, and risk/reward are available.</span>
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
              <span className="profit-confidence">{opportunity.signal.confidence}</span>
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
              <span className="profit-action">{opportunity.signal.action}</span>
            </button>
          ))}
        </div>
      )}
    </section>
  );
}

function getUpsideBadge(value: number) {
  if (value >= 20) {
    return "High Upside";
  }

  if (value >= 10) {
    return "Good Upside";
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
