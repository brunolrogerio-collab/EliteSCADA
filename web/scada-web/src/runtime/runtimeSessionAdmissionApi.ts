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
    public readonly status?: number,
    public readonly outcome?: RuntimeSessionAdmissionOutcome,
    public readonly capacityReasonCode?: string | null
  ) {
    super(message);
    this.name = 'RuntimeSessionAdmissionError';
  }
}

export type RuntimeSessionConnectionClass = 'interactive' | 'viewOnly';

export type RuntimeSessionAdmissionOutcome = {
  sessionId: string;
  clientInstanceId: string;
  headers: Record<string, string>;
  requestedClass: RuntimeSessionConnectionClass | null;
  grantedClass: RuntimeSessionConnectionClass | null;
  admissionReasonCode: string | null;
  capacityReasonCode: string | null;
};

type RuntimeSessionAdmission = {
  sessionId?: unknown;
  clientInstanceId?: unknown;
  requestedClass?: unknown;
  grantedClass?: unknown;
  admissionReasonCode?: unknown;
  capacityReasonCode?: unknown;
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
  const outcome = await admitRuntimeSession('interactive', fetcher, clientInstanceId);
  if (outcome.grantedClass && outcome.grantedClass !== 'interactive') {
    try {
      await releaseRuntimeSession(outcome, fetcher);
    } catch (reason) {
      throw new RuntimeSessionAdmissionError(
        `${describeInteractiveFallback(outcome)} The fallback lease could not be released: ${reason instanceof Error ? reason.message : String(reason)}`,
        undefined,
        outcome
      );
    }
    throw new RuntimeSessionAdmissionError(
      describeInteractiveFallback(outcome),
      undefined,
      outcome
    );
  }

  return outcome.headers;
}

/**
 * Preserves the server-calculated admission decision. Callers that require mutation
 * must use admitInteractiveRuntimeSession so a viewOnly fallback is never mistaken
 * for an interactive grant.
 */
export async function admitRuntimeSession(
  requestedClass: RuntimeSessionConnectionClass,
  fetcher: RuntimeSessionFetch = fetch,
  clientInstanceId = getRuntimeClientInstanceId()
): Promise<RuntimeSessionAdmissionOutcome> {
  let response: Response;
  try {
    response = await fetcher(`${API}/api/runtime/sessions`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        accept: 'application/json',
        'content-type': 'application/json; charset=utf-8'
      },
      body: JSON.stringify({ clientInstanceId, connectionClass: requestedClass })
    });
  } catch (reason) {
    throw new RuntimeSessionAdmissionError(reason instanceof Error ? reason.message : String(reason));
  }

  if (!response.ok) {
    const body = await response.text();
    const failure = parseAdmissionFailure(body);
    throw new RuntimeSessionAdmissionError(
      failure.message || `${response.status} ${response.statusText}`.trim(),
      response.status,
      undefined,
      failure.capacityReasonCode
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
    sessionId: admission.sessionId,
    clientInstanceId,
    headers: {
      [RUNTIME_SESSION_HEADER]: admission.sessionId,
      [RUNTIME_CLIENT_INSTANCE_HEADER]: clientInstanceId
    },
    requestedClass: parseConnectionClass(admission.requestedClass),
    grantedClass: parseConnectionClass(admission.grantedClass),
    admissionReasonCode: stringValue(admission.admissionReasonCode),
    capacityReasonCode: stringValue(admission.capacityReasonCode)
  };
}

/** Releases a server-owned lease when the caller deliberately cannot consume it. */
export async function releaseRuntimeSession(
  outcome: Pick<RuntimeSessionAdmissionOutcome, 'sessionId' | 'clientInstanceId'>,
  fetcher: RuntimeSessionFetch = fetch
): Promise<void> {
  let response: Response;
  try {
    response = await fetcher(`${API}/api/runtime/sessions/${encodeURIComponent(outcome.sessionId)}/terminate`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { accept: 'application/json', 'content-type': 'application/json; charset=utf-8' },
      body: JSON.stringify({ clientInstanceId: outcome.clientInstanceId })
    });
  } catch (reason) {
    throw new RuntimeSessionAdmissionError(reason instanceof Error ? reason.message : String(reason));
  }
  if (response.ok) return;
  const body = await response.text();
  throw new RuntimeSessionAdmissionError(body || `${response.status} ${response.statusText}`.trim(), response.status);
}

function parseAdmissionFailure(body: string): { message: string; capacityReasonCode: string | null } {
  if (!body.trim()) return { message: '', capacityReasonCode: null };
  try {
    const parsed = JSON.parse(body) as { error?: unknown; capacityReasonCode?: unknown };
    const error = stringValue(parsed.error);
    const capacityReasonCode = stringValue(parsed.capacityReasonCode);
    return {
      message: error ?? '',
      capacityReasonCode
    };
  } catch {
    return { message: body, capacityReasonCode: null };
  }
}

function parseConnectionClass(value: unknown): RuntimeSessionConnectionClass | null {
  if (value === 'interactive') return value;
  // `viewer` was accepted by older contracts; browser presentation remains canonical.
  if (value === 'viewOnly' || value === 'viewer') return 'viewOnly';
  return null;
}

function stringValue(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value : null;
}

function describeInteractiveFallback(outcome: RuntimeSessionAdmissionOutcome): string {
  const reasons = [outcome.admissionReasonCode, outcome.capacityReasonCode].filter(Boolean).join(', ');
  return `Interactive Runtime access was requested, but the server granted viewOnly access${reasons ? ` (${reasons})` : ''}.`;
}

function createClientInstanceId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `runtime-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}
