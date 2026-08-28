import { AUDIT_NEXT_CURSOR_HEADER, buildAuditQueryPath } from './contract';
import type { AuditDiagnostics, AuditEventView, AuditFilterState } from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type AuditApiErrorKind = 'unauthenticated' | 'forbidden' | 'invalid-query' | 'unavailable' | 'server';

export class AuditApiError extends Error {
  constructor(
    public readonly kind: AuditApiErrorKind,
    public readonly status?: number
  ) {
    super(kind);
    this.name = 'AuditApiError';
  }
}

function errorFor(response: Response): AuditApiError {
  if (response.status === 401) return new AuditApiError('unauthenticated', 401);
  if (response.status === 403) return new AuditApiError('forbidden', 403);
  if (response.status === 400) return new AuditApiError('invalid-query', 400);
  if (response.status >= 500) return new AuditApiError('server', response.status);
  return new AuditApiError('unavailable', response.status);
}

async function fetchJson<T>(path: string): Promise<{ response: Response; data: T }> {
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, { headers: { accept: 'application/json' } });
  } catch {
    throw new AuditApiError('unavailable');
  }

  if (!response.ok) throw errorFor(response);

  try {
    return { response, data: await response.json() as T };
  } catch {
    throw new AuditApiError('server', response.status);
  }
}

export type AuditPageResult = {
  events: AuditEventView[];
  nextCursor: string | null;
};

export async function loadAuditPage(
  filters: AuditFilterState,
  cursor?: string | null
): Promise<AuditPageResult> {
  let path: string;
  try {
    path = buildAuditQueryPath(filters, cursor);
  } catch {
    throw new AuditApiError('invalid-query');
  }

  const { response, data } = await fetchJson<unknown>(path);
  if (!Array.isArray(data)) throw new AuditApiError('server', response.status);

  const nextCursor = response.headers.get(AUDIT_NEXT_CURSOR_HEADER);
  return {
    events: data as AuditEventView[],
    nextCursor: nextCursor && nextCursor.trim() ? nextCursor : null
  };
}

export async function loadAuditDiagnostics(): Promise<AuditDiagnostics> {
  const { data } = await fetchJson<AuditDiagnostics>('/api/audit/diagnostics');
  return data;
}
