import { useEffect, useMemo, useState } from 'react';
import { loadCommunicationDiagnostics } from './api';
import type { EngineeringLocale } from './i18n';
import type { CommunicationDriverDiagnostic } from './types';

type Copy = {
  title: string;
  description: string;
  loading: string;
  empty: string;
  error: string;
  dataSource: string;
  driver: string;
  endpoint: string;
  state: string;
  lastSuccess: string;
  failures: string;
  timeouts: string;
  reconnects: string;
  tags: string;
  details: string;
  instance: string;
  lastFailure: string;
  failureRate: string;
  scan: string;
  latency: string;
  cycles: string;
  requests: string;
  reads: string;
  writes: string;
  quality: string;
  noSample: string;
  errorMessage: string;
};

const copy: Record<EngineeringLocale, Copy> = {
  'pt-BR': {
    title: 'Comunicação ativa',
    description: 'Diagnóstico operacional dos Data Sources de comunicação da Active Revision. Fontes internas não fabricam métricas de rede.',
    loading: 'Carregando diagnóstico de comunicação...',
    empty: 'Nenhum Data Source de comunicação está ativo.',
    error: 'Não foi possível carregar o diagnóstico de comunicação.',
    dataSource: 'Data Source', driver: 'Driver', endpoint: 'Endpoint', state: 'Estado', lastSuccess: 'Último sucesso',
    failures: 'Falhas', timeouts: 'Timeouts', reconnects: 'Reconexões', tags: 'TAGs', details: 'Detalhes',
    instance: 'Instância runtime', lastFailure: 'Última falha', failureRate: 'Taxa recente de falha', scan: 'Scan configurado',
    latency: 'Latência média', cycles: 'Ciclos', requests: 'Requests', reads: 'Leituras', writes: 'Escritas',
    quality: 'Qualidade das TAGs', noSample: 'Sem amostra', errorMessage: 'Último erro'
  },
  en: {
    title: 'Active communication',
    description: 'Operational diagnostics for communication Data Sources in the Active Revision. Internal sources do not fabricate network metrics.',
    loading: 'Loading communication diagnostics...',
    empty: 'No communication Data Source is active.',
    error: 'Communication diagnostics could not be loaded.',
    dataSource: 'Data Source', driver: 'Driver', endpoint: 'Endpoint', state: 'State', lastSuccess: 'Last success',
    failures: 'Failures', timeouts: 'Timeouts', reconnects: 'Reconnects', tags: 'TAGs', details: 'Details',
    instance: 'Runtime instance', lastFailure: 'Last failure', failureRate: 'Recent failure rate', scan: 'Configured scan',
    latency: 'Average latency', cycles: 'Cycles', requests: 'Requests', reads: 'Reads', writes: 'Writes',
    quality: 'TAG quality', noSample: 'No sample', errorMessage: 'Last error'
  },
  es: {
    title: 'Comunicación activa',
    description: 'Diagnóstico operacional de los Data Sources de comunicación de la Active Revision. Las fuentes internas no inventan métricas de red.',
    loading: 'Cargando diagnóstico de comunicación...',
    empty: 'No hay Data Sources de comunicación activos.',
    error: 'No fue posible cargar el diagnóstico de comunicación.',
    dataSource: 'Data Source', driver: 'Driver', endpoint: 'Endpoint', state: 'Estado', lastSuccess: 'Último éxito',
    failures: 'Fallos', timeouts: 'Timeouts', reconnects: 'Reconexiones', tags: 'TAGs', details: 'Detalles',
    instance: 'Instancia runtime', lastFailure: 'Último fallo', failureRate: 'Tasa reciente de fallo', scan: 'Scan configurado',
    latency: 'Latencia media', cycles: 'Ciclos', requests: 'Requests', reads: 'Lecturas', writes: 'Escrituras',
    quality: 'Calidad de TAGs', noSample: 'Sin muestra', errorMessage: 'Último error'
  }
};

const numericStates = ['Stopped', 'Starting', 'Healthy', 'Degraded', 'Reconnecting', 'Faulted', 'Stopping'];

export function CommunicationDiagnosticsPanel({ locale }: { locale: EngineeringLocale }) {
  const text = copy[locale];
  const [items, setItems] = useState<CommunicationDriverDiagnostic[]>([]);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;
    const refresh = async () => {
      try {
        const next = await loadCommunicationDiagnostics();
        if (disposed) return;
        setItems(next);
        setError(null);
        setSelectedKey(current => current && next.some(item => item.dataSourceKey === current)
          ? current
          : next[0]?.dataSourceKey ?? null);
      } catch (reason) {
        if (!disposed) setError(reason instanceof Error ? reason.message : String(reason));
      } finally {
        if (!disposed) setLoading(false);
      }
    };

    void refresh();
    const timer = window.setInterval(() => void refresh(), 3000);
    return () => {
      disposed = true;
      window.clearInterval(timer);
    };
  }, []);

  const selected = useMemo(
    () => items.find(item => item.dataSourceKey === selectedKey) ?? null,
    [items, selectedKey]
  );

  return (
    <section className="eng-panel" style={{ marginTop: 16 }}>
      <h2>{text.title}</h2>
      <p>{text.description}</p>

      {loading && <div className="eng-empty"><span>{text.loading}</span></div>}
      {!loading && error && (
        <div className="eng-empty">
          <strong>{text.error}</strong>
          <span>{error}</span>
        </div>
      )}
      {!loading && !error && items.length === 0 && (
        <div className="eng-empty"><span>{text.empty}</span></div>
      )}

      {!loading && !error && items.length > 0 && (
        <>
          <div className="eng-table-wrap">
            <table className="eng-table" aria-label={text.title}>
              <thead>
                <tr>
                  <th>{text.dataSource}</th><th>{text.driver}</th><th>{text.endpoint}</th><th>{text.state}</th>
                  <th>{text.lastSuccess}</th><th>{text.failures}</th><th>{text.timeouts}</th><th>{text.reconnects}</th><th>{text.tags}</th>
                </tr>
              </thead>
              <tbody>
                {items.map(item => (
                  <tr key={item.dataSourceKey}>
                    <td>
                      <button
                        type="button"
                        onClick={() => setSelectedKey(item.dataSourceKey)}
                        style={{ background: 'transparent', border: 0, padding: 0, color: 'inherit', cursor: 'pointer', textAlign: 'left' }}
                      >
                        <strong>{item.dataSourceName}</strong><br />
                        <code className="eng-code">{item.dataSourceKey}</code>
                      </button>
                    </td>
                    <td><code className="eng-code">{item.driverType}</code></td>
                    <td>{item.endpoint ?? '—'}</td>
                    <td><State value={item.state} /></td>
                    <td>{formatMoment(item.lastSuccessfulCommunicationAt, locale)}</td>
                    <td>{item.counters.failedOperations}</td>
                    <td>{item.counters.timeouts}</td>
                    <td>{item.counters.reconnects}</td>
                    <td>{item.tagQuality.good}/{item.associatedTagCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {selected && (
            <div style={{ marginTop: 18 }}>
              <h2>{text.details}: {selected.dataSourceName}</h2>
              <div className="eng-diagnostic-grid">
                <Metric label={text.instance} value={selected.runtimeInstanceId} mono />
                <Metric label={text.lastFailure} value={formatMoment(selected.lastFailedCommunicationAt, locale)} />
                <Metric label={text.failureRate} value={`${(selected.recentFailureRate * 100).toFixed(1)}%`} />
                <Metric label={text.scan} value={selected.configuredScanInterval ?? '—'} />
                <Metric label={text.latency} value={selected.averageOperationDuration ?? '—'} />
                <Metric label={text.cycles} value={String(selected.counters.cycles)} />
                <Metric label={text.requests} value={String(selected.counters.requests)} />
                <Metric label={text.reads} value={String(selected.counters.readOperations)} />
                <Metric label={text.writes} value={String(selected.counters.writeOperations)} />
                <Metric label={text.quality} value={`Good ${selected.tagQuality.good} · BadComm ${selected.tagQuality.badCommunication} · ${text.noSample} ${selected.tagQuality.noCurrentSample}`} />
                <Metric label={text.errorMessage} value={selected.lastError ?? '—'} />
              </div>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function State({ value }: { value: string | number }) {
  const state = typeof value === 'number' ? numericStates[value] ?? String(value) : value;
  const warning = state === 'Degraded' || state === 'Reconnecting';
  const danger = state === 'Faulted';
  return <strong style={{ color: danger ? 'var(--eng-danger)' : warning ? 'var(--eng-warning)' : 'inherit' }}>{state}</strong>;
}

function Metric({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="eng-diagnostic-card">
      <span>{label}</span>
      <strong className={mono ? 'mono' : ''}>{value}</strong>
    </div>
  );
}

function formatMoment(value: string | null | undefined, locale: EngineeringLocale) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}
