import type { EngineeringEntityBrowserFilter } from './EngineeringEntityBrowser.logic';
import type { AlarmEngineering } from './types';

export type AlarmWorkspaceFilterLabels = {
  enabled: string;
  disabled: string;
  requiresAck: string;
  priority: string;
  area: string;
  type: string;
};

export function alarmEngineeringKey(alarm: AlarmEngineering): string {
  if (alarm.id) return alarm.id;
  return [
    'alarm',
    alarm.name,
    alarm.tagPath ?? alarm.tagId ?? '',
    alarm.type,
    alarm.priority
  ].join('::');
}

export function alarmTagReference(alarm: AlarmEngineering): string {
  return alarm.tagPath ?? alarm.tagId ?? '—';
}

export function alarmSearchText(alarm: AlarmEngineering): readonly string[] {
  const metadata = Object.entries(alarm.metadata ?? {}).flatMap(([key, value]) => [key, value]);

  return [
    alarm.id ?? '',
    alarm.name,
    alarmTagReference(alarm),
    alarm.tagId ?? '',
    alarm.tagPath ?? '',
    alarm.type,
    alarm.priority,
    alarm.alarmClass ?? '',
    alarm.area ?? '',
    alarm.message ?? '',
    alarm.setpoint === null || alarm.setpoint === undefined ? '' : String(alarm.setpoint),
    alarm.digitalActiveValue === null || alarm.digitalActiveValue === undefined ? '' : String(alarm.digitalActiveValue),
    ...metadata
  ];
}

export function alarmConditionSummary(alarm: AlarmEngineering, notConfigured: string): string {
  if (alarm.setpoint !== null && alarm.setpoint !== undefined) {
    return `${alarm.type} · ${alarm.setpoint}`;
  }

  if (alarm.digitalActiveValue !== null && alarm.digitalActiveValue !== undefined) {
    return `${alarm.type} · ${alarm.digitalActiveValue}`;
  }

  return alarm.type || notConfigured;
}

export function buildAlarmWorkspaceFilters(
  alarms: readonly AlarmEngineering[],
  labels: AlarmWorkspaceFilterLabels
): EngineeringEntityBrowserFilter<AlarmEngineering>[] {
  const filters: EngineeringEntityBrowserFilter<AlarmEngineering>[] = [
    {
      key: 'status:enabled',
      label: labels.enabled,
      matches: alarm => alarm.enabled !== false
    },
    {
      key: 'status:disabled',
      label: labels.disabled,
      matches: alarm => alarm.enabled === false
    },
    {
      key: 'ack:required',
      label: labels.requiresAck,
      matches: alarm => alarm.requiresAcknowledgement === true
    }
  ];

  for (const priority of uniqueCanonicalValues(alarms.map(alarm => alarm.priority))) {
    filters.push({
      key: `priority:${priority}`,
      label: `${labels.priority} · ${priority}`,
      matches: alarm => canonicalValue(alarm.priority) === priority
    });
  }

  for (const area of uniqueCanonicalValues(alarms.map(alarm => alarm.area))) {
    filters.push({
      key: `area:${area}`,
      label: `${labels.area} · ${area}`,
      matches: alarm => canonicalValue(alarm.area) === area
    });
  }

  for (const type of uniqueCanonicalValues(alarms.map(alarm => alarm.type))) {
    filters.push({
      key: `type:${type}`,
      label: `${labels.type} · ${type}`,
      matches: alarm => canonicalValue(alarm.type) === type
    });
  }

  return filters;
}

function uniqueCanonicalValues(values: readonly (string | null | undefined)[]): string[] {
  return [...new Set(values.map(canonicalValue).filter(Boolean))]
    .sort((left, right) => left.localeCompare(right));
}

function canonicalValue(value: string | null | undefined): string {
  return value?.trim() ?? '';
}
