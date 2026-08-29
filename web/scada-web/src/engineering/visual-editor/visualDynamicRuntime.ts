import {
  compileVisualExpression,
  evaluateVisualExpression,
  type VisualExpressionDependency,
  type VisualExpressionSourceSample
} from '../../expressions';
import { projectTagValueReference } from '../project-reference/tagValueReferenceProjection';
import type {
  BindingEngineering,
  TagValueReferenceEngineering,
  VisualAnalogFillEngineering,
  VisualBooleanConditionEngineering,
  VisualElementEngineering,
  VisualExpressionDependencyEngineering,
  VisualExpressionEngineering,
  VisualExpressionValueTypeEngineering,
  VisualValueSourceEngineering
} from '../types';
import type { VisualPropertyValue } from '../../visual-runtime';
import {
  computeAnalogFillPresentation,
  type AnalogFillPresentation
} from './analogFillPresentation';

export type VisualDynamicSample = Readonly<{
  reference: string;
  tagId?: string | null;
  value: unknown;
  dataType: string;
  quality?: string | number | null;
  state?: string | null;
  timestamp?: string | null;
}>;

export type VisualDynamicDiagnostic = Readonly<{
  propertyKey?: string;
  sourceKind: string;
  message: string;
}>;

export type VisualDynamicResolution = Readonly<{
  values: Readonly<Record<string, VisualPropertyValue>>;
  analogFill: Readonly<{
    presentation: AnalogFillPresentation;
    fillColor: string;
  }> | null;
  diagnostics: readonly VisualDynamicDiagnostic[];
}>;

type SourceResult =
  | Readonly<{ ok: true; value: boolean | number; valueType: 'boolean' | 'number' }>
  | Readonly<{ ok: false; message: string }>;

export function visualTagSampleKey(tagId: string): string {
  return `tag:${tagId.trim().toLocaleLowerCase()}`;
}

export function resolveVisualDynamicState(
  element: VisualElementEngineering,
  baseValues: Readonly<Record<string, VisualPropertyValue>>,
  samples: ReadonlyMap<string, VisualDynamicSample>
): VisualDynamicResolution {
  const values: Record<string, VisualPropertyValue> = { ...baseValues };
  const diagnostics: VisualDynamicDiagnostic[] = [];

  for (const binding of element.bindings ?? []) {
    const kind = binding.kind?.trim().toLowerCase();
    if (kind !== 'tag' && kind !== 'clientmemory') continue;
    if (binding.key === 'text' || !(binding.key in values)) continue;

    const resolved = resolveBinding(binding, values[binding.key], samples);
    if (resolved.ok) values[binding.key] = resolved.value;
    else diagnostics.push(Object.freeze({ propertyKey: binding.key, sourceKind: 'Binding', message: resolved.message }));
  }

  for (const configured of element.propertyExpressions ?? []) {
    if (!(configured.propertyKey in values)) continue;
    const resolved = resolveExpression(configured.expression, samples);
    if (resolved.ok) values[configured.propertyKey] = resolved.value;
    else diagnostics.push(Object.freeze({ propertyKey: configured.propertyKey, sourceKind: 'Expression', message: resolved.message }));
  }

  for (const condition of element.booleanConditions ?? []) {
    if (!(condition.propertyKey in values)) continue;
    const resolved = resolveBooleanCondition(condition, samples);
    if (resolved.ok) values[condition.propertyKey] = resolved.value;
    else diagnostics.push(Object.freeze({ propertyKey: condition.propertyKey, sourceKind: 'BooleanCondition', message: resolved.message }));
  }

  let analogFill: VisualDynamicResolution['analogFill'] = null;
  if (element.analogFill) {
    const resolved = resolveAnalogFill(element.analogFill, samples);
    if (resolved.ok) analogFill = resolved.value;
    else diagnostics.push(Object.freeze({ sourceKind: 'AnalogFill', message: resolved.message }));
  }

  return Object.freeze({
    values: Object.freeze(values),
    analogFill,
    diagnostics: Object.freeze(diagnostics)
  });
}

function resolveBinding(
  binding: BindingEngineering,
  fallbackValue: VisualPropertyValue,
  samples: ReadonlyMap<string, VisualDynamicSample>
): Readonly<{ ok: true; value: VisualPropertyValue }> | Readonly<{ ok: false; message: string }> {
  const sample = findSample(binding.tagReference, binding.target, samples);
  const usable = validateSample(sample, binding.target);
  if (!usable.ok) return usable;

  const projected = binding.tagReference?.selector
    ? projectTagValueReference(binding.tagReference, usable.sample.dataType, usable.sample.value)
    : { ok: true, value: usable.sample.value, dataType: usable.sample.dataType };
  if (!projected.ok) return Object.freeze({ ok: false, message: projected.detail ?? 'Binding selector is unavailable.' });

  if (typeof fallbackValue === 'boolean') {
    return typeof projected.value === 'boolean'
      ? Object.freeze({ ok: true, value: projected.value })
      : Object.freeze({ ok: false, message: `Binding '${binding.target}' did not produce Boolean.` });
  }
  if (typeof fallbackValue === 'number') {
    return typeof projected.value === 'number' && Number.isFinite(projected.value)
      ? Object.freeze({ ok: true, value: projected.value })
      : Object.freeze({ ok: false, message: `Binding '${binding.target}' did not produce a finite Number.` });
  }
  if (typeof fallbackValue === 'string') {
    return typeof projected.value === 'string'
      ? Object.freeze({ ok: true, value: projected.value })
      : Object.freeze({ ok: false, message: `Binding '${binding.target}' did not produce String.` });
  }

  return Object.freeze({ ok: false, message: `Binding '${binding.target}' cannot drive this property type.` });
}

function resolveBooleanCondition(
  condition: VisualBooleanConditionEngineering,
  samples: ReadonlyMap<string, VisualDynamicSample>
): SourceResult {
  if (condition.kind === 'Direct') {
    const source = resolveValueSource(condition.source, samples);
    if (!source.ok) return source;
    if (source.valueType !== 'boolean') return Object.freeze({ ok: false, message: 'Direct Boolean Condition requires a Boolean source.' });
    return Object.freeze({ ok: true, value: condition.negate ? !source.value : source.value, valueType: 'boolean' });
  }

  if (condition.kind !== 'NumericInterval') {
    return Object.freeze({ ok: false, message: `Unsupported Boolean Condition kind '${String(condition.kind)}'.` });
  }

  const source = resolveValueSource(condition.source, samples);
  if (!source.ok) return source;
  if (source.valueType !== 'number') return Object.freeze({ ok: false, message: 'Numeric interval requires a Number source.' });

  const minimum = condition.minimum ?? null;
  const maximum = condition.maximum ?? null;
  if (minimum === null && maximum === null) return Object.freeze({ ok: false, message: 'Numeric interval requires at least one bound.' });
  if (minimum !== null && !Number.isFinite(minimum)) return Object.freeze({ ok: false, message: 'Numeric interval minimum must be finite.' });
  if (maximum !== null && !Number.isFinite(maximum)) return Object.freeze({ ok: false, message: 'Numeric interval maximum must be finite.' });
  if (minimum !== null && maximum !== null && minimum > maximum) return Object.freeze({ ok: false, message: 'Numeric interval minimum cannot exceed maximum.' });

  const lower = minimum === null
    ? true
    : condition.minimumInclusive === false ? source.value > minimum : source.value >= minimum;
  const upper = maximum === null
    ? true
    : condition.maximumInclusive === false ? source.value < maximum : source.value <= maximum;
  const inside = lower && upper;
  const intervalResult = (condition.intervalMode ?? 'Inside') === 'Outside' ? !inside : inside;
  const value = condition.negate ? !intervalResult : intervalResult;
  return Object.freeze({ ok: true, value, valueType: 'boolean' });
}

function resolveAnalogFill(
  config: VisualAnalogFillEngineering,
  samples: ReadonlyMap<string, VisualDynamicSample>
): Readonly<{ ok: true; value: NonNullable<VisualDynamicResolution['analogFill']> }> | Readonly<{ ok: false; message: string }> {
  const source = resolveValueSource(config.source, samples);
  if (!source.ok) return source;
  if (source.valueType !== 'number') return Object.freeze({ ok: false, message: 'Analog Fill requires a Number source.' });

  try {
    const presentation = computeAnalogFillPresentation({
      value: source.value,
      inputMinimum: config.inputMinimum,
      inputMaximum: config.inputMaximum,
      direction: config.direction ?? 'BottomToTop',
      clamp: config.clamp ?? true,
      invertScale: config.invertScale ?? false
    });
    return Object.freeze({
      ok: true,
      value: Object.freeze({ presentation, fillColor: config.fillColor })
    });
  } catch (reason) {
    return Object.freeze({ ok: false, message: reason instanceof Error ? reason.message : String(reason) });
  }
}

function resolveValueSource(
  source: VisualValueSourceEngineering,
  samples: ReadonlyMap<string, VisualDynamicSample>
): SourceResult {
  if (source.kind === 'Expression') {
    if (!source.expression) return Object.freeze({ ok: false, message: 'Expression source is missing its expression.' });
    return resolveExpression(source.expression, samples);
  }

  if (source.kind !== 'Tag' && source.kind !== 'ClientMemory') {
    return Object.freeze({ ok: false, message: `Unsupported value source kind '${String(source.kind)}'.` });
  }

  const target = source.target ?? source.tagReference?.tagId ?? source.kind;
  const sample = findSample(source.tagReference, source.target, samples);
  const usable = validateSample(sample, target);
  if (!usable.ok) return usable;

  const projected = source.kind === 'Tag'
    ? projectTagValueReference(source.tagReference, usable.sample.dataType, usable.sample.value)
    : { ok: true, value: usable.sample.value, dataType: usable.sample.dataType };
  if (!projected.ok) return Object.freeze({ ok: false, message: projected.detail ?? `Source '${target}' is unavailable.` });

  return typedSourceValue(projected.value, source.valueType, target);
}

function resolveExpression(
  expression: VisualExpressionEngineering,
  samples: ReadonlyMap<string, VisualDynamicSample>
): SourceResult {
  const dependencies = (expression.dependencies ?? []).map(toRuntimeDependency);
  const resultType = expressionValueType(expression.resultType);
  const compiled = compileVisualExpression(expression.text, resultType, dependencies);
  if (!compiled.ok) {
    const first = compiled.diagnostics[0];
    return Object.freeze({ ok: false, message: first ? `${first.code}: ${first.message}` : 'Expression could not be compiled.' });
  }

  const evaluated = evaluateVisualExpression(compiled.expression, dependency => {
    const sample = findSample(dependency.tagReference, dependency.target, samples);
    if (!sample) return null;
    return toExpressionSample(sample);
  });
  if (!evaluated.ok) return Object.freeze({ ok: false, message: `${evaluated.diagnostic.code}: ${evaluated.diagnostic.message}` });
  return Object.freeze({ ok: true, value: evaluated.value, valueType: evaluated.valueType });
}

function toRuntimeDependency(dependency: VisualExpressionDependencyEngineering): VisualExpressionDependency {
  return Object.freeze({
    symbol: dependency.symbol,
    kind: dependency.kind === 'ClientMemory' ? 'clientMemory' : 'tag',
    valueType: expressionValueType(dependency.valueType),
    tagReference: dependency.tagReference,
    ...(dependency.target === undefined ? {} : { target: dependency.target })
  });
}

function expressionValueType(valueType: VisualExpressionValueTypeEngineering): 'boolean' | 'number' {
  return valueType === 'Boolean' ? 'boolean' : 'number';
}

function typedSourceValue(value: unknown, expected: VisualExpressionValueTypeEngineering, target: string): SourceResult {
  if (expected === 'Boolean') {
    return typeof value === 'boolean'
      ? Object.freeze({ ok: true, value, valueType: 'boolean' })
      : Object.freeze({ ok: false, message: `Source '${target}' did not produce Boolean.` });
  }
  return typeof value === 'number' && Number.isFinite(value)
    ? Object.freeze({ ok: true, value, valueType: 'number' })
    : Object.freeze({ ok: false, message: `Source '${target}' did not produce a finite Number.` });
}

function findSample(
  reference: TagValueReferenceEngineering | null | undefined,
  target: string | null | undefined,
  samples: ReadonlyMap<string, VisualDynamicSample>
): VisualDynamicSample | undefined {
  const tagId = reference?.tagId?.trim();
  if (tagId) {
    const byId = samples.get(visualTagSampleKey(tagId));
    if (byId) return byId;
  }
  return target ? samples.get(target) : undefined;
}

function validateSample(
  sample: VisualDynamicSample | undefined,
  target: string
): Readonly<{ ok: true; sample: VisualDynamicSample }> | Readonly<{ ok: false; message: string }> {
  if (!sample) return Object.freeze({ ok: false, message: `Source '${target}' is unavailable.` });
  if (sample.state && sample.state !== 'LocalSession') return Object.freeze({ ok: false, message: `Source '${target}' state is ${sample.state}.` });
  if (sample.quality !== undefined && sample.quality !== null && sample.quality !== 0 && String(sample.quality).toLowerCase() !== 'good') {
    return Object.freeze({ ok: false, message: `Source '${target}' quality is ${String(sample.quality)}.` });
  }
  if (sample.value === null || sample.value === undefined) return Object.freeze({ ok: false, message: `Source '${target}' has no usable value.` });
  return Object.freeze({ ok: true, sample });
}

function toExpressionSample(sample: VisualDynamicSample): VisualExpressionSourceSample {
  return Object.freeze({
    value: sample.value,
    dataType: sample.dataType,
    quality: sample.quality ?? null,
    state: sample.state ?? null,
    available: sample.value !== null && sample.value !== undefined,
    detail: null
  });
}
