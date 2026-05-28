export const collapsedSectionsStorageKey = "marketagent.collapsedSections.v1";

export type DashboardSectionId =
  | "market-context"
  | "my-watchlist"
  | "top-profit-opportunities"
  | "alert-center"
  | "signal-outcomes"
  | "setup-performance"
  | "score-confidence-performance"
  | "signal-performance-preview"
  | "all-signals";

export const defaultCollapsedSections: Record<DashboardSectionId, boolean> = {
  "market-context": false,
  "my-watchlist": false,
  "top-profit-opportunities": false,
  "alert-center": true,
  "signal-outcomes": true,
  "setup-performance": true,
  "score-confidence-performance": true,
  "signal-performance-preview": true,
  "all-signals": false
};

export function loadCollapsedSections() {
  if (typeof window === "undefined") {
    return defaultCollapsedSections;
  }

  try {
    const raw = window.localStorage.getItem(collapsedSectionsStorageKey);
    if (!raw) {
      return defaultCollapsedSections;
    }

    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return defaultCollapsedSections;
    }

    return {
      ...defaultCollapsedSections,
      ...Object.fromEntries(
        Object.keys(defaultCollapsedSections).map((key) => [
          key,
          typeof parsed[key] === "boolean"
            ? parsed[key]
            : defaultCollapsedSections[key as DashboardSectionId]
        ])
      )
    } as Record<DashboardSectionId, boolean>;
  } catch {
    return defaultCollapsedSections;
  }
}

export function saveCollapsedSections(sections: Record<DashboardSectionId, boolean>) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(collapsedSectionsStorageKey, JSON.stringify(sections));
}
