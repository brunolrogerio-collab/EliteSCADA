import type {
  VisualElementEngineering,
  VisualEngineeringPropertyValue
} from '../engineering/types';

export const BROWSER_CONFIG_PROPERTY = 'browserConfig';
export const BROWSER_CONFIG_VERSION = 1 as const;

export type BrowserSortDirection = 'ascending' | 'descending';
export type AlarmBrowserMode = 'current' | 'history';
export type AlarmBrowserLifecycle = 'all' | 'active' | 'returned';
export type AlarmBrowserAcknowledgement = 'all' | 'acknowledged' | 'unacknowledged';

export const ALARM_BROWSER_COLUMNS = Object.freeze([
  'timestamp', 'state', 'priority', 'name', 'area', 'tag.path', 'message', 'acknowledgedBy'
] as const);
export type AlarmBrowserColumn = typeof ALARM_BROWSER_COLUMNS[number];

export const EVENT_BROWSER_COLUMNS = Object.freeze([
  'timestamp', 'type', 'category', 'source', 'area', 'equipment.path', 'tag.path',
  'operator', 'operation', 'command.key', 'message'
] as const);
export type EventBrowserColumn = typeof EVENT_BROWSER_COLUMNS[number];

export type AlarmBrowserConfig = Readonly<{
  version: typeof BROWSER_CONFIG_VERSION;
  mode: AlarmBrowserMode;
  lifecycle: AlarmBrowserLifecycle;
  acknowledgement: AlarmBrowserAcknowledgement;
  minimumPriority: number | null;
  area: string;
  tagPath: string;
  text: string;
  lookbackSeconds: number;
  columns: readonly AlarmBrowserColumn[];
  sortField: 'timestamp' | 'state' | 'priority' | 'tag.path';
  sortDirection: BrowserSortDirection;
  pageSize: number;
  acknowledgeEnabled: boolean;
}>;

export type EventBrowserConfig = Readonly<{
  version: typeof BROWSER_CONFIG_VERSION;
  type: string;
  category: string;
  source: string;
  area: string;
  equipmentPath: string;
  tagPath: string;
  operator: string;
  operation: string;
  commandKey: string;
  text: string;
  lookbackSeconds: number;
  columns: readonly EventBrowserColumn[];
  sortField: 'timestamp' | 'type' | 'category' | 'source' | 'area' | 'tag.path';
  sortDirection: BrowserSortDirection;
  pageSize: number;
}>;

export const DEFAULT_ALARM_BROWSER_CONFIG: AlarmBrowserConfig = Object.freeze({
  version: BROWSER_CONFIG_VERSION,
  mode: 'current',
  lifecycle: 'active',
  acknowledgement: 'all',
  minimumPriority: null,
  area: '',
  tagPath: '',
  text: '',
  lookbackSeconds: 86400,
  columns: Object.freeze([
    'timestamp', 'state', 'priority', 'name', 'area', 'tag.path', 'message'
  ] as const),
  sortField: 'timestamp',
  sortDirection: 'descending',
  pageSize: 50,
  acknowledgeEnabled: true
});

export const DEFAULT_EVENT_BROWSER_CONFIG: EventBrowserConfig = Object.freeze({
  version: BROWSER_CONFIG_VERSION,
  type: '',
  category: '',
  source: '',
  area: '',
  equipmentPath: '',
  tagPath: '',
  operator: '',
  operation: '',
  commandKey: '',
  text: '',
  lookbackSeconds: 86400,
  columns: Object.freeze([
    'timestamp', 'type', 'category', 'source', 'area', 'equipment.path', 'tag.path',
    'operator', 'operation', 'message'
  ] as const),
  sortField: 'timestamp',
  sortDirection: 'descending',
  pageSize: 50
});

export function readAlarmBrowserConfig(element: Pick<VisualElementEngineering, 'properties'>): AlarmBrowserConfig {
  return normalizeAlarmBrowserConfig(element.properties?.[BROWSER_CONFIG_PROPERTY]);
}

export function readEventBrowserConfig(element: Pick<VisualElementEngineering, 'properties'>): EventBrowserConfig {
  return normalizeEventBrowserConfig(element.properties?.[BROWSER_CONFIG_PROPERTY]);
}

export function normalizeAlarmBrowserConfig(input: unknown): AlarmBrowserConfig {
  if (input === undefined || input === null) return DEFAULT_ALARM_BROWSER_CONFIG;
  if (!isRecord(input)) throw new Error('Alarm Browser configuration must be an object.');
  ensureVersion(input.version, 'Alarm Browser');
  const minimumPriority = input.minimumPriority === null || input.minimumPriority === undefined
    ? null
    : integer(input.minimumPriority, 'Alarm Browser minimum priority', 1, 4);
  return Object.freeze({
    version: BROWSER_CONFIG_VERSION,
    mode: enumValue(input.mode, ['current', 'history'] as const, 'Alarm Browser mode', DEFAULT_ALARM_BROWSER_CONFIG.mode),
    lifecycle: enumValue(input.lifecycle, ['all', 'active', 'returned'] as const, 'Alarm Browser lifecycle', DEFAULT_ALARM_BROWSER_CONFIG.lifecycle),
    acknowledgement: enumValue(input.acknowledgement, ['all', 'acknowledged', 'unacknowledged'] as const, 'Alarm Browser acknowledgement', DEFAULT_ALARM_BROWSER_CONFIG.acknowledgement),
    minimumPriority,
    area: optionalText(input.area, 'Alarm Browser area', 240),
    tagPath: optionalText(input.tagPath, 'Alarm Browser TAG path', 500),
    text: optionalText(input.text, 'Alarm Browser text', 500),
    lookbackSeconds: integer(input.lookbackSeconds ?? DEFAULT_ALARM_BROWSER_CONFIG.lookbackSeconds, 'Alarm Browser lookback', 60, 2678400),
    columns: normalizeColumns(input.columns, ALARM_BROWSER_COLUMNS, DEFAULT_ALARM_BROWSER_CONFIG.columns, 'Alarm Browser'),
    sortField: enumValue(input.sortField, ['timestamp', 'state', 'priority', 'tag.path'] as const, 'Alarm Browser sort field', DEFAULT_ALARM_BROWSER_CONFIG.sortField),
    sortDirection: enumValue(input.sortDirection, ['ascending', 'descending'] as const, 'Alarm Browser sort direction', DEFAULT_ALARM_BROWSER_CONFIG.sortDirection),
    pageSize: integer(input.pageSize ?? DEFAULT_ALARM_BROWSER_CONFIG.pageSize, 'Alarm Browser page size', 10, 200),
    acknowledgeEnabled: booleanValue(input.acknowledgeEnabled, DEFAULT_ALARM_BROWSER_CONFIG.acknowledgeEnabled, 'Alarm Browser acknowledge enabled')
  });
}

export function normalizeEventBrowserConfig(input: unknown): EventBrowserConfig {
  if (input === undefined || input === null) return DEFAULT_EVENT_BROWSER_CONFIG;
  if (!isRecord(input)) throw new Error('Event Browser configuration must be an object.');
  ensureVersion(input.version, 'Event Browser');
  return Object.freeze({
    version: BROWSER_CONFIG_VERSION,
    type: optionalText(input.type, 'Event Browser type', 120),
    category: optionalText(input.category, 'Event Browser category', 120),
    source: optionalText(input.source, 'Event Browser source', 240),
    area: optionalText(input.area, 'Event Browser area', 240),
    equipmentPath: optionalText(input.equipmentPath, 'Event Browser equipment path', 500),
    tagPath: optionalText(input.tagPath, 'Event Browser TAG path', 500),
    operator: optionalText(input.operator, 'Event Browser operator', 240),
    operation: optionalText(input.operation, 'Event Browser operation', 240),
    commandKey: optionalText(input.commandKey, 'Event Browser command key', 240),
    text: optionalText(input.text, 'Event Browser text', 500),
    lookbackSeconds: integer(input.lookbackSeconds ?? DEFAULT_EVENT_BROWSER_CONFIG.lookbackSeconds, 'Event Browser lookback', 60, 2678400),
    columns: normalizeColumns(input.columns, EVENT_BROWSER_COLUMNS, DEFAULT_EVENT_BROWSER_CONFIG.columns, 'Event Browser'),
    sortField: enumValue(input.sortField, ['timestamp', 'type', 'category', 'source', 'area', 'tag.path'] as const, 'Event Browser sort field', DEFAULT_EVENT_BROWSER_CONFIG.sortField),
    sortDirection: enumValue(input.sortDirection, ['ascending', 'descending'] as const, 'Event Browser sort direction', DEFAULT_EVENT_BROWSER_CONFIG.sortDirection),
    pageSize: integer(input.pageSize ?? DEFAULT_EVENT_BROWSER_CONFIG.pageSize, 'Event Browser page size', 10, 200)
  });
}

export function alarmBrowserEngineeringValue(config: AlarmBrowserConfig): VisualEngineeringPropertyValue {
  return cloneStructured(normalizeAlarmBrowserConfig(config));
}

export function eventBrowserEngineeringValue(config: EventBrowserConfig): VisualEngineeringPropertyValue {
  return cloneStructured(normalizeEventBrowserConfig(config));
}

function cloneStructured<T>(value: T): VisualEngineeringPropertyValue {
  return JSON.parse(JSON.stringify(value)) as VisualEngineeringPropertyValue;
}

function ensureVersion(value: unknown, label: string): void {
  if (value === undefined) return;
  if (value !== BROWSER_CONFIG_VERSION) throw new Error(`${label} configuration version is unsupported.`);
}

function normalizeColumns<T extends string>(
  value: unknown,
  allowed: readonly T[],
  fallback: readonly T[],
  label: string
): readonly T[] {
  if (value === undefined) return fallback;
  if (!Array.isArray(value) || value.length === 0) throw new Error(`${label} must show at least one column.`);
  const result: T[] = [];
  const seen = new Set<string>();
  for (const candidate of value) {
    if (typeof candidate !== 'string' || !allowed.includes(candidate as T)) throw new Error(`${label} column '${String(candidate)}' is invalid.`);
    if (seen.add(candidate)) result.push(candidate as T);
  }
  return Object.freeze(result);
}

function optionalText(value: unknown, label: string, maximumLength: number): string {
  if (value === undefined || value === null) return '';
  if (typeof value !== 'string') throw new Error(`${label} must be text.`);
  const normalized = value.trim();
  if (normalized.length > maximumLength) throw new Error(`${label} exceeds ${maximumLength} characters.`);
  if (/[\u0000-\u001F\u007F]/.test(normalized)) throw new Error(`${label} contains control characters.`);
  return normalized;
}

function integer(value: unknown, label: string, minimum: number, maximum: number): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum || value > maximum) {
    throw new Error(`${label} must be an integer from ${minimum} to ${maximum}.`);
  }
  return value;
}

function booleanValue(value: unknown, fallback: boolean, label: string): boolean {
  if (value === undefined) return fallback;
  if (typeof value !== 'boolean') throw new Error(`${label} must be boolean.`);
  return value;
}

function enumValue<T extends string>(value: unknown, allowed: readonly T[], label: string, fallback: T): T {
  if (value === undefined) return fallback;
  if (typeof value !== 'string' || !allowed.includes(value as T)) throw new Error(`${label} is invalid.`);
  return value as T;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
