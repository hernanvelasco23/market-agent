export type SparklineTrend = "up" | "down" | "flat";

export type SparklineProps = {
  prices?: number[] | null;
  width: number;
  height: number;
  trend?: SparklineTrend;
};

const TREND_COLORS: Record<SparklineTrend, string> = {
  up: "#86efac",
  down: "#fca5a5",
  flat: "#9fb0c9"
};

export function Sparkline({ prices, width, height, trend }: SparklineProps) {
  const safeWidth = Math.max(1, width);
  const safeHeight = Math.max(1, height);
  const padding = Math.min(4, safeHeight / 4, safeWidth / 8);
  const midY = safeHeight / 2;
  const values = (prices ?? []).filter(Number.isFinite);

  if (values.length === 0) {
    return (
      <svg className="sparkline" width={safeWidth} height={safeHeight} viewBox={`0 0 ${safeWidth} ${safeHeight}`} role="img" aria-label="No price history">
        <line className="sparkline-placeholder" x1={padding} x2={safeWidth - padding} y1={midY} y2={midY} />
      </svg>
    );
  }

  const inferredTrend = trend ?? inferTrend(values);
  const color = TREND_COLORS[inferredTrend];

  if (values.length === 1) {
    return (
      <svg className="sparkline" width={safeWidth} height={safeHeight} viewBox={`0 0 ${safeWidth} ${safeHeight}`} role="img" aria-label="Price sparkline">
        <circle cx={safeWidth / 2} cy={midY} r={2.2} fill={color} />
      </svg>
    );
  }

  const min = Math.min(...values);
  const max = Math.max(...values);

  if (min === max) {
    return (
      <svg className="sparkline" width={safeWidth} height={safeHeight} viewBox={`0 0 ${safeWidth} ${safeHeight}`} role="img" aria-label="Flat price sparkline">
        <line x1={padding} x2={safeWidth - padding} y1={midY} y2={midY} stroke={color} strokeWidth="2" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
      </svg>
    );
  }

  const drawableWidth = Math.max(1, safeWidth - padding * 2);
  const drawableHeight = Math.max(1, safeHeight - padding * 2);
  const path = values
    .map((value, index) => {
      const x = padding + (index / Math.max(1, values.length - 1)) * drawableWidth;
      const y = padding + ((max - value) / (max - min)) * drawableHeight;
      return `${index === 0 ? "M" : "L"} ${round(x)} ${round(y)}`;
    })
    .join(" ");

  return (
    <svg className="sparkline" width={safeWidth} height={safeHeight} viewBox={`0 0 ${safeWidth} ${safeHeight}`} role="img" aria-label="Price sparkline">
      <path d={path} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}

function inferTrend(values: number[]): SparklineTrend {
  const first = values[0];
  const last = values[values.length - 1];

  if (last > first) {
    return "up";
  }

  if (last < first) {
    return "down";
  }

  return "flat";
}

function round(value: number) {
  return Math.round(value * 100) / 100;
}
