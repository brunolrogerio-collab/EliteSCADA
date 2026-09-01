import type { BindingEngineering, VisualElementEngineering } from '../types';
import { VISUAL_PROPERTY_KEYS, type VisualPropertyValue } from '../../visual-runtime';
import { visualTagSampleKey, type VisualDynamicDiagnostic, type VisualDynamicSample } from './visualDynamicRuntime';

export type SliderResolvedConfiguration = Readonly<{
  value: number;
  minimum: number;
  maximum: number;
  step: number;
  orientation: 'horizontal' | 'vertical';
  interactionEnabled: boolean;
  reverseDirection: boolean;
  trackColor: string;
  thumbColor: string;
  valueBinding: BindingEngineering | null;
  tagId: string | null;
  sourceAvailable: boolean;
  sourceReadOnly: boolean;
  writeDirection: boolean;
}>;

export function resolveSliderConfiguration(
  element: VisualElementEngineering,
  values: Readonly<Record<string, VisualPropertyValue>>,
  diagnostics: readonly VisualDynamicDiagnostic[],
  liveSamples: ReadonlyMap<string, VisualDynamicSample>
): SliderResolvedConfiguration {
  const minimum = requiredNumber(values[VISUAL_PROPERTY_KEYS.minimum], 'minimum');
  const maximum = requiredNumber(values[VISUAL_PROPERTY_KEYS.maximum], 'maximum');
  const step = requiredNumber(values[VISUAL_PROPERTY_KEYS.step], 'step');
  if (minimum >= maximum) throw new Error('Slider minimum must be less than maximum.');
  if (step <= 0) throw new Error('Slider step must be greater than zero.');

  const value = quantizeAndClamp(requiredNumber(values[VISUAL_PROPERTY_KEYS.value], 'value'), minimum, maximum, step);
  const orientationValue = values[VISUAL_PROPERTY_KEYS.orientation];
  const orientation = orientationValue === 'vertical' ? 'vertical' : 'horizontal';
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

  return Object.freeze({
    value,
    minimum,
    maximum,
    step,
    orientation,
    interactionEnabled: values[VISUAL_PROPERTY_KEYS.interactionEnabled] === true,
    reverseDirection: values[VISUAL_PROPERTY_KEYS.reverseDirection] === true,
    trackColor: color(values[VISUAL_PROPERTY_KEYS.trackColor], '#6B7280'),
    thumbColor: color(values[VISUAL_PROPERTY_KEYS.thumbColor], '#E5E7EB'),
    valueBinding,
    tagId,
    sourceAvailable: Boolean(sample) && !valueDiagnostic && isGoodSample(sample!),
    sourceReadOnly: sample?.readOnly === true,
    writeDirection: hasWriteDirection(valueBinding?.direction)
  });
}

export function quantizeAndClamp(value: number, minimum: number, maximum: number, step: number): number {
  if (![value, minimum, maximum, step].every(Number.isFinite) || minimum >= maximum || step <= 0) {
    throw new Error('Slider numeric configuration is invalid.');
  }
  const clamped = Math.max(minimum, Math.min(maximum, value));
  const steps = Math.round((clamped - minimum) / step);
  return Math.max(minimum, Math.min(maximum, Number((minimum + steps * step).toFixed(12))));
}

function requiredNumber(value: VisualPropertyValue | undefined, label: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error(`Slider ${label} must be finite.`);
  return value;
}

function color(value: VisualPropertyValue | undefined, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
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
