import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  RuntimeTagInspectorApiError,
  connectRuntimeTagRealtime,
  loadRecentTagHistory,
  loadRuntimeTagDetail,
  loadRuntimeTags,
  type RuntimeTagRealtimeDisposer,
  type RuntimeTagRealtimeState
} from './tagInspectorApi';
import {
  applyRuntimeTagRealtimeEvent,
  buildRuntimeTagInspectorSummary,
  classifyRuntimeTagEndpointIssue,
  filterRuntimeTags,
  normalizeRuntimeTagQuality,
  runtimeTagQualityBucket
} from './tagInspectorModel';
import type {
  RuntimeTagAccessFilter,
  RuntimeTagDetailResponse,
  RuntimeTagEndpointIssue,
  RuntimeTagHistorySample,
  RuntimeTagInspectorLocale,
  RuntimeTagListItem,
  RuntimeTagQualityFilter,
  RuntimeTagRealtimeEvent
} from './tagInspectorTypes';
import './runtime-tag-inspector.css';

export type RuntimeTagListLoader = (signal?: AbortSignal) => Promise<RuntimeTagListItem[]>;
export type RuntimeTagDetailLoader = (path: string, signal?: AbortSignal) => Promise<RuntimeTagDetailResponse>;
export type RuntimeTagHistoryLoader = (tagId: string, minutes: number, limit: number, signal?: AbortSignal) => Promise<RuntimeTagHistorySample[]>;
export type RuntimeTagRealtimeConnector = (
  onEvent: (event: RuntimeTagRealtimeEvent) => void,
  onState?: (state: RuntimeTagRealtimeState) => void
) => RuntimeTagRealtimeDisposer;

export type RuntimeTagInspectorProps = {
  locale?: RuntimeTagInspectorLocale;
  refreshIntervalMs?: number;
  historyMinutes?: number;
  listLoader?: RuntimeTagListLoader;
  detailLoader?: RuntimeTagDetailLoader;
  historyLoader?: RuntimeTagHistoryLoader;
  realtimeConnector?: RuntimeTagRealtimeConnector;
};

type Copy = {
  title: string;
  description: string;
  search: string;
  searchPlaceholder: string;
  qualityFilter: string;
  accessFilter: string;
  all: string;
  good: string;
  attention: string;
  bad: string;
  noSample: string;
  readOnly: string;
  writable: string;
  refresh: string;
  refreshing: string;
  loading: string;
  empty: string;
  noMatches: string;
  total: string;
  live: string;
  connecting: string;
  polling: string;
  realtimeError: string;
  currentValue: string;
  quality: string;
  timestamp: string;
  sourceTimestamp: string;
  serverTimestamp: string;
  dataType: string;
  unit: string;
  source: string;
  descriptionLabel: string;
  access: string;
  path: string;
  identity: string;
  recentHistory: string;
  historyWindow: string;
  historyEmpty: string;
  historyLoading: string;
  historyRefresh: string;
  value: string;
  unauthenticated: string;
  forbidden: string;
  notFound: string;
  unavailable: string;
  selectedUnavailable: string;
  unknown: string;
};

const copy: Record<RuntimeTagInspectorLocale, Copy> = {
  'pt-BR': {
    title: 'Inspector de TAGs',
    description: 'Consulta operacional somente leitura de TAGs, qualidade, timestamps e histórico recente do Runtime ativo.',
    search: 'Buscar TAGs', searchPlaceholder: 'Path, nome, tipo, unidade, origem ou valor', qualityFilter: 'Qualidade', accessFilter: 'Acesso',
    all: 'Todos', good: 'Good', attention: 'Atenção', bad: 'Bad', noSample: 'Sem amostra', readOnly: 'Somente leitura', writable: 'Gravável',
    refresh: 'Atualizar', refreshing: 'Atualizando…', loading: 'Carregando TAGs do Runtime…', empty: 'Nenhuma TAG visível no Runtime ativo.', noMatches: 'Nenhuma TAG corresponde aos filtros.', total: 'TAGs',
    live: 'Realtime conectado', connecting: 'Conectando realtime…', polling: 'Realtime desconectado · atualização periódica ativa', realtimeError: 'Realtime indisponível · atualização periódica ativa',
    currentValue: 'Valor atual', quality: 'Qualidade', timestamp: 'Timestamp EliteSCADA', sourceTimestamp: 'Timestamp da origem', serverTimestamp: 'Timestamp do servidor', dataType: 'Tipo', unit: 'Unidade', source: 'Origem / Data Source', descriptionLabel: 'Descrição', access: 'Acesso', path: 'Path', identity: 'ID estável',
    recentHistory: 'Histórico recente', historyWindow: 'janela', historyEmpty: 'Nenhuma amostra histórica neste intervalo.', historyLoading: 'Carregando histórico…', historyRefresh: 'Atualizar histórico', value: 'Valor',
    unauthenticated: 'Sessão não autenticada para consultar TAGs.', forbidden: 'Sem permissão para consultar este recurso do Runtime.', notFound: 'A TAG selecionada não existe mais no Runtime ativo.', unavailable: 'Serviço de TAGs indisponível no momento.', selectedUnavailable: 'Não foi possível carregar os detalhes desta TAG.', unknown: 'Desconhecido'
  },
  en: {
    title: 'TAG Inspector',
    description: 'Read-only operational view of active Runtime TAGs, quality, timestamps and recent history.',
    search: 'Search TAGs', searchPlaceholder: 'Path, name, type, unit, source or value', qualityFilter: 'Quality', accessFilter: 'Access',
    all: 'All', good: 'Good', attention: 'Attention', bad: 'Bad', noSample: 'No sample', readOnly: 'Read-only', writable: 'Writable',
    refresh: 'Refresh', refreshing: 'Refreshing…', loading: 'Loading Runtime TAGs…', empty: 'No TAG is visible in the active Runtime.', noMatches: 'No TAG matches the filters.', total: 'TAGs',
    live: 'Realtime connected', connecting: 'Connecting realtime…', polling: 'Realtime disconnected · periodic refresh active', realtimeError: 'Realtime unavailable · periodic refresh active',
    currentValue: 'Current value', quality: 'Quality', timestamp: 'EliteSCADA timestamp', sourceTimestamp: 'Source timestamp', serverTimestamp: 'Server timestamp', dataType: 'Type', unit: 'Unit', source: 'Source / Data Source', descriptionLabel: 'Description', access: 'Access', path: 'Path', identity: 'Stable ID',
    recentHistory: 'Recent history', historyWindow: 'window', historyEmpty: 'No historical sample in this interval.', historyLoading: 'Loading history…', historyRefresh: 'Refresh history', value: 'Value',
    unauthenticated: 'The session is not authenticated to read TAGs.', forbidden: 'Not authorized to read this Runtime resource.', notFound: 'The selected TAG no longer exists in the active Runtime.', unavailable: 'TAG service is currently unavailable.', selectedUnavailable: 'The selected TAG details could not be loaded.', unknown: 'Unknown'
  },
  es: {
    title: 'Inspector de TAGs',
    description: 'Vista operacional de solo lectura de TAGs, calidad, marcas de tiempo e histórico reciente del Runtime activo.',
    search: 'Buscar TAGs', searchPlaceholder: 'Path, nombre, tipo, unidad, origen o valor', qualityFilter: 'Calidad', accessFilter: 'Acceso',
    all: 'Todos', good: 'Good', attention: 'Atención', bad: 'Bad', noSample: 'Sin muestra', readOnly: 'Solo lectura', writable: 'Escribible',
    refresh: 'Actualizar', refreshing: 'Actualizando…', loading: 'Cargando TAGs del Runtime…', empty: 'No hay TAGs visibles en el Runtime activo.', noMatches: 'Ninguna TAG coincide con los filtros.', total: 'TAGs',
    live: 'Realtime conectado', connecting: 'Conectando realtime…', polling: 'Realtime desconectado · actualización periódica activa', realtimeError: 'Realtime no disponible · actualización periódica activa',
    currentValue: 'Valor actual', quality: 'Calidad', timestamp: 'Timestamp EliteSCADA', sourceTimestamp: 'Timestamp de origen', serverTimestamp: 'Timestamp del servidor', dataType: 'Tipo', unit: 'Unidad', source: 'Origen / Data Source', descriptionLabel: 'Descripción', access: 'Acceso', path: 'Path', identity: 'ID estable',
    recentHistory: 'Histórico reciente', historyWindow: 'ventana', historyEmpty: 'No hay muestras históricas en este intervalo.', historyLoading: 'Cargando histórico…', historyRefresh: 'Actualizar histórico', value: 'Valor',
    unauthenticated: 'La sesión no está autenticada para consultar TAGs.', forbidden: 'Sin permiso para consultar este recurso del Runtime.', notFound: 'La TAG seleccionada ya no existe en el Runtime activo.', unavailable: 'El servicio de TAGs no está disponible.', selectedUnavailable: 'No fue posible cargar los detalles de esta TAG.', unknown: 'Desconocido'
  }
};

function apiIssue(error: unknown): RuntimeTagEndpointIssue {
  if (error instanceof RuntimeTagInspectorApiError) return error.issue;
  return classifyRuntimeTagEndpointIssue();
}

function issueText(issue: RuntimeTagEndpointIssue, text: Copy) {
  if (issue === 'unauthenticated') return text.unauthenticated;
  if (issue === 'forbidden') return text.forbidden;
  if (issue === 'not-found') return text.notFound;
  return text.unavailable;
}

function formatValue(value: unknown, locale: RuntimeTagInspectorLocale) {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'number') return new Intl.NumberFormat(locale, { maximumFractionDigits: 6 }).format(value);
  if (typeof value === 'boolean') return value ? 'TRUE' : 'FALSE';
  if (typeof value === 'string') return value;
  try { return JSON.stringify(value); } catch { return String(value); }
}

function formatMoment(value: string | null | undefined, locale: RuntimeTagInspectorLocale) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}

function qualityLabel(value: string | number | null | undefined) {
  const normalized = normalizeRuntimeTagQuality(value);
  const labels: Record<typeof normalized, string> = {
    good: 'Good', uncertain: 'Uncertain', bad: 'Bad', 'bad-communication': 'BadCommunication',
    'bad-configuration': 'BadConfiguration', 'bad-device': 'BadDevice', stale: 'Stale', disabled: 'Disabled', unknown: 'Unknown'
  };
  return labels[normalized];
}

export function RuntimeTagInspector({
  locale = 'pt-BR',
  refreshIntervalMs = 10_000,
  historyMinutes = 15,
  listLoader = loadRuntimeTags,
  detailLoader = loadRuntimeTagDetail,
  historyLoader = loadRecentTagHistory,
  realtimeConnector = connectRuntimeTagRealtime
}: RuntimeTagInspectorProps) {
  const text = copy[locale];
  const [tags, setTags] = useState<RuntimeTagListItem[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [qualityFilter, setQualityFilter] = useState<RuntimeTagQualityFilter>('all');
  const [accessFilter, setAccessFilter] = useState<RuntimeTagAccessFilter>('all');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [listIssue, setListIssue] = useState<RuntimeTagEndpointIssue | null>(null);
  const [detail, setDetail] = useState<RuntimeTagDetailResponse | null>(null);
  const [detailIssue, setDetailIssue] = useState<RuntimeTagEndpointIssue | null>(null);
  const [history, setHistory] = useState<RuntimeTagHistorySample[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyIssue, setHistoryIssue] = useState<RuntimeTagEndpointIssue | null>(null);
  const [realtimeState, setRealtimeState] = useState<RuntimeTagRealtimeState>('connecting');
  const listAbort = useRef<AbortController | null>(null);
  const selectionAbort = useRef<AbortController | null>(null);

  const refreshTags = useCallback(async () => {
    listAbort.current?.abort();
    const controller = new AbortController();
    listAbort.current = controller;
    setRefreshing(true);
    try {
      const next = await listLoader(controller.signal);
      if (controller.signal.aborted) return;
      setTags(next);
      setListIssue(null);
      setSelectedId(current => current && next.some(tag => tag.id === current) ? current : next[0]?.id ?? null);
    } catch (error) {
      if (controller.signal.aborted) return;
      setListIssue(apiIssue(error));
    } finally {
      if (listAbort.current === controller) {
        listAbort.current = null;
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [listLoader]);

  useEffect(() => {
    void refreshTags();
    const timer = refreshIntervalMs > 0 ? window.setInterval(() => void refreshTags(), refreshIntervalMs) : undefined;
    return () => {
      if (timer !== undefined) window.clearInterval(timer);
      listAbort.current?.abort();
    };
  }, [refreshIntervalMs, refreshTags]);

  useEffect(() => realtimeConnector(
    event => setTags(current => applyRuntimeTagRealtimeEvent(current, event)),
    setRealtimeState
  ), [realtimeConnector]);

  const selectedTag = useMemo(() => tags.find(tag => tag.id === selectedId) ?? null, [selectedId, tags]);

  const loadSelection = useCallback(async () => {
    if (!selectedTag) {
      setDetail(null);
      setHistory([]);
      return;
    }

    selectionAbort.current?.abort();
    const controller = new AbortController();
    selectionAbort.current = controller;
    setHistoryLoading(true);
    setDetailIssue(null);
    setHistoryIssue(null);

    const [detailResult, historyResult] = await Promise.allSettled([
      detailLoader(selectedTag.path, controller.signal),
      historyLoader(selectedTag.id, historyMinutes, 120, controller.signal)
    ]);

    if (controller.signal.aborted) return;
    if (detailResult.status === 'fulfilled') setDetail(detailResult.value);
    else {
      setDetail(null);
      setDetailIssue(apiIssue(detailResult.reason));
    }

    if (historyResult.status === 'fulfilled') setHistory(historyResult.value);
    else {
      setHistory([]);
      setHistoryIssue(apiIssue(historyResult.reason));
    }

    if (selectionAbort.current === controller) {
      selectionAbort.current = null;
      setHistoryLoading(false);
    }
  }, [detailLoader, historyLoader, historyMinutes, selectedTag?.id, selectedTag?.path]);

  useEffect(() => {
    void loadSelection();
    return () => selectionAbort.current?.abort();
  }, [loadSelection]);

  const filtered = useMemo(() => filterRuntimeTags(tags, { query, quality: qualityFilter, access: accessFilter }), [accessFilter, qualityFilter, query, tags]);
  const summary = useMemo(() => buildRuntimeTagInspectorSummary(tags), [tags]);
  const orderedHistory = useMemo(
    () => [...history].sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp)),
    [history]
  );

  if (loading && tags.length === 0) return <section className="runtime-tag-inspector runtime-tag-state">{text.loading}</section>;

  if (listIssue && tags.length === 0) {
    return (
      <section className="runtime-tag-inspector runtime-tag-state runtime-tag-state-error" aria-label={text.title}>
        <strong>{issueText(listIssue, text)}</strong>
        <button type="button" onClick={() => void refreshTags()}>{text.refresh}</button>
      </section>
    );
  }

  return (
    <section className="runtime-tag-inspector" aria-label={text.title} aria-busy={refreshing}>
      <header className="runtime-tag-header">
        <div>
          <span className="runtime-tag-eyebrow">Runtime / TAGs</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <div className="runtime-tag-header-actions">
          <span className={`runtime-tag-live state-${realtimeState}`} aria-live="polite">
            {realtimeState === 'live' ? text.live : realtimeState === 'connecting' ? text.connecting : realtimeState === 'error' ? text.realtimeError : text.polling}
          </span>
          <button type="button" disabled={refreshing} onClick={() => void refreshTags()}>{refreshing ? text.refreshing : text.refresh}</button>
        </div>
      </header>

      <div className="runtime-tag-summary" aria-label={`${summary.total} ${text.total}`}>
        <Summary label={text.total} value={summary.total} />
        <Summary label={text.good} value={summary.good} tone="good" />
        <Summary label={text.attention} value={summary.attention} tone="attention" />
        <Summary label={text.bad} value={summary.bad} tone="bad" />
        <Summary label={text.noSample} value={summary.noSample} />
      </div>

      <div className="runtime-tag-controls">
        <label className="runtime-tag-search">
          <span>{text.search}</span>
          <input value={query} onChange={event => setQuery(event.target.value)} placeholder={text.searchPlaceholder} />
        </label>
        <label>
          <span>{text.qualityFilter}</span>
          <select value={qualityFilter} onChange={event => setQualityFilter(event.target.value as RuntimeTagQualityFilter)}>
            <option value="all">{text.all}</option><option value="good">{text.good}</option><option value="attention">{text.attention}</option><option value="bad">{text.bad}</option><option value="no-sample">{text.noSample}</option>
          </select>
        </label>
        <label>
          <span>{text.accessFilter}</span>
          <select value={accessFilter} onChange={event => setAccessFilter(event.target.value as RuntimeTagAccessFilter)}>
            <option value="all">{text.all}</option><option value="read-only">{text.readOnly}</option><option value="writable">{text.writable}</option>
          </select>
        </label>
      </div>

      {listIssue && <div className="runtime-tag-inline-warning">{issueText(listIssue, text)}</div>}

      <div className="runtime-tag-workspace">
        <div className="runtime-tag-list" role="listbox" aria-label={text.title}>
          {tags.length === 0 && <div className="runtime-tag-empty">{text.empty}</div>}
          {tags.length > 0 && filtered.length === 0 && <div className="runtime-tag-empty">{text.noMatches}</div>}
          {filtered.map(tag => {
            const bucket = runtimeTagQualityBucket(tag);
            return (
              <button
                type="button"
                role="option"
                aria-selected={tag.id === selectedId}
                className={`runtime-tag-row quality-${bucket}${tag.id === selectedId ? ' selected' : ''}`}
                key={tag.id}
                onClick={() => setSelectedId(tag.id)}
              >
                <span className="runtime-tag-quality-marker" aria-hidden="true" />
                <span className="runtime-tag-row-copy">
                  <strong>{tag.path}</strong>
                  <span>{tag.name} · {tag.dataType}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</span>
                </span>
                <span className="runtime-tag-row-value">
                  <strong>{formatValue(tag.current?.value, locale)}{tag.engineeringUnit ? ` ${tag.engineeringUnit}` : ''}</strong>
                  <small>{tag.current ? qualityLabel(tag.current.quality) : text.noSample}</small>
                  <time>{formatMoment(tag.current?.timestamp, locale)}</time>
                </span>
              </button>
            );
          })}
        </div>

        <div className="runtime-tag-detail">
          {!selectedTag && <div className="runtime-tag-empty">{text.empty}</div>}
          {selectedTag && (
            <>
              <header>
                <div><span>{selectedTag.name}</span><h3>{selectedTag.path}</h3></div>
                <span className={`runtime-tag-quality-badge quality-${runtimeTagQualityBucket(selectedTag)}`}>{selectedTag.current ? qualityLabel(selectedTag.current.quality) : text.noSample}</span>
              </header>

              {detailIssue && <div className="runtime-tag-inline-warning">{detailIssue === 'not-found' ? text.notFound : detailIssue === 'forbidden' ? text.forbidden : text.selectedUnavailable}</div>}

              <dl className="runtime-tag-facts">
                <Fact label={text.currentValue} value={`${formatValue(selectedTag.current?.value, locale)}${selectedTag.engineeringUnit ? ` ${selectedTag.engineeringUnit}` : ''}`} />
                <Fact label={text.quality} value={selectedTag.current ? qualityLabel(selectedTag.current.quality) : text.noSample} />
                <Fact label={text.dataType} value={selectedTag.dataType} />
                <Fact label={text.unit} value={selectedTag.engineeringUnit || '—'} />
                <Fact label={text.source} value={detail?.tag.source || selectedTag.current?.source || '—'} mono />
                <Fact label={text.access} value={selectedTag.readOnly ? text.readOnly : text.writable} />
                <Fact label={text.timestamp} value={formatMoment(selectedTag.current?.timestamp, locale)} />
                <Fact label={text.sourceTimestamp} value={formatMoment(selectedTag.current?.sourceTimestamp, locale)} />
                <Fact label={text.serverTimestamp} value={formatMoment(selectedTag.current?.serverTimestamp, locale)} />
                <Fact label={text.descriptionLabel} value={selectedTag.description || detail?.tag.description || '—'} />
                <Fact label={text.path} value={selectedTag.path} mono />
                <Fact label={text.identity} value={selectedTag.id} mono />
              </dl>

              <section className="runtime-tag-history">
                <header>
                  <div><h4>{text.recentHistory}</h4><span>{historyMinutes} min {text.historyWindow}</span></div>
                  <button type="button" disabled={historyLoading} onClick={() => void loadSelection()}>{text.historyRefresh}</button>
                </header>
                {historyLoading && orderedHistory.length === 0 && <div className="runtime-tag-history-state">{text.historyLoading}</div>}
                {historyIssue && <div className="runtime-tag-inline-warning">{issueText(historyIssue, text)}</div>}
                {!historyLoading && !historyIssue && orderedHistory.length === 0 && <div className="runtime-tag-history-state">{text.historyEmpty}</div>}
                {orderedHistory.length > 0 && (
                  <div className="runtime-tag-history-table-wrap">
                    <table className="runtime-tag-history-table">
                      <thead><tr><th>{text.timestamp}</th><th>{text.value}</th><th>{text.quality}</th><th>{text.source}</th></tr></thead>
                      <tbody>{orderedHistory.map((sample, index) => (
                        <tr key={`${sample.timestamp}-${index}`}>
                          <td>{formatMoment(sample.timestamp, locale)}</td>
                          <td>{formatValue(sample.value, locale)}{selectedTag.engineeringUnit ? ` ${selectedTag.engineeringUnit}` : ''}</td>
                          <td><span className={`history-quality quality-${normalizeRuntimeTagQuality(sample.quality)}`}>{qualityLabel(sample.quality)}</span></td>
                          <td>{sample.source || '—'}</td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </div>
                )}
              </section>
            </>
          )}
        </div>
      </div>
    </section>
  );
}

function Summary({ label, value, tone }: { label: string; value: number; tone?: string }) {
  return <div className={`runtime-tag-summary-item${tone ? ` tone-${tone}` : ''}`}><span>{label}</span><strong>{value}</strong></div>;
}

function Fact({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return <div><dt>{label}</dt><dd className={mono ? 'mono' : undefined}>{value}</dd></div>;
}
