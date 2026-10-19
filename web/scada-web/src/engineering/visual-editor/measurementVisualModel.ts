import { normalizeRuntimeTagQuality } from '../../runtime/tagInspectorModel';
import type { RuntimeTagQualityName } from '../../runtime/tagInspectorTypes';
import type { VisualDynamicSample } from './visualDynamicRuntime';

export type MeasurementScale = Readonly<{
  minimum: number;
  maximum: number;
  percent: number;
  valid: boolean;
}>;

export function measurementScale(value: number, minimum: number, maximum: number): MeasurementScale {
  const valid = Number.isFinite(value) && Number.isFinite(minimum) && Number.isFinite(maximum) && minimum < maximum;
  const safeMinimum = Number.isFinite(minimum) ? minimum : 0;
  const safeMaximum = Number.isFinite(maximum) && maximum > safeMinimum ? maximum : safeMinimum + 1;
  const safeValue = Number.isFinite(value) ? value : safeMinimum;
  return Object.freeze({
    minimum: safeMinimum,
    maximum: safeMaximum,
    percent: Math.max(0, Math.min(100, ((safeValue - safeMinimum) / (safeMaximum - safeMinimum)) * 100)),
    valid
  });
}

export function measurementBandPercentages(
  minimum: number,
  maximum: number,
  firstEnd: number,
  secondEnd: number
): Readonly<{ first: number; second: number; valid: boolean }> {
  const valid = [minimum, maximum, firstEnd, secondEnd].every(Number.isFinite) &&
    minimum < firstEnd && firstEnd < secondEnd && secondEnd < maximum;
  const scale = measurementScale(minimum, minimum, maximum);
  const first = Math.max(0, Math.min(100, ((firstEnd - scale.minimum) / (scale.maximum - scale.minimum)) * 100));
  const second = Math.max(first, Math.min(100, ((secondEnd - scale.minimum) / (scale.maximum - scale.minimum)) * 100));
  return Object.freeze({ first, second, valid });
}

export type MeasurementQualityState = RuntimeTagQualityName | 'unavailable';

/**
 * Keep the widget on the same serialized TagQuality contract as the Runtime
 * inspector. A non-local sample state overrides an otherwise-good quality so
 * an unavailable projection can never be presented as a healthy measurement.
 */
export function measurementQualityState(sample: Pick<VisualDynamicSample, 'quality' | 'state'> | undefined, hasValueBinding: boolean): MeasurementQualityState {
  if (!hasValueBinding) return 'unknown';
  if (sample?.state && sample.state !== 'LocalSession') return 'unavailable';
  return normalizeRuntimeTagQuality(sample?.quality);
}

