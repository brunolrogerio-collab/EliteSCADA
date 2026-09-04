import {
  historicalBrowserCopy,
  type HistoricalBrowserLocale
} from './historicalBrowserI18n';

export const HISTORICAL_BROWSER_DATASET_KEYS = [
  'historian.samples',
  'alarm.events',
  'operational.events'
] as const;

export type HistoricalBrowserDatasetKey = typeof HISTORICAL_BROWSER_DATASET_KEYS[number];
export type HistoricalBrowserTimeMode = 'relative' | 'absolute';

export const HISTORICAL_BROWSER_RELATIVE_PRESETS = Object.freeze([
  Object.freeze({ seconds: 15 * 60, label: '15 min' }),
  Object.freeze({ seconds: 60 * 60, label: '1 h' }),
  Object.freeze({ seconds: 8 * 60 * 60, label: '8 h' }),
  Object.freeze({ seconds: 24 * 60 * 60, label: '24 h' }),
  Object.freeze({ seconds: 7 * 24 * 60 * 60, label: '7 d' })
] as const);

/**
 * Transient view state only. It is deliberately not a Historical Query DTO and
 * must not be serialized as query authority. The shared Historical Query v1
 * contract remains the only API/query authority.
 */
export type HistoricalBrowserDraft = Readonly<{
  datasetKey: HistoricalBrowserDatasetKey;
  timeMode: HistoricalBrowserTimeMode;
  relativeDurationSeconds: number;
  absoluteFromLocal: string;
  absoluteToLocal: string;
}>;

export type HistoricalBrowserDraftValidation = Readonly<{
  ok: boolean;
  diagnostics: readonly string[];
}>;

export type HistoricalScalarType =
  | 'Boolean'
  | 'Int16'
  | 'Int32'
  | 'Int64'
  | 'Float'
  | 'Double'
  | 'String'
  | 'DateTime';

export function createHistoricalBrowserDraft(): HistoricalBrowserDraft {
  return Object.freeze({
    datasetKey: 'historian.samples',
    timeMode: 'relative',
    relativeDurationSeconds: 60 * 60,
    absoluteFromLocal: '',
    absoluteToLocal: ''
  });
}

/**
 * UI preflight only. Server-side Historical Query validation remains authoritative.
 */
export function validateHistoricalBrowserDraft(
  draft: HistoricalBrowserDraft,
  locale: HistoricalBrowserLocale = 'en'
): HistoricalBrowserDraftValidation {
  const text = historicalBrowserCopy(locale);
  const diagnostics: string[] = [];

  if (!HISTORICAL_BROWSER_DATASET_KEYS.includes(draft.datasetKey)) {
    diagnostics.push(text.unknownDataset);
  }

  if (draft.timeMode === 'relative') {
    if (!Number.isSafeInteger(draft.relativeDurationSeconds) || draft.relativeDurationSeconds <= 0) {
      diagnostics.push(text.relativePositive);
    }
  } else {
    const from = parseLocalDateTime(draft.absoluteFromLocal);
    const to = parseLocalDateTime(draft.absoluteToLocal);
    if (from === null || to === null) diagnostics.push(text.absoluteRequired);
    else if (from >= to) diagnostics.push(text.absoluteOrder);
  }

  return Object.freeze({ ok: diagnostics.length === 0, diagnostics: Object.freeze(diagnostics) });
}

/**
 * Presentation-only scalar formatter. In particular, Int64 is never routed
 * through Number(), preserving the exact decimal string supplied by the shared
 * query wire contract.
 */
export function formatHistoricalScalar(
  value: unknown,
  scalarType: HistoricalScalarType,
  locale: HistoricalBrowserLocale = 'en'
): string {
  if (value === null || value === undefined) return '—';
  const text = historicalBrowserCopy(locale);

  switch (scalarType) {
    case 'Int64':
      if (typeof value !== 'string' || !/^-?\d+$/.test(value)) return text.unavailable;
      return value;
    case 'Boolean':
      return typeof value === 'boolean'
        ? (value ? text.trueLabel : text.falseLabel)
        : text.unavailable;
    case 'Int16':
    case 'Int32':
      return typeof value === 'number' && Number.isSafeInteger(value) ? String(value) : text.unavailable;
    case 'Float':
    case 'Double':
      return typeof value === 'number' && Number.isFinite(value) ? String(value) : text.unavailable;
    case 'String':
      return typeof value === 'string' ? value : text.unavailable;
    case 'DateTime':
      return typeof value === 'string' && value.trim() ? value : text.unavailable;
  }
}

export function historicalDatasetLabel(
  datasetKey: HistoricalBrowserDatasetKey,
  locale: HistoricalBrowserLocale = 'en'
): string {
  const text = historicalBrowserCopy(locale);
  switch (datasetKey) {
    case 'historian.samples': return text.datasetHistorian;
    case 'alarm.events': return text.datasetAlarms;
    case 'operational.events': return text.datasetOperationalEvents;
  }
}

export function historicalTimeSummary(
  draft: HistoricalBrowserDraft,
  locale: HistoricalBrowserLocale = 'en'
): string {
  const text = historicalBrowserCopy(locale);
  if (draft.timeMode === 'relative') {
    const preset = HISTORICAL_BROWSER_RELATIVE_PRESETS.find(item => item.seconds === draft.relativeDurationSeconds);
    return `${text.last} ${preset?.label ?? `${draft.relativeDurationSeconds} s`}`;
  }

  return draft.absoluteFromLocal && draft.absoluteToLocal
    ? `${draft.absoluteFromLocal} → ${draft.absoluteToLocal}`
    : text.absoluteNotSelected;
}

function parseLocalDateTime(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Date.parse(value);
  return Number.isFinite(parsed) ? parsed : null;
}
