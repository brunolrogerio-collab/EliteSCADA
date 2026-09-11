import type { BindingEngineering, VisualElementEngineering } from '../../types';
import type { VisualLiveScalarSample } from '../visualEditorLiveValues';
import {
  resolveDynamoVisualState,
  type DynamoCommandIntent,
  type DynamoQualityState,
  type DynamoResolvedVisualState,
  type DynamoSettledState
} from './dynamoStateModel';

export type DynamoRuntimeStateResolution = Readonly<{
  state: DynamoResolvedVisualState;
  parameterSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  feedbackMismatch: boolean;
}>;

/**
 * Resolves the C07 semantic state from the per-instance projected Dynamo
 * composition. Only bindings that explicitly opt into a public
 * `dynamoParameter` participate, so renderer-private child details do not become
 * an accidental public contract.
 */
export function resolveDynamoRuntimeState(
  elements: readonly VisualElementEngineering[],
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>,
  commandIntent: DynamoCommandIntent = null
): DynamoRuntimeStateResolution {
  const parameterSamples = collectPublicParameterSamples(elements, liveSamples);
  const quality = worstQuality([...parameterSamples.values()].map(sampleQuality));
  const fault = booleanParameter(parameterSamples, 'fault');
  const alarm = booleanParameter(parameterSamples, 'alarm') || booleanParameter(parameterSamples, 'high');
  const running = optionalBooleanParameter(parameterSamples, 'running');
  const open = optionalBooleanParameter(parameterSamples, 'open');
  const closed = optionalBooleanParameter(parameterSamples, 'closed');

  let feedbackMismatch = false;
  let settledState: DynamoSettledState = 'unknown';
  if (running !== null) {
    settledState = running ? 'active' : 'inactive';
  } else if (open !== null || closed !== null) {
    if (open === true && closed === true) {
      feedbackMismatch = true;
    } else if (open === true) {
      settledState = 'active';
    } else if (closed === true) {
      settledState = 'inactive';
    } else if (open === false && closed === false) {
      settledState = 'transitioning';
    }
  }

  const state = resolveDynamoVisualState({
    quality,
    fault: fault || feedbackMismatch,
    alarm,
    commandIntent,
    settledState
  });

  return Object.freeze({
    state,
    parameterSamples,
    feedbackMismatch
  });
}

export function collectPublicParameterSamples(
  elements: readonly VisualElementEngineering[],
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>
): ReadonlyMap<string, VisualLiveScalarSample> {
  const result = new Map<string, VisualLiveScalarSample>();
  const visit = (element: VisualElementEngineering) => {
    for (const binding of element.bindings ?? []) {
      const parameterKey = binding.metadata?.dynamoParameter?.trim();
      if (!parameterKey) continue;
      const normalized = normalizeKey(parameterKey);
      if (result.has(normalized)) continue;
      const sample = bindingSample(liveSamples, binding);
      if (sample) result.set(normalized, sample);
    }
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements) visit(element);
  return new Map(result);
}

function bindingSample(
  samples: ReadonlyMap<string, VisualLiveScalarSample>,
  binding: BindingEngineering
): VisualLiveScalarSample | undefined {
  if (binding.tagReference?.tagId) {
    const byId = samples.get(`tag:${normalizeKey(binding.tagReference.tagId)}`);
    if (byId) return byId;
  }
  return samples.get(binding.target);
}

function booleanParameter(
  samples: ReadonlyMap<string, VisualLiveScalarSample>,
  key: string
): boolean {
  return optionalBooleanParameter(samples, key) === true;
}

function optionalBooleanParameter(
  samples: ReadonlyMap<string, VisualLiveScalarSample>,
  key: string
): boolean | null {
  const sample = samples.get(normalizeKey(key));
  if (!sample || !sampleUsable(sample)) return null;
  if (typeof sample.value === 'boolean') return sample.value;
  if (typeof sample.value === 'number' && Number.isFinite(sample.value)) return sample.value !== 0;
  if (typeof sample.value === 'string') {
    const normalized = sample.value.trim().toLocaleLowerCase('en-US');
    if (['true', '1', 'on', 'open', 'running', 'active'].includes(normalized)) return true;
    if (['false', '0', 'off', 'closed', 'stopped', 'inactive'].includes(normalized)) return false;
  }
  return null;
}

function sampleUsable(sample: VisualLiveScalarSample): boolean {
  return sampleQuality(sample) === 'good' || sampleQuality(sample) === 'uncertain';
}

/**
 * Mirrors Scada.Core.Tags.TagQuality numeric serialization:
 * Good=0, Uncertain=1, Bad=2, BadCommunication=3,
 * BadConfiguration=4, BadDevice=5, Stale=6, Disabled=7.
 * Unknown numeric values remain fail-closed rather than being guessed.
 */
export function sampleQuality(sample: VisualLiveScalarSample): DynamoQualityState {
  const explicitState = sample.state?.trim().toLocaleLowerCase('en-US') ?? '';
  if (explicitState && ['unavailable', 'disconnected', 'offline', 'error', 'failed'].some(token => explicitState.includes(token))) {
    return 'bad';
  }

  const quality = sample.quality;
  if (typeof quality === 'string') {
    const normalized = quality.trim().toLocaleLowerCase('en-US');
    if (!normalized) return 'unknown';
    if (normalized.includes('stale')) return 'stale';
    if (normalized.includes('uncertain')) return 'uncertain';
    if (['bad', 'invalid', 'error', 'failed', 'failure', 'offline', 'disconnected', 'unavailable', 'disabled']
      .some(token => normalized.includes(token))) return 'bad';
    if (['good', 'ok', 'online', 'valid'].some(token => normalized.includes(token))) return 'good';
    return 'unknown';
  }

  if (typeof quality === 'number' && Number.isInteger(quality)) {
    switch (quality) {
      case 0: return 'good';
      case 1: return 'uncertain';
      case 6: return 'stale';
      case 2:
      case 3:
      case 4:
      case 5:
      case 7:
        return 'bad';
      default:
        return 'unknown';
    }
  }

  return 'unknown';
}

function worstQuality(values: readonly DynamoQualityState[]): DynamoQualityState {
  if (values.length === 0) return 'unknown';
  if (values.some(value => value === 'bad')) return 'bad';
  if (values.some(value => value === 'stale')) return 'stale';
  if (values.some(value => value === 'unknown')) return 'unknown';
  if (values.some(value => value === 'uncertain')) return 'uncertain';
  return 'good';
}

function normalizeKey(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
}
