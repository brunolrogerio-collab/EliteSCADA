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
  locationId?: string | null;
  locationPath?: string | null;
  isSuppressed?: boolean;
  suppressionCauses?: RuntimeAlarmSuppressionCause[];
};

export type RuntimeAlarmSuppressionTargetKind = 'alarm' | 'location';

export type RuntimeAlarmSuppressionCause = {
  targetKind: RuntimeAlarmSuppressionTargetKind;
  targetId: string;
  targetName: string;
  targetPath: string;
  actor: string;
  reasonCode: string;
  appliedAtUtc: string;
  expiresAtUtc?: string | null;
};

export type RuntimeAlarmSuppressionLocation = {
  id: string;
  parentLocationId?: string | null;
  name: string;
  path: string;
  alarmCount: number;
  suppressedAlarmCount: number;
};

export type RuntimeAlarmSuppressionSnapshot = {
  projectKey?: string | null;
  engineeringRevision?: number | null;
  suppressionRevision: number;
  alarms: Array<Pick<RuntimeAlarmCenterItem,
    'definitionId' | 'name' | 'area' | 'locationId' | 'locationPath' | 'isSuppressed' | 'suppressionCauses'>>;
  locations: RuntimeAlarmSuppressionLocation[];
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
  soundProfile?: string | null;
};

export type RuntimeAlarmCenterEndpoint<T> =
  | { available: true; value: T }
  | { available: false; status?: number; error: string };

export type RuntimeAlarmAcknowledgeResult =
  | { ok: true; status: number }
  | { ok: false; status?: number; error: string };

export type RuntimeAlarmSuppressionResult =
  | { ok: true; status: number; changed: boolean }
  | { ok: false; status?: number; error: string };

export type RuntimeAlarmCenterSummary = {
  total: number;
  awaitingAcknowledgement: number;
  acknowledged: number;
  criticalAwaitingAcknowledgement: number;
  highAwaitingAcknowledgement: number;
};
