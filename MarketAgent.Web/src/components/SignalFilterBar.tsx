import { RotateCcw, SlidersHorizontal } from "lucide-react";
import type { SignalFilters, SignalSortKey } from "../types";
import { hasActiveSignalFilters, rvolThresholds, rsThresholds, scoreThresholds, sortOptions } from "../signalFilters";

type SignalFilterBarProps = {
  filters: SignalFilters;
  setupTypes: string[];
  visibleCount: number;
  totalCount: number;
  onChange: (filters: SignalFilters) => void;
  onReset: () => void;
};

export function SignalFilterBar({
  filters,
  setupTypes,
  visibleCount,
  totalCount,
  onChange,
  onReset
}: SignalFilterBarProps) {
  const active = hasActiveSignalFilters(filters);
  const sortLabel = sortOptions.find((option) => option.value === filters.sortBy)?.label ?? "Score";
  const summaryItems = getFilterSummaryItems(filters);

  return (
    <section className="card filter-bar">
      <div className="filter-title">
        <div className="card-title">
          <SlidersHorizontal size={17} />
          <span>Signal Filters</span>
          <b>{visibleCount}/{totalCount}</b>
        </div>
        {active ? (
          <button className="filter-reset" type="button" onClick={onReset}>
            <RotateCcw size={14} />
            <span>Clear filters</span>
          </button>
        ) : null}
      </div>

      <div className="filter-summary">
        <span>{totalCount === 0 ? "No signals loaded" : `Showing ${visibleCount} of ${totalCount} signals`}</span>
        {summaryItems.map((item) => (
          <span key={item}>{item}</span>
        ))}
        <span>Sort: {sortLabel}</span>
      </div>

      <div className="filter-controls">
        <label className="filter-select">
          <span>Setup</span>
          <select
            value={filters.setupType}
            onChange={(event) => onChange({ ...filters, setupType: event.target.value })}
          >
            <option value="all">All setups</option>
            {setupTypes.map((setupType) => (
              <option key={setupType} value={setupType}>{setupType}</option>
            ))}
          </select>
        </label>

        <ThresholdGroup
          label="Score"
          value={filters.minScore}
          values={scoreThresholds}
          formatter={(value) => `${value}+`}
          onChange={(minScore) => onChange({ ...filters, minScore })}
        />

        <ThresholdGroup
          label="RS"
          value={filters.minRs}
          values={rsThresholds}
          formatter={(value) => `${value}+`}
          onChange={(minRs) => onChange({ ...filters, minRs })}
        />

        <ThresholdGroup
          label="RVOL"
          value={filters.minRvol}
          values={rvolThresholds}
          formatter={(value) => `${value}x+`}
          onChange={(minRvol) => onChange({ ...filters, minRvol })}
        />

        <div className="filter-chip-group">
          <span>Focus</span>
          <ToggleChip label="Risk" active={filters.riskOnly} onClick={() => onChange({ ...filters, riskOnly: !filters.riskOnly })} />
          <ToggleChip label="Opportunity" active={filters.opportunityOnly} onClick={() => onChange({ ...filters, opportunityOnly: !filters.opportunityOnly })} />
          <ToggleChip label="Extended" active={filters.overextendedOnly} onClick={() => onChange({ ...filters, overextendedOnly: !filters.overextendedOnly })} />
          <ToggleChip label="ORR" active={filters.openingRedReversalOnly} onClick={() => onChange({ ...filters, openingRedReversalOnly: !filters.openingRedReversalOnly })} />
        </div>

        <label className="filter-select">
          <span>Sort</span>
          <select
            value={filters.sortBy}
            onChange={(event) => onChange({ ...filters, sortBy: event.target.value as SignalSortKey })}
          >
            {sortOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
      </div>
    </section>
  );
}

function ThresholdGroup({
  label,
  value,
  values,
  formatter,
  onChange
}: {
  label: string;
  value: number | null;
  values: readonly number[];
  formatter: (value: number) => string;
  onChange: (value: number | null) => void;
}) {
  return (
    <div className="filter-chip-group">
      <span>{label}</span>
      <ToggleChip label="Any" active={value == null} onClick={() => onChange(null)} />
      {values.map((threshold) => (
        <ToggleChip
          key={threshold}
          label={formatter(threshold)}
          active={value === threshold}
          onClick={() => onChange(value === threshold ? null : threshold)}
        />
      ))}
    </div>
  );
}

function ToggleChip({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button className={`filter-chip ${active ? "active" : ""}`} type="button" onClick={onClick}>
      {label}
    </button>
  );
}

function getFilterSummaryItems(filters: SignalFilters) {
  const items: string[] = [];

  if (filters.setupType !== "all") {
    items.push(`Filter: ${filters.setupType}`);
  }

  if (filters.minScore != null) {
    items.push(`Score ${filters.minScore}+`);
  }

  if (filters.minRs != null) {
    items.push(`RS ${filters.minRs}+`);
  }

  if (filters.minRvol != null) {
    items.push(`RVOL ${filters.minRvol}x+`);
  }

  if (filters.riskOnly) {
    items.push("Risk only");
  }

  if (filters.opportunityOnly) {
    items.push("Opportunity only");
  }

  if (filters.overextendedOnly) {
    items.push("Overextended only");
  }

  if (filters.openingRedReversalOnly) {
    items.push("ORR only");
  }

  return items;
}
