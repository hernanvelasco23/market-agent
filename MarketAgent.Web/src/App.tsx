import { AlertTriangle, BarChart3, Bot, RefreshCw, Search, ShieldAlert, Sparkles, TrendingUp, Zap } from "lucide-react";
import type { ReactNode } from "react";
import { useEffect, useMemo, useState } from "react";
import { buildSparklinePricesBySymbol, loadDashboard, loadHistoricalCandles, runBriefing, runIngestion, runSignals, toDashboardSignal } from "./api";
import { deriveDashboardAlerts } from "./alerts";
import { AlertCenter } from "./components/AlertCenter";
import { SignalFilterBar } from "./components/SignalFilterBar";
import { SignalDetailPanel } from "./components/SignalDetailPanel";
import { Sparkline } from "./components/Sparkline";
import { WatchlistSelector } from "./components/WatchlistSelector";
import { applySignalFilters, defaultSignalFilters, getAvailableSetupTypes, hasActiveSignalFilters } from "./signalFilters";
import type { BriefingResult, DashboardSignal, IngestionResult, SignalFilters, SparklinePricesBySymbol, Watchlist } from "./types";
import { allSignalsWatchlist, applyWatchlistFilter, getAllWatchlists, loadCustomWatchlists, saveCustomWatchlists } from "./watchlists";

type Status = {
  text: string;
  tone: "idle" | "ok" | "warn" | "error";
};

export function App() {
  const [briefing, setBriefing] = useState<BriefingResult | null>(null);
  const [selectedSymbol, setSelectedSymbol] = useState<string | null>(null);
  const [status, setStatus] = useState<Status>({ text: "Ready", tone: "idle" });
  const [loadingAction, setLoadingAction] = useState<string | null>(null);
  const [ingestion, setIngestion] = useState<IngestionResult | null>(null);
  const [usingMock, setUsingMock] = useState(false);
  const [sparklinePrices, setSparklinePrices] = useState<SparklinePricesBySymbol>({});
  const [filters, setFilters] = useState<SignalFilters>(defaultSignalFilters);
  const [customWatchlists, setCustomWatchlists] = useState<Watchlist[]>(() => loadCustomWatchlists());
  const [activeWatchlistId, setActiveWatchlistId] = useState(allSignalsWatchlist.id);

  const allSignals = briefing?.allSignals ?? [];
  const watchlists = useMemo(() => getAllWatchlists(customWatchlists), [customWatchlists]);
  const activeWatchlist = watchlists.find((watchlist) => watchlist.id === activeWatchlistId) ?? allSignalsWatchlist;
  const watchlistSignals = useMemo(() => applyWatchlistFilter(allSignals, activeWatchlist), [allSignals, activeWatchlist]);
  const alerts = useMemo(() => deriveDashboardAlerts(watchlistSignals), [watchlistSignals]);
  const setupTypes = useMemo(() => getAvailableSetupTypes(watchlistSignals), [watchlistSignals]);
  const filteredSignals = useMemo(() => applySignalFilters(watchlistSignals, filters), [watchlistSignals, filters]);
  const hasActiveFilters = hasActiveSignalFilters(filters);
  const topOpportunities = useMemo(
    () => applyWatchlistFilter(briefing?.topOpportunities ?? [], activeWatchlist),
    [briefing?.topOpportunities, activeWatchlist]
  );
  const watchlistPullbacks = useMemo(
    () => applyWatchlistFilter(briefing?.watchlistPullbacks ?? [], activeWatchlist),
    [briefing?.watchlistPullbacks, activeWatchlist]
  );
  const topRisks = useMemo(
    () => applyWatchlistFilter(briefing?.topRisks ?? [], activeWatchlist),
    [briefing?.topRisks, activeWatchlist]
  );
  const selectedSignal = useMemo(
    () => filteredSignals.find((signal) => signal.symbol === selectedSymbol) ?? filteredSignals[0] ?? null,
    [filteredSignals, selectedSymbol]
  );
  const selectedSparklinePrices = selectedSignal
    ? sparklinePrices[selectedSignal.symbol.toUpperCase()]
    : null;

  useEffect(() => {
    refreshDashboard();
  }, []);

  useEffect(() => {
    saveCustomWatchlists(customWatchlists);
  }, [customWatchlists]);

  useEffect(() => {
    if (!watchlists.some((watchlist) => watchlist.id === activeWatchlistId)) {
      setActiveWatchlistId(allSignalsWatchlist.id);
    }
  }, [activeWatchlistId, watchlists]);

  useEffect(() => {
    const fallbackSymbol = filteredSignals[0]?.symbol ?? null;

    if (selectedSymbol == null) {
      if (fallbackSymbol != null) {
        setSelectedSymbol(fallbackSymbol);
      }

      return;
    }

    if (!filteredSignals.some((signal) => signal.symbol === selectedSymbol)) {
      setSelectedSymbol(fallbackSymbol);
    }
  }, [filteredSignals, selectedSymbol]);

  async function withAction(label: string, action: () => Promise<void>) {
    setLoadingAction(label);
    setStatus({ text: `${label}...`, tone: "idle" });

    try {
      await action();
      setStatus({ text: `${label} completed`, tone: "ok" });
    } catch (error) {
      setStatus({
        text: error instanceof Error ? error.message : `${label} failed`,
        tone: "error"
      });
    } finally {
      setLoadingAction(null);
    }
  }

  async function refreshDashboard() {
    await withAction("Refresh dashboard", async () => {
      const state = await loadDashboard();
      setBriefing(state.briefing);
      setUsingMock(state.isMock);
      setSelectedSymbol(state.briefing.allSignals[0]?.symbol ?? null);
      await refreshSparklines();
      if (state.isMock) {
        setStatus({ text: "API unavailable. Mock preview loaded.", tone: "warn" });
      }
    });
  }

  async function handleRunIngestion() {
    await withAction("Run ingestion", async () => {
      const result = await runIngestion();
      setIngestion(result);
    });
  }

  async function handleRunSignals() {
    await withAction("Run signals", async () => {
      const result = await runSignals();
      const all = result.signals.map(toDashboardSignal);
      setUsingMock(false);
      setBriefing((current) => ({
        generatedAtUtc: result.generatedAtUtc,
        marketRegime: current?.marketRegime ?? "Signals only",
        summary: current?.summary ?? "Calculated signals returned from the API.",
        signalSummary: `${all.length} calculated signals returned from the API.`,
        allSignals: all,
        topOpportunities: all.filter((signal) => signal.score >= 60 && signal.action === "Candidate"),
        watchlistPullbacks: all.filter((signal) => signal.setupType === "Pullback" || signal.action.startsWith("Watch")),
        topRisks: all.filter((signal) => signal.score < 40 || signal.action === "Avoid / high risk"),
        highlights: current?.highlights ?? [],
        risks: current?.risks ?? [],
        watchItems: current?.watchItems ?? []
      }));
      setSelectedSymbol(all[0]?.symbol ?? null);
      await refreshSparklines();
    });
  }

  async function handleGenerateBriefing() {
    await withAction("Generate briefing", async () => {
      const result = await runBriefing();
      setUsingMock(false);
      setBriefing(result);
      setSelectedSymbol(result.allSignals[0]?.symbol ?? null);
      await refreshSparklines();
    });
  }

  async function refreshSparklines() {
    try {
      const historical = await loadHistoricalCandles();
      setSparklinePrices(buildSparklinePricesBySymbol(historical.candles));
    } catch {
      setSparklinePrices({});
    }
  }

  function handleSaveCustomWatchlist(watchlist: Watchlist) {
    setCustomWatchlists((current) => {
      const existingIndex = current.findIndex((item) => item.id === watchlist.id);
      if (existingIndex < 0) {
        return [...current, watchlist];
      }

      return current.map((item) => (item.id === watchlist.id ? watchlist : item));
    });
  }

  function handleRemoveCustomWatchlist(id: string) {
    setCustomWatchlists((current) => current.filter((watchlist) => watchlist.id !== id));
    if (activeWatchlistId === id) {
      setActiveWatchlistId(allSignalsWatchlist.id);
    }
  }

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">MarketAgent</p>
          <h1>Signal Dashboard</h1>
        </div>
        <div className="actions">
          <ActionButton icon={<Zap size={16} />} label="Run Ingestion" onClick={handleRunIngestion} loading={loadingAction === "Run ingestion"} />
          <ActionButton icon={<BarChart3 size={16} />} label="Run Signals" onClick={handleRunSignals} loading={loadingAction === "Run signals"} />
          <ActionButton icon={<Bot size={16} />} label="Generate Briefing" onClick={handleGenerateBriefing} loading={loadingAction === "Generate briefing"} />
          <ActionButton icon={<RefreshCw size={16} />} label="Refresh Dashboard" onClick={refreshDashboard} loading={loadingAction === "Refresh dashboard"} />
        </div>
      </header>

      <section className="status-row">
        <span className={`status ${status.tone}`}>{status.text}</span>
        {usingMock ? <span className="status warn">Mock fallback</span> : null}
        {briefing ? <span className="timestamp">Generated {formatDate(briefing.generatedAtUtc)}</span> : null}
        {ingestion ? (
          <span className={ingestion.failed > 0 ? "status warn" : "status ok"}>
            Ingestion {ingestion.succeeded}/{ingestion.totalRequested}
          </span>
        ) : null}
      </section>

      <section className="hero-grid">
        <InfoCard title="Market Regime" value={briefing?.marketRegime ?? "Loading"} icon={<TrendingUp size={18} />} />
        <TextCard title="Summary" text={briefing?.summary ?? "Waiting for dashboard data."} />
        <TextCard title="Signal Summary" text={briefing?.signalSummary ?? "Signals will appear after the API responds."} />
      </section>

      <section className="signal-groups">
        <SignalGroup title="Top Opportunities" tone="opportunity" icon={<Sparkles size={17} />} signals={topOpportunities} onSelect={setSelectedSymbol} />
        <SignalGroup title="Watchlist Pullbacks" tone="watch" icon={<Search size={17} />} signals={watchlistPullbacks} onSelect={setSelectedSymbol} />
        <SignalGroup title="Top Risks" tone="risk" icon={<ShieldAlert size={17} />} signals={topRisks} onSelect={setSelectedSymbol} />
      </section>

      <AlertCenter alerts={alerts} onSelectSymbol={setSelectedSymbol} />

      <WatchlistSelector
        watchlists={watchlists}
        activeWatchlistId={activeWatchlist.id}
        visibleCount={watchlistSignals.length}
        totalCount={allSignals.length}
        onSelect={setActiveWatchlistId}
        onSaveCustom={handleSaveCustomWatchlist}
        onRemoveCustom={handleRemoveCustomWatchlist}
      />

      <SignalFilterBar
        filters={filters}
        setupTypes={setupTypes}
        visibleCount={filteredSignals.length}
        totalCount={watchlistSignals.length}
        onChange={setFilters}
        onReset={() => setFilters(defaultSignalFilters)}
      />

      <section className="workspace">
        <SignalsTable
          signals={filteredSignals}
          totalSignals={watchlistSignals.length}
          hasLoadedSignals={allSignals.length > 0}
          hasActiveFilters={hasActiveFilters}
          selectedSymbol={selectedSignal?.symbol ?? null}
          sparklinePrices={sparklinePrices}
          onSelect={setSelectedSymbol}
          onResetFilters={() => setFilters(defaultSignalFilters)}
        />
        <SignalDetailPanel signal={selectedSignal} sparklinePrices={selectedSparklinePrices} />
      </section>

      <section className="briefing-lists">
        <ListCard title="Highlights" items={briefing?.highlights ?? []} />
        <ListCard title="Risks" items={briefing?.risks ?? []} />
        <ListCard title="Watch Items" items={briefing?.watchItems ?? []} />
      </section>
    </main>
  );
}

function ActionButton({
  icon,
  label,
  loading,
  onClick
}: {
  icon: ReactNode;
  label: string;
  loading: boolean;
  onClick: () => void;
}) {
  return (
    <button className="action-button" type="button" onClick={onClick} disabled={loading}>
      {loading ? <RefreshCw className="spin" size={16} /> : icon}
      <span>{label}</span>
    </button>
  );
}

function InfoCard({ title, value, icon }: { title: string; value: string; icon: ReactNode }) {
  return (
    <article className="card metric-card">
      <div className="card-title">
        {icon}
        <span>{title}</span>
      </div>
      <strong>{value}</strong>
    </article>
  );
}

function TextCard({ title, text }: { title: string; text: string }) {
  return (
    <article className="card text-card">
      <span className="card-heading">{title}</span>
      <p>{text}</p>
    </article>
  );
}

function SignalGroup({
  title,
  tone,
  icon,
  signals,
  onSelect
}: {
  title: string;
  tone: "opportunity" | "watch" | "risk";
  icon: ReactNode;
  signals: DashboardSignal[];
  onSelect: (symbol: string) => void;
}) {
  return (
    <article className={`card group-card ${tone}`}>
      <div className="card-title">
        {icon}
        <span>{title}</span>
        <b>{signals.length}</b>
      </div>
      <div className="group-list">
        {signals.length === 0 ? <span className="empty">None</span> : null}
        {signals.slice(0, 6).map((signal) => (
          <button key={signal.symbol} className="group-item" type="button" onClick={() => onSelect(signal.symbol)}>
            <span>{signal.symbol}</span>
            <Score value={signal.score} />
            <small>{signal.action}</small>
          </button>
        ))}
      </div>
    </article>
  );
}

function SignalsTable({
  signals,
  totalSignals,
  hasLoadedSignals,
  hasActiveFilters,
  selectedSymbol,
  sparklinePrices,
  onSelect,
  onResetFilters
}: {
  signals: DashboardSignal[];
  totalSignals: number;
  hasLoadedSignals: boolean;
  hasActiveFilters: boolean;
  selectedSymbol: string | null;
  sparklinePrices: SparklinePricesBySymbol;
  onSelect: (symbol: string) => void;
  onResetFilters: () => void;
}) {
  return (
    <article className="card table-card">
      <div className="card-title">
        <BarChart3 size={17} />
        <span>All Signals</span>
        <b>{signals.length}</b>
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Symbol</th>
              <th>Trend</th>
              <th>Score</th>
              <th>Setup</th>
              <th>Flag</th>
              <th>Action</th>
              <th>Confidence</th>
              <th>Timeframe</th>
              <th>RS</th>
              <th>RVOL</th>
              <th>EXT</th>
              <th>RSI14</th>
              <th>EMA9</th>
              <th>EMA20</th>
              <th>EMA50</th>
              <th>ATR14</th>
            </tr>
          </thead>
          <tbody>
            {signals.length === 0 ? (
              <tr>
                <td className="table-empty" colSpan={16}>
                  {!hasLoadedSignals ? (
                    <div className="table-empty-state">
                      <strong>No signals loaded yet.</strong>
                      <span>Run signals or generate a briefing to populate the dashboard.</span>
                    </div>
                  ) : (
                    <div className="table-empty-state">
                      <strong>No signals match the current watchlist or filters.</strong>
                      <span>Try another watchlist, clearing filters, or lowering thresholds.</span>
                      {hasActiveFilters ? (
                        <button className="filter-reset inline" type="button" onClick={onResetFilters}>
                          Clear filters
                        </button>
                      ) : null}
                    </div>
                  )}
                </td>
              </tr>
            ) : null}
            {signals.map((signal) => {
              const prices = sparklinePrices[signal.symbol.toUpperCase()];

              return (
                <tr
                  key={signal.symbol}
                  className={signal.symbol === selectedSymbol ? "selected" : ""}
                  onClick={() => onSelect(signal.symbol)}
                >
                  <td className="symbol-cell">{signal.symbol}</td>
                  <td className="sparkline-cell"><Sparkline prices={prices} width={96} height={28} /></td>
                  <td><Score value={signal.score} /></td>
                  <td>{signal.setupType}</td>
                  <td>{signal.openingRedReversalDetected ? <span className="signal-flag">ORR</span> : null}</td>
                  <td><Pill value={signal.action} /></td>
                  <td>{signal.confidence}</td>
                  <td>{signal.timeframe}</td>
                  <td><SignalMetric value={signal.relativeStrengthVsSpy} kind="rs" suffix="%" /></td>
                  <td><SignalMetric value={signal.relativeVolume} kind="rvol" /></td>
                  <td><SignalMetric value={getEma20Extension(signal)} kind="ext" suffix="%" /></td>
                  <td>{formatNumber(signal.rsi14)}</td>
                  <td>{formatMoney(signal.ema9)}</td>
                  <td>{formatMoney(signal.ema20)}</td>
                  <td>{formatMoney(signal.ema50)}</td>
                  <td>{formatNumber(signal.atr14)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </article>
  );
}

function ListCard({ title, items }: { title: string; items: string[] }) {
  return (
    <article className="card list-card">
      <div className="card-title">
        <AlertTriangle size={16} />
        <span>{title}</span>
        <b>{items.length}</b>
      </div>
      {items.length === 0 ? <span className="empty">None</span> : null}
      {items.map((item) => (
        <p key={item}>{item}</p>
      ))}
    </article>
  );
}

function Score({ value, large = false }: { value: number; large?: boolean }) {
  const tone = value >= 60 ? "score-good" : value < 40 ? "score-risk" : "score-watch";
  return <span className={`score ${tone} ${large ? "large" : ""}`}>{formatNumber(value)}</span>;
}

function Pill({ value }: { value: string }) {
  const tone = value === "Candidate" ? "pill-good" : value === "Avoid / high risk" ? "pill-risk" : "pill-watch";
  return <span className={`pill ${tone}`}>{value}</span>;
}

function SignalMetric({
  value,
  kind,
  suffix
}: {
  value?: number | null;
  kind: "rs" | "rvol" | "ext";
  suffix?: string;
}) {
  return <span className={`metric-chip ${metricTone(value, kind)}`}>{formatMetric(value, suffix)}</span>;
}

function metricTone(value: number | null | undefined, kind: "rs" | "rvol" | "ext") {
  if (value == null) {
    return "metric-neutral";
  }

  if (kind === "rs") {
    if (value > 3) return "metric-strong";
    if (value > 1) return "metric-good";
    if (value >= 0) return "metric-neutral";
    return "metric-risk";
  }

  if (kind === "rvol") {
    if (value > 3) return "metric-strong";
    if (value > 2) return "metric-good";
    if (value > 1) return "metric-neutral";
    return "metric-muted";
  }

  if (value > 7) return "metric-risk";
  if (value >= 3) return "metric-watch";
  if (value >= 0) return "metric-good";
  return "metric-muted";
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function formatMoney(value?: number | null) {
  return value == null ? "n/a" : value.toFixed(2);
}

function formatNumber(value?: number | null) {
  return value == null ? "n/a" : Number(value).toFixed(2);
}

function formatMetric(value?: number | null, suffix = "") {
  return value == null ? "n/a" : `${formatNumber(value)}${suffix}`;
}

function getEma20Extension(signal: DashboardSignal) {
  return signal.extensionFromEma20Percent ?? signal.distanceFromEma20Percent;
}
