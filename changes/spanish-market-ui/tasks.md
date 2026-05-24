# Spanish Market UI Tasks

## 1. Inventory User-Facing Strings

- [ ] Review `MarketAgent.Web/src/App.tsx`.
- [ ] Review `MarketAgent.Web/src/components/AlertCenter.tsx`.
- [ ] Review `MarketAgent.Web/src/components/TopProfitOpportunitiesPanel.tsx`.
- [ ] Review `MarketAgent.Web/src/components/SignalOutcomeSummaryPanel.tsx`.
- [ ] Review `MarketAgent.Web/src/components/SetupPerformancePanel.tsx`.
- [ ] Review `MarketAgent.Web/src/components/ScoreConfidencePerformancePanel.tsx`.
- [ ] Review `MarketAgent.Web/src/components/SignalPerformancePreviewPanel.tsx`.
- [ ] Review `MarketAgent.Web/src/components/SignalFilterBar.tsx`.
- [ ] Review `MarketAgent.Web/src/components/SignalDetailPanel.tsx`.
- [ ] Review `MarketAgent.Web/src/components/WatchlistSelector.tsx`.
- [ ] Review `MarketAgent.Web/src/api.ts` mock/fallback copy.
- [ ] Review `MarketAgent.Web/src/alerts.ts`.
- [ ] Review `src/MarketAgent.Application/Alerts/AlertEmailTemplateBuilder.cs`.
- [ ] Review AI briefing prompt/user-facing summary generation.
- [ ] Review alert evaluator title/message text.

## 2. Add Lightweight Copy Helpers

- [ ] Add frontend helper for signal action labels.
- [ ] Add frontend helper for confidence labels.
- [ ] Add frontend helper for severity labels.
- [ ] Add helper for common empty/error/loading phrases if useful.
- [ ] Keep helpers lightweight; do not add i18n library.
- [ ] Keep API values unchanged.

## 3. Translate Main Dashboard

- [ ] Translate page header and status row.
- [ ] Translate action buttons.
- [ ] Translate metric card titles.
- [ ] Translate signal group titles.
- [ ] Translate list card titles.
- [ ] Translate generated/last-updated/auto-refresh labels.
- [ ] Preserve selected signal, filters, and watchlist behavior.

## 4. Translate Signal Table And Detail Panel

- [ ] Translate table title.
- [ ] Translate table headers.
- [ ] Translate empty states.
- [ ] Translate filter reset text.
- [ ] Translate detail panel labels.
- [ ] Map known action values to Spanish labels.
- [ ] Map confidence values to Spanish labels.
- [ ] Keep indicator names like RSI, EMA, ATR, RVOL unchanged.

## 5. Translate Analytics Panels

- [ ] Translate `SignalOutcomeSummaryPanel`.
- [ ] Translate `SetupPerformancePanel`.
- [ ] Translate `ScoreConfidencePerformancePanel`.
- [ ] Translate `SignalPerformancePreviewPanel`.
- [ ] Translate loading/error/empty states.
- [ ] Label partial/intraday metrics in natural Spanish.

## 6. Translate Profit Ranking

- [ ] `Top Profit Opportunities` -> `Mejores oportunidades por upside`.
- [ ] Translate empty state.
- [ ] Translate `High Upside` -> `Upside alto`.
- [ ] Translate `Good Upside` -> `Buen upside`.
- [ ] Keep calculation and sorting unchanged.

## 7. Translate Alert Center

- [ ] `Alert Center` -> `Centro de alertas`.
- [ ] Translate empty state.
- [ ] Translate derived alert titles/descriptions in `alerts.ts`.
- [ ] Translate metric labels where visible.
- [ ] Keep alert ids/types unchanged.

## 8. Translate Email Digest

- [ ] Translate subject line.
- [ ] Translate digest title/header.
- [ ] Translate summary card labels.
- [ ] Translate table headers.
- [ ] Translate confidence badges.
- [ ] Translate upside badges.
- [ ] Translate reason summary labels.
- [ ] Keep HTML escaping and inline CSS unchanged.
- [ ] Keep delivery status and repository logic unchanged.

## 9. Adapt AI Briefing Copy

- [ ] Update prompt instructions to request Spanish output for Argentina/CEDEAR users.
- [ ] Keep common trading terms in English where natural.
- [ ] Translate fallback/mock briefing text in frontend.
- [ ] Do not change model, endpoint, or scoring behavior.

## 10. Layout Review

- [ ] Check desktop dashboard for label overflow.
- [ ] Check mobile/narrow viewport for long Spanish labels.
- [ ] Check table header fit.
- [ ] Check email rendering remains compact.
- [ ] Prefer concise Spanish copy when layout is tight.

## 11. Tests And Validation

- [ ] Run `npm.cmd run build`.
- [ ] Run `dotnet build MarketAgent.sln --no-restore`.
- [ ] Run unit tests if backend copy changes touch tested code.
- [ ] Manually review dashboard labels.
- [ ] Manually review generated email HTML.
- [ ] Manually review alert center text.
- [ ] Confirm API JSON fields remain unchanged.

## 12. Rollback

- [ ] Revert frontend copy/helper changes.
- [ ] Revert email template copy changes.
- [ ] Revert AI prompt copy changes.
- [ ] No migration rollback required.
- [ ] No API contract rollback required.
