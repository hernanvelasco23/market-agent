# Spanish Market UI Design

## Design Principles

1. Keep code and contracts in English.
2. Translate only visible user-facing text.
3. Keep trading terms that Argentina-market users naturally use in English.
4. Prefer centralized copy constants where practical, but avoid a large i18n framework in V1.
5. Preserve existing layout, panel structure, ranking logic, and API payloads.

## Terminology Guide

Recommended V1 terminology:

| Current English | Spanish V1 |
| --- | --- |
| Signal Dashboard | Panel de señales |
| Market Regime | Contexto de mercado |
| Summary | Resumen |
| Signal Summary | Resumen de señales |
| Top Opportunities | Mejores oportunidades |
| Watchlist Pullbacks | Pullbacks en seguimiento |
| Top Risks | Riesgos principales |
| Top Profit Opportunities | Mejores oportunidades por upside |
| Alert Center | Centro de alertas |
| Signal Outcomes | Resultados de señales |
| Setup Performance | Performance por setup |
| Score & Confidence Performance | Performance por score y confianza |
| All Signals | Todas las señales |
| Candidate | Candidato |
| Watch for confirmation | Esperar confirmación |
| Avoid / high risk | Evitar / riesgo alto |
| Potential Upside | Upside potencial |
| Entry | Entrada |
| Stop | Stop |
| Take Profit | Take Profit |
| Risk/Reward | Riesgo/beneficio |
| Confidence | Confianza |
| Last updated | Última actualización |
| Auto-refresh: ON | Auto-refresh activo |
| Generated | Generado |
| Run Ingestion | Ejecutar ingesta |
| Run Signals | Generar señales |
| Generate Briefing | Generar briefing |
| Refresh Dashboard | Actualizar panel |

Terms intentionally kept in English:

- setup
- pullback
- breakout
- upside
- stop
- take profit
- score
- momentum
- briefing
- watchlist
- risk-on / risk-off

Reason: these are widely used by local traders and are more concise than literal Spanish alternatives.

## Frontend Design

### App Shell

Update visible labels in `App.tsx`:

- Header title
- Action buttons
- Status row
- Metric cards
- Signal groups
- Table title and headers
- Empty states
- List cards

Keep function names, component names, and types in English.

Example:

```tsx
<h1>Panel de señales</h1>
```

Do not rename:

```tsx
function SignalsTable(...)
```

### Panels

Update component copy:

- `TopProfitOpportunitiesPanel`
  - title: `Mejores oportunidades por upside`
  - empty state: `No hay candidatos con entrada, objetivo y riesgo/beneficio válidos.`
  - badges: `Upside alto`, `Buen upside`

- `SignalOutcomeSummaryPanel`
  - title: `Resultados de señales`
  - explain partial metrics as intraday/partial

- `SetupPerformancePanel`
  - title: `Performance por setup`
  - `n/a` can remain `n/a`

- `ScoreConfidencePerformancePanel`
  - title: `Performance por score y confianza`

- `AlertCenter`
  - title: `Centro de alertas`
  - empty: `No hay alertas activas para el set actual de señales.`
  - severity badges can map to `Riesgo`, `Oportunidad`, `Atención`, `Info`

- `SignalFilterBar`
  - filter labels and empty/clear labels

- `SignalDetailPanel`
  - title/field labels/action labels

### Table Headers

Suggested table headers:

- `Símbolo`
- `Tendencia`
- `Score`
- `Setup`
- `Señal`
- `Acción`
- `Confianza`
- `Timeframe`
- `RS`
- `RVOL`
- `EXT`
- `RSI14`
- `EMA9`
- `EMA20`
- `EMA50`
- `ATR14`

Keep indicator names unchanged.

### Status And Errors

Suggested states:

- `Ready` -> `Listo`
- `Refresh dashboard...` -> `Actualizando panel...`
- `Refresh dashboard completed` -> `Panel actualizado`
- `API unavailable. Mock preview loaded.` -> `API no disponible. Se muestra una vista previa.`
- `No signals loaded yet.` -> `Todavía no hay señales cargadas.`
- `Run signals or generate a briefing...` -> `Generá señales o un briefing para poblar el panel.`
- `No signals match...` -> `No hay señales que coincidan con la watchlist o los filtros actuales.`
- `Clear filters` -> `Limpiar filtros`

## Email Digest Design

Update `AlertEmailTemplateBuilder` copy:

Subject:

- `[MarketAgent] 1 alerta nueva - NVDA`
- `[MarketAgent] 5 alertas nuevas - NVDA, TSLA, GOOG`

Header:

- `MarketAgent Alert Digest` -> `Resumen de alertas MarketAgent`
- `Generated` -> `Generado`

Summary cards:

- `Alerts` -> `Alertas`
- `Top Candidate` -> `Mejor candidato`
- `Highest Score` -> `Score más alto`
- `Best Upside` -> `Mejor upside`

Table headers:

- `Symbol` -> `Símbolo`
- `Setup` -> `Setup`
- `Score` -> `Score`
- `Confidence` -> `Confianza`
- `Price` -> `Precio`
- `Upside` -> `Upside`
- `Entry` -> `Entrada`
- `TP` -> `TP`
- `Title` -> `Título`
- `Message` -> `Mensaje`
- `Reason` -> `Motivo`
- `Created UTC` -> `Creada UTC`

Badges:

- `HIGH CONFIDENCE` -> `ALTA CONFIANZA`
- `MEDIUM CONFIDENCE` -> `CONFIANZA MEDIA`
- `HIGH UPSIDE` -> `UPSIDE ALTO`
- `GOOD UPSIDE` -> `BUEN UPSIDE`

Reason summaries:

- `setup avg 15m` -> `promedio setup 15m`
- `score threshold` -> `umbral score`
- `confidence` -> `confianza`
- `setup performance` -> `performance setup`

Do not include raw JSON blobs.

## Alert Copy Design

Update derived alert text in `alerts.ts`:

Examples:

- `Momentum breakout watch` -> `Breakout de momentum en observación`
- `Opening red reversal` -> `Reversión desde apertura roja`
- `EMA20 reclaim context` -> `Recuperación sobre EMA20`
- `Overextended above EMA20` -> `Extendida sobre EMA20`
- `Momentum failure risk` -> `Riesgo de falla de momentum`

Keep alert types/internal ids unchanged.

Backend persisted alert titles/messages may also be adapted, but avoid changing alert rule decisions.

## AI Briefing Design

AI output should be instructed to write in Spanish for Argentina/CEDEAR users.

Potential prompt instruction:

```text
Write all user-facing summaries in natural Spanish for Argentina-market and CEDEAR investors. Keep common trading terms such as setup, pullback, breakout, upside, stop, take profit, score, momentum, risk-on, and risk-off in English when natural.
```

Do not change model configuration or business logic in V1.

Fallback/mock copy in `api.ts` should also be Spanish.

## Centralization Strategy

V1 should avoid a full i18n framework.

Recommended:

- Add small copy maps where useful:
  - action label map
  - confidence label map
  - severity label map
- Use helper functions:
  - `formatActionLabel(action)`
  - `formatConfidenceLabel(confidence)`
  - `formatSeverityLabel(severity)`

This keeps future i18n possible without introducing routing, locale negotiation, or a translation runtime now.

## Risks And Conflicts

- Some signal actions may be persisted in English; frontend should map known values without changing API values.
- AI-generated text may still include English unless prompt guidance is updated.
- Email subject Spanish pluralization should be simple and correct.
- Over-translating terms like `setup` or `upside` may feel less natural to target users.
- Long Spanish labels may affect compact card/table layouts; verify responsive fit.

## Rollback Plan

- Revert copy maps/helper changes.
- Revert frontend component text.
- Revert email template text.
- Revert AI prompt/user-facing summary text.
- No migration or API rollback needed.
