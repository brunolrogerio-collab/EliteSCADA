import type {
  GatewayRuntimeDiagnostic,
  RuntimeAlarm,
  RuntimeDiagnosticsPayload,
  RuntimeOperationsEndpoint,
  RuntimeOperationsSnapshot
} from './operationsTypes';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

async function requestEndpoint<T>(path: string, signal?: AbortSignal): Promise<RuntimeOperationsEndpoint<T>> {
  try {
    const response = await fetch(`${API}${path}`, {
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

export async function loadRuntimeOperationsSnapshot(signal?: AbortSignal): Promise<RuntimeOperationsSnapshot> {
  const [diagnostics, gateways, alarms] = await Promise.all([
    requestEndpoint<RuntimeDiagnosticsPayload>('/api/diagnostics/runtime', signal),
    requestEndpoint<GatewayRuntimeDiagnostic[]>('/api/gateway/diagnostics', signal),
    requestEndpoint<RuntimeAlarm[]>('/api/alarms?activeOnly=true', signal)
  ]);

  return {
    capturedAt: new Date().toISOString(),
    diagnostics,
    gateways,
    alarms
  };
}
