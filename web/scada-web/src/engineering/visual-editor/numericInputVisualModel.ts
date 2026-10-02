import type { BindingEngineering, VisualElementEngineering } from '../types';
import { VISUAL_PROPERTY_KEYS, type VisualPropertyValue } from '../../visual-runtime';
import { visualTagSampleKey, type VisualDynamicDiagnostic, type VisualDynamicSample } from './visualDynamicRuntime';

export type NumericInputResolvedConfiguration = Readonly<{
  value: number;
  minimum: number;
  maximum: number;
  step: number;
  interactionEnabled: boolean;
  valueBinding: BindingEngineering | null;
  tagId: string | null;
  sourceAvailable: boolean;
  sourceReadOnly: boolean;
  writeDirection: boolean;
  sampleTimestamp: string | null;
  unit: string;
  precision: number | null;
}>;

export function resolveNumericInputConfiguration(
  element: VisualElementEngineering,
  values: Readonly<Record<string, VisualPropertyValue>>,
  diagnostics: readonly VisualDynamicDiagnostic[],
  liveSamples: ReadonlyMap<string, VisualDynamicSample>
): NumericInputResolvedConfiguration {
  const minimum = requiredNumber(values[VISUAL_PROPERTY_KEYS.minimum], 'minimum');
  const maximum = requiredNumber(values[VISUAL_PROPERTY_KEYS.maximum], 'maximum');
  const step = requiredNumber(values[VISUAL_PROPERTY_KEYS.step], 'step');
  if (minimum >= maximum) throw new Error('Numeric Input minimum must be less than maximum.');
  if (step <= 0) throw new Error('Numeric Input step must be greater than zero.');

  const value = requiredNumber(values[VISUAL_PROPERTY_KEYS.value], 'value');
  const valueBinding = (element.bindings ?? []).find(binding =>
    binding.key === VISUAL_PROPERTY_KEYS.value && binding.kind.trim().toLowerCase() === 'tag'
  ) ?? null;
  const tagId = valueBinding?.tagReference?.selector == null
    ? valueBinding?.tagReference?.tagId?.trim() || null
    : null;
  const sample = tagId
    ? liveSamples.get(visualTagSampleKey(tagId)) ?? (valueBinding?.target ? liveSamples.get(valueBinding.target) : undefined)
    : undefined;
  const valueDiagnostic = diagnostics.some(diagnostic => diagnostic.propertyKey === VISUAL_PROPERTY_KEYS.value);
  const decimalPlacesEnabled = values[VISUAL_PROPERTY_KEYS.decimalPlacesEnabled] === true;
  const configuredPrecision = values[VISUAL_PROPERTY_KEYS.decimalPlaces];
  const precision = decimalPlacesEnabled
    ? typeof configuredPrecision === 'number' && Number.isInteger(configuredPrecision) && configuredPrecision >= 0 && configuredPrecision <= 12
      ? configuredPrecision
      : 2
    : null;
  const configuredUnit = values[VISUAL_PROPERTY_KEYS.unit];
  const unit = (typeof configuredUnit === 'string' ? configuredUnit.trim() : '') || valueBinding?.metadata?.engineeringUnit?.trim() || '';

  return Object.freeze({
    value,
    minimum,
    maximum,
    step,
    interactionEnabled: values[VISUAL_PROPERTY_KEYS.interactionEnabled] === true,
    valueBinding,
    tagId,
    sourceAvailable: Boolean(sample) && !valueDiagnostic && isGoodSample(sample!),
    sourceReadOnly: sample?.readOnly === true,
    writeDirection: hasWriteDirection(valueBinding?.direction),
    sampleTimestamp: sample?.timestamp ?? null,
    unit,
    precision
  });
}

export function validateNumericInputCandidate(
  raw: string,
  minimum: number,
  maximum: number,
  step?: number
): number {
  if (!raw.trim()) throw new Error('A numeric setpoint is required.');
  const value = Number(raw);
  if (!Number.isFinite(value)) throw new Error('Setpoint must be a finite number.');
  if (value < minimum || value > maximum) {
    throw new Error(`Setpoint must be between ${minimum} and ${maximum}.`);
  }
  if (step !== undefined) {
    if (!Number.isFinite(step) || step <= 0) throw new Error('Setpoint step must be a positive finite number.');
    const nearestStep = minimum + Math.round((value - minimum) / step) * step;
    if (Math.abs(value - nearestStep) > Math.max(1, Math.abs(value)) * 1e-9) {
      throw new Error(`Setpoint must use increments of ${step} from ${minimum}.`);
    }
  }
  return value;
}

export function formatNumericInputValue(value: number, precision: number | null): string {
  if (precision !== null) return value.toFixed(precision);
  return String(value);
}

function requiredNumber(value: VisualPropertyValue | undefined, label: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error(`Numeric Input ${label} must be finite.`);
  return value;
}

function hasWriteDirection(direction: string | null | undefined): boolean {
  const normalized = direction?.trim().toLowerCase();
  return ['write', 'readwrite', 'read-write', 'bidirectional', 'twoway', 'two-way'].includes(normalized ?? '');
}

function isGoodSample(sample: VisualDynamicSample): boolean {
  if (sample.state && sample.state !== 'LocalSession') return false;
  if (sample.value === null || sample.value === undefined) return false;
  return sample.quality === undefined || sample.quality === null || sample.quality === 0 || String(sample.quality).toLowerCase() === 'good';
}
