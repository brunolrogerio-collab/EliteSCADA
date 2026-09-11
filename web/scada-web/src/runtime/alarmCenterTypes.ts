import type { RuntimeOperationsLocale } from './operationsTypes';

export type RuntimeAlarmCenterLocale = RuntimeOperationsLocale;

export type RuntimeAlarmCenterItem = {
  definitionId: string;
  name: string;
  tagId: string;
  type: string | number;
  priority: string | number;
  state: string | number;
  lastTransition: string;
  lastValue: unknown;
  area?: string | null;
  message?: string | null;
  activatedAt?: string | null;
  acknowledgedAt?: string | null;
  acknowledgedBy?: string | null;
  shelvedAt?: string | null;
  shelvedBy?: string | null;
};

/**
 * Narrow Runtime projection of the backend AlarmDefinition contract. C18 uses
 * this protected endpoint to enrich historical rows and translate authored Area
 * filters into stable alarm identities without inventing historical fields that
 * are not persisted by the alarm-history provider.
 */
export type RuntimeAlarmDefinition = {
  id: string;
  name: string;
  tagId: string;
  type: string | number;
  priority: string | number;
  area?: string | null;
  message?: string | null;
  enabled?: boolean;
  shelvingAllowed?: boolean;
};

export type RuntimeAlarmCenterEndpoint<T> =
  | { available: true; value: T }
  | { available: false; status?: number; error: string };

export type RuntimeAlarmAcknowledgeResult =
  | { ok: true; status: number }
  | { ok: false; status?: number; error: string };

export type RuntimeAlarmCenterSummary = {
  total: number;
  awaitingAcknowledgement: number;
  acknowledged: number;
  criticalAwaitingAcknowledgement: number;
  highAwaitingAcknowledgement: number;
};
