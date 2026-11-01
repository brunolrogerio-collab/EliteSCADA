import type { VisualEngineeringPropertyValue } from '../types';

export const CHECK_BOX_CONFIG_PROPERTY = 'checkBoxConfig';
export const COMBO_BOX_OPTIONS_PROPERTY = 'comboBoxOptions';

export type AlarmSuppressionReason = 'maintenance' | 'startup' | 'testing' | 'other';
export type AlarmSuppressionDuration = 'untilReleased' | '1h' | '4h' | '8h';

export type CheckBoxAlarmSuppressionConfig = Readonly<{
  mode: 'alarmSuppression';
  alarmDefinitionId: string;
  reasonCode: AlarmSuppressionReason;
  duration: AlarmSuppressionDuration;
}>;

export type ComboBoxOption = Readonly<{
  value: string;
  label: string;
}>;

export type ComboBoxSelection =
  | Readonly<{ state: 'unconfigured'; message: string }>
  | Readonly<{ state: 'unavailable'; message: string }>
  | Readonly<{ state: 'incompatible'; message: string }>
  | Readonly<{ state: 'missing-option'; value: string }>
  | Readonly<{ state: 'selected'; value: string }>;

const REASONS = new Set<AlarmSuppressionReason>(['maintenance', 'startup', 'testing', 'other']);
const DURATIONS = new Set<AlarmSuppressionDuration>(['untilReleased', '1h', '4h', '8h']);
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function readCheckBoxAlarmSuppressionConfig(value: unknown): CheckBoxAlarmSuppressionConfig | null {
  if (value === undefined || value === null) return null;
  if (!isRecord(value) || value.mode !== 'alarmSuppression' ||
      typeof value.alarmDefinitionId !== 'string' || !UUID.test(value.alarmDefinitionId) ||
      typeof value.reasonCode !== 'string' || !REASONS.has(value.reasonCode as AlarmSuppressionReason) ||
      typeof value.duration !== 'string' || !DURATIONS.has(value.duration as AlarmSuppressionDuration)) {
    throw new Error('CheckBox alarm-suppression configuration is invalid.');
  }
  return Object.freeze({
    mode: 'alarmSuppression',
    alarmDefinitionId: value.alarmDefinitionId,
    reasonCode: value.reasonCode as AlarmSuppressionReason,
    duration: value.duration as AlarmSuppressionDuration
  });
}

export function normalizeComboBoxOptions(value: unknown): readonly ComboBoxOption[] {
  if (!Array.isArray(value) || value.length === 0 || value.length > 64) {
    throw new Error('ComboBox must have between 1 and 64 options.');
  }
  const seen = new Set<string>();
  const options = value.map((candidate, index) => {
    if (!isRecord(candidate) || typeof candidate.value !== 'string' || typeof candidate.label !== 'string') {
      throw new Error(`ComboBox option ${index + 1} must have a text value and label.`);
    }
    const optionValue = candidate.value.trim();
    const label = candidate.label.trim();
    if (!optionValue || optionValue.length > 120 || !label || label.length > 120) {
      throw new Error(`ComboBox option ${index + 1} must have a value and label of at most 120 characters.`);
    }
    if (seen.has(optionValue)) throw new Error(`ComboBox option value '${optionValue}' is duplicated.`);
    seen.add(optionValue);
    return Object.freeze({ value: optionValue, label });
  });
  return Object.freeze(options);
}

export function comboBoxOptionsEngineeringValue(options: readonly ComboBoxOption[]): VisualEngineeringPropertyValue {
  return Object.freeze(normalizeComboBoxOptions(options).map(option => Object.freeze({
    value: option.value,
    label: option.label
  }))) as VisualEngineeringPropertyValue;
}

export function resolveComboBoxSelection(
  rawOptions: unknown,
  sample: Readonly<{ value: unknown; dataType?: string | null; quality?: string | number | null; state?: string | null }> | undefined
): ComboBoxSelection {
  let options: readonly ComboBoxOption[];
  try { options = normalizeComboBoxOptions(rawOptions); }
  catch (error) {
    return Object.freeze({ state: 'unconfigured', message: error instanceof Error ? error.message : String(error) });
  }
  if (!sample || sample.state || sample.quality !== 0 && String(sample.quality).toLowerCase() !== 'good') {
    return Object.freeze({ state: 'unavailable', message: 'The selected TAG does not have a current Good value.' });
  }
  const dataType = sample.dataType?.trim().toLowerCase();
  if ((dataType !== 'string' && dataType !== 'enum') || typeof sample.value !== 'string') {
    return Object.freeze({ state: 'incompatible', message: 'ComboBox requires a String or Enum TAG.' });
  }
  const current = sample.value;
  if (!options.some(option => option.value === current)) {
    return Object.freeze({ state: 'missing-option', value: current });
  }
  return Object.freeze({ state: 'selected', value: current });
}

export function suppressionExpiry(duration: AlarmSuppressionDuration, now = new Date()): string | null {
  const seconds: Readonly<Record<AlarmSuppressionDuration, number | null>> = {
    untilReleased: null,
    '1h': 3600,
    '4h': 14400,
    '8h': 28800
  };
  const value = seconds[duration];
  return value === null ? null : new Date(now.getTime() + value * 1000).toISOString();
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
