import {
  HISTORICAL_QUERY_VERSION,
  type HistoricalColumn,
  type HistoricalFilter,
  type HistoricalQueryRequest,
  type HistoricalQueryResponse,
  type HistoricalQueryValue,
  type HistoricalSort
} from './historicalQueryApi';
import {
  historicalBrowserCopy,
  type HistoricalBrowserLocale
} from './historicalBrowserI18n';
import {
  validateHistoricalBrowserDraft,
  type HistoricalBrowserDraft
} from './historicalBrowserPresentation';

export const HISTORICAL_BROWSER_PAGE_LIMIT = 100;
export const HISTORICAL_BROWSER_SEARCH_LIMIT = 200;

export type HistoricalBrowserProjectedColumn = Readonly<{
  key: string;
  label: string;
  filterable: boolean;
  sortable: boolean;
  searchable: boolean;
}>;

export type HistoricalBrowserProjectedRow = Readonly<{
  id: string;
  cells: Readonly<Record<string, string>>;
  detail: readonly Readonly<{ label: string; value: string }>[];
}>;

export type HistoricalBrowserProjectedPage = Readonly<{
  columns: readonly HistoricalBrowserProjectedColumn[];
  rows: readonly HistoricalBrowserProjectedRow[];
  fromUtc: string;
  toUtc: string;
  nextCursor: string | null;
  pageSize: number;
}>;

export function buildHistoricalQueryRequest(
  draft: HistoricalBrowserDraft,
  options: Readonly<{
    search?: string;
    sort?: HistoricalSort | null;
    cursor?: string | null;
    filters?: readonly HistoricalFilter[];
  }> = {}
): HistoricalQueryRequest {
  const validation = validateHistoricalBrowserDraft(draft);
  if (!validation.ok) throw new Error(validation.diagnostics.join(' '));

  const search = (options.search ?? '').trim();
  if (search.length > HISTORICAL_BROWSER_SEARCH_LIMIT) {
    throw new Error(`Historical search cannot exceed ${HISTORICAL_BROWSER_SEARCH_LIMIT} characters.`);
  }

  const timeRange = draft.timeMode === 'relative'
    ? Object.freeze({
        kind: 'relative' as const,
        durationSeconds: draft.relativeDurationSeconds,
        anchor: 'now' as const
      })
    : Object.freeze({
        kind: 'absolute' as const,
        fromUtc: localDateTimeToUtc(draft.absoluteFromLocal),
        toUtc: localDateTimeToUtc(draft.absoluteToLocal)
      });

  return Object.freeze({
    version: HISTORICAL_QUERY_VERSION,
    datasetKey: draft.datasetKey,
    timeRange,
    filters: options.filters?.length ? Object.freeze([...options.filters]) : undefined,
    search: search || undefined,
    orderBy: options.sort ? Object.freeze([options.sort]) : undefined,
    page: Object.freeze({ limit: HISTORICAL_BROWSER_PAGE_LIMIT, cursor: options.cursor ?? undefined })
  });
}

export function projectHistoricalQueryResponse(
  response: HistoricalQueryResponse,
  locale: HistoricalBrowserLocale = 'en'
): HistoricalBrowserProjectedPage {
  const columns = response.columns.map(column => Object.freeze({
    key: column.field,
    label: historicalFieldLabel(column),
    filterable: column.filterable,
    sortable: column.sortable,
    searchable: column.searchable
  }));

  const rows = response.rows.map((row, rowIndex) => {
    const cells: Record<string, string> = {};
    const detail: Array<Readonly<{ label: string; value: string }>> = [];
    for (const column of response.columns) {
      const formatted = formatHistoricalQueryValue(row.cells[column.field], locale);
      cells[column.field] = formatted;
      detail.push(Object.freeze({ label: historicalFieldLabel(column), value: formatted }));
    }
    return Object.freeze({
      id: historicalRowIdentity(row.cells, rowIndex),
      cells: Object.freeze(cells),
      detail: Object.freeze(detail)
    });
  });

  return Object.freeze({
    columns: Object.freeze(columns),
    rows: Object.freeze(rows),
    fromUtc: response.fromUtc,
    toUtc: response.toUtc,
    nextCursor: response.nextCursor,
    pageSize: response.pageSize
  });
}

export function formatHistoricalQueryValue(
  value: HistoricalQueryValue | undefined,
  locale: HistoricalBrowserLocale = 'en'
): string {
  if (!value || value.kind === 'null' || value.value === null) return '—';
  const text = historicalBrowserCopy(locale);

  switch (value.kind) {
    case 'int64':
      return /^-?\d+$/.test(value.value) ? value.value : text.unavailable;
    case 'int16':
    case 'int32':
      return /^-?\d+$/.test(value.value) ? value.value : text.unavailable;
    case 'float':
    case 'double':
    case 'number': {
      const parsed = Number(value.value);
      return Number.isFinite(parsed) ? value.value : text.unavailable;
    }
    case 'boolean':
      if (value.value === 'true') return text.trueLabel;
      if (value.value === 'false') return text.falseLabel;
      return text.unavailable;
    case 'dateTime':
      return value.value.trim() ? value.value : text.unavailable;
    case 'guid':
    case 'string':
    case 'enum':
      return value.value;
  }
}

export function canSearchHistoricalColumns(columns: readonly HistoricalColumn[]): boolean {
  return columns.some(column => column.searchable);
}

export function sortableHistoricalColumns(columns: readonly HistoricalColumn[]): readonly HistoricalColumn[] {
  return columns.filter(column => column.sortable);
}

function historicalFieldLabel(column: Pick<HistoricalColumn, 'field'>): string {
  return column.field
    .split('.')
    .map(part => part.replace(/([a-z0-9])([A-Z])/g, '$1 $2'))
    .join(' / ')
    .replace(/^./, value => value.toUpperCase());
}

function historicalRowIdentity(cells: Readonly<Record<string, HistoricalQueryValue>>, index: number): string {
  const identity = cells['event.id']?.value ?? cells['alarm.id']?.value ?? cells['tag.id']?.value ?? 'row';
  const timestamp = cells.timestamp?.value ?? 'no-time';
  return `${identity}:${timestamp}:${index}`;
}

function localDateTimeToUtc(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) throw new Error('Absolute period contains an invalid local date/time.');
  return parsed.toISOString();
}
