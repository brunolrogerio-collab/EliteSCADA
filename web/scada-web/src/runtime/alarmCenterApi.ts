import type {
  RuntimeAlarmAcknowledgeResult,
  RuntimeAlarmCenterEndpoint,
  RuntimeAlarmCenterItem,
  RuntimeAlarmDefinition
} from './alarmCenterTypes';

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

export async function acknowledgeRuntimeAlarm(
  definitionId: string,
  signal?: AbortSignal
): Promise<RuntimeAlarmAcknowledgeResult> {
  if (!definitionId.trim()) return { ok: false, error: 'Alarm definition ID is required.' };

  try {
    const response = await fetch(`${API}/api/alarms/${encodeURIComponent(definitionId)}/ack`, {
      method: 'POST',
      headers: {
        accept: 'application/json',
        'content-type': 'application/json'
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
