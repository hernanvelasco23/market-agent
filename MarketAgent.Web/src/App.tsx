import { AlertTriangle, BarChart3, Bot, RefreshCw, Search, ShieldAlert, Sparkles, TrendingUp, Zap } from "lucide-react";
import type { ReactNode } from "react";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  buildSparklinePricesBySymbol,
  loadDashboard,
  loadHistoricalCandles,
  loadSignalOutcomeScoreBuckets,
  loadSignalOutcomeSetupSummary,
  loadSignalOutcomeSummary,
  loadSignalPerformancePreview,
  runBriefing,
  runIngestion,
  runSignals,
  toDashboardSignal
} from "./api";
import { deriveDashboardAlerts } from "./alerts";
import { AlertCenter } from "./components/AlertCenter";
import { ScoreConfidencePerformancePanel } from "./components/ScoreConfidencePerformancePanel";
import { SignalFilterBar } from "./components/SignalFilterBar";
import { SignalDetailPanel } from "./components/SignalDetailPanel";
import { SignalOutcomeSummaryPanel } from "./components/SignalOutcomeSummaryPanel";
import { SignalPerformancePreviewPanel } from "./components/SignalPerformancePreviewPanel";
import { SetupPerformancePanel } from "./components/SetupPerformancePanel";
import { Sparkline } from "./components/Sparkline";
import { TopProfitOpportunitiesPanel } from "./components/TopProfitOpportunitiesPanel";
import { WatchlistSelector } from "./components/WatchlistSelector";
import { formatActionLabel, formatConfidenceLabel } from "./displayLabels";
import { applySignalFilters, defaultSignalFilters, getAvailableSetupTypes, hasActiveSignalFilters } from "./signalFilters";
import type {
  BriefingResult,
  DashboardSignal,
  IngestionResult,
  SignalFilters,
  SignalOutcomeScoreBucketSummary,
  SignalOutcomeSetupSummary,
  SignalOutcomeSummary,
  SignalPerformancePreviewResult,
  SparklinePricesBySymbol,
  Watchlist
} from "./types";
import { allSignalsWatchlist, applyWatchlistFilter, getAllWatchlists, loadCustomWatchlists, saveCustomWatchlists } from "./watchlists";

type Status = {
  text: string;
  tone: "idle" | "ok" | "warn" | "error";
};

const AUTO_REFRESH_INTERVAL_MS = 60_000;

export function App() {
  const [briefing, setBriefing] = useState<BriefingResult | null>(null);
  const [selectedSymbol, setSelectedSymbol] = useState<string | null>(null);
  const [status, setStatus] = useState<Status>({ text: "Listo", tone: "idle" });
  const [loadingAction, setLoadingAction] = useState<string | null>(null);
  const [ingestion, setIngestion] = useState<IngestionResult | null>(null);
  const [usingMock, setUsingMock] = useState(false);
  const [sparklinePrices, setSparklinePrices] = useState<SparklinePricesBySymbol>({});
  const [performancePreview, setPerformancePreview] = useState<SignalPerformancePreviewResult | null>(null);
  const [performancePreviewUnavailable, setPerformancePreviewUnavailable] = useState(false);
  const [outcomeSummary, setOutcomeSummary] = useState<SignalOutcomeSummary | null>(null);
  const [outcomeSummaryLoading, setOutcomeSummaryLoading] = useState(false);
  const [outcomeSummaryUnavailable, setOutcomeSummaryUnavailable] = useState(false);
  const [setupSummary, setSetupSummary] = useState<SignalOutcomeSetupSummary | null>(null);
  const [setupSummaryLoading, setSetupSummaryLoading] = useState(false);
  const [setupSummaryUnavailable, setSetupSummaryUnavailable] = useState(false);
  const [scoreBucketSummary, setScoreBucketSummary] = useState<SignalOutcomeScoreBucketSummary | null>(null);
  const [scoreBucketSummaryLoading, setScoreBucketSummaryLoading] = useState(false);
  const [scoreBucketSummaryUnavailable, setScoreBucketSummaryUnavailable] = useState(false);
  const [filters, setFilters] = useState<SignalFilters>(defaultSignalFilters);
  const [customWatchlists, setCustomWatchlists] = useState<Watchlist[]>(() => loadCustomWatchlists());
  const [activeWatchlistId, setActiveWatchlistId] = useState(allSignalsWatchlist.id);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const autoRefreshInFlight = useRef(false);

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
    const intervalId = window.setInterval(() => {
      void refreshDashboardSilently();
    }, AUTO_REFRESH_INTERVAL_MS);

    return () => window.clearInterval(intervalId);
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
      setStatus({ text: `${label} completado`, tone: "ok" });
    } catch (error) {
      setStatus({
        text: error instanceof Error ? error.message : `${label} falló`,
        tone: "error"
      });
    } finally {
      setLoadingAction(null);
    }
  }

  async function refreshDashboard() {
    await withAction("Actualizar panel", async () => {
      await loadDashboardData();
    });
  }

  async function refreshDashboardSilently() {
    if (autoRefreshInFlight.current) {
      return;
    }

    autoRefreshInFlight.current = true;

    try {
      await loadDashboardData();
    } catch (error) {
      setStatus({
        text: error instanceof Error ? error.message : "Falló el auto-refresh",
        tone: "error"
      });
    } finally {
      autoRefreshInFlight.current = false;
    }
  }

  async function loadDashboardData() {
      const state = await loadDashboard();
      setBriefing(state.briefing);
      setUsingMock(state.isMock);
      await Promise.all([
        refreshSparklines(),
        refreshPerformancePreview(),
        refreshOutcomeSummary(),
        refreshSetupSummary(),
        refreshScoreBucketSummary()
      ]);
      setLastUpdatedAt(new Date());
      if (state.isMock) {
        setStatus({ text: "API no disponible. Se muestra una vista previa.", tone: "warn" });
      }
  }

  async function handleRunIngestion() {
    await withAction("Ejecutar ingesta", async () => {
      const result = await runIngestion();
      setIngestion(result);
      await Promise.all([refreshOutcomeSummary(), refreshSetupSummary(), refreshScoreBucketSummary()]);
    });
  }

  async function handleRunSignals() {
    await withAction("Generar señales", async () => {
      const result = await runSignals();
      const all = result.signals.map(toDashboardSignal);
      setUsingMock(false);
      setBriefing((current) => ({
        generatedAtUtc: result.generatedAtUtc,
        marketRegime: current?.marketRegime ?? "Solo señales",
        summary: current?.summary ?? "Señales calculadas devueltas por la API.",
        signalSummary: `${all.length} señales calculadas devueltas por la API.`,
        allSignals: all,
        topOpportunities: all.filter((signal) => signal.score >= 60 && signal.action === "Candidate"),
        watchlistPullbacks: all.filter((signal) => signal.setupType === "Pullback" || signal.action.startsWith("Watch")),
        topRisks: all.filter((signal) => signal.score < 40 || signal.action === "Avoid / high risk"),
        highlights: current?.highlights ?? [],
        risks: current?.risks ?? [],
        watchItems: current?.watchItems ?? []
      }));
      setSelectedSymbol(all[0]?.symbol ?? null);
      await Promise.all([
        refreshSparklines(),
        refreshPerformancePreview(),
        refreshOutcomeSummary(),
        refreshSetupSummary(),
        refreshScoreBucketSummary()
      ]);
    });
  }

  async function handleGenerateBriefing() {
    await withAction("Generar briefing", async () => {
      const result = await runBriefing();
      setUsingMock(false);
      setBriefing(result);
      setSelectedSymbol(result.allSignals[0]?.symbol ?? null);
      await Promise.all([
        refreshSparklines(),
        refreshPerformancePreview(),
        refreshOutcomeSummary(),
        refreshSetupSummary(),
        refreshScoreBucketSummary()
      ]);
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

  async function refreshPerformancePreview() {
    try {
      const preview = await loadSignalPerformancePreview();
      setPerformancePreview(preview);
      setPerformancePreviewUnavailable(false);
    } catch {
      setPerformancePreview(null);
      setPerformancePreviewUnavailable(true);
    }
  }

  async function refreshOutcomeSummary() {
    setOutcomeSummaryLoading(true);

    try {
      const summary = await loadSignalOutcomeSummary();
      setOutcomeSummary(summary);
      setOutcomeSummaryUnavailable(false);
    } catch {
      setOutcomeSummary(null);
      setOutcomeSummaryUnavailable(true);
    } finally {
      setOutcomeSummaryLoading(false);
    }
  }

  async function refreshSetupSummary() {
    setSetupSummaryLoading(true);

    try {
      const summary = await loadSignalOutcomeSetupSummary();
      setSetupSummary(summary);
      setSetupSummaryUnavailable(false);
    } catch {
      setSetupSummary(null);
      setSetupSummaryUnavailable(true);
    } finally {
      setSetupSummaryLoading(false);
    }
  }

  async function refreshScoreBucketSummary() {
    setScoreBucketSummaryLoading(true);

    try {
      const summary = await loadSignalOutcomeScoreBuckets();
      setScoreBucketSummary(summary);
      setScoreBucketSummaryUnavailable(false);
    } catch {
      setScoreBucketSummary(null);
      setScoreBucketSummaryUnavailable(true);
    } finally {
      setScoreBucketSummaryLoading(false);
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
          <h1>Panel de señales</h1>
        </div>
        <div className="actions">
          <ActionButton icon={<Zap size={16} />} label="Ejecutar ingesta" onClick={handleRunIngestion} loading={loadingAction === "Ejecutar ingesta"} />
          <ActionButton icon={<BarChart3 size={16} />} label="Generar señales" onClick={handleRunSignals} loading={loadingAction === "Generar señales"} />
          <ActionButton icon={<Bot size={16} />} label="Generar briefing" onClick={handleGenerateBriefing} loading={loadingAction === "Generar briefing"} />
          <ActionButton icon={<RefreshCw size={16} />} label="Actualizar panel" onClick={refreshDashboard} loading={loadingAction === "Actualizar panel"} />
        </div>
      </header>

      <section className="status-row">
        <span className={`status ${status.tone}`}>{status.text}</span>
        <span className="status ok">Auto-refresh activo</span>
        {lastUpdatedAt ? <span className="timestamp">Última actualización {formatDateTime(lastUpdatedAt)}</span> : null}
        {usingMock ? <span className="status warn">Vista previa</span> : null}
        {briefing ? <span className="timestamp">Generado {formatDate(briefing.generatedAtUtc)}</span> : null}
        {ingestion ? (
          <span className={ingestion.failed > 0 ? "status warn" : "status ok"}>
            Ingesta {ingestion.succeeded}/{ingestion.totalRequested}
          </span>
        ) : null}
      </section>

      <section className="hero-grid">
        <InfoCard title="Contexto de mercado" value={briefing?.marketRegime ?? "Cargando"} icon={<TrendingUp size={18} />} />
        <TextCard title="Resumen" text={briefing?.summary ?? "Esperando datos del panel."} />
        <TextCard title="Resumen de señales" text={briefing?.signalSummary ?? "Las señales aparecerán cuando responda la API."} />
      </section>

      <section className="signal-groups">
        <SignalGroup title="Mejores oportunidades" tone="opportunity" icon={<Sparkles size={17} />} signals={topOpportunities} onSelect={setSelectedSymbol} />
        <SignalGroup title="Pullbacks en seguimiento" tone="watch" icon={<Search size={17} />} signals={watchlistPullbacks} onSelect={setSelectedSymbol} />
        <SignalGroup title="Riesgos principales" tone="risk" icon={<ShieldAlert size={17} />} signals={topRisks} onSelect={setSelectedSymbol} />
      </section>

      <TopProfitOpportunitiesPanel signals={watchlistSignals} onSelectSymbol={setSelectedSymbol} />

      <AlertCenter alerts={alerts} onSelectSymbol={setSelectedSymbol} />

      <SignalOutcomeSummaryPanel
        loading={outcomeSummaryLoading}
        summary={outcomeSummary}
        unavailable={outcomeSummaryUnavailable}
      />

      <SetupPerformancePanel loading={setupSummaryLoading} summary={setupSummary} unavailable={setupSummaryUnavailable} />

      <ScoreConfidencePerformancePanel
        loading={scoreBucketSummaryLoading}
        summary={scoreBucketSummary}
        unavailable={scoreBucketSummaryUnavailable}
      />

      <SignalPerformancePreviewPanel preview={performancePreview} unavailable={performancePreviewUnavailable} />

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
        <ListCard title="Destacados" items={briefing?.highlights ?? []} />
        <ListCard title="Riesgos" items={briefing?.risks ?? []} />
        <ListCard title="Items en seguimiento" items={briefing?.watchItems ?? []} />
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
        {signals.length === 0 ? <span className="empty">Sin datos</span> : null}
        {signals.slice(0, 6).map((signal) => (
          <button key={signal.symbol} className="group-item" type="button" onClick={() => onSelect(signal.symbol)}>
            <span>{signal.symbol}</span>
            <Score value={signal.score} />
            <small>{formatActionLabel(signal.action)}</small>
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
        <span>Todas las señales</span>
        <b>{signals.length}</b>
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Símbolo</th>
              <th>Tendencia</th>
              <th>Score</th>
              <th>Setup</th>
              <th>Señal</th>
              <th>Acción</th>
              <th>Confianza</th>
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
                      <strong>Todavía no hay señales cargadas.</strong>
                      <span>Generá señales o un briefing para poblar el panel.</span>
                    </div>
                  ) : (
                    <div className="table-empty-state">
                      <strong>No hay señales que coincidan con la watchlist o los filtros actuales.</strong>
                      <span>Probá otra watchlist, limpiá filtros o bajá los umbrales.</span>
                      {hasActiveFilters ? (
                        <button className="filter-reset inline" type="button" onClick={onResetFilters}>
                          Limpiar filtros
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
                  <td>{formatConfidenceLabel(signal.confidence)}</td>
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
      {items.length === 0 ? <span className="empty">Sin datos</span> : null}
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
  return <span className={`pill ${tone}`}>{formatActionLabel(value)}</span>;
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

function formatDateTime(value: Date) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(value);
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
