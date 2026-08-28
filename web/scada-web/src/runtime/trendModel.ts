import { normalizeRuntimeTagQuality } from './tagInspectorModel';
import type {
  BasicTrendMode,
  BasicTrendPlot,
  BasicTrendQualityTone,
  BasicTrendRange,
  BasicTrendSummary,
  BasicTrendWindow,
  RuntimeTagHistorySample
} from './trendTypes';

const windowMilliseconds: Record<BasicTrendWindow, number> = {
  '15m': 15 * 60_000,
  '1h': 60 * 60_000,
  '6h': 6 * 60 * 60_000,
  '24h': 24 * 60 * 60_000
};

export const MAX_TREND_WINDOW_MILLISECONDS = windowMilliseconds['24h'];
export const MAX_TREND_SAMPLES = 1000;

export function trendWindowMilliseconds(window: BasicTrendWindow): number {
  return windowMilliseconds[window];
}

export function buildBasicTrendRange(
  mode: BasicTrendMode,
  window: BasicTrendWindow,
  historicalEnd: Date | null,
  now = new Date()
): BasicTrendRange {
  const nowMs = now.getTime();
  const requestedEnd = mode === 'historical' && historicalEnd && Number.isFinite(historicalEnd.getTime())
    ? historicalEnd.getTime()
    : nowMs;
  const endMs = Math.min(requestedEnd, nowMs);
  const fromMs = endMs - trendWindowMilliseconds(window);
  return {
    from: new Date(fromMs).toISOString(),
    to: new Date(endMs).toISOString(),
    window
  };
}

export function clampTrendSampleLimit(limit: number): number {
  if (!Number.isFinite(limit)) return 500;
  return Math.min(MAX_TREND_SAMPLES, Math.max(1, Math.floor(limit)));
}

export function validateTrendRange(from: string, to: string): void {
  const start = new Date(from).getTime();
  const end = new Date(to).getTime();
  if (!Number.isFinite(start) || !Number.isFinite(end)) throw new Error('Trend range timestamps must be valid ISO dates.');
  if (end < start) throw new Error('Trend range end must be greater than or equal to start.');
  if (end - start > MAX_TREND_WINDOW_MILLISECONDS) throw new Error('Trend range cannot exceed 24 hours.');
}

export function trendQualityTone(value: string | number | null | undefined): BasicTrendQualityTone {
  const quality = normalizeRuntimeTagQuality(value);
  if (quality === 'good') return 'good';
  if (quality === 'uncertain' || quality === 'stale') return 'attention';
  if (quality === 'unknown') return 'unknown';
  return 'bad';
}

export function trendNumericValue(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'boolean') return value ? 1 : 0;
  return null;
}

export function sortTrendSamples(samples: readonly RuntimeTagHistorySample[]): RuntimeTagHistorySample[] {
  return [...samples].sort((left, right) => {
    const leftTime = new Date(left.timestamp).getTime();
    const rightTime = new Date(right.timestamp).getTime();
    if (!Number.isFinite(leftTime) && !Number.isFinite(rightTime)) return 0;
    if (!Number.isFinite(leftTime)) return 1;
    if (!Number.isFinite(rightTime)) return -1;
    return leftTime - rightTime;
  });
}

export function summarizeTrendSamples(samples: readonly RuntimeTagHistorySample[]): BasicTrendSummary {
  const ordered = sortTrendSamples(samples);
  const summary: BasicTrendSummary = {
    total: ordered.length,
    good: 0,
    attention: 0,
    bad: 0,
    unknown: 0,
    numericCount: 0,
    minimum: null,
    maximum: null,
    latestValue: ordered.at(-1)?.value ?? null,
    latestTimestamp: ordered.at(-1)?.timestamp ?? null
  };

  for (const sample of ordered) {
    summary[trendQualityTone(sample.quality)] += 1;
    const numeric = trendNumericValue(sample.value);
    if (numeric === null) continue;
    summary.numericCount += 1;
    summary.minimum = summary.minimum === null ? numeric : Math.min(summary.minimum, numeric);
    summary.maximum = summary.maximum === null ? numeric : Math.max(summary.maximum, numeric);
  }

  return summary;
}

export function buildTrendPlot(
  samples: readonly RuntimeTagHistorySample[],
  width = 1000,
  height = 280,
  padding = 24
): BasicTrendPlot {
  const ordered = sortTrendSamples(samples)
    .map(sample => ({ sample, numeric: trendNumericValue(sample.value), time: new Date(sample.timestamp).getTime() }))
    .filter(item => item.numeric !== null && Number.isFinite(item.time));

  if (ordered.length === 0) return { points: [], minimum: 0, maximum: 0 };

  const values = ordered.map(item => item.numeric as number);
  const times = ordered.map(item => item.time);
  let minimum = Math.min(...values);
  let maximum = Math.max(...values);
  if (minimum === maximum) {
    const margin = Math.max(Math.abs(minimum) * 0.05, 1);
    minimum -= margin;
    maximum += margin;
  }

  const minTime = Math.min(...times);
  const maxTime = Math.max(...times);
  const plotWidth = Math.max(1, width - padding * 2);
  const plotHeight = Math.max(1, height - padding * 2);
  const timeSpan = Math.max(1, maxTime - minTime);
  const valueSpan = Math.max(Number.EPSILON, maximum - minimum);

  return {
    minimum,
    maximum,
    points: ordered.map(item => ({
      x: padding + ((item.time - minTime) / timeSpan) * plotWidth,
      y: padding + (1 - (((item.numeric as number) - minimum) / valueSpan)) * plotHeight,
      value: item.numeric as number,
      timestamp: item.sample.timestamp,
      qualityTone: trendQualityTone(item.sample.quality)
    }))
  };
}
