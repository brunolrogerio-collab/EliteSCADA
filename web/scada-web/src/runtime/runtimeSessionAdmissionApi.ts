const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export const RUNTIME_SESSION_HEADER = 'X-EliteSCADA-Runtime-Session';
export const RUNTIME_CLIENT_INSTANCE_HEADER = 'X-EliteSCADA-Runtime-Client-Instance';

const CLIENT_INSTANCE_STORAGE_KEY = 'elitescada.runtime.client-instance-id';
let ephemeralClientInstanceId: string | undefined;

export type RuntimeSessionFetch = (
  input: RequestInfo | URL,
  init?: RequestInit
) => Promise<Response>;

export class RuntimeSessionAdmissionError extends Error {
  constructor(
    message: string,
    public readonly status?: number
  ) {
    super(message);
    this.name = 'RuntimeSessionAdmissionError';
  }
}

type RuntimeSessionAdmission = {
  sessionId?: unknown;
  clientInstanceId?: unknown;
};

/**
 * Returns a stable logical Runtime client identity for this browser tab. It is deliberately
 * distinct from user identity: the server owns the resulting subject/client lease.
 */
export function getRuntimeClientInstanceId(): string {
  try {
    const stored = window.sessionStorage.getItem(CLIENT_INSTANCE_STORAGE_KEY)?.trim();
    if (stored) return stored;

    const created = createClientInstanceId();
    window.sessionStorage.setItem(CLIENT_INSTANCE_STORAGE_KEY, created);
    return created;
  } catch {
    return ephemeralClientInstanceId ??= createClientInstanceId();
  }
}

/**
 * Acquires the server-owned admission before a mutable Runtime operation. No quota is
 * reserved here; the returned headers bind the operation to the current logical lease.
 */
export async function admitInteractiveRuntimeSession(
  fetcher: RuntimeSessionFetch = fetch,
  clientInstanceId = getRuntimeClientInstanceId()
): Promise<Record<string, string>> {
  let response: Response;
  try {
    response = await fetcher(`${API}/api/runtime/sessions`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        accept: 'application/json',
        'content-type': 'application/json; charset=utf-8'
      },
      body: JSON.stringify({ clientInstanceId, connectionClass: 'interactive' })
    });
  } catch (reason) {
    throw new RuntimeSessionAdmissionError(reason instanceof Error ? reason.message : String(reason));
  }

  if (!response.ok) {
    const body = await response.text();
    throw new RuntimeSessionAdmissionError(
      body || `${response.status} ${response.statusText}`.trim(),
      response.status
    );
  }

  let admission: RuntimeSessionAdmission;
  try {
    admission = await response.json() as RuntimeSessionAdmission;
  } catch {
    throw new RuntimeSessionAdmissionError('Runtime session admission returned an invalid response.', response.status);
  }

  if (typeof admission.sessionId !== 'string' || !admission.sessionId.trim() ||
      typeof admission.clientInstanceId !== 'string' || admission.clientInstanceId !== clientInstanceId) {
    throw new RuntimeSessionAdmissionError('Runtime session admission did not bind the requested client instance.', response.status);
  }

  return {
    [RUNTIME_SESSION_HEADER]: admission.sessionId,
    [RUNTIME_CLIENT_INSTANCE_HEADER]: clientInstanceId
  };
}

function createClientInstanceId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `runtime-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}
