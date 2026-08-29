import {
  compileVisualExpression,
  type VisualExpressionDiagnostic
} from '../../../expressions';
import { getBuiltinVisualObjectSchema } from '../../../visual-runtime/builtinVisualObjectSchemas';
import type { VisualPropertyDefinition } from '../../../visual-runtime/visualPropertyTypes';
import type {
  TagValueReferenceEngineering,
  VisualAnalogFillDirectionEngineering,
  VisualAnalogFillEngineering,
  VisualBooleanConditionEngineering,
  VisualElementEngineering,
  VisualExpressionDependencyEngineering,
  VisualExpressionEngineering,
  VisualExpressionValueTypeEngineering,
  VisualNumericIntervalModeEngineering,
  VisualValueSourceEngineering
} from '../../types';
import type { VisualEditorBindingSourceCatalogItem } from '../visualEditorContracts';
import {
  createTagBitBindingSource,
  normalizeBindingSourceCatalog,
  resolveBindingSourceReference
} from '../binding-editor/bindingEditorModel';

export type DynamicPropertySourceMode = 'Constant' | 'DirectBinding' | 'BooleanCondition' | 'Expression';

export type DynamicPropertyDestination = Readonly<{
  propertyKey: string;
  propertyType: VisualPropertyDefinition['type'];
  sourceModes: readonly DynamicPropertySourceMode[];
}>;

export type DynamicAuthoringValidation = Readonly<{
  ok: boolean;
  diagnostics: readonly VisualExpressionDiagnostic[];
  message?: string;
}>;

export function listDynamicPropertyDestinations(
  element: Pick<VisualElementEngineering, 'type'>
): readonly DynamicPropertyDestination[] {
  const schema = getBuiltinVisualObjectSchema(element.type);
  return Object.freeze(schema.definitions()
    .filter(definition => definition.supportsBinding && (definition.type === 'boolean' || definition.type === 'number'))
    .map(definition => Object.freeze({
      propertyKey: definition.key,
      propertyType: definition.type,
      sourceModes: Object.freeze(definition.type === 'boolean'
        ? ['Constant', 'DirectBinding', 'BooleanCondition', 'Expression'] as const
        : ['Constant', 'DirectBinding', 'Expression'] as const)
    })));
}

export function resolveDynamicAuthoringSource(
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[],
  reference: string,
  bitIndex?: number | null
): VisualEditorBindingSourceCatalogItem {
  const resolution = resolveBindingSourceReference(sourceCatalog, reference);
  if (resolution.status !== 'found' || !resolution.source) {
    throw new Error(resolution.status === 'ambiguous'
      ? `Reference '${reference}' is ambiguous.`
      : `Reference '${reference}' was not found in the canonical source catalog.`);
  }

  if (bitIndex === undefined || bitIndex === null) return resolution.source;
  return createTagBitBindingSource(resolution.source, bitIndex);
}

export function createVisualExpressionEngineering(
  resultType: VisualExpressionValueTypeEngineering,
  text: string,
  dependencies: readonly VisualExpressionDependencyEngineering[]
): VisualExpressionEngineering {
  const runtimeDependencies = dependencies.map(dependency => Object.freeze({
    symbol: dependency.symbol,
    kind: dependency.kind === 'ClientMemory' ? 'clientMemory' as const : 'tag' as const,
    valueType: dependency.valueType === 'Boolean' ? 'boolean' as const : 'number' as const,
    tagReference: dependency.tagReference,
    ...(dependency.target === undefined ? {} : { target: dependency.target })
  }));
  const compiled = compileVisualExpression(
    text,
    resultType === 'Boolean' ? 'boolean' : 'number',
    runtimeDependencies
  );
  if (!compiled.ok) {
    throw new Error(compiled.diagnostics.map(diagnostic => `${diagnostic.code}: ${diagnostic.message}`).join('\n'));
  }

  const used = new Set(compiled.expression.dependencies.map(dependency => dependency.symbol.toLocaleLowerCase()));
  return Object.freeze({
    text,
    resultType,
    dependencies: Object.freeze(dependencies
      .filter(dependency => used.has(dependency.symbol.toLocaleLowerCase()))
      .map(snapshotDependency)),
    version: 1
  });
}

export function validateVisualExpressionAuthoring(
  resultType: VisualExpressionValueTypeEngineering,
  text: string,
  dependencies: readonly VisualExpressionDependencyEngineering[]
): DynamicAuthoringValidation {
  const runtimeDependencies = dependencies.map(dependency => ({
    symbol: dependency.symbol,
    kind: dependency.kind === 'ClientMemory' ? 'clientMemory' as const : 'tag' as const,
    valueType: dependency.valueType === 'Boolean' ? 'boolean' as const : 'number' as const,
    tagReference: dependency.tagReference,
    target: dependency.target
  }));
  const compiled = compileVisualExpression(
    text,
    resultType === 'Boolean' ? 'boolean' : 'number',
    runtimeDependencies
  );
  if (compiled.ok) return Object.freeze({ ok: true, diagnostics: Object.freeze([]) });
  return Object.freeze({
    ok: false,
    diagnostics: compiled.diagnostics,
    message: compiled.diagnostics[0]?.message
  });
}

export function createExpressionDependency(
  symbol: string,
  valueType: VisualExpressionValueTypeEngineering,
  source: VisualEditorBindingSourceCatalogItem
): VisualExpressionDependencyEngineering {
  const normalized = normalizeDynamicValueSource(source);
  if (!normalized.tagReference?.tagId) {
    throw new Error(
      normalized.kind === 'ClientMemory'
        ? `Client Memory expression dependency '${symbol}' requires canonical stable identity.`
        : `Expression dependency '${symbol}' requires canonical stable source identity.`
    );
  }

  return Object.freeze({
    symbol: requireSymbol(symbol),
    kind: normalized.kind,
    valueType,
    tagReference: snapshotTagReference(normalized.tagReference),
    target: normalized.target,
    version: 1
  });
}

export function createValueSource(
  valueType: VisualExpressionValueTypeEngineering,
  source: VisualEditorBindingSourceCatalogItem
): VisualValueSourceEngineering {
  const normalized = normalizeDynamicValueSource(source);
  if (!normalized.tagReference?.tagId) {
    throw new Error(
      normalized.kind === 'ClientMemory'
        ? `Client Memory visual source '${normalized.target}' requires canonical stable identity.`
        : `Visual TAG source '${normalized.target}' requires canonical stable source identity.`
    );
  }

  return Object.freeze({
    kind: normalized.kind,
    valueType,
    target: normalized.target,
    tagReference: snapshotTagReference(normalized.tagReference),
    version: 1
  });
}

export function createExpressionValueSource(
  expression: VisualExpressionEngineering
): VisualValueSourceEngineering {
  return Object.freeze({
    kind: 'Expression',
    valueType: expression.resultType,
    expression,
    version: 1
  });
}

export function createDirectBooleanCondition(
  propertyKey: string,
  source: VisualValueSourceEngineering,
  negate = false
): VisualBooleanConditionEngineering {
  if (source.valueType !== 'Boolean') throw new Error('Direct Boolean Condition requires a Boolean source.');
  return Object.freeze({
    propertyKey: requirePropertyKey(propertyKey),
    kind: 'Direct',
    source,
    negate,
    version: 1
  });
}

export function createNumericIntervalCondition(
  propertyKey: string,
  source: VisualValueSourceEngineering,
  options: Readonly<{
    minimum?: number | null;
    minimumInclusive?: boolean;
    maximum?: number | null;
    maximumInclusive?: boolean;
    intervalMode?: VisualNumericIntervalModeEngineering;
    negate?: boolean;
  }>
): VisualBooleanConditionEngineering {
  if (source.valueType !== 'Number') throw new Error('Numeric interval requires a Number source.');
  const minimum = options.minimum ?? null;
  const maximum = options.maximum ?? null;
  if (minimum === null && maximum === null) throw new Error('Numeric interval requires at least one bound.');
  if (minimum !== null && !Number.isFinite(minimum)) throw new Error('Numeric interval minimum must be finite.');
  if (maximum !== null && !Number.isFinite(maximum)) throw new Error('Numeric interval maximum must be finite.');
  if (minimum !== null && maximum !== null && minimum > maximum) throw new Error('Numeric interval minimum cannot exceed maximum.');

  return Object.freeze({
    propertyKey: requirePropertyKey(propertyKey),
    kind: 'NumericInterval',
    source,
    negate: options.negate ?? false,
    minimum,
    minimumInclusive: options.minimumInclusive ?? true,
    maximum,
    maximumInclusive: options.maximumInclusive ?? true,
    intervalMode: options.intervalMode ?? 'Inside',
    version: 1
  });
}

export function createAnalogFillEngineering(
  source: VisualValueSourceEngineering,
  options: Readonly<{
    inputMinimum: number;
    inputMaximum: number;
    fillColor: string;
    clamp?: boolean;
    invertScale?: boolean;
    direction?: VisualAnalogFillDirectionEngineering;
  }>
): VisualAnalogFillEngineering {
  if (source.valueType !== 'Number') throw new Error('Analog Fill requires a Number source.');
  if (!Number.isFinite(options.inputMinimum) || !Number.isFinite(options.inputMaximum)) {
    throw new Error('Analog Fill scale limits must be finite.');
  }
  if (options.inputMinimum === options.inputMaximum) throw new Error('Analog Fill scale limits must be different.');
  if (!/^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$/.test(options.fillColor)) {
    throw new Error('Analog Fill color must be a canonical #RRGGBB or #RRGGBBAA value.');
  }

  return Object.freeze({
    source,
    inputMinimum: options.inputMinimum,
    inputMaximum: options.inputMaximum,
    fillColor: options.fillColor.toUpperCase(),
    clamp: options.clamp ?? true,
    invertScale: options.invertScale ?? false,
    direction: options.direction ?? 'BottomToTop',
    version: 1
  });
}

function normalizeDynamicValueSource(source: VisualEditorBindingSourceCatalogItem): Readonly<{
  kind: 'Tag' | 'ClientMemory';
  target: string;
  tagReference?: TagValueReferenceEngineering;
}> {
  if (source.bindable === false) throw new Error('Visual source is not bindable.');
  if (source.kind === 'Tag') {
    const normalized = normalizeBindingSourceCatalog([source])[0];
    if (!normalized) throw new Error('Visual source is not bindable.');
    return Object.freeze({
      kind: 'Tag',
      target: requireTarget(normalized.target),
      ...(normalized.tagReference ? { tagReference: snapshotTagReference(normalized.tagReference) } : {})
    });
  }
  if (source.kind !== 'ClientMemory') {
    throw new Error(`Visual value source kind '${source.kind}' is not supported.`);
  }
  const target = requireTarget(source.target);
  const reference = source.tagReference;
  if (reference?.selector) {
    throw new Error('Client Memory sources cannot carry TAG bit selectors.');
  }
  return Object.freeze({
    kind: 'ClientMemory',
    target,
    ...(reference?.tagId ? { tagReference: Object.freeze({ tagId: requireTarget(reference.tagId) }) } : {})
  });
}

function snapshotDependency(dependency: VisualExpressionDependencyEngineering): VisualExpressionDependencyEngineering {
  return Object.freeze({
    ...dependency,
    tagReference: snapshotTagReference(dependency.tagReference)
  });
}

function snapshotTagReference(reference: TagValueReferenceEngineering): TagValueReferenceEngineering {
  return Object.freeze({
    tagId: reference.tagId,
    ...(reference.selector
      ? { selector: Object.freeze({ kind: reference.selector.kind, index: reference.selector.index }) }
      : {})
  });
}

function requireSymbol(symbol: string): string {
  const normalized = symbol.trim();
  if (!normalized) throw new Error('Expression dependency symbol is required.');
  return normalized;
}

function requirePropertyKey(propertyKey: string): string {
  const normalized = propertyKey.trim();
  if (!normalized) throw new Error('Visual property key is required.');
  return normalized;
}

function requireTarget(target: string): string {
  const normalized = target.trim();
  if (!normalized) throw new Error('Visual source target is required.');
  return normalized;
}
