import type { RuntimeAlarmCenterItem, RuntimeAlarmCenterSummary } from './alarmCenterTypes';

export type RuntimeAlarmStateKey = 'normal' | 'active' | 'acknowledged' | 'returned' | 'disabled' | 'shelved' | 'unknown';
export type RuntimeAlarmPriorityKey = 'low' | 'medium' | 'high' | 'critical' | 'unknown';
export type RuntimeAlarmTone = 'quiet' | 'attention' | 'danger';
export type RuntimeAlarmEndpointIssue = 'unauthenticated' | 'forbidden' | 'not-found' | 'unavailable';

const stateByNumber: Record<number, RuntimeAlarmStateKey> = {
  0: 'normal',
  1: 'active',
  2: 'acknowledged',
  3: 'returned',
  4: 'disabled',
  5: 'shelved'
};

const priorityByNumber: Record<number, RuntimeAlarmPriorityKey> = {
  1: 'low',
  2: 'medium',
  3: 'high',
  4: 'critical'
};

const priorityRank: Record<RuntimeAlarmPriorityKey, number> = {
  unknown: 0,
  low: 1,
  medium: 2,
  high: 3,
  critical: 4
};

export function normalizeRuntimeAlarmState(value: string | number): RuntimeAlarmStateKey {
  if (typeof value === 'number') return stateByNumber[value] ?? 'unknown';
  const normalized = normalizeToken(value);
  if (normalized === 'normal') return 'normal';
  if (normalized === 'active') return 'active';
  if (normalized === 'acknowledged' || normalized === 'acknowledge' || normalized === 'acked') return 'acknowledged';
  if (normalized === 'returned' || normalized === 'returntonormal') return 'returned';
  if (normalized === 'disabled') return 'disabled';
  if (normalized === 'shelved') return 'shelved';
  const numeric = Number(value);
  return Number.isInteger(numeric) ? stateByNumber[numeric] ?? 'unknown' : 'unknown';
}

export function normalizeRuntimeAlarmPriority(value: string | number): RuntimeAlarmPriorityKey {
  if (typeof value === 'number') return priorityByNumber[value] ?? 'unknown';
  const normalized = normalizeToken(value);
  if (normalized === 'low') return 'low';
  if (normalized === 'medium') return 'medium';
  if (normalized === 'high') return 'high';
  if (normalized === 'critical') return 'critical';
  const numeric = Number(value);
  return Number.isInteger(numeric) ? priorityByNumber[numeric] ?? 'unknown' : 'unknown';
}

export function runtimeAlarmPriorityRank(alarm: RuntimeAlarmCenterItem): number {
  return priorityRank[normalizeRuntimeAlarmPriority(alarm.priority)];
}

export function canAcknowledgeRuntimeAlarm(alarm: RuntimeAlarmCenterItem): boolean {
  return normalizeRuntimeAlarmState(alarm.state) === 'active';
}

export function runtimeAlarmTone(alarm: RuntimeAlarmCenterItem): RuntimeAlarmTone {
  const state = normalizeRuntimeAlarmState(alarm.state);
  if (state === 'acknowledged') return 'quiet';
  const priority = normalizeRuntimeAlarmPriority(alarm.priority);
  if (priority === 'critical' || priority === 'high') return 'danger';
  return 'attention';
}

export function sortRuntimeAlarmsForAttention(items: RuntimeAlarmCenterItem[]): RuntimeAlarmCenterItem[] {
  return [...items].sort((left, right) => {
    const priorityDifference = runtimeAlarmPriorityRank(right) - runtimeAlarmPriorityRank(left);
    if (priorityDifference !== 0) return priorityDifference;

    const leftAcknowledged = normalizeRuntimeAlarmState(left.state) === 'acknowledged' ? 1 : 0;
    const rightAcknowledged = normalizeRuntimeAlarmState(right.state) === 'acknowledged' ? 1 : 0;
    if (leftAcknowledged !== rightAcknowledged) return leftAcknowledged - rightAcknowledged;

    const leftTime = operationalTimestamp(left);
    const rightTime = operationalTimestamp(right);
    if (leftTime !== rightTime) return leftTime - rightTime;

    const areaDifference = (left.area ?? '').localeCompare(right.area ?? '', undefined, { sensitivity: 'base' });
    if (areaDifference !== 0) return areaDifference;
    return left.name.localeCompare(right.name, undefined, { sensitivity: 'base' });
  });
}

export function buildRuntimeAlarmCenterSummary(items: RuntimeAlarmCenterItem[]): RuntimeAlarmCenterSummary {
  let awaitingAcknowledgement = 0;
  let acknowledged = 0;
  let criticalAwaitingAcknowledgement = 0;
  let highAwaitingAcknowledgement = 0;

  for (const alarm of items) {
    const state = normalizeRuntimeAlarmState(alarm.state);
    if (state === 'active') {
      awaitingAcknowledgement += 1;
      const priority = normalizeRuntimeAlarmPriority(alarm.priority);
      if (priority === 'critical') criticalAwaitingAcknowledgement += 1;
      if (priority === 'high') highAwaitingAcknowledgement += 1;
    } else if (state === 'acknowledged') {
      acknowledged += 1;
    }
  }

  return {
    total: items.length,
    awaitingAcknowledgement,
    acknowledged,
    criticalAwaitingAcknowledgement,
    highAwaitingAcknowledgement
  };
}

export function classifyRuntimeAlarmEndpointIssue(status?: number): RuntimeAlarmEndpointIssue {
  if (status === 401) return 'unauthenticated';
  if (status === 403) return 'forbidden';
  if (status === 404) return 'not-found';
  return 'unavailable';
}

function operationalTimestamp(alarm: RuntimeAlarmCenterItem): number {
  const parsed = Date.parse(alarm.activatedAt ?? alarm.lastTransition);
  return Number.isFinite(parsed) ? parsed : Number.MAX_SAFE_INTEGER;
}

function normalizeToken(value: string): string {
  return value.trim().toLowerCase().replace(/[\s_-]+/g, '');
}
