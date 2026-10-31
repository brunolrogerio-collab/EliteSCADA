const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export const RUNTIME_SESSION_HEADER = 'X-EliteSCADA-Runtime-Session';
export const RUNTIME_CLIENT_INSTANCE_HEADER = 'X-EliteSCADA-Runtime-Client-Instance';

const CLIENT_INSTANCE_STORAGE_KEY = 'elitescada.runtime.client-instance-id';
const PUBLIC_RUNTIME_SESSION_STORAGE_KEY = 'scadivo.public-runtime-session';
let ephemeralClientInstanceId: string | undefined;
let identityClaim: Promise<string> | undefined;
const tabToken = createClientInstanceId();
const identityChannel = typeof window === 'undefined' || typeof BroadcastChannel === 'undefined'
  ? null : new BroadcastChannel('scadivo.runtime.client-instance');

identityChannel?.addEventListener('message', event => {
  const message = event.data as { kind?: string; id?: string; token?: string };
  if (message?.kind === 'probe' && message.token !== tabToken &&
      message.id === ephemeralClientInstanceId) {
    identityChannel.postMessage({ kind: 'occupied', id: message.id, token: message.token });
  }
});

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
  publicRuntime?: boolean;
};

let activeRuntimeSession: RuntimeSessionAdmissionOutcome | null = null;

export function getActiveRuntimeSession(): RuntimeSessionAdmissionOutcome | null {
  return activeRuntimeSession;
}

export function setActiveRuntimeSession(outcome: RuntimeSessionAdmissionOutcome | null): void {
  activeRuntimeSession = outcome;
}

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
  if (ephemeralClientInstanceId) return ephemeralClientInstanceId;
  try {
    const stored = window.sessionStorage.getItem(CLIENT_INSTANCE_STORAGE_KEY)?.trim();
    if (stored) return ephemeralClientInstanceId = stored;

    const created = createClientInstanceId();
    window.sessionStorage.setItem(CLIENT_INSTANCE_STORAGE_KEY, created);
    return ephemeralClientInstanceId = created;
  } catch {
    return ephemeralClientInstanceId ??= createClientInstanceId();
  }
}

async function claimRuntimeClientInstanceId(): Promise<string> {
  // A duplicated browser tab may inherit sessionStorage. Coordinate with live
  // tabs before using that identity for a server-owned lease.
  if (!identityChannel) return getRuntimeClientInstanceId();
  return identityClaim ??= new Promise(resolve => {
    const id = getRuntimeClientInstanceId();
    let occupied = false;
    const onMessage = (event: MessageEvent) => {
      const message = event.data as { kind?: string; id?: string; token?: string };
      if (message?.kind === 'occupied' && message.id === id && message.token === tabToken) occupied = true;
    };
    identityChannel.addEventListener('message', onMessage);
    identityChannel.postMessage({ kind: 'probe', id, token: tabToken });
    window.setTimeout(() => {
      identityChannel.removeEventListener('message', onMessage);
      if (occupied) {
        ephemeralClientInstanceId = createClientInstanceId();
        try { window.sessionStorage.setItem(CLIENT_INSTANCE_STORAGE_KEY, ephemeralClientInstanceId); } catch { /* private storage */ }
      }
      resolve(getRuntimeClientInstanceId());
    }, 100);
  });
}

/**
 * Acquires the server-owned admission before a mutable Runtime operation. No quota is
 * reserved here; the returned headers bind the operation to the current logical lease.
 */
export async function admitInteractiveRuntimeSession(
  fetcher: RuntimeSessionFetch = fetch,
  clientInstanceId?: string
): Promise<Record<string, string>> {
  if (activeRuntimeSession &&
      (!clientInstanceId || clientInstanceId === activeRuntimeSession.clientInstanceId)) {
    if (activeRuntimeSession.grantedClass !== 'interactive') {
      throw new RuntimeSessionAdmissionError(
        'This Runtime window is admitted as View Only. Switch this window to Interactive before issuing commands or writes.',
        undefined,
        activeRuntimeSession
      );
    }
    return activeRuntimeSession.headers;
  }

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
  clientInstanceId?: string
): Promise<RuntimeSessionAdmissionOutcome> {
  clientInstanceId ??= await claimRuntimeClientInstanceId();
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

export async function admitPublicRuntimeSession(fetcher: RuntimeSessionFetch = fetch): Promise<RuntimeSessionAdmissionOutcome> {
  let response: Response;
  try {
    response = await fetcher(`${API}/api/runtime/public/session`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { accept: 'application/json' }
    });
  } catch (reason) {
    throw new RuntimeSessionAdmissionError(reason instanceof Error ? reason.message : String(reason));
  }
  if (!response.ok) {
    const body = await response.text();
    throw new RuntimeSessionAdmissionError(body || `${response.status} ${response.statusText}`, response.status);
  }
  const payload = await response.json() as RuntimeSessionAdmission;
  if (typeof payload.sessionId !== 'string' || typeof payload.clientInstanceId !== 'string')
    throw new RuntimeSessionAdmissionError('Public Runtime session returned an invalid identity.', response.status);
  const outcome: RuntimeSessionAdmissionOutcome = {
    sessionId: payload.sessionId,
    clientInstanceId: payload.clientInstanceId,
    headers: {
      [RUNTIME_SESSION_HEADER]: payload.sessionId,
      [RUNTIME_CLIENT_INSTANCE_HEADER]: payload.clientInstanceId
    },
    requestedClass: 'viewOnly',
    grantedClass: 'viewOnly',
    admissionReasonCode: 'public-runtime-read-only',
    capacityReasonCode: null,
    publicRuntime: true
  };
  activeRuntimeSession = outcome;
  try { window.sessionStorage.setItem(PUBLIC_RUNTIME_SESSION_STORAGE_KEY, JSON.stringify(outcome)); } catch { /* Private storage is optional. */ }
  return outcome;
}

export async function releaseStoredPublicRuntimeSession(fetcher: RuntimeSessionFetch = fetch): Promise<void> {
  let stored: RuntimeSessionAdmissionOutcome | null = null;
  try {
    const raw = window.sessionStorage.getItem(PUBLIC_RUNTIME_SESSION_STORAGE_KEY);
    if (raw) stored = JSON.parse(raw) as RuntimeSessionAdmissionOutcome;
  } catch { stored = null; }
  if (stored?.publicRuntime) await releaseRuntimeSession(stored, fetcher);
}

export async function heartbeatRuntimeSession(
  outcome: RuntimeSessionAdmissionOutcome,
  fetcher: RuntimeSessionFetch = fetch
): Promise<RuntimeSessionAdmissionOutcome> {
  if (outcome.publicRuntime) {
    const response = await fetcher(`${API}/api/runtime/public/session/heartbeat`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { accept: 'application/json', 'content-type': 'application/json; charset=utf-8' },
      body: JSON.stringify({ sessionId: outcome.sessionId, clientInstanceId: outcome.clientInstanceId })
    });
    if (!response.ok) throw new RuntimeSessionAdmissionError(await response.text() || `${response.status} ${response.statusText}`, response.status, outcome);
    const renewed = { ...outcome };
    try { window.sessionStorage.setItem(PUBLIC_RUNTIME_SESSION_STORAGE_KEY, JSON.stringify(renewed)); } catch { /* Private storage is optional. */ }
    return renewed;
  }
  let response: Response;
  try {
    response = await fetcher(`${API}/api/runtime/sessions/${encodeURIComponent(outcome.sessionId)}/heartbeat`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { accept: 'application/json', 'content-type': 'application/json; charset=utf-8' },
      body: JSON.stringify({ clientInstanceId: outcome.clientInstanceId })
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
      outcome,
      failure.capacityReasonCode
    );
  }

  let admission: RuntimeSessionAdmission;
  try {
    admission = await response.json() as RuntimeSessionAdmission;
  } catch {
    throw new RuntimeSessionAdmissionError('Runtime session heartbeat returned an invalid response.', response.status, outcome);
  }
  if (admission.sessionId !== outcome.sessionId || admission.clientInstanceId !== outcome.clientInstanceId) {
    throw new RuntimeSessionAdmissionError('Runtime session heartbeat changed the logical client identity.', response.status, outcome);
  }

  return {
    ...outcome,
    requestedClass: parseConnectionClass(admission.requestedClass) ?? outcome.requestedClass,
    grantedClass: parseConnectionClass(admission.grantedClass) ?? outcome.grantedClass,
    admissionReasonCode: stringValue(admission.admissionReasonCode) ?? outcome.admissionReasonCode,
    capacityReasonCode: stringValue(admission.capacityReasonCode) ?? outcome.capacityReasonCode
  };
}

/** Releases a server-owned lease when the caller deliberately cannot consume it. */
export async function releaseRuntimeSession(
  outcome: Pick<RuntimeSessionAdmissionOutcome, 'sessionId' | 'clientInstanceId'> &
    Partial<Pick<RuntimeSessionAdmissionOutcome, 'publicRuntime'>>,
  fetcher: RuntimeSessionFetch = fetch
): Promise<void> {
  if (outcome.publicRuntime) {
    const response = await fetcher(`${API}/api/runtime/public/session/terminate`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { accept: 'application/json', 'content-type': 'application/json; charset=utf-8' },
      body: JSON.stringify({ sessionId: outcome.sessionId, clientInstanceId: outcome.clientInstanceId })
    });
    if (!response.ok) throw new RuntimeSessionAdmissionError(await response.text() || `${response.status} ${response.statusText}`, response.status);
    activeRuntimeSession = null;
    try { window.sessionStorage.removeItem(PUBLIC_RUNTIME_SESSION_STORAGE_KEY); } catch { /* Private storage is optional. */ }
    return;
  }
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
  if (response.ok) {
    if (activeRuntimeSession?.sessionId === outcome.sessionId) activeRuntimeSession = null;
    return;
  }
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
