import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  acknowledgeRuntimeAlarm,
  loadActiveRuntimeAlarms,
  loadRuntimeAlarmDefinitions,
  loadRuntimeAlarmSuppressions,
  releaseRuntimeAlarmSuppression,
  setRuntimeAlarmSuppression
} from './alarmCenterApi';
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
  RuntimeAlarmDefinition,
  RuntimeAlarmCenterLocale,
  RuntimeAlarmSuppressionCause,
  RuntimeAlarmSuppressionSnapshot,
  RuntimeAlarmSuppressionTargetKind
} from './alarmCenterTypes';
import { playAlarmSoundProfile, resumeAlarmAudio, selectUnacknowledgedAlarmSounds, supportsAlarmAudio } from './alarmSounds';
import './runtime-alarm-center.css';

export type RuntimeAlarmCenterLoader = (
  signal?: AbortSignal
) => Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]>>;

export type RuntimeAlarmAcknowledger = (
  definitionId: string,
  signal?: AbortSignal
) => Promise<RuntimeAlarmAcknowledgeResult>;

export type RuntimeAlarmDefinitionLoader = (
  signal?: AbortSignal
) => Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmDefinition[]>>;

export type RuntimeAlarmCenterProps = {
  locale?: RuntimeAlarmCenterLocale;
  refreshIntervalMs?: number;
  loader?: RuntimeAlarmCenterLoader;
  acknowledger?: RuntimeAlarmAcknowledger;
  definitionsLoader?: RuntimeAlarmDefinitionLoader;
  visible?: boolean;
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
  enableSounds: string;
  muteSounds: string;
  soundsEnabled: string;
  soundsDisabled: string;
  soundUnavailable: string;
  suppressionTitle: string;
  suppressionDescription: string;
  suppressionTarget: string;
  suppressionAlarm: string;
  suppressionLocation: string;
  suppressionReason: string;
  suppressionMaintenance: string;
  suppressionStartup: string;
  suppressionTesting: string;
  suppressionOther: string;
  suppressionDuration: string;
  suppressionUntilReleased: string;
  suppressionOneHour: string;
  suppressionFourHours: string;
  suppressionEightHours: string;
  suppress: string;
  releaseSuppression: string;
  applyingSuppression: string;
  suppressionActive: string;
  suppressionInherited: string;
  suppressionNoTargets: string;
  suppressionUnavailable: string;
  suppressionSaved: string;
  suppressionReleased: string;
};

const copy: Record<RuntimeAlarmCenterLocale, Copy> = {
  'pt-BR': {
    title: 'Central de alarmes',
    description: 'Alarmes ativos do Runtime. Sons são opcionais e, quando escolhidos, repetem até o reconhecimento.',
    refresh: 'Atualizar', loading: 'Carregando alarmes...', updated: 'Atualizado', activeVisible: 'Alarmes visíveis',
    awaitingAck: 'Aguardando ACK', acknowledged: 'Reconhecidos', criticalHigh: 'Críticos/altos sem ACK', empty: 'Nenhum alarme visível.',
    selectAlarm: 'Selecione um alarme', details: 'Detalhes', priority: 'Prioridade', state: 'Estado', type: 'Tipo', area: 'Área', activated: 'Ativado em',
    lastTransition: 'Última transição', lastValue: 'Último valor', acknowledgedAt: 'Reconhecido em', acknowledgedBy: 'Reconhecido por', tagId: 'TAG ID', definitionId: 'Alarm ID',
    acknowledge: 'Reconhecer alarme', acknowledging: 'Reconhecendo...', ackSuccess: 'Reconhecimento confirmado pelo servidor e estado atualizado.',
    ackRefreshFailed: 'O servidor aceitou o reconhecimento, mas a releitura do estado não ficou disponível.', ackAlreadyHandled: 'Este alarme não está mais aguardando reconhecimento.',
    unauthenticated: 'Sessão não autenticada ou expirada.', forbidden: 'Sua sessão não possui autorização para esta operação.', notFound: 'O alarme não existe mais no Runtime ativo.',
    unavailable: 'O serviço de alarmes está indisponível no momento.', retry: 'Tentar novamente', unknown: 'Desconhecido', noArea: 'Sem área',
    priorityLow: 'Baixa', priorityMedium: 'Média', priorityHigh: 'Alta', priorityCritical: 'Crítica',
    stateNormal: 'Normal', stateActive: 'Ativo', stateAcknowledged: 'Reconhecido', stateReturned: 'Retornado', stateDisabled: 'Desabilitado', stateShelved: 'Suprimido',
    enableSounds: 'Ativar sons de alarmes', muteSounds: 'Silenciar sons', soundsEnabled: 'Sons de alarme ativados nesta sessão.', soundsDisabled: 'Sons de alarme silenciados.', soundUnavailable: 'O navegador não conseguiu ativar o áudio.',
    suppressionTitle: 'Supressão operacional', suppressionDescription: 'A supressão silencia o anúncio, mas mantém a condição e o alarme visíveis no Runtime.', suppressionTarget: 'Aplicar a', suppressionAlarm: 'Alarme', suppressionLocation: 'Área ou subárea', suppressionReason: 'Motivo', suppressionMaintenance: 'Manutenção', suppressionStartup: 'Partida', suppressionTesting: 'Teste', suppressionOther: 'Outro', suppressionDuration: 'Duração', suppressionUntilReleased: 'Até liberar manualmente', suppressionOneHour: '1 hora', suppressionFourHours: '4 horas', suppressionEightHours: '8 horas', suppress: 'Suprimir anúncio', releaseSuppression: 'Liberar supressão', applyingSuppression: 'Aplicando...', suppressionActive: 'Supressão ativa', suppressionInherited: 'Esta supressão vem de uma área ou subárea acima.', suppressionNoTargets: 'Não há alarmes ou áreas visíveis para configurar.', suppressionUnavailable: 'Não foi possível carregar a configuração de supressão.', suppressionSaved: 'Supressão aplicada. O alarme continua visível com sua condição real.', suppressionReleased: 'Supressão liberada.'
  },
  en: {
    title: 'Alarm center',
    description: 'Active Runtime alarms. Sounds are optional and repeat until acknowledgement when configured.',
    refresh: 'Refresh', loading: 'Loading alarms...', updated: 'Updated', activeVisible: 'Visible alarms',
    awaitingAck: 'Awaiting ACK', acknowledged: 'Acknowledged', criticalHigh: 'Critical/high without ACK', empty: 'No visible alarms.',
    selectAlarm: 'Select an alarm', details: 'Details', priority: 'Priority', state: 'State', type: 'Type', area: 'Area', activated: 'Activated at',
    lastTransition: 'Last transition', lastValue: 'Last value', acknowledgedAt: 'Acknowledged at', acknowledgedBy: 'Acknowledged by', tagId: 'TAG ID', definitionId: 'Alarm ID',
    acknowledge: 'Acknowledge alarm', acknowledging: 'Acknowledging...', ackSuccess: 'Acknowledgement confirmed by the server and state refreshed.',
    ackRefreshFailed: 'The server accepted the acknowledgement, but the authoritative state could not be reloaded.', ackAlreadyHandled: 'This alarm is no longer awaiting acknowledgement.',
    unauthenticated: 'Session is unauthenticated or expired.', forbidden: 'Your session is not authorized for this operation.', notFound: 'The alarm no longer exists in the active Runtime.',
    unavailable: 'The alarm service is currently unavailable.', retry: 'Retry', unknown: 'Unknown', noArea: 'No area',
    priorityLow: 'Low', priorityMedium: 'Medium', priorityHigh: 'High', priorityCritical: 'Critical',
    stateNormal: 'Normal', stateActive: 'Active', stateAcknowledged: 'Acknowledged', stateReturned: 'Returned', stateDisabled: 'Disabled', stateShelved: 'Shelved',
    enableSounds: 'Enable alarm sounds', muteSounds: 'Mute sounds', soundsEnabled: 'Alarm sounds enabled for this session.', soundsDisabled: 'Alarm sounds are muted.', soundUnavailable: 'The browser could not enable audio.',
    suppressionTitle: 'Operational suppression', suppressionDescription: 'Suppression silences annunciation while keeping the condition and alarm visible in Runtime.', suppressionTarget: 'Apply to', suppressionAlarm: 'Alarm', suppressionLocation: 'Area or subarea', suppressionReason: 'Reason', suppressionMaintenance: 'Maintenance', suppressionStartup: 'Startup', suppressionTesting: 'Test', suppressionOther: 'Other', suppressionDuration: 'Duration', suppressionUntilReleased: 'Until manually released', suppressionOneHour: '1 hour', suppressionFourHours: '4 hours', suppressionEightHours: '8 hours', suppress: 'Suppress annunciation', releaseSuppression: 'Release suppression', applyingSuppression: 'Applying...', suppressionActive: 'Suppression active', suppressionInherited: 'This suppression comes from a parent area or subarea.', suppressionNoTargets: 'There are no visible alarms or areas to configure.', suppressionUnavailable: 'Could not load suppression settings.', suppressionSaved: 'Suppression applied. The alarm remains visible with its actual condition.', suppressionReleased: 'Suppression released.'
  },
  es: {
    title: 'Centro de alarmas',
    description: 'Alarmas activas del Runtime. Los sonidos son opcionales y se repiten hasta el reconocimiento cuando están configurados.',
    refresh: 'Actualizar', loading: 'Cargando alarmas...', updated: 'Actualizado', activeVisible: 'Alarmas visibles',
    awaitingAck: 'Esperando ACK', acknowledged: 'Reconocidas', criticalHigh: 'Críticas/altas sin ACK', empty: 'No hay alarmas visibles.',
    selectAlarm: 'Seleccione una alarma', details: 'Detalles', priority: 'Prioridad', state: 'Estado', type: 'Tipo', area: 'Área', activated: 'Activada en',
    lastTransition: 'Última transición', lastValue: 'Último valor', acknowledgedAt: 'Reconocida en', acknowledgedBy: 'Reconocida por', tagId: 'TAG ID', definitionId: 'Alarm ID',
    acknowledge: 'Reconocer alarma', acknowledging: 'Reconociendo...', ackSuccess: 'Reconocimiento confirmado por el servidor y estado actualizado.',
    ackRefreshFailed: 'El servidor aceptó el reconocimiento, pero no fue posible volver a leer el estado autoritativo.', ackAlreadyHandled: 'Esta alarma ya no espera reconocimiento.',
    unauthenticated: 'Sesión no autenticada o expirada.', forbidden: 'Su sesión no tiene autorización para esta operación.', notFound: 'La alarma ya no existe en el Runtime activo.',
    unavailable: 'El servicio de alarmas no está disponible en este momento.', retry: 'Reintentar', unknown: 'Desconocido', noArea: 'Sin área',
    priorityLow: 'Baja', priorityMedium: 'Media', priorityHigh: 'Alta', priorityCritical: 'Crítica',
    stateNormal: 'Normal', stateActive: 'Activa', stateAcknowledged: 'Reconocida', stateReturned: 'Retornada', stateDisabled: 'Deshabilitada', stateShelved: 'Suprimida',
    enableSounds: 'Activar sonidos de alarmas', muteSounds: 'Silenciar sonidos', soundsEnabled: 'Sonidos de alarma activados en esta sesión.', soundsDisabled: 'Sonidos de alarma silenciados.', soundUnavailable: 'No se pudo activar el audio del navegador.',
    suppressionTitle: 'Supresión operativa', suppressionDescription: 'La supresión silencia el anuncio, pero mantiene la condición y la alarma visibles en Runtime.', suppressionTarget: 'Aplicar a', suppressionAlarm: 'Alarma', suppressionLocation: 'Área o subárea', suppressionReason: 'Motivo', suppressionMaintenance: 'Mantenimiento', suppressionStartup: 'Arranque', suppressionTesting: 'Prueba', suppressionOther: 'Otro', suppressionDuration: 'Duración', suppressionUntilReleased: 'Hasta liberar manualmente', suppressionOneHour: '1 hora', suppressionFourHours: '4 horas', suppressionEightHours: '8 horas', suppress: 'Suprimir anuncio', releaseSuppression: 'Liberar supresión', applyingSuppression: 'Aplicando...', suppressionActive: 'Supresión activa', suppressionInherited: 'Esta supresión viene de un área o subárea superior.', suppressionNoTargets: 'No hay alarmas o áreas visibles para configurar.', suppressionUnavailable: 'No se pudo cargar la configuración de supresión.', suppressionSaved: 'Supresión aplicada. La alarma sigue visible con su condición real.', suppressionReleased: 'Supresión liberada.'
  }
};

type AckFeedback = { tone: 'success' | 'warning' | 'error'; message: string };

export function RuntimeAlarmCenter({
  locale = 'pt-BR',
  refreshIntervalMs = 4000,
  loader = loadActiveRuntimeAlarms,
  acknowledger = acknowledgeRuntimeAlarm,
  definitionsLoader = loadRuntimeAlarmDefinitions,
  visible = true
}: RuntimeAlarmCenterProps) {
  const text = copy[locale];
  const [endpoint, setEndpoint] = useState<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]> | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [acknowledgingId, setAcknowledgingId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<AckFeedback | null>(null);
  const [definitionEndpoint, setDefinitionEndpoint] = useState<RuntimeAlarmCenterEndpoint<RuntimeAlarmDefinition[]> | null>(null);
  const [suppressionEndpoint, setSuppressionEndpoint] = useState<RuntimeAlarmCenterEndpoint<RuntimeAlarmSuppressionSnapshot> | null>(null);
  const [suppressionTargetKind, setSuppressionTargetKind] = useState<RuntimeAlarmSuppressionTargetKind>('alarm');
  const [suppressionTargetId, setSuppressionTargetId] = useState('');
  const [suppressionReason, setSuppressionReason] = useState('maintenance');
  const [suppressionDuration, setSuppressionDuration] = useState('until-released');
  const [suppressionBusy, setSuppressionBusy] = useState(false);
  const [alarmSoundsEnabled, setAlarmSoundsEnabled] = useState(false);
  const loadController = useRef<AbortController | null>(null);
  const definitionsController = useRef<AbortController | null>(null);
  const suppressionController = useRef<AbortController | null>(null);
  const ackController = useRef<AbortController | null>(null);
  const soundQueueRef = useRef<ReturnType<typeof selectUnacknowledgedAlarmSounds>>([]);

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

  const refreshDefinitions = useCallback(async () => {
    definitionsController.current?.abort();
    const controller = new AbortController();
    definitionsController.current = controller;
    try {
      const next = await definitionsLoader(controller.signal);
      if (!controller.signal.aborted) setDefinitionEndpoint(next);
    } catch (error) {
      if (!controller.signal.aborted) setDefinitionEndpoint({ available: false, error: error instanceof Error ? error.message : String(error) });
    } finally {
      if (definitionsController.current === controller) definitionsController.current = null;
    }
  }, [definitionsLoader]);

  const refreshSuppressions = useCallback(async () => {
    suppressionController.current?.abort();
    const controller = new AbortController();
    suppressionController.current = controller;
    try {
      const next = await loadRuntimeAlarmSuppressions(controller.signal);
      if (!controller.signal.aborted) setSuppressionEndpoint(next);
      return next;
    } catch (error) {
      if (controller.signal.aborted) return null;
      const next: RuntimeAlarmCenterEndpoint<RuntimeAlarmSuppressionSnapshot> = {
        available: false,
        error: error instanceof Error ? error.message : String(error)
      };
      setSuppressionEndpoint(next);
      return next;
    } finally {
      if (suppressionController.current === controller) suppressionController.current = null;
    }
  }, []);

  useEffect(() => {
    if (!visible && !alarmSoundsEnabled) return;
    void refresh();
    void refreshDefinitions();
    void refreshSuppressions();
    const timer = refreshIntervalMs > 0
      ? window.setInterval(() => void refresh(), refreshIntervalMs)
      : undefined;
    const definitionsTimer = window.setInterval(() => void refreshDefinitions(), Math.max(30000, refreshIntervalMs * 8));
    const suppressionsTimer = window.setInterval(() => void refreshSuppressions(), Math.max(10000, refreshIntervalMs * 3));

    return () => {
      if (timer !== undefined) window.clearInterval(timer);
      window.clearInterval(definitionsTimer);
      window.clearInterval(suppressionsTimer);
      loadController.current?.abort();
      definitionsController.current?.abort();
      suppressionController.current?.abort();
      ackController.current?.abort();
    };
  }, [refresh, refreshDefinitions, refreshSuppressions, refreshIntervalMs, visible, alarmSoundsEnabled]);

  const alarms = useMemo(
    () => sortRuntimeAlarmsForAttention(endpoint?.available ? endpoint.value : []),
    [endpoint]
  );
  const summary = useMemo(() => buildRuntimeAlarmCenterSummary(alarms), [alarms]);
  const selected = alarms.find(alarm => alarm.definitionId === selectedId) ?? alarms[0] ?? null;
  const definitions = definitionEndpoint?.available ? definitionEndpoint.value : [];
  const suppressionSnapshot = suppressionEndpoint?.available ? suppressionEndpoint.value : null;
  const soundQueue = useMemo(() => selectUnacknowledgedAlarmSounds(alarms, definitions), [alarms, definitions]);
  const soundQueueSignature = soundQueue.map(item => `${item.definitionId}:${item.profile}`).join('|');
  soundQueueRef.current = soundQueue;

  useEffect(() => {
    if (!alarmSoundsEnabled || soundQueue.length === 0) return undefined;
    let cursor = 0;
    let timer = 0;
    const soundNextAlarm = () => {
      const queue = soundQueueRef.current;
      if (queue.length === 0) return;
      const item = queue[cursor % queue.length];
      cursor++;
      void playAlarmSoundProfile(item.profile).catch(() => {
        setAlarmSoundsEnabled(false);
        setFeedback({ tone: 'warning', message: text.soundUnavailable });
      });
      timer = window.setTimeout(soundNextAlarm, 3500);
    };
    soundNextAlarm();
    return () => window.clearTimeout(timer);
  }, [alarmSoundsEnabled, soundQueueSignature, text.soundUnavailable, soundQueue.length]);

  useEffect(() => {
    if (!alarms.length) {
      if (selectedId !== null) setSelectedId(null);
      return;
    }
    if (!selectedId || !alarms.some(alarm => alarm.definitionId === selectedId)) {
      setSelectedId(alarms[0].definitionId);
    }
  }, [alarms, selectedId]);

  useEffect(() => {
    const choices = suppressionTargetKind === 'alarm'
      ? suppressionSnapshot?.alarms.map(alarm => ({ id: alarm.definitionId })) ?? []
      : suppressionSnapshot?.locations.map(location => ({ id: location.id })) ?? [];
    if (choices.some(choice => choice.id === suppressionTargetId)) return;
    const preferred = suppressionTargetKind === 'alarm'
      ? selected?.definitionId
      : undefined;
    setSuppressionTargetId(
      (preferred && choices.some(choice => choice.id === preferred) ? preferred : choices[0]?.id) ?? '');
  }, [selected?.definitionId, suppressionSnapshot, suppressionTargetId, suppressionTargetKind]);

  useEffect(() => () => {
    loadController.current?.abort();
    definitionsController.current?.abort();
    suppressionController.current?.abort();
    ackController.current?.abort();
  }, []);

  const suppressionTargetName = suppressionTargetKind === 'alarm'
    ? suppressionSnapshot?.alarms.find(alarm => alarm.definitionId === suppressionTargetId)?.name ?? ''
    : suppressionSnapshot?.locations.find(location => location.id === suppressionTargetId)?.path ?? '';
  const targetCauses = useMemo(() => {
    if (!suppressionSnapshot || !suppressionTargetId) return [] as RuntimeAlarmSuppressionCause[];
    const alarmsForTarget = suppressionTargetKind === 'alarm'
      ? suppressionSnapshot.alarms.filter(alarm => alarm.definitionId === suppressionTargetId)
      : suppressionSnapshot.alarms;
    const unique = new Map<string, RuntimeAlarmSuppressionCause>();
    for (const alarm of alarmsForTarget) {
      for (const cause of alarm.suppressionCauses ?? []) {
        if (cause.targetKind === suppressionTargetKind && cause.targetId === suppressionTargetId)
          unique.set(`${cause.targetKind}:${cause.targetId}`, cause);
      }
    }
    return [...unique.values()];
  }, [suppressionSnapshot, suppressionTargetId, suppressionTargetKind]);

  const selectedSuppressionCauses = selected
    ? suppressionSnapshot?.alarms.find(alarm => alarm.definitionId === selected.definitionId)?.suppressionCauses ?? selected.suppressionCauses ?? []
    : [];

  const changeSuppressionTargetKind = (value: string) => {
    const kind = value === 'location' ? 'location' : 'alarm';
    setSuppressionTargetKind(kind);
    const nextId = kind === 'alarm'
      ? suppressionSnapshot?.alarms.find(alarm => alarm.definitionId === selected?.definitionId)?.definitionId
        ?? suppressionSnapshot?.alarms[0]?.definitionId
      : suppressionSnapshot?.locations[0]?.id;
    setSuppressionTargetId(nextId ?? '');
  };

  const applySuppression = async () => {
    if (!suppressionTargetId || !suppressionTargetName || suppressionBusy) return;
    const durationMs: Record<string, number | null> = {
      'until-released': null,
      '1-hour': 60 * 60 * 1000,
      '4-hours': 4 * 60 * 60 * 1000,
      '8-hours': 8 * 60 * 60 * 1000
    };
    const duration = durationMs[suppressionDuration];
    const expiresAtUtc = duration == null ? null : new Date(Date.now() + duration).toISOString();
    setSuppressionBusy(true);
    setFeedback(null);
    try {
      const result = await setRuntimeAlarmSuppression(
        suppressionTargetKind,
        suppressionTargetId,
        suppressionReason,
        expiresAtUtc);
      if (!result.ok) {
        setFeedback({ tone: 'error', message: result.status ? issueMessage(result.status, text) : result.error || text.unavailable });
        return;
      }
      await Promise.all([refreshSuppressions(), refresh()]);
      setFeedback({ tone: 'success', message: text.suppressionSaved });
    } catch (error) {
      setFeedback({ tone: 'error', message: error instanceof Error ? error.message : text.unavailable });
    } finally {
      setSuppressionBusy(false);
    }
  };

  const releaseSuppression = async (kind: RuntimeAlarmSuppressionTargetKind, targetId: string) => {
    if (suppressionBusy) return;
    setSuppressionBusy(true);
    setFeedback(null);
    try {
      const result = await releaseRuntimeAlarmSuppression(kind, targetId);
      if (!result.ok) {
        setFeedback({ tone: 'error', message: result.status ? issueMessage(result.status, text) : result.error || text.unavailable });
        return;
      }
      await Promise.all([refreshSuppressions(), refresh()]);
      setFeedback({ tone: 'success', message: text.suppressionReleased });
    } catch (error) {
      setFeedback({ tone: 'error', message: error instanceof Error ? error.message : text.unavailable });
    } finally {
      setSuppressionBusy(false);
    }
  };

  const toggleAlarmSounds = async () => {
    if (alarmSoundsEnabled) {
      setAlarmSoundsEnabled(false);
      setFeedback({ tone: 'success', message: text.soundsDisabled });
      return;
    }
    try {
      await resumeAlarmAudio();
      setAlarmSoundsEnabled(true);
      setFeedback({ tone: 'success', message: text.soundsEnabled });
    } catch {
      setFeedback({ tone: 'warning', message: text.soundUnavailable });
    }
  };

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
          <h2>{text.title}</h2>
          <a href="/help?topic=alarms.runtime-operation">{locale === 'pt-BR' ? 'Ajuda sobre estados e reconhecimento' : locale === 'es' ? 'Ayuda sobre estados y reconocimiento' : 'Help with states and acknowledgement'}</a>
        </div>
        <div className="runtime-alarm-refresh">
          <button type="button" aria-pressed={alarmSoundsEnabled} disabled={!supportsAlarmAudio()} onClick={() => void toggleAlarmSounds()}>{alarmSoundsEnabled ? text.muteSounds : text.enableSounds}</button>
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

      <section className="runtime-alarm-suppression" aria-label={text.suppressionTitle}>
        <header>
          <div><h3>{text.suppressionTitle}</h3><p>{text.suppressionDescription}</p></div>
          <button type="button" disabled={suppressionBusy} onClick={() => void refreshSuppressions()}>{text.refresh}</button>
        </header>
        {!suppressionSnapshot ? (
          <p className="runtime-alarm-suppression-unavailable" role="status">{text.suppressionUnavailable}</p>
        ) : suppressionSnapshot.alarms.length === 0 && suppressionSnapshot.locations.length === 0 ? (
          <p className="runtime-alarm-suppression-unavailable">{text.suppressionNoTargets}</p>
        ) : (
          <div className="runtime-alarm-suppression-form">
            <label><span>{text.suppressionTarget}</span><select value={suppressionTargetKind} onChange={event => changeSuppressionTargetKind(event.target.value)}>
              <option value="alarm">{text.suppressionAlarm}</option>
              <option value="location" disabled={suppressionSnapshot.locations.length === 0}>{text.suppressionLocation}</option>
            </select></label>
            <label><span>{suppressionTargetKind === 'alarm' ? text.suppressionAlarm : text.suppressionLocation}</span><select value={suppressionTargetId} onChange={event => setSuppressionTargetId(event.target.value)}>
              {suppressionTargetKind === 'alarm'
                ? suppressionSnapshot.alarms.map(alarm => <option key={alarm.definitionId} value={alarm.definitionId}>{alarm.name}{alarm.locationPath ? ` · ${alarm.locationPath}` : ''}</option>)
                : suppressionSnapshot.locations.map(location => <option key={location.id} value={location.id}>{location.path} · {location.alarmCount}</option>)}
            </select></label>
            <label><span>{text.suppressionReason}</span><select value={suppressionReason} onChange={event => setSuppressionReason(event.target.value)}>
              <option value="maintenance">{text.suppressionMaintenance}</option>
              <option value="startup">{text.suppressionStartup}</option>
              <option value="testing">{text.suppressionTesting}</option>
              <option value="other">{text.suppressionOther}</option>
            </select></label>
            <label><span>{text.suppressionDuration}</span><select value={suppressionDuration} onChange={event => setSuppressionDuration(event.target.value)}>
              <option value="until-released">{text.suppressionUntilReleased}</option>
              <option value="1-hour">{text.suppressionOneHour}</option>
              <option value="4-hours">{text.suppressionFourHours}</option>
              <option value="8-hours">{text.suppressionEightHours}</option>
            </select></label>
            {targetCauses.length > 0 ? (
              <div className="runtime-alarm-suppression-current" role="status">
                <strong>{text.suppressionActive}: {suppressionTargetName}</strong>
                {targetCauses.map(cause => <span key={`${cause.targetKind}:${cause.targetId}`}>{suppressionReasonLabel(cause.reasonCode, text)} · {cause.expiresAtUtc ? formatMoment(cause.expiresAtUtc, locale) : text.suppressionUntilReleased}</span>)}
                <button type="button" disabled={suppressionBusy} onClick={() => void releaseSuppression(suppressionTargetKind, suppressionTargetId)}>{suppressionBusy ? text.applyingSuppression : text.releaseSuppression}</button>
              </div>
            ) : (
              <button type="button" className="runtime-alarm-suppress" disabled={suppressionBusy || !suppressionTargetId || !suppressionTargetName} onClick={() => void applySuppression()}>
                {suppressionBusy ? text.applyingSuppression : text.suppress}
              </button>
            )}
          </div>
        )}
      </section>

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
                    {alarm.isSuppressed && <span className="runtime-alarm-suppressed-label">{text.suppressionActive} · {stateLabel(state, text)}</span>}
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

                {selected.isSuppressed && <div className="runtime-alarm-suppressed-detail" role="status">
                  <strong>{text.suppressionActive} · {stateLabel(normalizeRuntimeAlarmState(selected.state), text)}</strong>
                  {selectedSuppressionCauses.map(cause => <div key={`${cause.targetKind}:${cause.targetId}`}>
                    <span>{cause.targetKind === 'location' ? cause.targetPath : cause.targetName} · {suppressionReasonLabel(cause.reasonCode, text)}</span>
                    {cause.targetKind === 'location' && <small>{text.suppressionInherited}</small>}
                    <small>{cause.expiresAtUtc ? formatMoment(cause.expiresAtUtc, locale) : text.suppressionUntilReleased}</small>
                    <button type="button" disabled={suppressionBusy} onClick={() => void releaseSuppression(cause.targetKind, cause.targetId)}>{text.releaseSuppression}</button>
                  </div>)}
                </div>}

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

function suppressionReasonLabel(reason: string, text: Copy): string {
  if (reason === 'maintenance') return text.suppressionMaintenance;
  if (reason === 'startup') return text.suppressionStartup;
  if (reason === 'testing') return text.suppressionTesting;
  return text.suppressionOther;
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
