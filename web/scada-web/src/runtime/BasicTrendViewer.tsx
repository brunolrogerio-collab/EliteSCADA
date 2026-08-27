import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { BasicTrendApiError, loadTrendHistory, loadTrendTags } from './trendApi';
import {
  buildBasicTrendRange,
  buildTrendPlot,
  sortTrendSamples,
  summarizeTrendSamples,
  trendQualityTone
} from './trendModel';
import type {
  BasicTrendLocale,
  BasicTrendMode,
  BasicTrendWindow,
  RuntimeTagEndpointIssue,
  RuntimeTagHistorySample,
  RuntimeTagListItem
} from './trendTypes';
import './basic-trend-viewer.css';

export type BasicTrendTagLoader = (signal?: AbortSignal) => Promise<RuntimeTagListItem[]>;
export type BasicTrendHistoryLoader = (
  tagId: string,
  from: string,
  to: string,
  limit: number,
  signal?: AbortSignal
) => Promise<RuntimeTagHistorySample[]>;

export type BasicTrendViewerProps = {
  locale?: BasicTrendLocale;
  refreshIntervalMs?: number;
  sampleLimit?: number;
  tagLoader?: BasicTrendTagLoader;
  historyLoader?: BasicTrendHistoryLoader;
};

type Copy = {
  title: string;
  description: string;
  tag: string;
  mode: string;
  live: string;
  historical: string;
  interval: string;
  historicalEnd: string;
  refresh: string;
  refreshing: string;
  loadingTags: string;
  loadingHistory: string;
  emptyTags: string;
  emptyHistory: string;
  chartUnavailable: string;
  currentContext: string;
  rollingWindow: string;
  frozenWindow: string;
  samples: string;
  good: string;
  attention: string;
  bad: string;
  unknown: string;
  minimum: string;
  maximum: string;
  latest: string;
  timestamp: string;
  value: string;
  quality: string;
  source: string;
  unit: string;
  unauthenticated: string;
  forbidden: string;
  notFound: string;
  unavailable: string;
  singlePenNote: string;
};

const copy: Record<BasicTrendLocale, Copy> = {
  'pt-BR': {
    title: 'Trend básico',
    description: 'Visualização somente leitura do histórico de uma TAG usando o Historian protegido do Runtime.',
    tag: 'TAG', mode: 'Contexto', live: 'Ao vivo', historical: 'Histórico', interval: 'Janela', historicalEnd: 'Fim do intervalo', refresh: 'Atualizar', refreshing: 'Atualizando…',
    loadingTags: 'Carregando TAGs…', loadingHistory: 'Carregando histórico…', emptyTags: 'Nenhuma TAG está disponível para consulta.', emptyHistory: 'Nenhuma amostra histórica existe nesta janela.', chartUnavailable: 'As amostras desta TAG não são numéricas/booleanas. Os valores continuam disponíveis na tabela.',
    currentContext: 'Contexto atual', rollingWindow: 'Janela rolante terminando no momento atual', frozenWindow: 'Snapshot histórico com fim fixo', samples: 'Amostras', good: 'Good', attention: 'Atenção', bad: 'Bad', unknown: 'Desconhecida',
    minimum: 'Mínimo', maximum: 'Máximo', latest: 'Último', timestamp: 'Timestamp', value: 'Valor', quality: 'Qualidade', source: 'Origem', unit: 'Unidade',
    unauthenticated: 'Sessão não autenticada para consultar o Historian.', forbidden: 'Sem permissão para consultar esta TAG.', notFound: 'A TAG selecionada não existe mais no Runtime ativo.', unavailable: 'Historian ou catálogo de TAGs indisponível no momento.',
    singlePenNote: 'Esta etapa usa uma Pen para preservar escala e unidade sem inventar semântica de múltiplos eixos.'
  },
  en: {
    title: 'Basic Trend',
    description: 'Read-only visualization of one TAG history using the protected Runtime Historian.',
    tag: 'TAG', mode: 'Context', live: 'Live', historical: 'Historical', interval: 'Window', historicalEnd: 'Window end', refresh: 'Refresh', refreshing: 'Refreshing…',
    loadingTags: 'Loading TAGs…', loadingHistory: 'Loading history…', emptyTags: 'No TAG is available for querying.', emptyHistory: 'No historical sample exists in this window.', chartUnavailable: 'This TAG has no numeric/boolean samples. Values remain available in the table.',
    currentContext: 'Current context', rollingWindow: 'Rolling window ending now', frozenWindow: 'Historical snapshot with fixed end', samples: 'Samples', good: 'Good', attention: 'Attention', bad: 'Bad', unknown: 'Unknown',
    minimum: 'Minimum', maximum: 'Maximum', latest: 'Latest', timestamp: 'Timestamp', value: 'Value', quality: 'Quality', source: 'Source', unit: 'Unit',
    unauthenticated: 'The session is not authenticated to query the Historian.', forbidden: 'Not authorized to query this TAG.', notFound: 'The selected TAG no longer exists in the active Runtime.', unavailable: 'Historian or TAG catalog is currently unavailable.',
    singlePenNote: 'This stage uses one Pen so scale and engineering unit remain honest without inventing multi-axis semantics.'
  },
  es: {
    title: 'Trend básico',
    description: 'Visualización de solo lectura del histórico de una TAG usando el Historian protegido del Runtime.',
    tag: 'TAG', mode: 'Contexto', live: 'En vivo', historical: 'Histórico', interval: 'Ventana', historicalEnd: 'Fin del intervalo', refresh: 'Actualizar', refreshing: 'Actualizando…',
    loadingTags: 'Cargando TAGs…', loadingHistory: 'Cargando histórico…', emptyTags: 'No hay TAGs disponibles para consulta.', emptyHistory: 'No hay muestras históricas en esta ventana.', chartUnavailable: 'Esta TAG no tiene muestras numéricas/booleanas. Los valores siguen disponibles en la tabla.',
    currentContext: 'Contexto actual', rollingWindow: 'Ventana móvil que termina ahora', frozenWindow: 'Snapshot histórico con fin fijo', samples: 'Muestras', good: 'Good', attention: 'Atención', bad: 'Bad', unknown: 'Desconocida',
    minimum: 'Mínimo', maximum: 'Máximo', latest: 'Último', timestamp: 'Timestamp', value: 'Valor', quality: 'Calidad', source: 'Origen', unit: 'Unidad',
    unauthenticated: 'La sesión no está autenticada para consultar el Historian.', forbidden: 'Sin permiso para consultar esta TAG.', notFound: 'La TAG seleccionada ya no existe en el Runtime activo.', unavailable: 'Historian o catálogo de TAGs no disponible.',
    singlePenNote: 'Esta etapa usa una Pen para preservar escala y unidad sin inventar semántica de múltiples ejes.'
  }
};

const windows: BasicTrendWindow[] = ['15m', '1h', '6h', '24h'];

function issueFrom(error: unknown): RuntimeTagEndpointIssue {
  return error instanceof BasicTrendApiError ? error.issue : 'unavailable';
}

function issueText(issue: RuntimeTagEndpointIssue, text: Copy) {
  if (issue === 'unauthenticated') return text.unauthenticated;
  if (issue === 'forbidden') return text.forbidden;
  if (issue === 'not-found') return text.notFound;
  return text.unavailable;
}

function formatValue(value: unknown, locale: BasicTrendLocale) {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'number') return new Intl.NumberFormat(locale, { maximumFractionDigits: 6 }).format(value);
  if (typeof value === 'boolean') return value ? 'TRUE' : 'FALSE';
  if (typeof value === 'string') return value;
  try { return JSON.stringify(value); } catch { return String(value); }
}

function formatMoment(value: string | null | undefined, locale: BasicTrendLocale) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}

function inputDateTimeValue(date = new Date()) {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function qualityText(quality: string | number, text: Copy) {
  const tone = trendQualityTone(quality);
  if (tone === 'good') return text.good;
  if (tone === 'attention') return text.attention;
  if (tone === 'bad') return text.bad;
  return text.unknown;
}

export function BasicTrendViewer({
  locale = 'pt-BR',
  refreshIntervalMs = 5000,
  sampleLimit = 500,
  tagLoader = loadTrendTags,
  historyLoader = loadTrendHistory
}: BasicTrendViewerProps) {
  const text = copy[locale];
  const [tags, setTags] = useState<RuntimeTagListItem[]>([]);
  const [selectedId, setSelectedId] = useState<string>('');
  const [mode, setMode] = useState<BasicTrendMode>('live');
  const [window, setWindow] = useState<BasicTrendWindow>('15m');
  const [historicalEnd, setHistoricalEnd] = useState(() => inputDateTimeValue());
  const [samples, setSamples] = useState<RuntimeTagHistorySample[]>([]);
  const [rangeLabel, setRangeLabel] = useState<{ from: string; to: string } | null>(null);
  const [tagIssue, setTagIssue] = useState<RuntimeTagEndpointIssue | null>(null);
  const [historyIssue, setHistoryIssue] = useState<RuntimeTagEndpointIssue | null>(null);
  const [loadingTags, setLoadingTags] = useState(true);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const tagAbort = useRef<AbortController | null>(null);
  const historyAbort = useRef<AbortController | null>(null);

  const selectedTag = useMemo(() => tags.find(tag => tag.id === selectedId) ?? null, [selectedId, tags]);
  const orderedSamples = useMemo(() => sortTrendSamples(samples), [samples]);
  const summary = useMemo(() => summarizeTrendSamples(orderedSamples), [orderedSamples]);
  const plot = useMemo(() => buildTrendPlot(orderedSamples), [orderedSamples]);

  const refreshTags = useCallback(async () => {
    tagAbort.current?.abort();
    const controller = new AbortController();
    tagAbort.current = controller;
    try {
      const next = await tagLoader(controller.signal);
      if (controller.signal.aborted) return;
      const ordered = [...next].sort((left, right) => left.path.localeCompare(right.path, undefined, { numeric: true, sensitivity: 'base' }));
      setTags(ordered);
      setSelectedId(current => current && ordered.some(tag => tag.id === current) ? current : ordered[0]?.id ?? '');
      setTagIssue(null);
    } catch (error) {
      if (!controller.signal.aborted) setTagIssue(issueFrom(error));
    } finally {
      if (tagAbort.current === controller) {
        tagAbort.current = null;
        setLoadingTags(false);
      }
    }
  }, [tagLoader]);

  useEffect(() => {
    void refreshTags();
    return () => tagAbort.current?.abort();
  }, [refreshTags]);

  const refreshHistory = useCallback(async () => {
    if (!selectedTag) {
      setSamples([]);
      setRangeLabel(null);
      return;
    }

    historyAbort.current?.abort();
    const controller = new AbortController();
    historyAbort.current = controller;
    setLoadingHistory(true);
    setHistoryIssue(null);

    const parsedEnd = mode === 'historical' ? new Date(historicalEnd) : null;
    const range = buildBasicTrendRange(mode, window, parsedEnd && Number.isFinite(parsedEnd.getTime()) ? parsedEnd : null);
    setRangeLabel(range);

    try {
      const next = await historyLoader(selectedTag.id, range.from, range.to, sampleLimit, controller.signal);
      if (controller.signal.aborted) return;
      setSamples(next);
    } catch (error) {
      if (controller.signal.aborted) return;
      setSamples([]);
      setHistoryIssue(issueFrom(error));
    } finally {
      if (historyAbort.current === controller) {
        historyAbort.current = null;
        setLoadingHistory(false);
      }
    }
  }, [historicalEnd, historyLoader, mode, sampleLimit, selectedTag, window]);

  useEffect(() => {
    void refreshHistory();
    if (mode !== 'live' || refreshIntervalMs <= 0) return () => historyAbort.current?.abort();
    const timer = globalThis.setInterval(() => void refreshHistory(), refreshIntervalMs);
    return () => {
      globalThis.clearInterval(timer);
      historyAbort.current?.abort();
    };
  }, [mode, refreshHistory, refreshIntervalMs]);

  if (loadingTags && tags.length === 0) {
    return <section className="basic-trend basic-trend-state" aria-label={text.title}>{text.loadingTags}</section>;
  }

  return (
    <section className="basic-trend" aria-label={text.title} aria-busy={loadingHistory}>
      <header className="basic-trend-header">
        <div>
          <span className="basic-trend-eyebrow">Runtime / Historian</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <button type="button" onClick={() => void refreshHistory()} disabled={!selectedTag || loadingHistory}>
          {loadingHistory ? text.refreshing : text.refresh}
        </button>
      </header>

      <div className="basic-trend-toolbar">
        <label>
          <span>{text.tag}</span>
          <select value={selectedId} onChange={event => setSelectedId(event.target.value)} disabled={tags.length === 0}>
            {tags.map(tag => <option key={tag.id} value={tag.id}>{tag.path}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</option>)}
          </select>
        </label>
        <label>
          <span>{text.mode}</span>
          <select value={mode} onChange={event => setMode(event.target.value as BasicTrendMode)}>
            <option value="live">{text.live}</option>
            <option value="historical">{text.historical}</option>
          </select>
        </label>
        <label>
          <span>{text.interval}</span>
          <select value={window} onChange={event => setWindow(event.target.value as BasicTrendWindow)}>
            {windows.map(value => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>
        {mode === 'historical' && (
          <label>
            <span>{text.historicalEnd}</span>
            <input type="datetime-local" value={historicalEnd} onChange={event => setHistoricalEnd(event.target.value)} />
          </label>
        )}
      </div>

      {tagIssue && <div className="basic-trend-message basic-trend-error">{issueText(tagIssue, text)}</div>}
      {!tagIssue && tags.length === 0 && <div className="basic-trend-message">{text.emptyTags}</div>}

      {selectedTag && (
        <>
          <div className="basic-trend-context">
            <div>
              <span>{text.currentContext}</span>
              <strong>{mode === 'live' ? text.rollingWindow : text.frozenWindow}</strong>
            </div>
            <div>
              <span>{text.tag}</span>
              <strong>{selectedTag.path}</strong>
            </div>
            <div>
              <span>{text.unit}</span>
              <strong>{selectedTag.engineeringUnit || '—'}</strong>
            </div>
            <div className="basic-trend-context-wide">
              <span>{text.interval}</span>
              <strong>{rangeLabel ? `${formatMoment(rangeLabel.from, locale)} → ${formatMoment(rangeLabel.to, locale)}` : '—'}</strong>
            </div>
          </div>

          <p className="basic-trend-note">{text.singlePenNote}</p>

          {historyIssue && <div className="basic-trend-message basic-trend-error">{issueText(historyIssue, text)}</div>}
          {!historyIssue && loadingHistory && samples.length === 0 && <div className="basic-trend-message">{text.loadingHistory}</div>}
          {!historyIssue && !loadingHistory && samples.length === 0 && <div className="basic-trend-message">{text.emptyHistory}</div>}

          {samples.length > 0 && (
            <>
              <div className="basic-trend-summary">
                <Summary label={text.samples} value={String(summary.total)} />
                <Summary label={text.good} value={String(summary.good)} tone="good" />
                <Summary label={text.attention} value={String(summary.attention)} tone="attention" />
                <Summary label={text.bad} value={String(summary.bad)} tone="bad" />
                <Summary label={text.minimum} value={summary.minimum === null ? '—' : formatValue(summary.minimum, locale)} />
                <Summary label={text.maximum} value={summary.maximum === null ? '—' : formatValue(summary.maximum, locale)} />
                <Summary label={text.latest} value={formatValue(summary.latestValue, locale)} />
              </div>

              {plot.points.length > 0 ? (
                <div className="basic-trend-chart" role="img" aria-label={`${text.title}: ${selectedTag.path}`}>
                  <div className="basic-trend-axis basic-trend-axis-top">{formatValue(plot.maximum, locale)} {selectedTag.engineeringUnit ?? ''}</div>
                  <svg viewBox="0 0 1000 280" preserveAspectRatio="none" aria-hidden="true">
                    <line className="basic-trend-grid" x1="24" y1="24" x2="976" y2="24" />
                    <line className="basic-trend-grid" x1="24" y1="140" x2="976" y2="140" />
                    <line className="basic-trend-grid" x1="24" y1="256" x2="976" y2="256" />
                    <polyline className="basic-trend-line" points={plot.points.map(point => `${point.x},${point.y}`).join(' ')} />
                    {plot.points.map((point, index) => (
                      <circle key={`${point.timestamp}-${index}`} className={`basic-trend-point tone-${point.qualityTone}`} cx={point.x} cy={point.y} r="5" />
                    ))}
                  </svg>
                  <div className="basic-trend-axis basic-trend-axis-bottom">{formatValue(plot.minimum, locale)} {selectedTag.engineeringUnit ?? ''}</div>
                  <div className="basic-trend-time-axis">
                    <span>{formatMoment(orderedSamples[0]?.timestamp, locale)}</span>
                    <span>{formatMoment(orderedSamples.at(-1)?.timestamp, locale)}</span>
                  </div>
                </div>
              ) : (
                <div className="basic-trend-message">{text.chartUnavailable}</div>
              )}

              <div className="basic-trend-table-wrap">
                <table className="basic-trend-table">
                  <thead><tr><th>{text.timestamp}</th><th>{text.value}</th><th>{text.quality}</th><th>{text.source}</th></tr></thead>
                  <tbody>
                    {orderedSamples.slice(-40).reverse().map((sample, index) => {
                      const tone = trendQualityTone(sample.quality);
                      return (
                        <tr key={`${sample.timestamp}-${index}`}>
                          <td>{formatMoment(sample.timestamp, locale)}</td>
                          <td>{formatValue(sample.value, locale)}{selectedTag.engineeringUnit ? ` ${selectedTag.engineeringUnit}` : ''}</td>
                          <td><span className={`basic-trend-quality tone-${tone}`}>{qualityText(sample.quality, text)}</span></td>
                          <td>{sample.source || '—'}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </section>
  );
}

function Summary({ label, value, tone }: { label: string; value: string; tone?: 'good' | 'attention' | 'bad' }) {
  return <div className={`basic-trend-summary-card${tone ? ` tone-${tone}` : ''}`}><span>{label}</span><strong>{value}</strong></div>;
}
