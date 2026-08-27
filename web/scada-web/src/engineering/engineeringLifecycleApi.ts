import type {
  EngineeringLifecycleAction,
  EngineeringLifecycleState,
  EngineeringPersistenceStatus,
  EngineeringProjectLifecycle,
  EngineeringRevisionMetadata,
  EngineeringRuntimeConsistency,
  EngineeringLifecycleWorkspaceDescriptor
} from './engineeringLifecycleTypes';

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class EngineeringLifecycleApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly responseBody: string,
    public readonly responseData?: unknown
  ) {
    super(extractErrorMessage(responseData) ?? responseBody || `HTTP ${status}`);
    this.name = 'EngineeringLifecycleApiError';
  }
}

export function engineeringLifecycleRequestBody(
  action: EngineeringLifecycleAction,
  projectName?: string
): Record<string, unknown> | undefined {
  if (action === 'save') return { projectName: projectName ?? '' };
  if (action === 'publish' || action === 'activate') return {};
  return undefined;
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    ...init,
    headers: {
      accept: 'application/json',
      ...(init?.body ? { 'content-type': 'application/json; charset=utf-8' } : {}),
      ...init?.headers
    }
  });

  const text = await response.text();
  let data: unknown;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = undefined;
    }
  }

  if (!response.ok) {
    throw new EngineeringLifecycleApiError(response.status, text, data);
  }

  return (data ?? {}) as T;
}

export async function loadEngineeringLifecycleState(): Promise<EngineeringLifecycleState> {
  const [workspace, persistence] = await Promise.all([
    requestJson<EngineeringLifecycleWorkspaceDescriptor>('/api/engineering/workspace'),
    requestJson<EngineeringPersistenceStatus>('/api/engineering/persistence/status')
  ]);

  const projectKey = normalizeKey(workspace.projectKey) ?? normalizeKey(persistence.configuredProjectKey);
  if (!persistence.enabled || !projectKey) {
    return {
      workspace,
      persistence,
      projectKey: projectKey ?? null,
      lifecycle: null,
      revisions: [],
      runtime: null
    };
  }

  const encodedKey = encodeURIComponent(projectKey);
  const [lifecycle, revisions, runtime] = await Promise.all([
    requestJson<EngineeringProjectLifecycle>(`/api/engineering/persistence/${encodedKey}/lifecycle`),
    requestJson<EngineeringRevisionMetadata[]>(`/api/engineering/persistence/${encodedKey}/revisions?limit=50`),
    requestJson<EngineeringRuntimeConsistency>(`/api/engineering/persistence/${encodedKey}/runtime`)
  ]);

  return {
    workspace,
    persistence,
    projectKey,
    lifecycle,
    revisions: [...revisions].sort((left, right) => right.revision - left.revision),
    runtime
  };
}

export async function saveEngineeringRevision(
  projectKey: string,
  projectName: string
): Promise<EngineeringRevisionMetadata> {
  return await requestJson<EngineeringRevisionMetadata>(
    `/api/engineering/persistence/${encodeURIComponent(projectKey)}/save`,
    {
      method: 'POST',
      body: JSON.stringify(engineeringLifecycleRequestBody('save', projectName))
    }
  );
}

export async function checkoutEngineeringRevision(projectKey: string, revision: number): Promise<unknown> {
  return await requestJson(
    `/api/engineering/persistence/${encodeURIComponent(projectKey)}/revisions/${revision}/checkout`,
    { method: 'POST' }
  );
}

export async function publishEngineeringRevision(projectKey: string, revision: number): Promise<unknown> {
  return await requestJson(
    `/api/engineering/persistence/${encodeURIComponent(projectKey)}/revisions/${revision}/publish`,
    {
      method: 'POST',
      body: JSON.stringify(engineeringLifecycleRequestBody('publish'))
    }
  );
}

export async function activatePublishedEngineeringRevision(projectKey: string): Promise<unknown> {
  return await requestJson(
    `/api/engineering/persistence/${encodeURIComponent(projectKey)}/published/activate`,
    {
      method: 'POST',
      body: JSON.stringify(engineeringLifecycleRequestBody('activate'))
    }
  );
}

function normalizeKey(value: string | null | undefined): string | null {
  const normalized = value?.trim();
  return normalized ? normalized : null;
}

function extractErrorMessage(value: unknown): string | null {
  if (!value || typeof value !== 'object') return null;
  if ('error' in value && typeof value.error === 'string') return value.error;
  return null;
}
