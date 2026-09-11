import type {
  RuntimeTagRealtimeMessage,
  RuntimeTagSnapshot
} from './liveTagTransport';
import type { TrendSample, TrendSeries } from './trendVisualQueryModel';
import type { TrendVisualPen } from '../visual-runtime/trendVisualModel';

export type TrendLiveBuffers = ReadonlyMap<string, readonly TrendSample[]>;

const DEFAULT_SAMPLE_LIMIT = 1000;

export function trendSampleFromRuntimeSnapshot(
  pen: TrendVisualPen,
  tag: RuntimeTagSnapshot
): TrendSample | null {
  if (normalizeId(tag.id) !== normalizeId(pen.tagId) || !tag.current) return null;
  return runtimeSample(
    pen,
    tag.path,
    tag.current.sourceTimestamp ?? tag.current.serverTimestamp ?? tag.current.timestamp,
    tag.current.value,
    tag.current.quality
  );
}

export function trendSampleFromRuntimeMessage(
  pen: TrendVisualPen,
  message: RuntimeTagRealtimeMessage
): TrendSample | null {
  if (normalizeId(message.tag.id) !== normalizeId(pen.tagId)) return null;
  return runtimeSample(
    pen,
    message.tag.path,
    message.timestamp,
    message.value,
    message.quality
  );
}

export function appendTrendLiveSample(
  buffers: TrendLiveBuffers,
  pen: TrendVisualPen,
  sample: TrendSample,
  windowSeconds: number,
  nowEpochMilliseconds = Date.now(),
  limit = DEFAULT_SAMPLE_LIMIT
): TrendLiveBuffers {
  assertWindow(windowSeconds);
  if (!Number.isInteger(limit) || limit < 1 || limit > 10000) {
    throw new Error('Trend live sample limit must be an integer between 1 and 10000.');
  }
  const cutoff = nowEpochMilliseconds - windowSeconds * 1000;
  const candidates = buffers.get(pen.id)
    ?.filter(candidate => candidate.epochMilliseconds >= cutoff && candidate.epochMilliseconds !== sample.epochMilliseconds) ?? [];
  if (sample.epochMilliseconds >= cutoff) candidates.push(sample);
  candidates.sort((left, right) => left.epochMilliseconds - right.epochMilliseconds);
  const retained = candidates.slice(Math.max(0, candidates.length - limit));
  const next = new Map(buffers);
  next.set(pen.id, Object.freeze(retained));
  return next;
}

export function pruneTrendLiveBuffers(
  buffers: TrendLiveBuffers,
  pens: readonly TrendVisualPen[],
  windowSeconds: number,
  nowEpochMilliseconds = Date.now()
): TrendLiveBuffers {
  assertWindow(windowSeconds);
  const cutoff = nowEpochMilliseconds - windowSeconds * 1000;
  const next = new Map<string, readonly TrendSample[]>();
  for (const pen of pens) {
    next.set(pen.id, Object.freeze((buffers.get(pen.id) ?? []).filter(sample => sample.epochMilliseconds >= cutoff)));
  }
  return next;
}

export function buildTrendLiveSeries(
  pens: readonly TrendVisualPen[],
  buffers: TrendLiveBuffers
): readonly TrendSeries[] {
  return Object.freeze(pens
    .filter(pen => pen.visible)
    .map(pen => Object.freeze({
      pen,
      samples: Object.freeze([...(buffers.get(pen.id) ?? [])])
    })));
}

export function isUsableTrendQuality(quality: string | number | null | undefined): boolean {
  if (quality === 0) return true;
  if (quality === null || quality === undefined) return false;
  const normalized = String(quality).trim().toLocaleLowerCase('en-US');
  return normalized === 'good' || normalized === '0';
}

function runtimeSample(
  pen: TrendVisualPen,
  tagPath: string,
  timestamp: string,
  value: unknown,
  quality: string | number
): TrendSample | null {
  const epochMilliseconds = Date.parse(timestamp);
  const numericValue = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(epochMilliseconds) || !Number.isFinite(numericValue)) return null;
  return Object.freeze({
    tagId: pen.tagId,
    tagPath: tagPath || pen.tagPath,
    timestamp,
    epochMilliseconds,
    value: numericValue,
    quality: normalizeQuality(quality)
  });
}

function normalizeQuality(quality: string | number): string {
  return isUsableTrendQuality(quality) ? 'Good' : String(quality).trim() || 'Unknown';
}

function normalizeId(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
}

function assertWindow(windowSeconds: number): void {
  if (!Number.isInteger(windowSeconds) || windowSeconds < 60 || windowSeconds > 604800) {
    throw new Error('Trend live window must be an integer between 60 and 604800 seconds.');
  }
}
