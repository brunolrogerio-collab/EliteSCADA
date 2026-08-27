import { expect, test } from '@playwright/test';
import {
  buildRuntimeAlarmCenterSummary,
  canAcknowledgeRuntimeAlarm,
  classifyRuntimeAlarmEndpointIssue,
  normalizeRuntimeAlarmPriority,
  normalizeRuntimeAlarmState,
  sortRuntimeAlarmsForAttention
} from '../src/runtime/alarmCenterModel';
import type { RuntimeAlarmCenterItem } from '../src/runtime/alarmCenterTypes';

function alarm(overrides: Partial<RuntimeAlarmCenterItem> = {}): RuntimeAlarmCenterItem {
  return {
    definitionId: crypto.randomUUID(),
    name: 'Alarm',
    tagId: crypto.randomUUID(),
    type: 1,
    priority: 2,
    state: 1,
    lastTransition: '2026-08-27T16:00:00Z',
    lastValue: 42,
    area: 'Process',
    message: 'Process alarm',
    activatedAt: '2026-08-27T15:55:00Z',
    ...overrides
  };
}

test('normalizes the backend numeric alarm enum values without inventing frontend state', () => {
  expect(normalizeRuntimeAlarmState(0)).toBe('normal');
  expect(normalizeRuntimeAlarmState(1)).toBe('active');
  expect(normalizeRuntimeAlarmState(2)).toBe('acknowledged');
  expect(normalizeRuntimeAlarmState(3)).toBe('returned');
  expect(normalizeRuntimeAlarmState(4)).toBe('disabled');
  expect(normalizeRuntimeAlarmState(5)).toBe('shelved');
  expect(normalizeRuntimeAlarmState(99)).toBe('unknown');

  expect(normalizeRuntimeAlarmPriority(1)).toBe('low');
  expect(normalizeRuntimeAlarmPriority(2)).toBe('medium');
  expect(normalizeRuntimeAlarmPriority(3)).toBe('high');
  expect(normalizeRuntimeAlarmPriority(4)).toBe('critical');
  expect(normalizeRuntimeAlarmPriority(99)).toBe('unknown');
});

test('sorts alarms for operator attention by priority, acknowledgement state and age', () => {
  const highActiveOlder = alarm({
    definitionId: 'high-active-old',
    name: 'High active old',
    priority: 3,
    state: 1,
    activatedAt: '2026-08-27T14:00:00Z'
  });
  const criticalAcknowledged = alarm({
    definitionId: 'critical-ack',
    name: 'Critical acknowledged',
    priority: 4,
    state: 2,
    activatedAt: '2026-08-27T13:00:00Z',
    acknowledgedAt: '2026-08-27T13:05:00Z',
    acknowledgedBy: 'Operator'
  });
  const criticalActiveNewer = alarm({
    definitionId: 'critical-active-new',
    name: 'Critical active new',
    priority: 'Critical',
    state: 'Active',
    activatedAt: '2026-08-27T15:00:00Z'
  });
  const criticalActiveOlder = alarm({
    definitionId: 'critical-active-old',
    name: 'Critical active old',
    priority: 4,
    state: 1,
    activatedAt: '2026-08-27T12:00:00Z'
  });

  expect(sortRuntimeAlarmsForAttention([
    highActiveOlder,
    criticalAcknowledged,
    criticalActiveNewer,
    criticalActiveOlder
  ]).map(item => item.definitionId)).toEqual([
    'critical-active-old',
    'critical-active-new',
    'critical-ack',
    'high-active-old'
  ]);
});

test('only an actually Active alarm is offered for acknowledgement', () => {
  expect(canAcknowledgeRuntimeAlarm(alarm({ state: 1 }))).toBeTruthy();
  expect(canAcknowledgeRuntimeAlarm(alarm({ state: 'Active' }))).toBeTruthy();
  expect(canAcknowledgeRuntimeAlarm(alarm({ state: 2 }))).toBeFalsy();
  expect(canAcknowledgeRuntimeAlarm(alarm({ state: 'Acknowledged' }))).toBeFalsy();
  expect(canAcknowledgeRuntimeAlarm(alarm({ state: 5 }))).toBeFalsy();
});

test('builds operational counts from authoritative active alarm items', () => {
  const summary = buildRuntimeAlarmCenterSummary([
    alarm({ priority: 4, state: 1 }),
    alarm({ priority: 3, state: 'Active' }),
    alarm({ priority: 2, state: 1 }),
    alarm({ priority: 4, state: 2 })
  ]);

  expect(summary).toEqual({
    total: 4,
    awaitingAcknowledgement: 3,
    acknowledged: 1,
    criticalAwaitingAcknowledgement: 1,
    highAwaitingAcknowledgement: 1
  });
});

test('keeps authentication and authorization failures explicit instead of mapping them to alarm state', () => {
  expect(classifyRuntimeAlarmEndpointIssue(401)).toBe('unauthenticated');
  expect(classifyRuntimeAlarmEndpointIssue(403)).toBe('forbidden');
  expect(classifyRuntimeAlarmEndpointIssue(404)).toBe('not-found');
  expect(classifyRuntimeAlarmEndpointIssue(500)).toBe('unavailable');
  expect(classifyRuntimeAlarmEndpointIssue(undefined)).toBe('unavailable');
});
