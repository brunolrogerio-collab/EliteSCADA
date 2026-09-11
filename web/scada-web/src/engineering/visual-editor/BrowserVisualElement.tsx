import React, { useEffect, useMemo, useState, type CSSProperties, type MouseEventHandler } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualElementEngineering } from '../types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  readAlarmBrowserConfig,
  readEventBrowserConfig,
  type AlarmBrowserConfig,
  type EventBrowserConfig
} from '../../visual-runtime';
import {
  acknowledgeRuntimeAlarm,
  loadRuntimeAlarmDefinitions,
  loadRuntimeAlarms
} from '../../runtime/alarmCenterApi';
import type {
  RuntimeAlarmCenterItem,
  RuntimeAlarmDefinition
} from '../../runtime/alarmCenterTypes';
import {
  HISTORICAL_QUERY_VERSION,
  executeHistoricalQuery,
  type HistoricalFilter,
  type HistoricalQueryRequest,
  type HistoricalQueryResponse,
  type HistoricalQueryValue
} from '../../runtime/historical-browser/historicalQueryApi';
import './BrowserVisualElement.css';

type BrowserVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  style: CSSProperties;
  runtimeObjectId?: string;
  title?: string;
  locale: EngineeringLocale;
  enabled: boolean;
  onClick?: MouseEventHandler<HTMLElement>;
}>;

type DisplayRow = Readonly<{
  id: string;
  cells: Readonly<Record<string, string>>;
  alarmDefinitionId?: string;
  canAcknowledge?: boolean;
}>;

type LoadedPage = Readonly<{
  rows: readonly DisplayRow[];
  nextCursor: string | null;
}>;

type Copy = Readonly<{
  alarmTitle: string;
  eventTitle: string;
  loading: string;
  empty: string;
  error: string;
  denied: string;
  acknowledge: string;
  acknowledging: string;
  previous: string;
  next: string;
  page: string;
}>;

const COPY: Readonly<Record<EngineeringLocale, Copy>> = Object.freeze({
  'pt-BR': Object.freeze({ alarmTitle: 'Alarmes', eventTitle: 'Eventos operacionais', loading: 'Carregando…', empty: 'Nenhum dado para os filtros configurados.', error: 'Falha ao consultar o backend.', denied: 'Operação não autorizada pelo backend.', acknowledge: 'Reconhecer', acknowledging: 'Reconhecendo…', previous: 'Anterior', next: 'Próxima', page: 'Página' }),
  en: Object.freeze({ alarmTitle: 'Alarms', eventTitle: 'Operational events', loading: 'Loading…', empty: 'No data matches the configured filters.', error: 'Backend query failed.', denied: 'Operation denied by the backend.', acknowledge: 'Acknowledge', acknowledging: 'Acknowledging…', previous: 'Previous', next: 'Next', page: 'Page' }),
  es: Object.freeze({ alarmTitle: 'Alarmas', eventTitle: 'Eventos operacionales', loading: 'Cargando…', empty: 'No hay datos para los filtros configurados.', error: 'Falló la consulta al backend.', denied: 'Operación denegada por el backend.', acknowledge: 'Reconocer', acknowledging: 'Reconociendo…', previous: 'Anterior', next: 'Siguiente', page: 'Página' })
});

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export function BrowserVisualElement(props: BrowserVisualElementProps) {
  const { element, locale } = props;
  if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser) {
    return <AlarmBrowserVisual {...props} config={readAlarmBrowserConfig(element)} copy={COPY[locale]} />;
  }
  if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser) {
    return <EventBrowserVisual {...props} config={readEventBrowserConfig(element)} copy={COPY[locale]} />;
  }
  return null;
}

function AlarmBrowserVisual(props: BrowserVisualElementProps & { config: AlarmBrowserConfig; copy: Copy }) {
  const { config, copy } = props;
  const [cursor, setCursor] = useState<string | null>(null);
  const [cursorStack, setCursorStack] = useState<readonly (string | null)[]>(Object.freeze([]));
  const [pageIndex, setPageIndex] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const [acknowledging, setAcknowledging] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const queryKey = useMemo(() => JSON.stringify(config), [config]);

  useEffect(() => {
    setCursor(null);
    setCursorStack(Object.freeze([]));
    setPageIndex(0);
  }, [queryKey]);

  const state = useBrowserLoad(
    async signal => config.mode === 'history'
      ? loadAlarmHistory(config, cursor, signal)
      : loadCurrentAlarms(config, pageIndex, signal),
    [queryKey, cursor, pageIndex, refresh]
  );

  async function acknowledge(row: DisplayRow) {
    if (!row.alarmDefinitionId || acknowledging) return;
    setAcknowledging(row.alarmDefinitionId);
    setActionError(null);
    try {
      const result = await acknowledgeRuntimeAlarm(row.alarmDefinitionId);
      if (!result.ok) {
        setActionError(result.status === 401 || result.status === 403 ? copy.denied : `${copy.error} ${result.error}`);
        return;
      }
      setRefresh(value => value + 1);
    } finally {
      setAcknowledging(null);
    }
  }

  return <BrowserFrame
    {...props}
    titleLabel={copy.alarmTitle}
    state={state}
    columns={config.columns}
    pageNumber={config.mode === 'history' ? cursorStack.length + 1 : pageIndex + 1}
    canPrevious={config.mode === 'history' ? cursorStack.length > 0 : pageIndex > 0}
    canNext={Boolean(state.page?.nextCursor)}
    onPrevious={() => {
      if (config.mode === 'history') {
        const stack = [...cursorStack];
        const previous = stack.pop() ?? null;
        setCursorStack(Object.freeze(stack));
        setCursor(previous);
      } else setPageIndex(value => Math.max(0, value - 1));
    }}
    onNext={() => {
      if (!state.page?.nextCursor) return;
      if (config.mode === 'history') {
        setCursorStack(stack => Object.freeze([...stack, cursor]));
        setCursor(state.page.nextCursor);
      } else setPageIndex(value => value + 1);
    }}
    renderAction={config.mode === 'current' && config.acknowledgeEnabled
      ? row => row.canAcknowledge ? <button
          type="button"
          className="hmi-browser__ack"
          disabled={acknowledging !== null}
          onClick={event => { event.stopPropagation(); void acknowledge(row); }}
        >{acknowledging === row.alarmDefinitionId ? copy.acknowledging : copy.acknowledge}</button> : null
      : undefined}
    actionError={actionError}
  />;
}

function EventBrowserVisual(props: BrowserVisualElementProps & { config: EventBrowserConfig; copy: Copy }) {
  const { config, copy } = props;
  const [cursor, setCursor] = useState<string | null>(null);
  const [cursorStack, setCursorStack] = useState<readonly (string | null)[]>(Object.freeze([]));
  const queryKey = useMemo(() => JSON.stringify(config), [config]);
  useEffect(() => {
    setCursor(null);
    setCursorStack(Object.freeze([]));
  }, [queryKey]);
  const state = useBrowserLoad(signal => loadEventHistory(config, cursor, signal), [queryKey, cursor]);

  return <BrowserFrame
    {...props}
    titleLabel={copy.eventTitle}
    state={state}
    columns={config.columns}
    pageNumber={cursorStack.length + 1}
    canPrevious={cursorStack.length > 0}
    canNext={Boolean(state.page?.nextCursor)}
    onPrevious={() => {
      const stack = [...cursorStack];
      const previous = stack.pop() ?? null;
      setCursorStack(Object.freeze(stack));
      setCursor(previous);
    }}
    onNext={() => {
      if (!state.page?.nextCursor) return;
      setCursorStack(stack => Object.freeze([...stack, cursor]));
      setCursor(state.page.nextCursor);
    }}
  />;
}

function BrowserFrame({
  style,
  runtimeObjectId,
  title,
  enabled,
  onClick,
  copy,
  titleLabel,
  state,
  columns,
  pageNumber,
  canPrevious,
  canNext,
  onPrevious,
  onNext,
  renderAction,
  actionError
}: BrowserVisualElementProps & Readonly<{
  copy: Copy;
  titleLabel: string;
  state: BrowserLoadState;
  columns: readonly string[];
  pageNumber: number;
  canPrevious: boolean;
  canNext: boolean;
  onPrevious: () => void;
  onNext: () => void;
  renderAction?: (row: DisplayRow) => React.ReactNode;
  actionError?: string | null;
}>) {
  return <section
    className="visual-editor-object hmi-browser"
    style={style}
    data-runtime-object-id={runtimeObjectId}
    data-enabled={enabled}
    data-browser-state={state.loading ? 'loading' : state.error ? 'error' : state.page?.rows.length ? 'ready' : 'empty'}
    title={title}
    onClick={onClick}
  >
    <header className="hmi-browser__header"><strong>{titleLabel}</strong><span>{copy.page} {pageNumber}</span></header>
    <div className="hmi-browser__body">
      {state.loading ? <div className="hmi-browser__message" role="status">{copy.loading}</div> : null}
      {!state.loading && state.error ? <div className="hmi-browser__message hmi-browser__message--error" role="alert">{copy.error} {state.error}</div> : null}
      {!state.loading && !state.error && state.page && state.page.rows.length === 0 ? <div className="hmi-browser__message">{copy.empty}</div> : null}
      {!state.loading && !state.error && state.page && state.page.rows.length > 0 ? <div className="hmi-browser__table-wrap"><table><thead><tr>{columns.map(column => <th key={column}>{columnLabel(column, localeFromCopy(copy))}</th>)}{renderAction ? <th /> : null}</tr></thead><tbody>{state.page.rows.map(row => <tr key={row.id}>{columns.map(column => <td key={column}>{row.cells[column] ?? '—'}</td>)}{renderAction ? <td>{renderAction(row)}</td> : null}</tr>)}</tbody></table></div> : null}
    </div>
    {actionError ? <div className="hmi-browser__action-error" role="alert">{actionError}</div> : null}
    <footer className="hmi-browser__footer"><button type="button" disabled={!canPrevious || state.loading} onClick={event => { event.stopPropagation(); onPrevious(); }}>{copy.previous}</button><button type="button" disabled={!canNext || state.loading} onClick={event => { event.stopPropagation(); onNext(); }}>{copy.next}</button></footer>
  </section>;
}

type BrowserLoadState = Readonly<{ loading: boolean; error: string | null; page: LoadedPage | null }>;

function useBrowserLoad(loader: (signal: AbortSignal) => Promise<LoadedPage>, dependencies: readonly unknown[]): BrowserLoadState {
  const [state, setState] = useState<BrowserLoadState>({ loading: true, error: null, page: null });
  useEffect(() => {
    const controller = new AbortController();
    setState(previous => ({ ...previous, loading: true, error: null }));
    void loader(controller.signal)
      .then(page => { if (!controller.signal.aborted) setState({ loading: false, error: null, page }); })
      .catch(reason => { if (!controller.signal.aborted) setState({ loading: false, error: reason instanceof Error ? reason.message : String(reason), page: null }); });
    return () => controller.abort();
    // Loader is intentionally reconstructed from canonical object configuration.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, dependencies);
  return state;
}

async function loadCurrentAlarms(config: AlarmBrowserConfig, pageIndex: number, signal: AbortSignal): Promise<LoadedPage> {
  const [alarmResult, tags] = await Promise.all([
    loadRuntimeAlarms(config.lifecycle === 'active', signal),
    loadReadableTags(signal)
  ]);
  if (!alarmResult.available) throw new Error(alarmResult.error);
  const tagPaths = new Map(tags.map(tag => [tag.id, tag.path]));
  const filtered = alarmResult.value.filter(alarm => currentAlarmMatches(alarm, tagPaths.get(alarm.tagId) ?? '', config));
  const sorted = [...filtered].sort((left, right) => compareCurrentAlarm(left, right, config.sortField, config.sortDirection, tagPaths));
  const start = pageIndex * config.pageSize;
  const rows = sorted.slice(start, start + config.pageSize).map(alarm => currentAlarmRow(alarm, tagPaths.get(alarm.tagId) ?? ''));
  return Object.freeze({ rows: Object.freeze(rows), nextCursor: start + config.pageSize < sorted.length ? String(pageIndex + 1) : null });
}

async function loadAlarmHistory(config: AlarmBrowserConfig, cursor: string | null, signal: AbortSignal): Promise<LoadedPage> {
  const definitionsResult = await loadRuntimeAlarmDefinitions(signal);
  const definitions = definitionsResult.available ? definitionsResult.value : Object.freeze([] as RuntimeAlarmDefinition[]);
  let allowedAlarmIds: readonly string[] | undefined;

  if (config.area) {
    if (!definitionsResult.available) throw new Error(definitionsResult.error);
    const wanted = config.area.toLocaleLowerCase();
    allowedAlarmIds = Object.freeze(definitions
      .filter(definition => (definition.area ?? '').toLocaleLowerCase().includes(wanted))
      .map(definition => definition.id));
    if (allowedAlarmIds.length === 0) return Object.freeze({ rows: Object.freeze([]), nextCursor: null });
  }

  const response = await executeHistoricalQuery(
    buildAlarmHistoricalRequest(config, cursor, allowedAlarmIds),
    signal
  );
  return projectHistoricalRows(response, definitions);
}

async function loadEventHistory(config: EventBrowserConfig, cursor: string | null, signal: AbortSignal): Promise<LoadedPage> {
  const response = await executeHistoricalQuery(buildEventHistoricalRequest(config, cursor), signal);
  return projectHistoricalRows(response);
}

export function buildAlarmHistoricalRequest(
  config: AlarmBrowserConfig,
  cursor: string | null,
  allowedAlarmIds?: readonly string[]
): HistoricalQueryRequest {
  const filters: HistoricalFilter[] = [];
  const states = historicalAlarmStates(config);
  if (states?.length === 1) filters.push(enumFilter('state', states[0]));
  else if (states && states.length > 1) {
    filters.push(Object.freeze({
      field: 'state',
      operator: 'in',
      values: Object.freeze(states.map(enumValue))
    }));
  }
  if (allowedAlarmIds?.length) {
    filters.push(Object.freeze({
      field: 'alarm.id',
      operator: 'in',
      values: Object.freeze(allowedAlarmIds.map(guidValue))
    }));
  }
  if (config.minimumPriority !== null) filters.push({ field: 'priority', operator: 'gte', values: [numberValue(config.minimumPriority)] });
  if (config.tagPath) filters.push(stringFilter('tag.path', config.tagPath));
  return Object.freeze({
    version: HISTORICAL_QUERY_VERSION,
    datasetKey: 'alarm.events',
    timeRange: Object.freeze({ kind: 'relative', durationSeconds: config.lookbackSeconds, anchor: 'now' }),
    filters: filters.length ? Object.freeze(filters) : undefined,
    search: config.text || undefined,
    orderBy: Object.freeze([{ field: config.sortField, direction: config.sortDirection }]),
    page: Object.freeze({ limit: config.pageSize, cursor: cursor ?? undefined })
  });
}

/**
 * Alarm history persists the state transition, not a separate acknowledgement
 * flag for returned rows. Keep historical filtering honest: Active means an
 * unacknowledged active transition, Acknowledged means an acknowledged active
 * transition, and Returned is never guessed to be acknowledged/unacknowledged.
 */
export function historicalAlarmStates(config: AlarmBrowserConfig): readonly string[] | null {
  if (config.lifecycle === 'returned') return Object.freeze(['Returned']);
  if (config.lifecycle === 'active') {
    if (config.acknowledgement === 'acknowledged') return Object.freeze(['Acknowledged']);
    if (config.acknowledgement === 'unacknowledged') return Object.freeze(['Active']);
    return Object.freeze(['Active', 'Acknowledged']);
  }
  if (config.acknowledgement === 'acknowledged') return Object.freeze(['Acknowledged']);
  if (config.acknowledgement === 'unacknowledged') return Object.freeze(['Active']);
  return null;
}

export function buildEventHistoricalRequest(config: EventBrowserConfig, cursor: string | null): HistoricalQueryRequest {
  const filters: HistoricalFilter[] = [];
  const entries: readonly [string, string][] = [
    ['type', config.type], ['category', config.category], ['source', config.source], ['area', config.area],
    ['equipment.path', config.equipmentPath], ['tag.path', config.tagPath], ['operator', config.operator],
    ['operation', config.operation], ['command.key', config.commandKey]
  ];
  for (const [field, value] of entries) if (value) filters.push(stringFilter(field, value));
  return Object.freeze({
    version: HISTORICAL_QUERY_VERSION,
    datasetKey: 'operational.events',
    timeRange: Object.freeze({ kind: 'relative', durationSeconds: config.lookbackSeconds, anchor: 'now' }),
    filters: filters.length ? Object.freeze(filters) : undefined,
    search: config.text || undefined,
    orderBy: Object.freeze([{ field: config.sortField, direction: config.sortDirection }]),
    page: Object.freeze({ limit: config.pageSize, cursor: cursor ?? undefined })
  });
}

function projectHistoricalRows(
  response: HistoricalQueryResponse,
  alarmDefinitions: readonly RuntimeAlarmDefinition[] = []
): LoadedPage {
  const definitions = new Map(alarmDefinitions.map(definition => [definition.id.toLocaleLowerCase(), definition]));
  const rows = response.rows.map((row, index) => {
    const cells: Record<string, string> = {};
    for (const [field, value] of Object.entries(row.cells)) cells[field] = displayHistoricalValue(value);
    const alarmId = row.cells['alarm.id']?.value ?? null;
    if (alarmId) {
      const definition = definitions.get(alarmId.toLocaleLowerCase());
      if (definition) {
        cells.name = definition.name;
        cells.area = definition.area?.trim() || '—';
      }
    }
    const identity = row.cells['event.id']?.value ?? alarmId ?? row.cells['tag.id']?.value ?? `${index}`;
    return Object.freeze({ id: `${identity}:${row.cells.timestamp?.value ?? index}`, cells: Object.freeze(cells) });
  });
  return Object.freeze({ rows: Object.freeze(rows), nextCursor: response.nextCursor });
}

function displayHistoricalValue(value: HistoricalQueryValue | undefined): string {
  if (!value || value.kind === 'null' || value.value === null) return '—';
  return value.value;
}

async function loadReadableTags(signal: AbortSignal): Promise<readonly Readonly<{ id: string; path: string }>[]> {
  const response = await fetch(`${API}/api/tags`, { headers: { accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`.trim());
  const payload: unknown = await response.json();
  if (!Array.isArray(payload)) return Object.freeze([]);
  return Object.freeze(payload.flatMap(candidate => isRecord(candidate) && typeof candidate.id === 'string' && typeof candidate.path === 'string' ? [Object.freeze({ id: candidate.id, path: candidate.path })] : []));
}

function currentAlarmMatches(alarm: RuntimeAlarmCenterItem, tagPath: string, config: AlarmBrowserConfig): boolean {
  const state = normalizedAlarmState(alarm.state);
  if (config.lifecycle === 'active' && state !== 'active' && state !== 'acknowledged') return false;
  if (config.lifecycle === 'returned' && state !== 'returned') return false;
  const acked = state === 'acknowledged' || Boolean(alarm.acknowledgedAt);
  if (config.acknowledgement === 'acknowledged' && !acked) return false;
  if (config.acknowledgement === 'unacknowledged' && acked) return false;
  const priority = normalizedAlarmPriority(alarm.priority);
  if (config.minimumPriority !== null && priority < config.minimumPriority) return false;
  if (config.area && !(alarm.area ?? '').toLocaleLowerCase().includes(config.area.toLocaleLowerCase())) return false;
  if (config.tagPath && !tagPath.toLocaleLowerCase().includes(config.tagPath.toLocaleLowerCase())) return false;
  if (config.text) {
    const haystack = `${alarm.name} ${alarm.message ?? ''} ${tagPath}`.toLocaleLowerCase();
    if (!haystack.includes(config.text.toLocaleLowerCase())) return false;
  }
  return true;
}

function currentAlarmRow(alarm: RuntimeAlarmCenterItem, tagPath: string): DisplayRow {
  const cells = Object.freeze({
    timestamp: alarm.lastTransition,
    state: String(alarm.state),
    priority: String(alarm.priority),
    name: alarm.name,
    area: alarm.area ?? '—',
    'tag.path': tagPath || alarm.tagId,
    message: alarm.message ?? '—',
    acknowledgedBy: alarm.acknowledgedBy ?? '—'
  });
  const state = normalizedAlarmState(alarm.state);
  return Object.freeze({
    id: `${alarm.definitionId}:${alarm.lastTransition}`,
    cells,
    alarmDefinitionId: alarm.definitionId,
    canAcknowledge: state === 'active'
  });
}

function compareCurrentAlarm(left: RuntimeAlarmCenterItem, right: RuntimeAlarmCenterItem, field: AlarmBrowserConfig['sortField'], direction: 'ascending' | 'descending', tagPaths: ReadonlyMap<string, string>): number {
  const factor = direction === 'ascending' ? 1 : -1;
  let leftValue: string | number;
  let rightValue: string | number;
  if (field === 'priority') { leftValue = normalizedAlarmPriority(left.priority); rightValue = normalizedAlarmPriority(right.priority); }
  else if (field === 'state') { leftValue = String(left.state); rightValue = String(right.state); }
  else if (field === 'tag.path') { leftValue = tagPaths.get(left.tagId) ?? ''; rightValue = tagPaths.get(right.tagId) ?? ''; }
  else { leftValue = Date.parse(left.lastTransition) || 0; rightValue = Date.parse(right.lastTransition) || 0; }
  return (leftValue < rightValue ? -1 : leftValue > rightValue ? 1 : 0) * factor;
}

function normalizedAlarmState(value: string | number): string {
  if (typeof value === 'number') return ['normal', 'active', 'acknowledged', 'returned', 'disabled', 'shelved'][value] ?? String(value);
  return value.trim().toLowerCase();
}

function normalizedAlarmPriority(value: string | number): number {
  if (typeof value === 'number') return value;
  const numeric = Number(value);
  if (Number.isFinite(numeric)) return numeric;
  return ({ low: 1, medium: 2, high: 3, critical: 4 } as Record<string, number>)[value.trim().toLowerCase()] ?? 0;
}

function enumFilter(field: string, value: string): HistoricalFilter { return Object.freeze({ field, operator: 'eq', values: Object.freeze([enumValue(value)]) }); }
function stringFilter(field: string, value: string): HistoricalFilter { return Object.freeze({ field, operator: 'contains', values: Object.freeze([stringValue(value)]) }); }
function enumValue(value: string): HistoricalQueryValue { return Object.freeze({ kind: 'enum', value }); }
function stringValue(value: string): HistoricalQueryValue { return Object.freeze({ kind: 'string', value }); }
function guidValue(value: string): HistoricalQueryValue { return Object.freeze({ kind: 'guid', value }); }
function numberValue(value: number): HistoricalQueryValue { return Object.freeze({ kind: 'number', value: String(value) }); }

function columnLabel(column: string, locale: EngineeringLocale): string {
  const labels: Readonly<Record<EngineeringLocale, Readonly<Record<string, string>>>> = {
    'pt-BR': { timestamp: 'Tempo', state: 'Estado', priority: 'Severidade', name: 'Alarme', area: 'Área', 'tag.path': 'TAG', message: 'Mensagem', acknowledgedBy: 'Reconhecido por', type: 'Tipo', category: 'Categoria', source: 'Origem', 'equipment.path': 'Equipamento', operator: 'Operador', operation: 'Operação', 'command.key': 'Comando' },
    en: { timestamp: 'Time', state: 'State', priority: 'Severity', name: 'Alarm', area: 'Area', 'tag.path': 'TAG', message: 'Message', acknowledgedBy: 'Acknowledged by', type: 'Type', category: 'Category', source: 'Source', 'equipment.path': 'Equipment', operator: 'Operator', operation: 'Operation', 'command.key': 'Command' },
    es: { timestamp: 'Tiempo', state: 'Estado', priority: 'Severidad', name: 'Alarma', area: 'Área', 'tag.path': 'TAG', message: 'Mensaje', acknowledgedBy: 'Reconocido por', type: 'Tipo', category: 'Categoría', source: 'Origen', 'equipment.path': 'Equipo', operator: 'Operador', operation: 'Operación', 'command.key': 'Comando' }
  };
  return labels[locale][column] ?? column;
}

function localeFromCopy(copy: Copy): EngineeringLocale {
  if (copy === COPY.en) return 'en';
  if (copy === COPY.es) return 'es';
  return 'pt-BR';
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
