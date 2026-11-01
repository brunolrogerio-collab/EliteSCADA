import type {
  RuntimeAlarmAcknowledgeResult,
  RuntimeAlarmCenterEndpoint,
  RuntimeAlarmCenterItem,
  RuntimeAlarmDefinition,
  RuntimeAlarmSuppressionCause,
  RuntimeAlarmSuppressionResult,
  RuntimeAlarmSuppressionSnapshot,
  RuntimeAlarmSuppressionTargetKind
} from './alarmCenterTypes';
import { admitInteractiveRuntimeSession } from './runtimeSessionAdmissionApi';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export async function loadRuntimeAlarms(
  activeOnly: boolean,
  signal?: AbortSignal
): Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]>> {
  return loadAlarmEndpoint<RuntimeAlarmCenterItem[]>(
    `${API}/api/alarms?activeOnly=${activeOnly ? 'true' : 'false'}`,
    signal
  );
}

export async function loadActiveRuntimeAlarms(
  signal?: AbortSignal
): Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]>> {
  return loadRuntimeAlarms(true, signal);
}

export async function loadRuntimeAlarmDefinitions(
  signal?: AbortSignal
): Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmDefinition[]>> {
  return loadAlarmEndpoint<RuntimeAlarmDefinition[]>(`${API}/api/alarms/definitions`, signal);
}

export async function loadRuntimeAlarmSuppressions(
  signal?: AbortSignal
): Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmSuppressionSnapshot>> {
  const result = await loadAlarmEndpoint<AlarmSuppressionRuntimeWireSnapshot>(`${API}/api/alarms/suppressions`, signal);
  if (!result.available) return result;

  try {
    return { available: true, value: normalizeAlarmSuppressionSnapshot(result.value) };
  } catch (error) {
    return {
      available: false,
      error: error instanceof Error ? error.message : String(error)
    };
  }
}

type AlarmSuppressionRuntimeWireSnapshot = Omit<RuntimeAlarmSuppressionSnapshot, 'alarms'> & {
  alarms: Array<{
    alarmId: string;
    alarmName: string;
    area?: string | null;
    locationId?: string | null;
    locationPath?: string | null;
    isSuppressed: boolean;
    causes: Array<Omit<RuntimeAlarmSuppressionCause, 'targetKind'> & { targetKind: number | string }>;
  }>;
};

function normalizeAlarmSuppressionSnapshot(wire: AlarmSuppressionRuntimeWireSnapshot): RuntimeAlarmSuppressionSnapshot {
  if (!Array.isArray(wire.alarms) || !Array.isArray(wire.locations)) {
    throw new Error('The alarm suppression service returned an invalid snapshot.');
  }

  return {
    ...wire,
    alarms: wire.alarms.map(alarm => ({
      definitionId: alarm.alarmId,
      name: alarm.alarmName,
      area: alarm.area ?? null,
      locationId: alarm.locationId ?? null,
      locationPath: alarm.locationPath ?? null,
      isSuppressed: alarm.isSuppressed,
      suppressionCauses: alarm.causes.map(cause => ({
        ...cause,
        targetKind: normalizeSuppressionTargetKind(cause.targetKind)
      }))
    }))
  };
}

function normalizeSuppressionTargetKind(value: number | string): RuntimeAlarmSuppressionCause['targetKind'] {
  if (value === 1 || (typeof value === 'string' && value.trim().toLowerCase() === 'alarm')) return 'alarm';
  if (value === 2 || (typeof value === 'string' && value.trim().toLowerCase() === 'location')) return 'location';
  throw new Error('The alarm suppression service returned an unknown target kind.');
}

export async function setRuntimeAlarmSuppression(
  targetKind: RuntimeAlarmSuppressionTargetKind,
  targetId: string,
  reasonCode: string,
  expiresAtUtc: string | null,
  signal?: AbortSignal
): Promise<RuntimeAlarmSuppressionResult> {
  return mutateRuntimeAlarmSuppression(
    `${API}/api/alarms/suppressions/${encodeURIComponent(targetKind)}/${encodeURIComponent(targetId)}`,
    'PUT',
    { reasonCode, expiresAtUtc },
    signal);
}

export async function releaseRuntimeAlarmSuppression(
  targetKind: RuntimeAlarmSuppressionTargetKind,
  targetId: string,
  signal?: AbortSignal
): Promise<RuntimeAlarmSuppressionResult> {
  return mutateRuntimeAlarmSuppression(
    `${API}/api/alarms/suppressions/${encodeURIComponent(targetKind)}/${encodeURIComponent(targetId)}`,
    'DELETE',
    undefined,
    signal);
}

export async function acknowledgeRuntimeAlarm(
  definitionId: string,
  signal?: AbortSignal
): Promise<RuntimeAlarmAcknowledgeResult> {
  if (!definitionId.trim()) return { ok: false, error: 'Alarm definition ID is required.' };

  try {
    const leaseHeaders = await admitInteractiveRuntimeSession();
    const response = await fetch(`${API}/api/alarms/${encodeURIComponent(definitionId)}/ack`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        accept: 'application/json',
        'content-type': 'application/json',
        ...leaseHeaders
      },
      body: '{}',
      signal
    });

    if (!response.ok) {
      return {
        ok: false,
        status: response.status,
        error: `${response.status} ${response.statusText}`.trim()
      };
    }

    return { ok: true, status: response.status };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    return {
      ok: false,
      error: error instanceof Error ? error.message : String(error)
    };
  }
}

async function loadAlarmEndpoint<T>(
  url: string,
  signal?: AbortSignal
): Promise<RuntimeAlarmCenterEndpoint<T>> {
  try {
    const response = await fetch(url, {
      headers: { accept: 'application/json' },
      signal
    });

    if (!response.ok) {
      return {
        available: false,
        status: response.status,
        error: `${response.status} ${response.statusText}`.trim()
      };
    }

    return {
      available: true,
      value: await response.json() as T
    };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    return {
      available: false,
      error: error instanceof Error ? error.message : String(error)
    };
  }
}

async function mutateRuntimeAlarmSuppression(
  url: string,
  method: 'PUT' | 'DELETE',
  body: { reasonCode: string; expiresAtUtc: string | null } | undefined,
  signal?: AbortSignal
): Promise<RuntimeAlarmSuppressionResult> {
  try {
    const leaseHeaders = await admitInteractiveRuntimeSession();
    const response = await fetch(url, {
      method,
      credentials: 'same-origin',
      headers: {
        accept: 'application/json',
        ...(body ? { 'content-type': 'application/json' } : {}),
        ...leaseHeaders
      },
      ...(body ? { body: JSON.stringify(body) } : {}),
      signal
    });

    if (!response.ok) {
      return { ok: false, status: response.status, error: await responseError(response) };
    }

    const result = await response.json() as { changed?: boolean };
    return { ok: true, status: response.status, changed: result.changed === true };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    return { ok: false, error: error instanceof Error ? error.message : String(error) };
  }
}

async function responseError(response: Response): Promise<string> {
  const fallback = `${response.status} ${response.statusText}`.trim();
  try {
    const value = await response.json() as { error?: unknown; detail?: unknown; title?: unknown };
    const message = value.error ?? value.detail ?? value.title;
    return typeof message === 'string' && message.trim() ? message : fallback;
  } catch {
    return fallback;
  }
}
