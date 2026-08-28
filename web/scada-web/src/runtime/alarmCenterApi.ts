import type {
  RuntimeAlarmAcknowledgeResult,
  RuntimeAlarmCenterEndpoint,
  RuntimeAlarmCenterItem
} from './alarmCenterTypes';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export async function loadActiveRuntimeAlarms(
  signal?: AbortSignal
): Promise<RuntimeAlarmCenterEndpoint<RuntimeAlarmCenterItem[]>> {
  try {
    const response = await fetch(`${API}/api/alarms?activeOnly=true`, {
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
      value: await response.json() as RuntimeAlarmCenterItem[]
    };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    return {
      available: false,
      error: error instanceof Error ? error.message : String(error)
    };
  }
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
