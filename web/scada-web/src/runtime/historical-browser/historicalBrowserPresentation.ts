import {
  HISTORICAL_TIME_RANGE_PRESETS,
  applyHistoricalPreset,
  createHistoricalTimeRangeState,
  historicalDurationSeconds,
  historicalPresetSeconds,
  localTimeZoneLabel,
  validateHistoricalTimeRange,
  type HistoricalTimeRangeMode,
  type HistoricalTimeRangeState,
  type HistoricalTimeRangeUnit
} from '../historicalTimeRange';
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
export type HistoricalBrowserTimeMode = HistoricalTimeRangeMode;
export const HISTORICAL_BROWSER_RELATIVE_PRESETS = HISTORICAL_TIME_RANGE_PRESETS;

/**
 * Transient view state only. It is deliberately not a Historical Query DTO and
 * must not be serialized as query authority. The shared Historical Query v1
 * contract remains the only API/query authority.
 */
export type HistoricalBrowserDraft = Readonly<{
  datasetKey: HistoricalBrowserDatasetKey;
  timeMode: HistoricalBrowserTimeMode;
  relativeAmount: number;
  relativeUnit: HistoricalTimeRangeUnit;
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
  const range = createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: 60 * 60 });
  return Object.freeze({
    datasetKey: 'historian.samples',
    timeMode: range.mode,
    relativeAmount: range.relativeAmount,
    relativeUnit: range.relativeUnit,
    absoluteFromLocal: range.absoluteFromLocal,
    absoluteToLocal: range.absoluteToLocal
  });
}

export function historicalBrowserTimeRange(draft: HistoricalBrowserDraft): HistoricalTimeRangeState {
  return Object.freeze({
    mode: draft.timeMode,
    relativeAmount: draft.relativeAmount,
    relativeUnit: draft.relativeUnit,
    absoluteFromLocal: draft.absoluteFromLocal,
    absoluteToLocal: draft.absoluteToLocal
  });
}

export function applyHistoricalBrowserPreset(
  draft: HistoricalBrowserDraft,
  seconds: number
): HistoricalBrowserDraft {
  const range = applyHistoricalPreset(historicalBrowserTimeRange(draft), seconds);
  return Object.freeze({
    ...draft,
    relativeAmount: range.relativeAmount,
    relativeUnit: range.relativeUnit
  });
}

export function historicalBrowserPresetSeconds(draft: HistoricalBrowserDraft): number | null {
  return historicalPresetSeconds(historicalBrowserTimeRange(draft));
}

export function historicalBrowserDurationSeconds(draft: HistoricalBrowserDraft): number {
  return historicalDurationSeconds(historicalBrowserTimeRange(draft));
}

/** UI preflight only. Server-side Historical Query validation remains authoritative. */
export function validateHistoricalBrowserDraft(
  draft: HistoricalBrowserDraft,
  locale: HistoricalBrowserLocale = 'en'
): HistoricalBrowserDraftValidation {
  const text = historicalBrowserCopy(locale);
  const diagnostics: string[] = [];

  if (!HISTORICAL_BROWSER_DATASET_KEYS.includes(draft.datasetKey)) diagnostics.push(text.unknownDataset);

  const validation = validateHistoricalTimeRange(historicalBrowserTimeRange(draft));
  for (const issue of validation.issues) {
    if (issue === 'relative-invalid') diagnostics.push(text.relativePositive);
    else if (issue === 'range-too-large') diagnostics.push(text.rangeTooLarge);
    else if (issue === 'absolute-required') diagnostics.push(text.absoluteRequired);
    else if (issue === 'absolute-order') diagnostics.push(text.absoluteOrder);
    else if (issue === 'absolute-ambiguous') diagnostics.push(text.absoluteAmbiguous);
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
      return typeof value === 'boolean' ? (value ? text.trueLabel : text.falseLabel) : text.unavailable;
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
  if (draft.timeMode === 'live' || draft.timeMode === 'relative') {
    const seconds = historicalBrowserDurationSeconds(draft);
    const preset = HISTORICAL_BROWSER_RELATIVE_PRESETS.find(item => item.seconds === seconds);
    const duration = preset?.label ?? String(draft.relativeAmount) + ' ' + unitLabel(draft.relativeUnit, text);
    return (draft.timeMode === 'live' ? text.live + ' · ' : text.last + ' ') + duration;
  }

  if (!draft.absoluteFromLocal || !draft.absoluteToLocal) return text.absoluteNotSelected;
  const from = new Date(draft.absoluteFromLocal);
  const to = new Date(draft.absoluteToLocal);
  const formatter = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' });
  return formatter.format(from) + ' → ' + formatter.format(to) + ' · ' + localTimeZoneLabel();
}

function unitLabel(unit: HistoricalTimeRangeUnit, text: ReturnType<typeof historicalBrowserCopy>): string {
  if (unit === 'seconds') return text.seconds;
  if (unit === 'minutes') return text.minutes;
  if (unit === 'hours') return text.hours;
  return text.days;
}
