import type { DashboardAlert } from "./types";

export function formatActionLabel(value?: string | null) {
  if (!value) {
    return "n/a";
  }

  const normalized = value.trim().toLowerCase();

  if (normalized === "candidate") {
    return "Candidato";
  }

  if (normalized === "watch for confirmation") {
    return "Esperar confirmación";
  }

  if (normalized === "avoid / high risk") {
    return "Evitar / riesgo alto";
  }

  return value;
}

export function formatConfidenceLabel(value?: string | null) {
  if (!value) {
    return "n/a";
  }

  const normalized = value.trim().toLowerCase();

  if (normalized === "high") {
    return "Alta";
  }

  if (normalized === "medium") {
    return "Media";
  }

  if (normalized === "low") {
    return "Baja";
  }

  return value;
}

export function formatSeverityLabel(value: DashboardAlert["severity"]) {
  switch (value) {
    case "risk":
      return "Riesgo";
    case "opportunity":
      return "Oportunidad";
    case "warning":
      return "Atención";
    case "info":
      return "Info";
  }
}
