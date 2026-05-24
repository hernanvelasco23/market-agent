# Spanish Market UI Proposal

## Business Goal

MarketAgent is moving toward a CEDEARs / Argentina-focused market intelligence product. The backend and API should remain stable in English, while visible user-facing communication should become natural Spanish for Argentina-market investors and traders.

The goal is to adapt the frontend, email digest, alert copy, and AI/user-facing summaries to Spanish without changing calculations, signal generation, ranking, persistence, scheduler behavior, or API contracts.

## Audience

Primary V1 audience:

- Spanish-speaking Argentina investors/traders
- Users following CEDEARs and US-listed underlying names
- Users comfortable with common trading terms such as `setup`, `pullback`, `breakout`, `upside`, `stop`, `take profit`, `score`, and `momentum`

Tone:

- Professional but accessible
- Clear and actionable
- Argentina-market natural Spanish
- Avoid overly literal translation
- Keep trader jargon when it is clearer than a forced Spanish replacement

## Scope

In scope:

- Frontend UI labels
- Dashboard section titles
- Table headers
- Badges and status text
- Empty/loading/error states
- Frontend mock/fallback copy
- Alert Center user-facing text
- Email digest subject, headers, table labels, badges, and reason summaries
- AI briefing prompts/output instructions where user-facing copy is generated
- Any summary text shown directly to the user

Out of scope:

- No i18n framework
- No language switcher
- No backend refactor
- No API contract changes
- No database changes
- No scheduler changes
- No alert rule changes
- No score/calibration changes

## Translation Strategy

Use Spanish for full user-facing phrases and section titles.

Keep selected market terms in English when they are common and compact:

- setup
- pullback
- breakout
- upside
- stop
- take profit
- score
- momentum
- risk-on / risk-off
- watchlist

Translate action/status language:

- `Candidate` -> `Candidato`
- `Watch for confirmation` -> `Esperar confirmación`
- `Avoid / high risk` -> `Evitar / riesgo alto`
- `High Confidence` -> `Alta confianza`
- `Medium Confidence` -> `Confianza media`
- `Low Confidence` -> `Baja confianza`

Use a consistent hybrid vocabulary:

- `Upside potencial`
- `Entrada`
- `Stop`
- `Take Profit`
- `Riesgo/beneficio`
- `Confianza`
- `Contexto de mercado`
- `Resultados de señales`

## User-Facing Areas Identified

Frontend:

- `MarketAgent.Web/src/App.tsx`
- `MarketAgent.Web/src/components/AlertCenter.tsx`
- `MarketAgent.Web/src/components/TopProfitOpportunitiesPanel.tsx`
- `MarketAgent.Web/src/components/SignalOutcomeSummaryPanel.tsx`
- `MarketAgent.Web/src/components/SetupPerformancePanel.tsx`
- `MarketAgent.Web/src/components/ScoreConfidencePerformancePanel.tsx`
- `MarketAgent.Web/src/components/SignalPerformancePreviewPanel.tsx`
- `MarketAgent.Web/src/components/SignalFilterBar.tsx`
- `MarketAgent.Web/src/components/SignalDetailPanel.tsx`
- `MarketAgent.Web/src/components/WatchlistSelector.tsx`
- `MarketAgent.Web/src/api.ts` mock/fallback briefing copy
- `MarketAgent.Web/src/alerts.ts` derived alert titles/descriptions/metric labels

Backend user-facing output:

- `src/MarketAgent.Application/Alerts/AlertEmailTemplateBuilder.cs`
- Alert event titles/messages created by the alert evaluator
- AI briefing generation prompts or fallback summaries
- Any generated summaries returned to the frontend

## Success Criteria

- Dashboard reads naturally in Spanish.
- Email digest reads naturally in Spanish.
- Alert Center text is Spanish and actionable.
- Trading terms are consistent across dashboard/email/alerts.
- Existing API contracts remain unchanged.
- Existing calculations and sorting remain unchanged.
- Layout remains stable and responsive.
- Future i18n remains possible by centralizing copy where practical.

## Rollback Plan

- Revert frontend copy changes.
- Revert email template copy changes.
- Revert AI prompt/copy changes.
- No database rollback required.
- No API rollback required.
