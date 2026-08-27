import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { loadRuntimeOperationsSnapshot } from './operationsApi';
import {
  buildRuntimeOperationsSummary,
  communicationTone,
  gatewayTone,
  normalizeCommunicationState,
  normalizeDriverState,
  sortCommunicationDiagnostics,
  sortGatewayDiagnostics
} from './operationsModel';
import type {
  OperationalTone,
  RuntimeAlarm,
  RuntimeOperationsEndpoint,
  RuntimeOperationsLocale,
  RuntimeOperationsSnapshot
} from './operationsTypes';
import './runtime-operations.css';

export type RuntimeOperationsLoader = (signal?: AbortSignal) => Promise<RuntimeOperationsSnapshot>;

export type RuntimeOperationsOverviewProps = {
  locale?: RuntimeOperationsLocale;
  refreshIntervalMs?: number;
  loader?: RuntimeOperationsLoader;
};

type Copy = {
  title: string;
  description: string;
  refresh: string;
  loading: string;
  refreshed: string;
  partial: string;
  unavailable: string;
  restricted: string;
  overallHealthy: string;
  overallAttention: string;
  overallDanger: string;
  overallUnknown: string;
  runtime: string;
  runtimeEngineering: string;
  runtimeSimulation: string;
  project: string;
  revision: string;
  drivers: string;
  tags: string;
  historian: string;
  pending: string;
  communication: string;
  externalSources: string;
  healthy: string;
  attention: string;
  faulted: string;
  noExternalSources: string;
  lastSuccess: string;
  failures: string;
  timeouts: string;
  reconnects: string;
  quality: string;
  good: string;
  badCommunication: string;
  otherBad: string;
  noSample: string;
  alarms: string;
  noActiveAlarms: string;
  alarmState: string;
  gateway: string;
  noGateways: string;
  running: string;
  waiting: string;
  degraded: string;
  transfers: string;
  writeFailures: string;
  source: string;
  destination: string;
  more: string;
  stateStopped: string;
  stateStarting: string;
  stateRunning: string;
  stateHealthy: string;
  stateDegraded: string;
  stateReconnecting: string;
  stateFaulted: string;
  stateStopping: string;
  stateWaitingForSource: string;
};

const copy: Record<RuntimeOperationsLocale, Copy> = {
  'pt-BR': {
    title: 'Visão operacional',
    description: 'Contexto do Runtime ativo usando os mesmos fatos protegidos de diagnóstico, alarmes e Gateway já expostos pelo backend.',
    refresh: 'Atualizar', loading: 'Carregando contexto operacional...', refreshed: 'Atualizado', partial: 'Dados parciais',
    unavailable: 'Indisponível no momento', restricted: 'Sem permissão para este diagnóstico',
    overallHealthy: 'Operação estável', overallAttention: 'Atenção operacional', overallDanger: 'Falha operacional', overallUnknown: 'Visão operacional parcial',
    runtime: 'Runtime', runtimeEngineering: 'Engineering ativo', runtimeSimulation: 'Simulação / demo', project: 'Projeto', revision: 'Revisão', drivers: 'Drivers', tags: 'TAGs', historian: 'Historian', pending: 'pendentes',
    communication: 'Comunicação', externalSources: 'Data Sources externos', healthy: 'saudáveis', attention: 'atenção', faulted: 'em falha', noExternalSources: 'Nenhum Data Source externo ativo', lastSuccess: 'Último sucesso', failures: 'Falhas', timeouts: 'Timeouts', reconnects: 'Reconexões',
    quality: 'Qualidade das TAGs de comunicação', good: 'Good', badCommunication: 'BadCommunication', otherBad: 'Outras anormais', noSample: 'Sem amostra',
    alarms: 'Alarmes ativos', noActiveAlarms: 'Nenhum alarme ativo visível', alarmState: 'Estado',
    gateway: 'Gateway', noGateways: 'Nenhuma rota Gateway ativa', running: 'executando', waiting: 'aguardando fonte', degraded: 'degradadas', transfers: 'Transferências', writeFailures: 'Falhas de escrita', source: 'Origem', destination: 'Destino', more: 'mais',
    stateStopped: 'Parado', stateStarting: 'Iniciando', stateRunning: 'Executando', stateHealthy: 'Saudável', stateDegraded: 'Degradado', stateReconnecting: 'Reconectando', stateFaulted: 'Falha', stateStopping: 'Parando', stateWaitingForSource: 'Aguardando fonte'
  },
  en: {
    title: 'Operational overview',
    description: 'Active Runtime context using the same protected diagnostics, alarm and Gateway facts already exposed by the backend.',
    refresh: 'Refresh', loading: 'Loading operational context...', refreshed: 'Updated', partial: 'Partial data',
    unavailable: 'Currently unavailable', restricted: 'Not authorized for this diagnostic',
    overallHealthy: 'Operation stable', overallAttention: 'Operational attention', overallDanger: 'Operational fault', overallUnknown: 'Partial operational view',
    runtime: 'Runtime', runtimeEngineering: 'Engineering active', runtimeSimulation: 'Simulation / demo', project: 'Project', revision: 'Revision', drivers: 'Drivers', tags: 'TAGs', historian: 'Historian', pending: 'pending',
    communication: 'Communication', externalSources: 'External Data Sources', healthy: 'healthy', attention: 'attention', faulted: 'faulted', noExternalSources: 'No external Data Source is active', lastSuccess: 'Last success', failures: 'Failures', timeouts: 'Timeouts', reconnects: 'Reconnects',
    quality: 'Communication TAG quality', good: 'Good', badCommunication: 'BadCommunication', otherBad: 'Other abnormal', noSample: 'No sample',
    alarms: 'Active alarms', noActiveAlarms: 'No visible active alarm', alarmState: 'State',
    gateway: 'Gateway', noGateways: 'No active Gateway route', running: 'running', waiting: 'waiting for source', degraded: 'degraded', transfers: 'Transfers', writeFailures: 'Write failures', source: 'Source', destination: 'Destination', more: 'more',
    stateStopped: 'Stopped', stateStarting: 'Starting', stateRunning: 'Running', stateHealthy: 'Healthy', stateDegraded: 'Degraded', stateReconnecting: 'Reconnecting', stateFaulted: 'Faulted', stateStopping: 'Stopping', stateWaitingForSource: 'Waiting for source'
  },
  es: {
    title: 'Vista operacional',
    description: 'Contexto del Runtime activo usando los mismos hechos protegidos de diagnóstico, alarmas y Gateway ya expuestos por el backend.',
    refresh: 'Actualizar', loading: 'Cargando contexto operacional...', refreshed: 'Actualizado', partial: 'Datos parciales',
    unavailable: 'No disponible en este momento', restricted: 'Sin permiso para este diagnóstico',
    overallHealthy: 'Operación estable', overallAttention: 'Atención operacional', overallDanger: 'Fallo operacional', overallUnknown: 'Vista operacional parcial',
    runtime: 'Runtime', runtimeEngineering: 'Engineering activo', runtimeSimulation: 'Simulación / demo', project: 'Proyecto', revision: 'Revisión', drivers: 'Drivers', tags: 'TAGs', historian: 'Historian', pending: 'pendientes',
    communication: 'Comunicación', externalSources: 'Data Sources externos', healthy: 'saludables', attention: 'atención', faulted: 'en fallo', noExternalSources: 'No hay Data Sources externos activos', lastSuccess: 'Último éxito', failures: 'Fallos', timeouts: 'Timeouts', reconnects: 'Reconexiones',
    quality: 'Calidad de TAGs de comunicación', good: 'Good', badCommunication: 'BadCommunication', otherBad: 'Otras anormales', noSample: 'Sin muestra',
    alarms: 'Alarmas activas', noActiveAlarms: 'No hay alarmas activas visibles', alarmState: 'Estado',
    gateway: 'Gateway', noGateways: 'No hay rutas Gateway activas', running: 'ejecutando', waiting: 'esperando fuente', degraded: 'degradadas', transfers: 'Transferencias', writeFailures: 'Fallos de escritura', source: 'Origen', destination: 'Destino', more: 'más',
    stateStopped: 'Detenido', stateStarting: 'Iniciando', stateRunning: 'Ejecutando', stateHealthy: 'Saludable', stateDegraded: 'Degradado', stateReconnecting: 'Reconectando', stateFaulted: 'Fallo', stateStopping: 'Deteniendo', stateWaitingForSource: 'Esperando fuente'
  }
};

export function RuntimeOperationsOverview({
  locale = 'pt-BR',
  refreshIntervalMs = 5000,
  loader = loadRuntimeOperationsSnapshot
}: RuntimeOperationsOverviewProps) {
  const text = copy[locale];
  const [snapshot, setSnapshot] = useState<RuntimeOperationsSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [fatalError, setFatalError] = useState<string | null>(null);
  const requestRef = useRef<AbortController | null>(null);

  const refresh = useCallback(async () => {
    requestRef.current?.abort();
    const controller = new AbortController();
    requestRef.current = controller;
    setRefreshing(true);

    try {
      const next = await loader(controller.signal);
      if (controller.signal.aborted) return;
      setSnapshot(next);
      setFatalError(null);
    } catch (error) {
      if (controller.signal.aborted) return;
      setFatalError(error instanceof Error ? error.message : String(error));
    } finally {
      if (requestRef.current === controller) {
        requestRef.current = null;
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [loader]);

  useEffect(() => {
    void refresh();
    const timer = refreshIntervalMs > 0
      ? window.setInterval(() => void refresh(), refreshIntervalMs)
      : undefined;

    return () => {
      if (timer !== undefined) window.clearInterval(timer);
      requestRef.current?.abort();
    };
  }, [refresh, refreshIntervalMs]);

  const summary = useMemo(() => snapshot ? buildRuntimeOperationsSummary(snapshot) : null, [snapshot]);
  const diagnostics = snapshot?.diagnostics.available ? snapshot.diagnostics.value : null;
  const runtime = diagnostics?.runtime ?? null;
  const communications = useMemo(
    () => sortCommunicationDiagnostics(runtime?.communicationDrivers ?? []),
    [runtime?.communicationDrivers]
  );
  const gateways = useMemo(
    () => sortGatewayDiagnostics(snapshot?.gateways.available ? snapshot.gateways.value : []),
    [snapshot]
  );
  const alarms = useMemo(
    () => sortAlarms(snapshot?.alarms.available ? snapshot.alarms.value : []),
    [snapshot]
  );
  const unavailableCount = snapshot
    ? [snapshot.diagnostics, snapshot.gateways, snapshot.alarms].filter(endpoint => !endpoint.available).length
    : 0;

  if (loading && !snapshot) {
    return <section className="runtime-ops runtime-ops-state" aria-label={text.title}>{text.loading}</section>;
  }

  if (!snapshot) {
    return (
      <section className="runtime-ops runtime-ops-state runtime-ops-state-error" aria-label={text.title}>
        <strong>{text.unavailable}</strong>
        <span>{fatalError ?? text.overallUnknown}</span>
        <button type="button" onClick={() => void refresh()}>{text.refresh}</button>
      </section>
    );
  }

  return (
    <section className="runtime-ops" aria-label={text.title} aria-busy={refreshing}>
      <header className="runtime-ops-header">
        <div>
          <span className="runtime-ops-eyebrow">Runtime / Active state</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <div className="runtime-ops-refresh">
          <span aria-live="polite">
            {unavailableCount > 0 ? `${text.partial} · ` : ''}{text.refreshed} {formatMoment(snapshot.capturedAt, locale)}
          </span>
          <button type="button" disabled={refreshing} onClick={() => void refresh()}>{text.refresh}</button>
        </div>
      </header>

      {summary && (
        <div className={`runtime-ops-overall tone-${summary.overallTone}`}>
          <span className="runtime-ops-status-dot" aria-hidden="true" />
          <div>
            <strong>{overallLabel(summary.overallTone, text)}</strong>
            <span>{overallDetail(summary, text)}</span>
          </div>
        </div>
      )}

      {fatalError && <div className="runtime-ops-inline-error">{fatalError}</div>}

      {summary && (
        <div className="runtime-ops-summary-grid">
          <SummaryCard
            label={text.runtime}
            tone={summary.runtimeTone}
            value={runtime ? runtimeModeLabel(runtime.mode, text) : '--'}
            detail={runtime
              ? runtime.projectKey
                ? `${runtime.projectKey}${runtime.revision != null ? ` · ${text.revision} ${runtime.revision}` : ''}`
                : `${summary.driverCount} ${text.drivers} · ${runtime.tagCount} ${text.tags}`
              : endpointMessage(snapshot.diagnostics, text)}
          />
          <SummaryCard
            label={text.communication}
            tone={summary.communicationTone}
            value={summary.communicationSourceCount === 0
              ? '0'
              : `${summary.healthyCommunicationSources}/${summary.communicationSourceCount}`}
            detail={summary.communicationSourceCount === 0
              ? text.noExternalSources
              : `${summary.healthyCommunicationSources} ${text.healthy} · ${summary.attentionCommunicationSources} ${text.attention} · ${summary.faultedCommunicationSources} ${text.faulted}`}
          />
          <SummaryCard
            label={text.alarms}
            tone={summary.alarmTone}
            value={formatNumber(summary.activeAlarmCount, locale)}
            detail={snapshot.alarms.available
              ? summary.activeAlarmCount === 0 ? text.noActiveAlarms : alarms[0]?.message ?? alarms[0]?.name ?? text.alarms
              : endpointMessage(snapshot.alarms, text)}
          />
          <SummaryCard
            label={text.gateway}
            tone={summary.gatewayTone}
            value={formatNumber(summary.gatewayCount, locale)}
            detail={snapshot.gateways.available
              ? summary.gatewayCount === 0
                ? text.noGateways
                : `${summary.runningGateways} ${text.running} · ${summary.waitingGateways} ${text.waiting} · ${summary.degradedGateways} ${text.degraded}`
              : endpointMessage(snapshot.gateways, text)}
          />
          <SummaryCard
            label={text.quality}
            tone={summary.communicationBadTags > 0 ? 'attention' : summary.communicationSourceCount > 0 ? 'healthy' : 'quiet'}
            value={summary.communicationTagCount > 0
              ? `${summary.communicationGoodTags}/${summary.communicationTagCount}`
              : '--'}
            detail={`${text.badCommunication}: ${communicationBadCommCount(communications)} · ${text.otherBad}: ${summary.communicationBadTags - communicationBadCommCount(communications)} · ${text.noSample}: ${summary.communicationNoSampleTags}`}
          />
        </div>
      )}

      <div className="runtime-ops-detail-grid">
        <section className="runtime-ops-panel runtime-ops-panel-wide">
          <PanelHeader title={text.externalSources} count={snapshot.diagnostics.available ? communications.length : undefined} />
          {!snapshot.diagnostics.available && <EndpointState endpoint={snapshot.diagnostics} text={text} />}
          {snapshot.diagnostics.available && communications.length === 0 && <EmptyState>{text.noExternalSources}</EmptyState>}
          {snapshot.diagnostics.available && communications.length > 0 && (
            <div className="runtime-ops-list">
              {communications.slice(0, 6).map(item => {
                const tone = communicationTone(item);
                const state = normalizeCommunicationState(item.state);
                return (
                  <article className="runtime-ops-row" key={`${item.dataSourceKey}-${item.runtimeInstanceId}`}>
                    <div className="runtime-ops-row-main">
                      <div>
                        <strong>{item.dataSourceName}</strong>
                        <span>{item.dataSourceKey} · {item.driverType}{item.endpoint ? ` · ${item.endpoint}` : ''}</span>
                      </div>
                      <StatePill tone={tone}>{stateLabel(state, text)}</StatePill>
                    </div>
                    <div className="runtime-ops-row-metrics">
                      <SmallMetric label={text.lastSuccess} value={formatAge(item.lastSuccessfulCommunicationAt, snapshot.capturedAt, locale)} />
                      <SmallMetric label={text.failures} value={formatNumber(item.counters.failedOperations, locale)} />
                      <SmallMetric label={text.timeouts} value={formatNumber(item.counters.timeouts, locale)} />
                      <SmallMetric label={text.reconnects} value={formatNumber(item.counters.reconnects, locale)} />
                      <SmallMetric label={text.good} value={`${item.tagQuality.good}/${item.associatedTagCount}`} />
                      <SmallMetric label={text.badCommunication} value={formatNumber(item.tagQuality.badCommunication, locale)} />
                    </div>
                    {item.lastError && tone !== 'healthy' && <div className="runtime-ops-row-error">{item.lastError}</div>}
                  </article>
                );
              })}
              {communications.length > 6 && <div className="runtime-ops-more">+{communications.length - 6} {text.more}</div>}
            </div>
          )}
        </section>

        <section className="runtime-ops-panel">
          <PanelHeader title={text.alarms} count={snapshot.alarms.available ? alarms.length : undefined} />
          {!snapshot.alarms.available && <EndpointState endpoint={snapshot.alarms} text={text} />}
          {snapshot.alarms.available && alarms.length === 0 && <EmptyState>{text.noActiveAlarms}</EmptyState>}
          {snapshot.alarms.available && alarms.length > 0 && (
            <div className="runtime-ops-list compact">
              {alarms.slice(0, 5).map(alarm => (
                <article className="runtime-ops-event" key={alarm.definitionId}>
                  <div>
                    <strong>{alarm.name}</strong>
                    <span>{alarm.message ?? alarm.type}</span>
                  </div>
                  <div className="runtime-ops-event-meta">
                    <span>{alarm.priority}</span>
                    <span>{text.alarmState}: {alarm.state}</span>
                    {alarm.area && <span>{alarm.area}</span>}
                  </div>
                </article>
              ))}
              {alarms.length > 5 && <div className="runtime-ops-more">+{alarms.length - 5} {text.more}</div>}
            </div>
          )}
        </section>

        <section className="runtime-ops-panel">
          <PanelHeader title={text.gateway} count={snapshot.gateways.available ? gateways.length : undefined} />
          {!snapshot.gateways.available && <EndpointState endpoint={snapshot.gateways} text={text} />}
          {snapshot.gateways.available && gateways.length === 0 && <EmptyState>{text.noGateways}</EmptyState>}
          {snapshot.gateways.available && gateways.length > 0 && (
            <div className="runtime-ops-list compact">
              {gateways.slice(0, 5).map(route => (
                <article className="runtime-ops-event" key={route.routeId}>
                  <div className="runtime-ops-event-title">
                    <div>
                      <strong>{route.name}</strong>
                      <span>{route.key}</span>
                    </div>
                    <StatePill tone={gatewayTone(route)}>{stateLabel(route.state, text)}</StatePill>
                  </div>
                  <div className="runtime-ops-route">
                    <span><b>{text.source}:</b> {route.sourceTagPath}</span>
                    <span><b>{text.destination}:</b> {route.destinationTagPath}</span>
                  </div>
                  <div className="runtime-ops-event-meta">
                    <span>{text.transfers}: {formatNumber(route.transferCount, locale)}</span>
                    <span>{text.writeFailures}: {formatNumber(route.writeFailureCount, locale)}</span>
                  </div>
                </article>
              ))}
              {gateways.length > 5 && <div className="runtime-ops-more">+{gateways.length - 5} {text.more}</div>}
            </div>
          )}
        </section>

        <section className="runtime-ops-panel runtime-ops-runtime-panel">
          <PanelHeader title={text.runtime} />
          {!snapshot.diagnostics.available && <EndpointState endpoint={snapshot.diagnostics} text={text} />}
          {diagnostics && runtime && (
            <dl className="runtime-ops-definition-list">
              <div><dt>{text.runtime}</dt><dd>{runtimeModeLabel(runtime.mode, text)}</dd></div>
              <div><dt>{text.project}</dt><dd>{runtime.projectKey ?? '--'}</dd></div>
              <div><dt>{text.revision}</dt><dd>{runtime.revision ?? '--'}</dd></div>
              <div><dt>{text.drivers}</dt><dd>{runtime.drivers.map(driver => `${driver.name}: ${stateLabel(normalizeDriverState(driver.state), text)}`).join(' · ') || '--'}</dd></div>
              <div><dt>{text.tags}</dt><dd>{formatNumber(runtime.tagCount, locale)}</dd></div>
              <div><dt>{text.historian}</dt><dd>{diagnostics.historian.provider} · {formatNumber(diagnostics.historian.writtenSamples, locale)} samples · {formatNumber(diagnostics.historian.pendingSamples, locale)} {text.pending}</dd></div>
            </dl>
          )}
        </section>
      </div>
    </section>
  );
}

function SummaryCard({ label, value, detail, tone }: { label: string; value: string; detail: string; tone: OperationalTone }) {
  return (
    <article className={`runtime-ops-summary-card tone-${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}

function PanelHeader({ title, count }: { title: string; count?: number }) {
  return <header className="runtime-ops-panel-header"><strong>{title}</strong>{count !== undefined && <span>{count}</span>}</header>;
}

function StatePill({ tone, children }: { tone: OperationalTone; children: string }) {
  return <span className={`runtime-ops-state-pill tone-${tone}`}>{children}</span>;
}

function SmallMetric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function EmptyState({ children }: { children: string }) {
  return <div className="runtime-ops-empty">{children}</div>;
}

function EndpointState<T>({ endpoint, text }: { endpoint: RuntimeOperationsEndpoint<T>; text: Copy }) {
  if (endpoint.available) return null;
  return (
    <div className="runtime-ops-empty runtime-ops-endpoint-error">
      <strong>{endpoint.status === 401 || endpoint.status === 403 ? text.restricted : text.unavailable}</strong>
      <span>{endpoint.error}</span>
    </div>
  );
}

function endpointMessage<T>(endpoint: RuntimeOperationsEndpoint<T>, text: Copy): string {
  if (endpoint.available) return '';
  return endpoint.status === 401 || endpoint.status === 403 ? text.restricted : text.unavailable;
}

function overallLabel(tone: OperationalTone, text: Copy): string {
  switch (tone) {
    case 'healthy': return text.overallHealthy;
    case 'attention': return text.overallAttention;
    case 'danger': return text.overallDanger;
    default: return text.overallUnknown;
  }
}

function overallDetail(summary: ReturnType<typeof buildRuntimeOperationsSummary>, text: Copy): string {
  const parts = [
    `${summary.communicationSourceCount} ${text.externalSources}`,
    `${summary.activeAlarmCount} ${text.alarms}`,
    `${summary.gatewayCount} ${text.gateway}`
  ];
  return parts.join(' · ');
}

function runtimeModeLabel(mode: string, text: Copy): string {
  return mode.toLocaleLowerCase() === 'engineering' ? text.runtimeEngineering : text.runtimeSimulation;
}

function stateLabel(state: string, text: Copy): string {
  switch (state) {
    case 'Stopped': return text.stateStopped;
    case 'Starting': return text.stateStarting;
    case 'Running': return text.stateRunning;
    case 'Healthy': return text.stateHealthy;
    case 'Degraded': return text.stateDegraded;
    case 'Reconnecting': return text.stateReconnecting;
    case 'Faulted': return text.stateFaulted;
    case 'Stopping': return text.stateStopping;
    case 'WaitingForSource': return text.stateWaitingForSource;
    default: return state;
  }
}

function formatMoment(value: string, locale: RuntimeOperationsLocale): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '--';
  return new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(date);
}

function formatAge(value: string | null | undefined, capturedAt: string, locale: RuntimeOperationsLocale): string {
  if (!value) return '--';
  const timestamp = new Date(value).getTime();
  const captured = new Date(capturedAt).getTime();
  if (!Number.isFinite(timestamp) || !Number.isFinite(captured)) return '--';
  const seconds = Math.max(0, Math.round((captured - timestamp) / 1000));
  if (seconds < 60) return `${formatNumber(seconds, locale)} s`;
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${formatNumber(minutes, locale)} min`;
  return `${formatNumber(Math.round(minutes / 60), locale)} h`;
}

function formatNumber(value: number, locale: RuntimeOperationsLocale): string {
  return new Intl.NumberFormat(locale).format(value);
}

function communicationBadCommCount(items: ReturnType<typeof sortCommunicationDiagnostics>): number {
  return items.reduce((sum, item) => sum + item.tagQuality.badCommunication, 0);
}

function sortAlarms(items: RuntimeAlarm[]): RuntimeAlarm[] {
  return [...items].sort((left, right) => {
    const rightTime = new Date(right.lastTransition).getTime();
    const leftTime = new Date(left.lastTransition).getTime();
    return (Number.isFinite(rightTime) ? rightTime : 0) - (Number.isFinite(leftTime) ? leftTime : 0);
  });
}
