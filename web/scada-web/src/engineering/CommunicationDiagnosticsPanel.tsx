import { useCallback, useEffect, useMemo, useState } from 'react';
import { loadCommunicationDiagnostics } from './api';
import type { EngineeringLocale } from './i18n';
import type { CommunicationDriverDiagnostic } from './types';
import './communication-diagnostics.css';

type FilterMode = 'all' | 'attention' | 'healthy';

type Copy = {
  title: string;
  description: string;
  loading: string;
  empty: string;
  noMatch: string;
  error: string;
  totalSources: string;
  healthy: string;
  attention: string;
  faulted: string;
  goodTags: string;
  search: string;
  filter: string;
  filterAll: string;
  filterAttention: string;
  filterHealthy: string;
  autoRefresh: string;
  refresh: string;
  refreshed: string;
  dataSource: string;
  driver: string;
  endpoint: string;
  state: string;
  lastSuccess: string;
  lastFailure: string;
  stateChanged: string;
  dataAge: string;
  failures: string;
  timeouts: string;
  reconnects: string;
  tags: string;
  details: string;
  instance: string;
  failureRate: string;
  scan: string;
  latency: string;
  lastLatency: string;
  scanDuration: string;
  cycles: string;
  requests: string;
  successes: string;
  reads: string;
  writes: string;
  published: string;
  connections: string;
  disconnections: string;
  consecutiveFailures: string;
  quality: string;
  qualityGood: string;
  qualityBadComm: string;
  qualityOther: string;
  noSample: string;
  operational: string;
  activity: string;
  protocol: string;
  noProtocol: string;
  errorMessage: string;
  stateStopped: string;
  stateStarting: string;
  stateHealthy: string;
  stateDegraded: string;
  stateReconnecting: string;
  stateFaulted: string;
  stateStopping: string;
};

const copy: Record<EngineeringLocale, Copy> = {
  'pt-BR': {
    title: 'Comunicação ativa',
    description: 'Saúde operacional dos Data Sources de comunicação da Active Revision. Fontes internas permanecem fora das métricas de transporte.',
    loading: 'Carregando diagnóstico de comunicação...',
    empty: 'Nenhum Data Source de comunicação está ativo nesta revisão.',
    noMatch: 'Nenhum Data Source corresponde ao filtro atual.',
    error: 'Não foi possível carregar o diagnóstico de comunicação.',
    totalSources: 'Data Sources', healthy: 'Saudáveis', attention: 'Atenção', faulted: 'Em falha', goodTags: 'TAGs Good',
    search: 'Buscar Data Source, driver ou endpoint', filter: 'Filtro', filterAll: 'Todos', filterAttention: 'Requer atenção', filterHealthy: 'Somente saudáveis',
    autoRefresh: 'Atualização automática', refresh: 'Atualizar agora', refreshed: 'Atualizado',
    dataSource: 'Data Source', driver: 'Driver', endpoint: 'Endpoint', state: 'Estado', lastSuccess: 'Último sucesso', lastFailure: 'Última falha',
    stateChanged: 'Mudança de estado', dataAge: 'Idade dos dados', failures: 'Falhas', timeouts: 'Timeouts', reconnects: 'Reconexões', tags: 'TAGs', details: 'Detalhes',
    instance: 'Instância runtime', failureRate: 'Taxa recente de falha', scan: 'Scan configurado', latency: 'Latência média', lastLatency: 'Última latência', scanDuration: 'Último ciclo',
    cycles: 'Ciclos', requests: 'Requests', successes: 'Sucessos', reads: 'Leituras', writes: 'Escritas', published: 'Atualizações publicadas', connections: 'Conexões', disconnections: 'Desconexões', consecutiveFailures: 'Falhas consecutivas',
    quality: 'Qualidade das TAGs', qualityGood: 'Good', qualityBadComm: 'BadCommunication', qualityOther: 'Outras', noSample: 'Sem amostra',
    operational: 'Estado operacional', activity: 'Atividade e contadores', protocol: 'Detalhes do protocolo', noProtocol: 'Nenhum detalhe adicional informado pelo driver.', errorMessage: 'Último erro',
    stateStopped: 'Parado', stateStarting: 'Iniciando', stateHealthy: 'Saudável', stateDegraded: 'Degradado', stateReconnecting: 'Reconectando', stateFaulted: 'Falha', stateStopping: 'Parando'
  },
  en: {
    title: 'Active communication',
    description: 'Operational health for communication Data Sources in the Active Revision. Internal sources remain outside transport metrics.',
    loading: 'Loading communication diagnostics...',
    empty: 'No communication Data Source is active in this revision.',
    noMatch: 'No Data Source matches the current filter.',
    error: 'Communication diagnostics could not be loaded.',
    totalSources: 'Data Sources', healthy: 'Healthy', attention: 'Attention', faulted: 'Faulted', goodTags: 'Good TAGs',
    search: 'Search Data Source, driver or endpoint', filter: 'Filter', filterAll: 'All', filterAttention: 'Needs attention', filterHealthy: 'Healthy only',
    autoRefresh: 'Automatic refresh', refresh: 'Refresh now', refreshed: 'Updated',
    dataSource: 'Data Source', driver: 'Driver', endpoint: 'Endpoint', state: 'State', lastSuccess: 'Last success', lastFailure: 'Last failure',
    stateChanged: 'State changed', dataAge: 'Data age', failures: 'Failures', timeouts: 'Timeouts', reconnects: 'Reconnects', tags: 'TAGs', details: 'Details',
    instance: 'Runtime instance', failureRate: 'Recent failure rate', scan: 'Configured scan', latency: 'Average latency', lastLatency: 'Last latency', scanDuration: 'Last cycle',
    cycles: 'Cycles', requests: 'Requests', successes: 'Successes', reads: 'Reads', writes: 'Writes', published: 'Published updates', connections: 'Connections', disconnections: 'Disconnections', consecutiveFailures: 'Consecutive failures',
    quality: 'TAG quality', qualityGood: 'Good', qualityBadComm: 'BadCommunication', qualityOther: 'Other', noSample: 'No sample',
    operational: 'Operational state', activity: 'Activity and counters', protocol: 'Protocol details', noProtocol: 'The driver reported no additional protocol details.', errorMessage: 'Last error',
    stateStopped: 'Stopped', stateStarting: 'Starting', stateHealthy: 'Healthy', stateDegraded: 'Degraded', stateReconnecting: 'Reconnecting', stateFaulted: 'Faulted', stateStopping: 'Stopping'
  },
  es: {
    title: 'Comunicación activa',
    description: 'Salud operacional de los Data Sources de comunicación de la Active Revision. Las fuentes internas quedan fuera de las métricas de transporte.',
    loading: 'Cargando diagnóstico de comunicación...',
    empty: 'No hay Data Sources de comunicación activos en esta revisión.',
    noMatch: 'Ningún Data Source coincide con el filtro actual.',
    error: 'No fue posible cargar el diagnóstico de comunicación.',
    totalSources: 'Data Sources', healthy: 'Saludables', attention: 'Atención', faulted: 'En fallo', goodTags: 'TAGs Good',
    search: 'Buscar Data Source, driver o endpoint', filter: 'Filtro', filterAll: 'Todos', filterAttention: 'Requiere atención', filterHealthy: 'Solo saludables',
    autoRefresh: 'Actualización automática', refresh: 'Actualizar ahora', refreshed: 'Actualizado',
    dataSource: 'Data Source', driver: 'Driver', endpoint: 'Endpoint', state: 'Estado', lastSuccess: 'Último éxito', lastFailure: 'Último fallo',
    stateChanged: 'Cambio de estado', dataAge: 'Edad de datos', failures: 'Fallos', timeouts: 'Timeouts', reconnects: 'Reconexiones', tags: 'TAGs', details: 'Detalles',
    instance: 'Instancia runtime', failureRate: 'Tasa reciente de fallo', scan: 'Scan configurado', latency: 'Latencia media', lastLatency: 'Última latencia', scanDuration: 'Último ciclo',
    cycles: 'Ciclos', requests: 'Requests', successes: 'Éxitos', reads: 'Lecturas', writes: 'Escrituras', published: 'Actualizaciones publicadas', connections: 'Conexiones', disconnections: 'Desconexiones', consecutiveFailures: 'Fallos consecutivos',
    quality: 'Calidad de TAGs', qualityGood: 'Good', qualityBadComm: 'BadCommunication', qualityOther: 'Otras', noSample: 'Sin muestra',
    operational: 'Estado operacional', activity: 'Actividad y contadores', protocol: 'Detalles del protocolo', noProtocol: 'El driver no informó detalles adicionales del protocolo.', errorMessage: 'Último error',
    stateStopped: 'Detenido', stateStarting: 'Iniciando', stateHealthy: 'Saludable', stateDegraded: 'Degradado', stateReconnecting: 'Reconectando', stateFaulted: 'Fallo', stateStopping: 'Deteniendo'
  }
};

const numericStates = ['Stopped', 'Starting', 'Healthy', 'Degraded', 'Reconnecting', 'Faulted', 'Stopping'];
const severityOrder: Record<string, number> = {
  Faulted: 0,
  Reconnecting: 1,
  Degraded: 2,
  Starting: 3,
  Stopping: 4,
  Stopped: 5,
  Healthy: 6
};

export function CommunicationDiagnosticsPanel({ locale }: { locale: EngineeringLocale }) {
  const text = copy[locale];
  const [items, setItems] = useState<CommunicationDriverDiagnostic[]>([]);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<FilterMode>('all');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [lastRefreshAt, setLastRefreshAt] = useState<Date | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    try {
      const next = await loadCommunicationDiagnostics();
      setItems(next);
      setError(null);
      setLastRefreshAt(new Date());
      setSelectedKey(current => current && next.some(item => item.dataSourceKey === current)
        ? current
        : next[0]?.dataSourceKey ?? null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
    if (!autoRefresh) return;
    const timer = window.setInterval(() => void refresh(), 3000);
    return () => window.clearInterval(timer);
  }, [autoRefresh, refresh]);

  const summary = useMemo(() => {
    const healthy = items.filter(item => normalizeState(item.state) === 'Healthy').length;
    const faulted = items.filter(item => normalizeState(item.state) === 'Faulted').length;
    const goodTags = items.reduce((sum, item) => sum + item.tagQuality.good, 0);
    const totalTags = items.reduce((sum, item) => sum + item.associatedTagCount, 0);
    return {
      total: items.length,
      healthy,
      attention: items.length - healthy,
      faulted,
      goodTags,
      totalTags
    };
  }, [items]);

  const filteredItems = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    return [...items]
      .filter(item => {
        const state = normalizeState(item.state);
        if (filter === 'attention' && state === 'Healthy') return false;
        if (filter === 'healthy' && state !== 'Healthy') return false;
        if (!needle) return true;
        return [item.dataSourceName, item.dataSourceKey, item.driverType, item.endpoint ?? '']
          .some(value => value.toLocaleLowerCase().includes(needle));
      })
      .sort((left, right) => {
        const severity = (severityOrder[normalizeState(left.state)] ?? 99) - (severityOrder[normalizeState(right.state)] ?? 99);
        return severity !== 0 ? severity : left.dataSourceName.localeCompare(right.dataSourceName);
      });
  }, [filter, items, query]);

  const selected = useMemo(
    () => filteredItems.find(item => item.dataSourceKey === selectedKey) ?? filteredItems[0] ?? null,
    [filteredItems, selectedKey]
  );

  return (
    <section className="eng-comm" aria-label={text.title}>
      <header className="eng-comm-header">
        <div>
          <span className="eng-eyebrow">Runtime / Active Revision</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <div className="eng-comm-refresh-state" aria-live="polite">
          <span className={refreshing ? 'eng-comm-live active' : 'eng-comm-live'} aria-hidden="true" />
          <span>{lastRefreshAt ? `${text.refreshed} ${formatMoment(lastRefreshAt.toISOString(), locale)}` : text.loading}</span>
        </div>
      </header>

      <div className="eng-comm-summary">
        <SummaryCard label={text.totalSources} value={summary.total} />
        <SummaryCard label={text.healthy} value={summary.healthy} tone="healthy" />
        <SummaryCard label={text.attention} value={summary.attention} tone={summary.attention > 0 ? 'warning' : 'quiet'} />
        <SummaryCard label={text.faulted} value={summary.faulted} tone={summary.faulted > 0 ? 'danger' : 'quiet'} />
        <SummaryCard label={text.goodTags} value={`${summary.goodTags}/${summary.totalTags}`} />
      </div>

      <div className="eng-panel eng-comm-toolbar">
        <label className="eng-comm-search">
          <span>{text.search}</span>
          <input value={query} onChange={event => setQuery(event.target.value)} placeholder={text.search} />
        </label>
        <label className="eng-comm-filter">
          <span>{text.filter}</span>
          <select value={filter} onChange={event => setFilter(event.target.value as FilterMode)}>
            <option value="all">{text.filterAll}</option>
            <option value="attention">{text.filterAttention}</option>
            <option value="healthy">{text.filterHealthy}</option>
          </select>
        </label>
        <label className="eng-comm-auto">
          <input type="checkbox" checked={autoRefresh} onChange={event => setAutoRefresh(event.target.checked)} />
          <span>{text.autoRefresh}</span>
        </label>
        <button type="button" className="eng-comm-refresh" disabled={refreshing} onClick={() => void refresh()}>
          {text.refresh}
        </button>
      </div>

      {loading && <div className="eng-panel eng-empty"><span>{text.loading}</span></div>}
      {!loading && error && (
        <div className="eng-panel eng-empty eng-comm-error">
          <strong>{text.error}</strong>
          <span>{error}</span>
        </div>
      )}
      {!loading && !error && items.length === 0 && <div className="eng-panel eng-empty"><span>{text.empty}</span></div>}
      {!loading && !error && items.length > 0 && filteredItems.length === 0 && <div className="eng-panel eng-empty"><span>{text.noMatch}</span></div>}

      {!loading && !error && filteredItems.length > 0 && (
        <div className="eng-comm-layout">
          <section className="eng-panel eng-comm-source-panel">
            <div className="eng-comm-panel-title">
              <strong>{text.dataSource}</strong>
              <span>{filteredItems.length}/{items.length}</span>
            </div>
            <div className="eng-comm-source-list">
              {filteredItems.map(item => {
                const state = normalizeState(item.state);
                return (
                  <button
                    type="button"
                    key={item.dataSourceKey}
                    className={`eng-comm-source ${selected?.dataSourceKey === item.dataSourceKey ? 'selected' : ''}`}
                    onClick={() => setSelectedKey(item.dataSourceKey)}
                    aria-pressed={selected?.dataSourceKey === item.dataSourceKey}
                  >
                    <span className="eng-comm-source-top">
                      <span>
                        <strong>{item.dataSourceName}</strong>
                        <code>{item.dataSourceKey}</code>
                      </span>
                      <StatusBadge state={state} text={text} />
                    </span>
                    <span className="eng-comm-source-context">
                      <code>{item.driverType}</code>
                      <span>{item.endpoint ?? '—'}</span>
                    </span>
                    <span className="eng-comm-source-stats">
                      <SmallStat label={text.lastSuccess} value={formatMoment(item.lastSuccessfulCommunicationAt, locale)} />
                      <SmallStat label={text.failures} value={item.counters.failedOperations} />
                      <SmallStat label={text.timeouts} value={item.counters.timeouts} />
                      <SmallStat label={text.tags} value={`${item.tagQuality.good}/${item.associatedTagCount}`} />
                    </span>
                  </button>
                );
              })}
            </div>
          </section>

          {selected && <DiagnosticDetail item={selected} locale={locale} text={text} />}
        </div>
      )}
    </section>
  );
}

function DiagnosticDetail({ item, locale, text }: { item: CommunicationDriverDiagnostic; locale: EngineeringLocale; text: Copy }) {
  const state = normalizeState(item.state);
  const totalQuality = Math.max(item.tagQuality.total, item.associatedTagCount, 1);
  const otherQuality = Math.max(0, totalQuality - item.tagQuality.good - item.tagQuality.badCommunication - item.tagQuality.noCurrentSample);
  const protocolEntries = Object.entries(item.protocolDetails ?? {}).sort(([left], [right]) => left.localeCompare(right));

  return (
    <section className="eng-panel eng-comm-detail-panel">
      <header className="eng-comm-detail-header">
        <div>
          <span className="eng-eyebrow">{item.dataSourceKey}</span>
          <h3>{item.dataSourceName}</h3>
          <p><code>{item.driverType}</code><span>·</span><span>{item.endpoint ?? '—'}</span></p>
        </div>
        <StatusBadge state={state} text={text} large />
      </header>

      {item.lastError && (
        <div className="eng-comm-alert" role="status">
          <span>{text.errorMessage}</span>
          <strong>{item.lastError}</strong>
        </div>
      )}

      <section className="eng-comm-detail-section">
        <div className="eng-comm-panel-title"><strong>{text.operational}</strong></div>
        <div className="eng-comm-metric-grid">
          <Metric label={text.instance} value={item.runtimeInstanceId} mono />
          <Metric label={text.lastSuccess} value={formatMoment(item.lastSuccessfulCommunicationAt, locale)} />
          <Metric label={text.lastFailure} value={formatMoment(item.lastFailedCommunicationAt, locale)} />
          <Metric label={text.stateChanged} value={formatMoment(item.stateChangedAt, locale)} />
          <Metric label={text.dataAge} value={formatDuration(item.dataAge)} />
          <Metric label={text.failureRate} value={`${(item.recentFailureRate * 100).toFixed(1)}%`} />
          <Metric label={text.scan} value={formatDuration(item.configuredScanInterval)} />
          <Metric label={text.latency} value={formatDuration(item.averageOperationDuration)} />
          <Metric label={text.lastLatency} value={formatDuration(item.lastOperationDuration)} />
          <Metric label={text.scanDuration} value={formatDuration(item.lastScanDuration)} />
        </div>
      </section>

      <section className="eng-comm-detail-section">
        <div className="eng-comm-panel-title"><strong>{text.quality}</strong><span>{item.tagQuality.good}/{item.associatedTagCount}</span></div>
        <div className="eng-comm-quality-bar" aria-label={text.quality}>
          <span className="good" style={{ width: `${item.tagQuality.good / totalQuality * 100}%` }} />
          <span className="badcomm" style={{ width: `${item.tagQuality.badCommunication / totalQuality * 100}%` }} />
          <span className="other" style={{ width: `${(otherQuality + item.tagQuality.noCurrentSample) / totalQuality * 100}%` }} />
        </div>
        <div className="eng-comm-quality-legend">
          <QualityItem label={text.qualityGood} value={item.tagQuality.good} tone="good" />
          <QualityItem label={text.qualityBadComm} value={item.tagQuality.badCommunication} tone="badcomm" />
          <QualityItem label={text.qualityOther} value={otherQuality} tone="other" />
          <QualityItem label={text.noSample} value={item.tagQuality.noCurrentSample} tone="other" />
        </div>
      </section>

      <section className="eng-comm-detail-section">
        <div className="eng-comm-panel-title"><strong>{text.activity}</strong></div>
        <div className="eng-comm-counter-grid">
          <Metric label={text.cycles} value={item.counters.cycles} />
          <Metric label={text.requests} value={item.counters.requests} />
          <Metric label={text.successes} value={item.counters.successfulOperations} />
          <Metric label={text.failures} value={item.counters.failedOperations} tone={item.counters.failedOperations > 0 ? 'warning' : undefined} />
          <Metric label={text.consecutiveFailures} value={item.counters.consecutiveFailures} tone={item.counters.consecutiveFailures > 0 ? 'warning' : undefined} />
          <Metric label={text.timeouts} value={item.counters.timeouts} tone={item.counters.timeouts > 0 ? 'warning' : undefined} />
          <Metric label={text.connections} value={item.counters.connections} />
          <Metric label={text.disconnections} value={item.counters.disconnections} />
          <Metric label={text.reconnects} value={item.counters.reconnects} />
          <Metric label={text.reads} value={item.counters.readOperations} />
          <Metric label={text.writes} value={item.counters.writeOperations} />
          <Metric label={text.published} value={item.counters.updatesPublished} />
        </div>
      </section>

      <section className="eng-comm-detail-section">
        <div className="eng-comm-panel-title"><strong>{text.protocol}</strong></div>
        {protocolEntries.length === 0 ? (
          <p className="eng-comm-muted">{text.noProtocol}</p>
        ) : (
          <dl className="eng-comm-protocol-grid">
            {protocolEntries.map(([key, value]) => (
              <div key={key}>
                <dt>{key}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
        )}
      </section>
    </section>
  );
}

function SummaryCard({ label, value, tone = 'quiet' }: { label: string; value: string | number; tone?: 'quiet' | 'healthy' | 'warning' | 'danger' }) {
  return <div className={`eng-comm-summary-card ${tone}`}><span>{label}</span><strong>{value}</strong></div>;
}

function StatusBadge({ state, text, large = false }: { state: string; text: Copy; large?: boolean }) {
  const tone = state === 'Faulted' ? 'danger' : state === 'Degraded' || state === 'Reconnecting' ? 'warning' : state === 'Healthy' ? 'healthy' : 'quiet';
  return (
    <span className={`eng-comm-status ${tone} ${large ? 'large' : ''}`}>
      <i aria-hidden="true" />
      {stateLabel(state, text)}
    </span>
  );
}

function SmallStat({ label, value }: { label: string; value: string | number }) {
  return <span><small>{label}</small><strong>{value}</strong></span>;
}

function Metric({ label, value, mono = false, tone }: { label: string; value: string | number; mono?: boolean; tone?: 'warning' | 'danger' }) {
  return (
    <div className={`eng-comm-metric ${tone ?? ''}`}>
      <span>{label}</span>
      <strong className={mono ? 'mono' : ''}>{value}</strong>
    </div>
  );
}

function QualityItem({ label, value, tone }: { label: string; value: number; tone: 'good' | 'badcomm' | 'other' }) {
  return <span className={`eng-comm-quality-item ${tone}`}><i aria-hidden="true" /><strong>{value}</strong>{label}</span>;
}

function normalizeState(value: string | number) {
  return typeof value === 'number' ? numericStates[value] ?? String(value) : value;
}

function stateLabel(state: string, text: Copy) {
  const labels: Record<string, string> = {
    Stopped: text.stateStopped,
    Starting: text.stateStarting,
    Healthy: text.stateHealthy,
    Degraded: text.stateDegraded,
    Reconnecting: text.stateReconnecting,
    Faulted: text.stateFaulted,
    Stopping: text.stateStopping
  };
  return labels[state] ?? state;
}

function formatMoment(value: string | null | undefined, locale: EngineeringLocale) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}

function formatDuration(value: string | null | undefined) {
  if (!value) return '—';
  const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(value);
  if (!match) return value;
  const [, daysRaw, hoursRaw, minutesRaw, secondsRaw, fractionRaw = ''] = match;
  const days = Number(daysRaw ?? 0);
  const hours = Number(hoursRaw);
  const minutes = Number(minutesRaw);
  const seconds = Number(secondsRaw);
  const milliseconds = Number(`0.${fractionRaw || '0'}`) * 1000;
  const totalMilliseconds = (((days * 24 + hours) * 60 + minutes) * 60 + seconds) * 1000 + milliseconds;
  if (totalMilliseconds < 1000) return `${Math.round(totalMilliseconds)} ms`;
  if (totalMilliseconds < 60000) return `${(totalMilliseconds / 1000).toFixed(totalMilliseconds < 10000 ? 1 : 0)} s`;
  if (totalMilliseconds < 3600000) return `${Math.floor(totalMilliseconds / 60000)} min ${Math.round((totalMilliseconds % 60000) / 1000)} s`;
  return `${Math.floor(totalMilliseconds / 3600000)} h ${Math.round((totalMilliseconds % 3600000) / 60000)} min`;
}
