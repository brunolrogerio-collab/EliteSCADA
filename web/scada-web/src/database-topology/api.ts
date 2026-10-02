import type {
  DatabaseCompatibilityResult,
  DatabaseConnectionHealth,
  DatabaseCutoverResult,
  DatabasePendingMigration,
  DatabaseRemoteEndpointRequest,
  DatabaseRemoteProfileRequest,
  DatabaseTopologyStatus,
  DatabaseMigrationVerification
} from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type DatabaseTopologyApiErrorKind =
  | 'unauthenticated'
  | 'forbidden'
  | 'invalid-request'
  | 'conflict'
  | 'unavailable'
  | 'server';

export class DatabaseTopologyApiError extends Error {
  constructor(
    public readonly kind: DatabaseTopologyApiErrorKind,
    public readonly status?: number,
    public readonly diagnostic?: string
  ) {
    super(diagnostic ?? kind);
    this.name = 'DatabaseTopologyApiError';
  }
}

function classify(response: Response, diagnostic?: string) {
  if (response.status === 401) return new DatabaseTopologyApiError('unauthenticated', 401, diagnostic);
  if (response.status === 403) return new DatabaseTopologyApiError('forbidden', 403, diagnostic);
  if (response.status === 400 || response.status === 422) {
    return new DatabaseTopologyApiError('invalid-request', response.status, diagnostic);
  }
  if (response.status === 409) return new DatabaseTopologyApiError('conflict', 409, diagnostic);
  if (response.status >= 500) return new DatabaseTopologyApiError('server', response.status, diagnostic);
  return new DatabaseTopologyApiError('unavailable', response.status, diagnostic);
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, {
      ...init,
      headers: {
        accept: 'application/json',
        ...(init?.body ? { 'content-type': 'application/json' } : {}),
        ...(init?.headers ?? {})
      }
    });
  } catch {
    throw new DatabaseTopologyApiError('unavailable');
  }

  let payload: unknown = null;
  try {
    payload = await response.json();
  } catch {
    if (response.ok) throw new DatabaseTopologyApiError('server', response.status);
  }

  if (!response.ok) {
    const diagnostic = payload && typeof payload === 'object' && 'error' in payload
      ? String((payload as { error?: unknown }).error ?? '')
      : undefined;
    throw classify(response, diagnostic);
  }

  return payload as T;
}

function post<T>(path: string, payload?: unknown) {
  return requestJson<T>(path, {
    method: 'POST',
    body: payload === undefined ? undefined : JSON.stringify(payload)
  });
}

export function loadDatabaseTopologyStatus(refresh = false) {
  return requestJson<DatabaseTopologyStatus>(
    `/api/admin/database-topology/${refresh ? '?refresh=true' : ''}`
  );
}

export function testDatabaseConnection(
  endpoint: DatabaseRemoteEndpointRequest,
  requireTimescale = false
) {
  return post<DatabaseConnectionHealth>('/api/admin/database-topology/test', {
    endpoint,
    requireTimescale
  });
}

export function validateDatabaseCompatibility(profile: DatabaseRemoteProfileRequest) {
  return post<DatabaseCompatibilityResult>(
    '/api/admin/database-topology/compatibility',
    profile
  );
}

export function prepareDatabaseMigration(profile: DatabaseRemoteProfileRequest) {
  return post<DatabasePendingMigration>('/api/admin/database-topology/prepare', profile);
}

export function startDatabaseMigration(operationId: string) {
  return post<DatabasePendingMigration>(
    `/api/admin/database-topology/operations/${encodeURIComponent(operationId)}/copy`
  );
}

export function verifyDatabaseMigration(operationId: string) {
  return post<DatabaseMigrationVerification>(
    `/api/admin/database-topology/operations/${encodeURIComponent(operationId)}/verify`
  );
}

export function commitDatabaseCutover(operationId: string) {
  return post<DatabaseCutoverResult>(
    `/api/admin/database-topology/operations/${encodeURIComponent(operationId)}/commit`
  );
}

export function rollbackDatabaseTopology(operationId?: string | null) {
  return post<DatabaseCutoverResult>('/api/admin/database-topology/rollback', {
    operationId: operationId || null
  });
}
