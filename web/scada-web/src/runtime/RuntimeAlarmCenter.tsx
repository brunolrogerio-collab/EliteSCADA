import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { acknowledgeRuntimeAlarm, loadActiveRuntimeAlarms } from './alarmCenterApi';
import {
  buildRuntimeAlarmCenterSummary,
  canAcknowledgeRuntimeAlarm,
  classifyRuntimeAlarmEndpointIssue,
  normalizeRuntimeAlarmPriority,
  normalizeRuntimeAlarmState,
  runtimeAlarmTone,
  sortRuntimeAlarmsForAttention
} from './alarmCenterModel';
import type {
  RuntimeAlarmAcknowledgeResult,
  RuntimeAlarmCenterEndpoint,
  RuntimeAlarmCenterItem,
  RuntimeAlarmCenterLocale
} from './alarmCenterTypes';
import './runtime-alarm-center.css';

export type RuntimeAlarmCenterLoader = (
  signal?: AbortSignal
) => Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]>>;

export type RuntimeAlarmAcknowledger = (
  definitionId: string,
  signal?: AbortSignal
) => Promise<RuntimeAlarmAcknowledgeResult>;

export type RuntimeAlarmCenterProps = {
  locale?: RuntimeAlarmCenterLocale;
  refreshIntervalMs?: number;
  loader?: RuntimeAlarmCenterLoader;
  acknowledger?: RuntimeAlarmAcknowledger;
};

type Copy = {
  title: string;
  description: string;
  refresh: string;
  loading: string;
  updated: string;
  activeVisible: string;
  awaitingAck: string;
  acknowledged: string;
  criticalHigh: string;
  empty: string;
  selectAlarm: string;
  details: string;
  priority: string;
  state: string;
  type: string;
  area: string;
  activated: string;
  lastTransition: string;
  lastValue: string;
  acknowledgedAt: string;
  acknowledgedBy: string;
  tagId: string;
  definitionId: string;
  acknowledge: string;
  acknowledging: string;
  ackSuccess: string;
  ackRefreshFailed: string;
  ackAlreadyHandled: string;
  unauthenticated: string;
  forbidden: string;
  notFound: string;
  unavailable: string;
  retry: string;
  unknown: string;
  noArea: string;
  priorityLow: string;
  priorityMedium: string;
  priorityHigh: string;
  priorityCritical: string;
  stateNormal: string;
  stateActive: string;
  stateAcknowledged: string;
  stateReturned: string;
  stateDisabled: string;
  stateShelved: string;
};

const copy: Record<RuntimeAlarmCenterLocale, Copy> = {
  'pt-BR': {
    title: 'Central de alarmes',
    description: 'Alarmes ativos do Runtime, ordenados para atenção operacional. O reconhecimento é confirmado pelo servidor e a lista é relida após o ACK.',
    refresh: 'Atualizar', loading: 'Carregando alarmes ativos...', updated: 'Atualizado', activeVisible: 'Alarmes ativos visíveis',
    awaitingAck: 'Aguardando ACK', acknowledged: 'Reconhecidos', criticalHigh: 'Críticos/altos sem ACK', empty: 'Nenhum alarme ativo visível.',
    selectAlarm: 'Selecione um alarme', details: 'Detalhes', priority: 'Prioridade', state: 'Estado', type: 'Tipo', area: 'Área', activated: 'Ativado em',
    lastTransition: 'Última transição', lastValue: 'Último valor', acknowledgedAt: 'Reconhecido em', acknowledgedBy: 'Reconhecido por', tagId: 'TAG ID', definitionId: 'Alarm ID',
    acknowledge: 'Reconhecer alarme', acknowledging: 'Reconhecendo...', ackSuccess: 'Reconhecimento confirmado pelo servidor e estado atualizado.',
    ackRefreshFailed: 'O servidor aceitou o reconhecimento, mas a releitura do estado não ficou disponível.', ackAlreadyHandled: 'Este alarme não está mais aguardando reconhecimento.',
    unauthenticated: 'Sessão não autenticada ou expirada.', forbidden: 'Sua sessão não possui autorização para esta operação.', notFound: 'O alarme não existe mais no Runtime ativo.',
    unavailable: 'O serviço de alarmes está indisponível no momento.', retry: 'Tentar novamente', unknown: 'Desconhecido', noArea: 'Sem área',
    priorityLow: 'Baixa', priorityMedium: 'Média', priorityHigh: 'Alta', priorityCritical: 'Crítica',
    stateNormal: 'Normal', stateActive: 'Ativo', stateAcknowledged: 'Reconhecido', stateReturned: 'Retornado', stateDisabled: 'Desabilitado', stateShelved: 'Suprimido'
  },
  en: {
    title: 'Alarm center',
    description: 'Active Runtime alarms sorted for operational attention. Acknowledgement is confirmed by the server and the list is reloaded after ACK.',
    refresh: 'Refresh', loading: 'Loading active alarms...', updated: 'Updated', activeVisible: 'Visible active alarms',
    awaitingAck: 'Awaiting ACK', acknowledged: 'Acknowledged', criticalHigh: 'Critical/high without ACK', empty: 'No visible active alarm.',
    selectAlarm: 'Select an alarm', details: 'Details', priority: 'Priority', state: 'State', type: 'Type', area: 'Area', activated: 'Activated at',
    lastTransition: 'Last transition', lastValue: 'Last value', acknowledgedAt: 'Acknowledged at', acknowledgedBy: 'Acknowledged by', tagId: 'TAG ID', definitionId: 'Alarm ID',
    acknowledge: 'Acknowledge alarm', acknowledging: 'Acknowledging...', ackSuccess: 'Acknowledgement confirmed by the server and state refreshed.',
    ackRefreshFailed: 'The server accepted the acknowledgement, but the authoritative state could not be reloaded.', ackAlreadyHandled: 'This alarm is no longer awaiting acknowledgement.',
    unauthenticated: 'Session is unauthenticated or expired.', forbidden: 'Your session is not authorized for this operation.', notFound: 'The alarm no longer exists in the active Runtime.',
    unavailable: 'The alarm service is currently unavailable.', retry: 'Retry', unknown: 'Unknown', noArea: 'No area',
    priorityLow: 'Low', priorityMedium: 'Medium', priorityHigh: 'High', priorityCritical: 'Critical',
    stateNormal: 'Normal', stateActive: 'Active', stateAcknowledged: 'Acknowledged', stateReturned: 'Returned', stateDisabled: 'Disabled', stateShelved: 'Shelved'
  },
  es: {
    title: 'Centro de alarmas',
    description: 'Alarmas activas del Runtime ordenadas para atención operativa. El reconocimiento lo confirma el servidor y la lista se vuelve a consultar después del ACK.',
    refresh: 'Actualizar', loading: 'Cargando alarmas activas...', updated: 'Actualizado', activeVisible: 'Alarmas activas visibles',
    awaitingAck: 'Esperando ACK', acknowledged: 'Reconocidas', criticalHigh: 'Críticas/altas sin ACK', empty: 'No hay alarmas activas visibles.',
    selectAlarm: 'Seleccione una alarma', details: 'Detalles', priority: 'Prioridad', state: 'Estado', type: 'Tipo', area: 'Área', activated: 'Activada en',
    lastTransition: 'Última transición', lastValue: 'Último valor', acknowledgedAt: 'Reconocida en', acknowledgedBy: 'Reconocida por', tagId: 'TAG ID', definitionId: 'Alarm ID',
    acknowledge: 'Reconocer alarma', acknowledging: 'Reconociendo...', ackSuccess: 'Reconocimiento confirmado por el servidor y estado actualizado.',
    ackRefreshFailed: 'El servidor aceptó el reconocimiento, pero no fue posible volver a leer el estado autoritativo.', ackAlreadyHandled: 'Esta alarma ya no espera reconocimiento.',
    unauthenticated: 'Sesión no autenticada o expirada.', forbidden: 'Su sesión no tiene autorización para esta operación.', notFound: 'La alarma ya no existe en el Runtime activo.',
    unavailable: 'El servicio de alarmas no está disponible en este momento.', retry: 'Reintentar', unknown: 'Desconocido', noArea: 'Sin área',
    priorityLow: 'Baja', priorityMedium: 'Media', priorityHigh: 'Alta', priorityCritical: 'Crítica',
    stateNormal: 'Normal', stateActive: 'Activa', stateAcknowledged: 'Reconocida', stateReturned: 'Retornada', stateDisabled: 'Deshabilitada', stateShelved: 'Suprimida'
  }
};

type AckFeedback = { tone: 'success' | 'warning' | 'error'; message: string };

export function RuntimeAlarmCenter({
  locale = 'pt-BR',
  refreshIntervalMs = 4000,
  loader = loadActiveRuntimeAlarms,
  acknowledger = acknowledgeRuntimeAlarm
}: RuntimeAlarmCenterProps) {
  const text = copy[locale];
  const [endpoint, setEndpoint] = useState<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]> | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [acknowledgingId, setAcknowledgingId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<AckFeedback | null>(null);
  const loadController = useRef<AbortController | null>(null);
  const ackController = useRef<AbortController | null>(null);

  const refresh = useCallback(async () => {
    loadController.current?.abort();
    const controller = new AbortController();
    loadController.current = controller;
    setRefreshing(true);

    try {
      const next = await loader(controller.signal);
      if (controller.signal.aborted) return null;
      setEndpoint(next);
      return next;
    } catch (error) {
      if (controller.signal.aborted) return null;
      const next: RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]> = {
        available: false,
        error: error instanceof Error ? error.message : String(error)
      };
      setEndpoint(next);
      return next;
    } finally {
      if (loadController.current === controller) {
        loadController.current = null;
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
      loadController.current?.abort();
      ackController.current?.abort();
    };
  }, [refresh, refreshIntervalMs]);

  const alarms = useMemo(
    () => sortRuntimeAlarmsForAttention(endpoint?.available ? endpoint.value : []),
    [endpoint]
  );
  const summary = useMemo(() => buildRuntimeAlarmCenterSummary(alarms), [alarms]);
  const selected = alarms.find(alarm => alarm.definitionId === selectedId) ?? alarms[0] ?? null;

  useEffect(() => {
    if (!alarms.length) {
      if (selectedId !== null) setSelectedId(null);
      return;
    }
    if (!selectedId || !alarms.some(alarm => alarm.definitionId === selectedId)) {
      setSelectedId(alarms[0].definitionId);
    }
  }, [alarms, selectedId]);

  const acknowledgeSelected = async () => {
    if (!selected || acknowledgingId) return;
    if (!canAcknowledgeRuntimeAlarm(selected)) {
      setFeedback({ tone: 'warning', message: text.ackAlreadyHandled });
      return;
    }

    ackController.current?.abort();
    const controller = new AbortController();
    ackController.current = controller;
    setAcknowledgingId(selected.definitionId);
    setFeedback(null);

    try {
      const result = await acknowledger(selected.definitionId, controller.signal);
      if (controller.signal.aborted) return;
      if (!result.ok) {
        setFeedback({ tone: 'error', message: issueMessage(result.status, text) });
        return;
      }

      const reloaded = await refresh();
      if (controller.signal.aborted) return;
      setFeedback(reloaded?.available
        ? { tone: 'success', message: text.ackSuccess }
        : { tone: 'warning', message: text.ackRefreshFailed });
    } catch (error) {
      if (controller.signal.aborted) return;
      setFeedback({ tone: 'error', message: error instanceof Error ? error.message : text.unavailable });
    } finally {
      if (ackController.current === controller) ackController.current = null;
      setAcknowledgingId(null);
    }
  };

  if (loading && !endpoint) {
    return <section className="runtime-alarm-center runtime-alarm-state" aria-label={text.title}>{text.loading}</section>;
  }

  if (!endpoint?.available) {
    return (
      <section className="runtime-alarm-center runtime-alarm-state runtime-alarm-state-error" aria-label={text.title}>
        <strong>{issueMessage(endpoint?.status, text)}</strong>
        {endpoint?.error && <span>{endpoint.error}</span>}
        <button type="button" onClick={() => void refresh()}>{text.retry}</button>
      </section>
    );
  }

  return (
    <section className="runtime-alarm-center" aria-label={text.title} aria-busy={refreshing}>
      <header className="runtime-alarm-header">
        <div>
          <span className="runtime-alarm-eyebrow">Runtime / Alarms</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <div className="runtime-alarm-refresh">
          <span aria-live="polite">{text.updated} {formatMoment(new Date().toISOString(), locale)}</span>
          <button type="button" disabled={refreshing} onClick={() => void refresh()}>{text.refresh}</button>
        </div>
      </header>

      <div className="runtime-alarm-summary" aria-label={text.activeVisible}>
        <Summary label={text.activeVisible} value={summary.total} />
        <Summary label={text.awaitingAck} value={summary.awaitingAcknowledgement} attention={summary.awaitingAcknowledgement > 0} />
        <Summary label={text.criticalHigh} value={summary.criticalAwaitingAcknowledgement + summary.highAwaitingAcknowledgement} danger={summary.criticalAwaitingAcknowledgement > 0} />
        <Summary label={text.acknowledged} value={summary.acknowledged} />
      </div>

      {feedback && <div className={`runtime-alarm-feedback ${feedback.tone}`} role={feedback.tone === 'error' ? 'alert' : 'status'}>{feedback.message}</div>}

      {alarms.length === 0 ? (
        <div className="runtime-alarm-empty">{text.empty}</div>
      ) : (
        <div className="runtime-alarm-workspace">
          <div className="runtime-alarm-list" aria-label={text.activeVisible}>
            {alarms.map(alarm => {
              const priority = normalizeRuntimeAlarmPriority(alarm.priority);
              const state = normalizeRuntimeAlarmState(alarm.state);
              return (
                <button
                  type="button"
                  className={`runtime-alarm-row tone-${runtimeAlarmTone(alarm)}${selected?.definitionId === alarm.definitionId ? ' selected' : ''}`}
                  key={alarm.definitionId}
                  aria-pressed={selected?.definitionId === alarm.definitionId}
                  onClick={() => { setSelectedId(alarm.definitionId); setFeedback(null); }}
                >
                  <span className="runtime-alarm-row-marker" aria-hidden="true" />
                  <span className="runtime-alarm-row-copy">
                    <strong>{alarm.message || alarm.name}</strong>
                    <span>{alarm.name} · {alarm.area || text.noArea}</span>
                  </span>
                  <span className="runtime-alarm-row-meta">
                    <b>{priorityLabel(priority, text)}</b>
                    <span>{stateLabel(state, text)}</span>
                    <time dateTime={alarm.activatedAt ?? alarm.lastTransition}>{formatMoment(alarm.activatedAt ?? alarm.lastTransition, locale)}</time>
                  </span>
                </button>
              );
            })}
          </div>

          <aside className="runtime-alarm-detail" aria-label={text.details}>
            {selected ? (
              <>
                <header>
                  <div>
                    <span>{selected.area || text.noArea}</span>
                    <h3>{selected.message || selected.name}</h3>
                    {selected.message && <p>{selected.name}</p>}
                  </div>
                  <span className={`runtime-alarm-badge tone-${runtimeAlarmTone(selected)}`}>
                    {priorityLabel(normalizeRuntimeAlarmPriority(selected.priority), text)}
                  </span>
                </header>

                <dl className="runtime-alarm-facts">
                  <Fact label={text.state} value={stateLabel(normalizeRuntimeAlarmState(selected.state), text)} />
                  <Fact label={text.priority} value={priorityLabel(normalizeRuntimeAlarmPriority(selected.priority), text)} />
                  <Fact label={text.type} value={String(selected.type)} />
                  <Fact label={text.area} value={selected.area || text.noArea} />
                  <Fact label={text.activated} value={formatMoment(selected.activatedAt ?? selected.lastTransition, locale)} />
                  <Fact label={text.lastTransition} value={formatMoment(selected.lastTransition, locale)} />
                  <Fact label={text.lastValue} value={formatValue(selected.lastValue)} />
                  <Fact label={text.acknowledgedAt} value={selected.acknowledgedAt ? formatMoment(selected.acknowledgedAt, locale) : '—'} />
                  <Fact label={text.acknowledgedBy} value={selected.acknowledgedBy || '—'} />
                  <Fact label={text.tagId} value={selected.tagId} mono />
                  <Fact label={text.definitionId} value={selected.definitionId} mono />
                </dl>

                <div className="runtime-alarm-actions">
                  <button
                    type="button"
                    className="runtime-alarm-ack"
                    disabled={!canAcknowledgeRuntimeAlarm(selected) || acknowledgingId === selected.definitionId}
                    onClick={() => void acknowledgeSelected()}
                  >
                    {acknowledgingId === selected.definitionId ? text.acknowledging : text.acknowledge}
                  </button>
                  {!canAcknowledgeRuntimeAlarm(selected) && <span>{text.ackAlreadyHandled}</span>}
                </div>
              </>
            ) : <div className="runtime-alarm-empty">{text.selectAlarm}</div>}
          </aside>
        </div>
      )}
    </section>
  );
}

function Summary({ label, value, attention = false, danger = false }: { label: string; value: number; attention?: boolean; danger?: boolean }) {
  return <div className={`runtime-alarm-summary-item${danger ? ' danger' : attention ? ' attention' : ''}`}><span>{label}</span><strong>{value}</strong></div>;
}

function Fact({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return <div><dt>{label}</dt><dd className={mono ? 'mono' : undefined}>{value}</dd></div>;
}

function priorityLabel(priority: ReturnType<typeof normalizeRuntimeAlarmPriority>, text: Copy): string {
  if (priority === 'critical') return text.priorityCritical;
  if (priority === 'high') return text.priorityHigh;
  if (priority === 'medium') return text.priorityMedium;
  if (priority === 'low') return text.priorityLow;
  return text.unknown;
}

function stateLabel(state: ReturnType<typeof normalizeRuntimeAlarmState>, text: Copy): string {
  if (state === 'normal') return text.stateNormal;
  if (state === 'active') return text.stateActive;
  if (state === 'acknowledged') return text.stateAcknowledged;
  if (state === 'returned') return text.stateReturned;
  if (state === 'disabled') return text.stateDisabled;
  if (state === 'shelved') return text.stateShelved;
  return text.unknown;
}

function issueMessage(status: number | undefined, text: Copy): string {
  const issue = classifyRuntimeAlarmEndpointIssue(status);
  if (issue === 'unauthenticated') return text.unauthenticated;
  if (issue === 'forbidden') return text.forbidden;
  if (issue === 'not-found') return text.notFound;
  return text.unavailable;
}

function formatMoment(value: string, locale: RuntimeAlarmCenterLocale): string {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return value || '—';
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') return String(value);
  try { return JSON.stringify(value); } catch { return String(value); }
}
