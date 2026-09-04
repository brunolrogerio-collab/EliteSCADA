export const HISTORICAL_QUERY_VERSION = 1 as const;
export const HISTORICAL_QUERY_ROUTE = '/api/historical/query' as const;

export type HistoricalDatasetKey = 'historian.samples' | 'alarm.events' | 'operational.events';
export type HistoricalFieldType = 'guid' | 'string' | 'enum' | 'number' | 'boolean' | 'dateTime' | 'int64' | 'scalar';
export type HistoricalValueKind =
  | 'guid'
  | 'string'
  | 'enum'
  | 'int16'
  | 'int32'
  | 'int64'
  | 'float'
  | 'double'
  | 'number'
  | 'boolean'
  | 'dateTime'
  | 'null';
export type HistoricalFilterOperator = 'eq' | 'notEq' | 'in' | 'contains' | 'startsWith' | 'gt' | 'gte' | 'lt' | 'lte';
export type HistoricalSortDirection = 'ascending' | 'descending';

export type HistoricalQueryValue = Readonly<{
  kind: HistoricalValueKind;
  value: string | null;
}>;

export type HistoricalTimeRange =
  | Readonly<{ kind: 'relative'; durationSeconds: number; anchor: 'now'; fromUtc?: null; toUtc?: null }>
  | Readonly<{ kind: 'absolute'; fromUtc: string; toUtc: string; durationSeconds?: null; anchor?: null }>;

export type HistoricalFilter = Readonly<{
  field: string;
  operator: HistoricalFilterOperator;
  values: readonly HistoricalQueryValue[];
}>;

export type HistoricalSort = Readonly<{
  field: string;
  direction: HistoricalSortDirection;
}>;

export type HistoricalQueryRequest = Readonly<{
  datasetKey: HistoricalDatasetKey;
  timeRange: HistoricalTimeRange;
  version: typeof HISTORICAL_QUERY_VERSION;
  filters?: readonly HistoricalFilter[];
  search?: string | null;
  orderBy?: readonly HistoricalSort[];
  page?: Readonly<{ limit: number; cursor?: string | null }>;
}>;

export type HistoricalColumn = Readonly<{
  field: string;
  type: HistoricalFieldType;
  operators: readonly HistoricalFilterOperator[];
  filterable: boolean;
  sortable: boolean;
  searchable: boolean;
}>;

export type HistoricalQueryRow = Readonly<{
  cells: Readonly<Record<string, HistoricalQueryValue>>;
}>;

export type HistoricalQueryResponse = Readonly<{
  version: number;
  datasetKey: HistoricalDatasetKey;
  columns: readonly HistoricalColumn[];
  rows: readonly HistoricalQueryRow[];
  fromUtc: string;
  toUtc: string;
  nextCursor: string | null;
  pageSize: number;
}>;

export type HistoricalQueryApiIssue = 'unauthenticated' | 'forbidden' | 'invalid-query' | 'unavailable';

export class HistoricalQueryApiError extends Error {
  readonly issue: HistoricalQueryApiIssue;
  readonly status: number | null;

  constructor(issue: HistoricalQueryApiIssue, message: string, status: number | null = null) {
    super(message);
    this.name = 'HistoricalQueryApiError';
    this.issue = issue;
    this.status = status;
  }
}

export async function executeHistoricalQuery(
  request: HistoricalQueryRequest,
  signal?: AbortSignal
): Promise<HistoricalQueryResponse> {
  const response = await fetch(HISTORICAL_QUERY_ROUTE, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(request),
    signal
  });

  if (!response.ok) {
    const message = await readApiError(response);
    if (response.status === 401) throw new HistoricalQueryApiError('unauthenticated', message, 401);
    if (response.status === 403) throw new HistoricalQueryApiError('forbidden', message, 403);
    if (response.status === 400) throw new HistoricalQueryApiError('invalid-query', message, 400);
    throw new HistoricalQueryApiError('unavailable', message, response.status);
  }

  const payload: unknown = await response.json();
  return normalizeHistoricalQueryResponse(payload);
}

export function normalizeHistoricalQueryResponse(payload: unknown): HistoricalQueryResponse {
  if (!isRecord(payload)) throw new HistoricalQueryApiError('unavailable', 'Historical query response is malformed.');
  if (payload.version !== HISTORICAL_QUERY_VERSION) throw new HistoricalQueryApiError('unavailable', 'Historical query response version is unsupported.');
  if (payload.datasetKey !== 'historian.samples' && payload.datasetKey !== 'alarm.events' && payload.datasetKey !== 'operational.events') {
    throw new HistoricalQueryApiError('unavailable', 'Historical query response dataset is not allowlisted.');
  }
  if (!Array.isArray(payload.columns) || !Array.isArray(payload.rows)) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query response columns or rows are malformed.');
  }
  if (typeof payload.fromUtc !== 'string' || typeof payload.toUtc !== 'string') {
    throw new HistoricalQueryApiError('unavailable', 'Historical query response range is malformed.');
  }
  if (payload.nextCursor !== null && typeof payload.nextCursor !== 'string') {
    throw new HistoricalQueryApiError('unavailable', 'Historical query response cursor is malformed.');
  }
  if (!Number.isSafeInteger(payload.pageSize) || Number(payload.pageSize) < 0) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query response page size is malformed.');
  }

  const columns = payload.columns.map(normalizeColumn);
  const rows = payload.rows.map(normalizeRow);
  return Object.freeze({
    version: HISTORICAL_QUERY_VERSION,
    datasetKey: payload.datasetKey,
    columns: Object.freeze(columns),
    rows: Object.freeze(rows),
    fromUtc: payload.fromUtc,
    toUtc: payload.toUtc,
    nextCursor: payload.nextCursor,
    pageSize: Number(payload.pageSize)
  });
}

function normalizeColumn(value: unknown): HistoricalColumn {
  if (!isRecord(value) || typeof value.field !== 'string' || !isFieldType(value.type)) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query column is malformed.');
  }
  if (!Array.isArray(value.operators) || !value.operators.every(isFilterOperator)) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query column operators are malformed.');
  }
  if (typeof value.filterable !== 'boolean' || typeof value.sortable !== 'boolean' || typeof value.searchable !== 'boolean') {
    throw new HistoricalQueryApiError('unavailable', 'Historical query column capabilities are malformed.');
  }
  if (value.filterable !== (value.operators.length > 0)) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query column filter capability is inconsistent.');
  }
  const operators = Object.freeze([...value.operators] as HistoricalFilterOperator[]);
  return Object.freeze({
    field: value.field,
    type: value.type,
    operators,
    filterable: value.filterable,
    sortable: value.sortable,
    searchable: value.searchable
  });
}

function normalizeRow(value: unknown): HistoricalQueryRow {
  if (!isRecord(value) || !isRecord(value.cells)) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query row is malformed.');
  }
  const cells: Record<string, HistoricalQueryValue> = {};
  for (const [field, cell] of Object.entries(value.cells)) cells[field] = normalizeValue(cell);
  return Object.freeze({ cells: Object.freeze(cells) });
}

function normalizeValue(value: unknown): HistoricalQueryValue {
  if (!isRecord(value) || !isValueKind(value.kind) || (value.value !== null && typeof value.value !== 'string')) {
    throw new HistoricalQueryApiError('unavailable', 'Historical query cell value is malformed.');
  }
  return Object.freeze({ kind: value.kind, value: value.value });
}

async function readApiError(response: Response): Promise<string> {
  try {
    const payload: unknown = await response.json();
    if (isRecord(payload) && typeof payload.error === 'string' && payload.error.trim()) return payload.error;
  } catch {
    // Preserve the status-based classification when an error body is absent or non-JSON.
  }
  return `Historical query failed with HTTP ${response.status}.`;
}

function isFieldType(value: unknown): value is HistoricalFieldType {
  return value === 'guid' || value === 'string' || value === 'enum' || value === 'number' || value === 'boolean' || value === 'dateTime' || value === 'int64' || value === 'scalar';
}

export function isHistoricalFilterOperator(value: unknown): value is HistoricalFilterOperator {
  return isFilterOperator(value);
}

function isFilterOperator(value: unknown): value is HistoricalFilterOperator {
  return value === 'eq' || value === 'notEq' || value === 'in' || value === 'contains' || value === 'startsWith' || value === 'gt' || value === 'gte' || value === 'lt' || value === 'lte';
}

function isValueKind(value: unknown): value is HistoricalValueKind {
  return value === 'guid' || value === 'string' || value === 'enum' || value === 'int16' || value === 'int32' || value === 'int64' || value === 'float' || value === 'double' || value === 'number' || value === 'boolean' || value === 'dateTime' || value === 'null';
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
