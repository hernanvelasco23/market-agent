import { ChevronDown } from "lucide-react";
import type { ReactNode } from "react";
import type { DashboardSectionId } from "../collapsibleSections";

interface CollapsibleSectionProps {
  id: DashboardSectionId;
  title: string;
  count?: ReactNode;
  collapsed: boolean;
  children: ReactNode;
  onToggle: (id: DashboardSectionId) => void;
}

export function CollapsibleSection({
  id,
  title,
  count,
  collapsed,
  children,
  onToggle
}: CollapsibleSectionProps) {
  return (
    <section className={`collapsible-section ${collapsed ? "is-collapsed" : ""}`}>
      <button
        className="collapsible-header"
        type="button"
        aria-expanded={!collapsed}
        aria-controls={`${id}-content`}
        onClick={() => onToggle(id)}
      >
        <span className="collapsible-title">{title}</span>
        {count != null ? <b className="collapsible-count">{count}</b> : null}
        <small className="collapsible-action">{collapsed ? "Expandir" : "Colapsar"}</small>
        <ChevronDown className={`collapsible-chevron ${collapsed ? "collapsed" : ""}`} size={17} />
      </button>
      {collapsed ? null : (
        <div id={`${id}-content`} className="collapsible-content">
          {children}
        </div>
      )}
    </section>
  );
}
