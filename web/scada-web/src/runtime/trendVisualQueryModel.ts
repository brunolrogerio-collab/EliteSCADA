import type { TrendVisualPen } from '../visual-runtime';
import type {
  HistoricalQueryRequest,
  HistoricalQueryResponse,
  HistoricalQueryRow
} from './historical-browser/historicalQueryApi';

export type TrendSample = Readonly<{
  tagId: string;
  tagPath: string;
  timestamp: string;
  epochMilliseconds: number;
  value: number;
  quality: string;
}>;

export type TrendSeries = Readonly<{
  pen: TrendVisualPen;
  samples: readonly TrendSample[];
}>;

export function buildTrendHistoricalQuery(
  pens: readonly TrendVisualPen[],
  windowSeconds: number,
  limit = 1000
): HistoricalQueryRequest {
  const tagIds = [...new Set(pens.filter(pen => pen.visible).map(pen => pen.tagId))];
  if (tagIds.length === 0) throw new Error('Trend requires at least one visible Pen before querying history.');
  if (!Number.isSafeInteger(windowSeconds) || windowSeconds < 60 || windowSeconds > 604800) {
    throw new Error('Trend windowSeconds must be an integer between 60 and 604800.');
  }
  if (!Number.isSafeInteger(limit) || limit < 1 || limit > 1000) {
    throw new Error('Trend query limit must be an integer between 1 and 1000.');
  }
  return Object.freeze({
    datasetKey: 'historian.samples',
    version: 1,
    timeRange: Object.freeze({ kind: 'relative', durationSeconds: windowSeconds, anchor: 'now' }),
    filters: Object.freeze([Object.freeze({
      field: 'tag.id',
      operator: 'in',
      values: Object.freeze(tagIds.map(tagId => Object.freeze({ kind: 'guid' as const, value: tagId })))
    })]),
    orderBy: Object.freeze([Object.freeze({ field: 'timestamp', direction: 'ascending' as const })]),
    page: Object.freeze({ limit })
  });
}

export function buildTrendSeries(
  response: HistoricalQueryResponse,
  pens: readonly TrendVisualPen[]
): readonly TrendSeries[] {
  if (response.datasetKey !== 'historian.samples') throw new Error('Trend response must use historian.samples.');
  const byTagId = new Map<string, TrendSample[]>();
  for (const row of response.rows) {
    const sample = normalizeTrendSample(row);
    if (!sample) continue;
    const bucket = byTagId.get(sample.tagId);
    if (bucket) bucket.push(sample);
    else byTagId.set(sample.tagId, [sample]);
  }
  return Object.freeze(pens.filter(pen => pen.visible).map(pen => Object.freeze({
    pen,
    samples: Object.freeze([...(byTagId.get(pen.tagId) ?? [])].sort((a, b) => a.epochMilliseconds - b.epochMilliseconds))
  })));
}

export function trendQueryRange(response: HistoricalQueryResponse): Readonly<{ from: number; to: number }> {
  const from = Date.parse(response.fromUtc);
  const to = Date.parse(response.toUtc);
  if (!Number.isFinite(from) || !Number.isFinite(to) || from >= to) throw new Error('Trend historical query range is invalid.');
  return Object.freeze({ from, to });
}

function normalizeTrendSample(row: HistoricalQueryRow): TrendSample | null {
  const tagId = cellText(row, 'tag.id');
  const tagPath = cellText(row, 'tag.path');
  const timestamp = cellText(row, 'timestamp');
  const quality = cellText(row, 'quality');
  const valueText = cellText(row, 'value');
  if (!tagId || !timestamp || valueText === null) return null;
  const epochMilliseconds = Date.parse(timestamp);
  const value = Number(valueText);
  if (!Number.isFinite(epochMilliseconds) || !Number.isFinite(value)) return null;
  return Object.freeze({ tagId, tagPath: tagPath ?? '', timestamp, epochMilliseconds, value, quality: quality ?? 'Unknown' });
}

function cellText(row: HistoricalQueryRow, field: string): string | null {
  return row.cells[field]?.value ?? null;
}
