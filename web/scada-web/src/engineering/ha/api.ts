import type {
  HaActionKind,
  HaAdministrationSnapshot,
  HaAuthoritySnapshot,
  HaHostConfigurationSnapshot,
  HaHostConfigurationUpdateRequest,
  HaHostConfigurationUpdateResult,
  HaLicensingSnapshot,
  HaPeerDiagnostics,
  HaProtectionOperation,
  HaTopologySnapshot,
  HaWorkspaceSnapshot
} from './types';

type ErrorPayload = {
  error?: string;
  reasonCode?: string;
  errors?: string[];
  operationId?: string;
  kind?: string;
  state?: string;
  sourceNodeId?: string | null;
  targetNodeId?: string | null;
  epoch?: number | null;
  startedAtUtc?: string;
  completedAtUtc?: string | null;
};

export class HaAdminHttpError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly reasonCode?: string,
    public readonly errors: string[] = []
  ) {
    super(message);
    this.name = 'HaAdminHttpError';
  }
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...(init?.headers ?? {})
    }
  });

  let payload: unknown = undefined;
  if (response.status !== 204) {
    try {
      payload = await response.json();
    } catch {
      payload = undefined;
    }
  }

  if (!response.ok) {
    const candidate = (payload ?? {}) as ErrorPayload;
    throw new HaAdminHttpError(
      response.status,
      candidate.error || candidate.reasonCode || `${response.status} ${response.statusText}`,
      candidate.reasonCode,
      Array.isArray(candidate.errors) ? candidate.errors : []
    );
  }

  return payload as T;
}

async function requestOperation(path: string, body: unknown): Promise<HaProtectionOperation> {
  const response = await fetch(path, {
    method: 'POST',
    headers: { accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });

  let payload: ErrorPayload = {};
  try {
    payload = await response.json() as ErrorPayload;
  } catch {
    if (!response.ok) throw new HaAdminHttpError(response.status, `${response.status} ${response.statusText}`);
  }

  if ((response.status === 202 || response.status === 409) && payload.operationId) {
    return payload as HaProtectionOperation;
  }
  if (!response.ok) {
    throw new HaAdminHttpError(
      response.status,
      payload.error || payload.reasonCode || `${response.status} ${response.statusText}`,
      payload.reasonCode
    );
  }
  return payload as HaProtectionOperation;
}

const loadTopology = () => requestJson<HaTopologySnapshot>('/api/runtime/ha/topology');
const loadAuthority = () => requestJson<HaAuthoritySnapshot>('/api/runtime/ha/authority');
const loadAdministration = () => requestJson<HaAdministrationSnapshot>('/api/runtime/ha/administration');
const loadConfiguration = () => requestJson<HaHostConfigurationSnapshot>('/api/runtime/ha/configuration');
const loadPeerStatus = () => requestJson<HaPeerDiagnostics>('/api/runtime/ha/peer/status');
const loadLicensing = () => requestJson<HaLicensingSnapshot>('/api/licensing/status');

export const haAdminApi = {
  topology: loadTopology,
  authority: loadAuthority,
  administration: loadAdministration,
  configuration: loadConfiguration,
  peerStatus: loadPeerStatus,
  licensing: loadLicensing,
  operation: (operationId: string) =>
    requestJson<HaProtectionOperation>('/api/runtime/ha/operations/' + encodeURIComponent(operationId)),
  updateConfiguration: (request: HaHostConfigurationUpdateRequest) =>
    requestJson<HaHostConfigurationUpdateResult>('/api/runtime/ha/configuration', {
      method: 'PUT',
      body: JSON.stringify(request)
    }),
  action: (kind: HaActionKind, targetNodeId?: string | null) => {
    const body = kind === 'switchover'
      ? { targetNodeId: targetNodeId ?? '' }
      : { targetNodeId: targetNodeId || null };
    return requestOperation('/api/runtime/ha/actions/' + kind, body);
  },
  async workspace(): Promise<HaWorkspaceSnapshot> {
    const [topology, authority, administration, configuration, peer, licensing] = await Promise.all([
      loadTopology(),
      loadAuthority(),
      loadAdministration(),
      loadConfiguration(),
      loadPeerStatus(),
      loadLicensing()
    ]);
    return { topology, authority, administration, configuration, peer, licensing };
  }
};
