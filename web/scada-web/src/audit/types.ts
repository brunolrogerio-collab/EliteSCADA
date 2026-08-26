export type AuditOutcome = 0 | 1 | 2 | 'Succeeded' | 'Denied' | 'Failed';

export type AuditEventView = {
  id: string;
  timestampUtc: string;
  subjectId: string;
  displayName?: string | null;
  action: string;
  outcome: AuditOutcome;
  targetKind: string;
  targetId: string;
  details?: Record<string, string> | null;
  correlationId?: string | null;
  area?: string | null;
  projectKey?: string | null;
  revision?: number | null;
  roles?: string[] | null;
  source?: string | null;
};

export type AuditStoreDiagnostics = {
  persistedCount: number;
  appendFailureCount: number;
  lastPersistedAtUtc?: string | null;
  lastAppendFailureAtUtc?: string | null;
  lastRetentionRunAtUtc?: string | null;
  lastRetentionDeletedCount: number;
};

export type AuditBufferDiagnostics = {
  queueDepth: number;
  successfullyForwardedCount: number;
  forwardFailureCount: number;
  rejectedCount: number;
  droppedOnShutdownCount: number;
  lastForwardedAtUtc?: string | null;
  lastFailureAtUtc?: string | null;
};

export type AuditRetentionDiagnostics = {
  enabled: boolean;
  maximumAge?: string | null;
  batchSize: number;
  interval?: string | null;
  maximumBatchesPerRun: number;
  finiteRetentionActive: boolean;
};

export type AuditDiagnostics = {
  store: AuditStoreDiagnostics;
  buffer: AuditBufferDiagnostics;
  retention: AuditRetentionDiagnostics;
};

export type AuditFilterState = {
  fromLocal: string;
  toLocal: string;
  subjectId: string;
  action: string;
  outcome: '' | 'Succeeded' | 'Denied' | 'Failed';
  targetKind: string;
  targetId: string;
  area: string;
  correlationId: string;
  pageSize: number;
};

export const defaultAuditFilters: AuditFilterState = {
  fromLocal: '',
  toLocal: '',
  subjectId: '',
  action: '',
  outcome: '',
  targetKind: '',
  targetId: '',
  area: '',
  correlationId: '',
  pageSize: 100
};
