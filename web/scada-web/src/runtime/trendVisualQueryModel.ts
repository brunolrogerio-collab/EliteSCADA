import {
  createHistoricalTimeRangeState,
  historicalTimeRangeToQuery,
  type HistoricalTimeRangeState
} from './historicalTimeRange';
import type { TrendVisualPen } from '../visual-runtime';
import {
  normalizeHistoricalQueryResponse,
  type HistoricalQueryRequest,
  type HistoricalQueryResponse,
  type HistoricalQueryRow
} from './historical-browser/historicalQueryApi';

const TREND_DATA_QUERY_ID = '48bba95f-2817-421e-b524-ac642ecc2eae';
const TREND_DATA_QUERY_KEY = 'runtime-trend-history-v1';
const MAX_TREND_RESULT_POINTS = 200;
const MAX_TREND_INTERPOLATION_GAP_MS = 60_000;

export type TrendSample = Readonly<{
  tagId: string;
  tagPath: string;
  timestamp: string;
  epochMilliseconds: number;
  value: number | null;
  quality: string;
}>;

export type TrendSeries = Readonly<{
  pen: TrendVisualPen;
  samples: readonly TrendSample[];
}>;

export type TrendHistoricalQueryEnvelope = Readonly<{
  definition: Readonly<{
    id: string;
    key: string;
    name: string;
    providerKey: 'historical';
    version: 1;
    query: HistoricalQueryRequest;
    selectedFields: readonly string[];
    historianRetrieval: Readonly<{
      mode: 'sampledFixedStep';
      stepMilliseconds: number;
      maximumGapMilliseconds: number;
    }>;
  }>;
  execution: Readonly<Record<string, never>>;
}>;

export function buildTrendHistoricalQuery(
  pens: readonly TrendVisualPen[],
  rangeOrWindowSeconds: HistoricalTimeRangeState | number,
  maximumResultPoints = MAX_TREND_RESULT_POINTS
): TrendHistoricalQueryEnvelope {
  const tagIds = [...new Set(pens.filter(pen => pen.visible).map(pen => pen.tagId))];
  if (tagIds.length === 0) throw new Error('Trend requires at least one visible Pen before querying history.');
  if (!Number.isSafeInteger(maximumResultPoints) || maximumResultPoints < tagIds.length * 2 || maximumResultPoints > MAX_TREND_RESULT_POINTS) {
    throw new Error(`Trend result point limit must fit at least two points per Pen and cannot exceed ${MAX_TREND_RESULT_POINTS}.`);
  }

  const range = typeof rangeOrWindowSeconds === 'number'
    ? createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: rangeOrWindowSeconds })
    : rangeOrWindowSeconds;
  if (range.mode === 'live') throw new Error('Live Trend does not query Historian history.');
  const timeRange = historicalTimeRangeToQuery(range);
  const durationMilliseconds = timeRange.kind === 'absolute'
    ? Date.parse(timeRange.toUtc) - Date.parse(timeRange.fromUtc)
    : timeRange.durationSeconds * 1000;
  if (!Number.isFinite(durationMilliseconds) || durationMilliseconds <= 0) {
    throw new Error('Trend historical query range must have a positive duration.');
  }

  // Historical Query pagination is table-oriented. A Trend samples the complete
  // selected window through the shared bounded retrieval provider instead of
  // plotting only the first raw page.
  const pointsPerPen = Math.floor(maximumResultPoints / tagIds.length);
  const intervalsPerPen = Math.max(1, pointsPerPen - 1);
  const stepMilliseconds = Math.max(1, Math.ceil(durationMilliseconds / intervalsPerPen));
  const maximumGapMilliseconds = Math.max(
    5_000,
    Math.min(MAX_TREND_INTERPOLATION_GAP_MS, stepMilliseconds)
  );

  const query: HistoricalQueryRequest = Object.freeze({
    datasetKey: 'historian.samples',
    version: 1,
    timeRange,
    filters: Object.freeze([Object.freeze({
      field: 'tag.id',
      operator: 'in',
      values: Object.freeze(tagIds.map(tagId => Object.freeze({ kind: 'guid' as const, value: tagId })))
    })]),
    orderBy: Object.freeze([Object.freeze({ field: 'timestamp', direction: 'ascending' as const })]),
    page: Object.freeze({ limit: maximumResultPoints })
  });

  return Object.freeze({
    definition: Object.freeze({
      id: TREND_DATA_QUERY_ID,
      key: TREND_DATA_QUERY_KEY,
      name: 'Runtime Trend History',
      providerKey: 'historical',
      version: 1,
      query,
      selectedFields: Object.freeze(['tag.id', 'tag.path', 'quality', 'value', 'timestamp']),
      historianRetrieval: Object.freeze({
        mode: 'sampledFixedStep',
        stepMilliseconds,
        maximumGapMilliseconds
      })
    }),
    execution: Object.freeze({})
  });
}

export async function executeTrendHistoricalQuery(
  request: TrendHistoricalQueryEnvelope,
  signal?: AbortSignal
): Promise<HistoricalQueryResponse> {
  const response = await fetch('/api/historical/data-query', {
    method: 'POST',
    credentials: 'same-origin',
    headers: { accept: 'application/json', 'content-type': 'application/json' },
    body: JSON.stringify(request),
    signal
  });
  if (!response.ok) {
    let message = `Trend historian query failed with HTTP ${response.status}.`;
    try {
      const payload: unknown = await response.json();
      if (isRecord(payload) && typeof payload.error === 'string' && payload.error.trim()) message = payload.error;
    } catch {
      // Keep the status-based diagnostic when the body is absent or non-JSON.
    }
    throw new Error(message);
  }

  const payload: unknown = await response.json();
  if (!isRecord(payload) || payload.datasetKey !== 'historian.samples' || !Array.isArray(payload.rows)) {
    throw new Error('Trend historian response is malformed.');
  }

  const rows: HistoricalQueryRow[] = payload.rows.map((candidate: unknown) => {
    if (!isRecord(candidate) || !isRecord(candidate.data) || !isRecord(candidate.data.cells)) {
      throw new Error('Trend historian row is malformed.');
    }

    const cells = { ...candidate.data.cells } as Record<string, unknown>;
    const value = cells.value;
    const isNullValue = isRecord(value) && (value.kind === 'null' || value.value === null);
    const provenance = isRecord(candidate.provenance) && typeof candidate.provenance.kind === 'string'
      ? candidate.provenance.kind.toLocaleLowerCase('en-US')
      : '';
    if (provenance === 'gap' || isNullValue) {
      cells.value = Object.freeze({ kind: 'null', value: null });
      cells.quality = Object.freeze({ kind: 'enum', value: 'Gap' });
    }
    return Object.freeze({ cells }) as HistoricalQueryRow;
  });

  return normalizeHistoricalQueryResponse({
    ...payload,
    rows,
    nextCursor: null,
    pageSize: payload.resultLimit
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
  if (!tagId || !timestamp) return null;
  const epochMilliseconds = Date.parse(timestamp);
  if (!Number.isFinite(epochMilliseconds)) return null;
  const value = valueText === null ? null : Number(valueText);
  if (value !== null && !Number.isFinite(value)) return null;
  return Object.freeze({ tagId, tagPath: tagPath ?? '', timestamp, epochMilliseconds, value, quality: quality ?? (value === null ? 'Gap' : 'Unknown') });
}

function cellText(row: HistoricalQueryRow, field: string): string | null {
  return row.cells[field]?.value ?? null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
