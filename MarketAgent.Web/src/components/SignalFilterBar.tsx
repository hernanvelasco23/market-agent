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
          <span>Filtros de señales</span>
          <b>{visibleCount}/{totalCount}</b>
        </div>
        {active ? (
          <button className="filter-reset" type="button" onClick={onReset}>
            <RotateCcw size={14} />
            <span>Limpiar filtros</span>
          </button>
        ) : null}
      </div>

      <div className="filter-summary">
        <span>{totalCount === 0 ? "Sin señales cargadas" : `Mostrando ${visibleCount} de ${totalCount} señales`}</span>
        {summaryItems.map((item) => (
          <span key={item}>{item}</span>
        ))}
        <span>Orden: {sortLabel}</span>
      </div>

      <div className="filter-controls">
        <label className="filter-select">
          <span>Setup</span>
          <select
            value={filters.setupType}
            onChange={(event) => onChange({ ...filters, setupType: event.target.value })}
          >
            <option value="all">Todos los setups</option>
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
          <span>Foco</span>
          <ToggleChip label="Riesgo" active={filters.riskOnly} onClick={() => onChange({ ...filters, riskOnly: !filters.riskOnly })} />
          <ToggleChip label="Oportunidad" active={filters.opportunityOnly} onClick={() => onChange({ ...filters, opportunityOnly: !filters.opportunityOnly })} />
          <ToggleChip label="Extendida" active={filters.overextendedOnly} onClick={() => onChange({ ...filters, overextendedOnly: !filters.overextendedOnly })} />
          <ToggleChip label="ORR" active={filters.openingRedReversalOnly} onClick={() => onChange({ ...filters, openingRedReversalOnly: !filters.openingRedReversalOnly })} />
        </div>

        <label className="filter-select">
          <span>Orden</span>
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
      <ToggleChip label="Cualquiera" active={value == null} onClick={() => onChange(null)} />
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
    items.push(`Filtro: ${filters.setupType}`);
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
    items.push("Solo riesgo");
  }

  if (filters.opportunityOnly) {
    items.push("Solo oportunidad");
  }

  if (filters.overextendedOnly) {
    items.push("Solo extendidas");
  }

  if (filters.openingRedReversalOnly) {
    items.push("Solo ORR");
  }

  return items;
}
