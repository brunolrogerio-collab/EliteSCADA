import type { AuditFilterState, AuditOutcome } from './types';

export const AUDIT_NEXT_CURSOR_HEADER = 'X-EliteSCADA-Audit-Next-Cursor';
export const AUDIT_CLIENT_MAX_PAGE_SIZE = 1000;

function optional(params: URLSearchParams, key: string, value: string) {
  const normalized = value.trim();
  if (normalized) params.set(key, normalized);
}

function toUtc(value: string, label: string): string | null {
  if (!value.trim()) return null;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) throw new Error(`${label} is invalid.`);
  return parsed.toISOString();
}

export function buildAuditQueryPath(filters: AuditFilterState, cursor?: string | null): string {
  if (!Number.isInteger(filters.pageSize) || filters.pageSize < 1 || filters.pageSize > AUDIT_CLIENT_MAX_PAGE_SIZE) {
    throw new Error(`Audit page size must be between 1 and ${AUDIT_CLIENT_MAX_PAGE_SIZE}.`);
  }

  const fromUtc = toUtc(filters.fromLocal, 'Audit start time');
  const toUtcValue = toUtc(filters.toLocal, 'Audit end time');
  if (fromUtc && toUtcValue && Date.parse(fromUtc) > Date.parse(toUtcValue)) {
    throw new Error('Audit start time must not be later than end time.');
  }

  const params = new URLSearchParams();
  params.set('limit', String(filters.pageSize));
  if (fromUtc) params.set('fromUtc', fromUtc);
  if (toUtcValue) params.set('toUtc', toUtcValue);
  optional(params, 'subjectId', filters.subjectId);
  optional(params, 'action', filters.action);
  if (filters.outcome) params.set('outcome', filters.outcome);
  optional(params, 'targetKind', filters.targetKind);
  optional(params, 'targetId', filters.targetId);
  optional(params, 'area', filters.area);
  optional(params, 'correlationId', filters.correlationId);

  // The backend owns the cursor format. The client only transports the exact opaque value.
  if (cursor) params.set('cursor', cursor);
  return `/api/audit?${params.toString()}`;
}

export function auditOutcomeLabel(outcome: AuditOutcome): 'succeeded' | 'denied' | 'failed' | 'unknown' {
  if (outcome === 0 || outcome === 'Succeeded') return 'succeeded';
  if (outcome === 1 || outcome === 'Denied') return 'denied';
  if (outcome === 2 || outcome === 'Failed') return 'failed';
  return 'unknown';
}

export function sortedAuditDetails(details?: Record<string, string> | null): Array<[string, string]> {
  if (!details) return [];
  return Object.entries(details).sort(([left], [right]) => left.localeCompare(right));
}
