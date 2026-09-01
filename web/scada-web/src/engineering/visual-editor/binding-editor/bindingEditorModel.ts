import type {
  BindingEngineering,
  TagValueReferenceEngineering,
  VisualElementEngineering
} from '../../types';
import { getBuiltinVisualObjectSchema } from '../../../visual-runtime/builtinVisualObjectSchemas';
import type { VisualPropertyDefinition, VisualPropertyType } from '../../../visual-runtime/visualPropertyTypes';
import type {
  VisualEditorBindingSelectorCapability,
  VisualEditorBindingSourceCatalogItem,
  VisualEditorMutationIntent
} from '../visualEditorContracts';

export const CANONICAL_VISUAL_BINDING_KINDS = ['Tag', 'ClientMemory', 'Property', 'Expression'] as const;
export type CanonicalVisualBindingKind = typeof CANONICAL_VISUAL_BINDING_KINDS[number];

export type BindableVisualProperty = Readonly<{
  key: string;
  type: VisualPropertyDefinition['type'];
  category?: string;
}>;

export type BindingSourceResolution = Readonly<{
  status: 'found' | 'ambiguous' | 'notFound';
  source?: VisualEditorBindingSourceCatalogItem;
}>;

export class VisualBindingEditorError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly propertyKey?: string
  ) {
    super(message);
    this.name = 'VisualBindingEditorError';
  }
}

export function listBindableVisualProperties(
  element: Pick<VisualElementEngineering, 'type'>
): readonly BindableVisualProperty[] {
  const schema = requireBuiltinSchema(element.type);
  return Object.freeze(schema.definitions()
    .filter(definition => definition.supportsBinding)
    .map(definition => Object.freeze({
      key: definition.key,
      type: definition.type,
      category: definition.category
    })));
}

export function bindingSourceIdentity(source: VisualEditorBindingSourceCatalogItem): string {
  const kind = requireCanonicalKind(source.kind);
  const target = requireStableReference(source.target, 'binding source target');
  const tagReference = normalizeTagValueReference(source, kind);
  return sourceIdentity(kind, target, tagReference);
}

export function normalizeBindingSourceCatalog(
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[]
): readonly VisualEditorBindingSourceCatalogItem[] {
  const identities = new Set<string>();
  const normalized: VisualEditorBindingSourceCatalogItem[] = [];

  for (const item of sourceCatalog) {
    if (item.bindable === false) continue;
    const kind = requireCanonicalKind(item.kind);
    const target = requireStableReference(item.target, 'binding source target');
    const label = requireDisplayLabel(item.label);
    const tagReference = normalizeTagValueReference(item, kind);
    const selectorCapability = normalizeSelectorCapability(item.selectorCapability, kind);
    const identity = sourceIdentity(kind, target, tagReference);
    if (identities.has(identity)) continue;
    identities.add(identity);

    normalized.push(Object.freeze({
      kind,
      target,
      label,
      ...(item.dataType !== undefined ? { dataType: item.dataType } : {}),
      ...(item.engineeringUnit !== undefined ? { engineeringUnit: item.engineeringUnit } : {}),
      ...(item.writable !== undefined ? { writable: item.writable } : {}),
      ...(item.family !== undefined ? { family: item.family } : {}),
      ...(tagReference !== undefined ? { tagReference } : {}),
      ...(selectorCapability !== undefined ? { selectorCapability } : {}),
      bindable: true
    }));
  }

  return Object.freeze(normalized);
}

export function compatibleBindingSources(
  destination: Pick<BindableVisualProperty, 'key' | 'type'>,
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[]
): readonly VisualEditorBindingSourceCatalogItem[] {
  const normalized = normalizeBindingSourceCatalog(sourceCatalog);
  return Object.freeze(normalized.filter(source =>
    isBindingSourceCompatible(destination, source) || isBitSelectorAuthoringSource(destination, source)
  ));
}

export function isBindingSourceCompatible(
  destination: Pick<BindableVisualProperty, 'key' | 'type'> | VisualPropertyType,
  source: VisualEditorBindingSourceCatalogItem
): boolean {
  const destinationType = typeof destination === 'string' ? destination : destination.type;
  const destinationKey = typeof destination === 'string' ? '' : destination.key;

  if (source.dataType === undefined || source.dataType === null || !source.dataType.trim()) {
    return source.kind !== 'Tag' && source.kind !== 'ClientMemory';
  }

  const dataType = source.dataType.trim().toLowerCase();
  if (destinationKey === 'text') {
    return ['boolean', 'int16', 'int32', 'int64', 'float', 'double', 'string', 'enum', 'datetime'].includes(dataType);
  }

  switch (destinationType) {
    case 'number':
      return ['int16', 'int32', 'int64', 'float', 'double'].includes(dataType);
    case 'boolean':
      return dataType === 'boolean';
    case 'string':
      return ['string', 'enum', 'datetime'].includes(dataType);
    case 'color':
      return dataType === 'string';
    case 'enum':
      return dataType === 'enum' || dataType === 'string';
    case 'assetRef':
      return false;
  }
}

export function isBitSelectorAuthoringSource(
  destination: Pick<BindableVisualProperty, 'key' | 'type'> | VisualPropertyType,
  source: VisualEditorBindingSourceCatalogItem
): boolean {
  const destinationType = typeof destination === 'string' ? destination : destination.type;
  const capability = source.selectorCapability;
  return destinationType === 'boolean' &&
    source.kind === 'Tag' &&
    Boolean(source.tagReference?.tagId) &&
    capability?.kind === 'bit' &&
    Number.isInteger(capability.minIndex) &&
    Number.isInteger(capability.maxIndex) &&
    capability.minIndex >= 0 &&
    capability.maxIndex >= capability.minIndex;
}

export function createTagBitBindingSource(
  source: VisualEditorBindingSourceCatalogItem,
  bitIndex: number
): VisualEditorBindingSourceCatalogItem {
  const kind = requireCanonicalKind(source.kind);
  if (kind !== 'Tag') {
    throw new VisualBindingEditorError('binding.source.bitKind', 'Bit selectors require a canonical Tag source.');
  }
  const capability = normalizeSelectorCapability(source.selectorCapability, kind);
  const baseReference = normalizeTagValueReference(source, kind);
  if (!capability || !baseReference?.tagId) {
    throw new VisualBindingEditorError('binding.source.bitUnsupported', 'This TAG source does not support bit selection.');
  }
  if (!Number.isInteger(bitIndex) || bitIndex < capability.minIndex || bitIndex > capability.maxIndex) {
    throw new VisualBindingEditorError(
      'binding.source.bitRange',
      `Bit index must be between ${capability.minIndex} and ${capability.maxIndex}.`
    );
  }

  const suffix = bitIndex.toString().padStart(2, '0');
  return Object.freeze({
    ...source,
    target: `${source.target}.${suffix}`,
    label: `${source.label}.${suffix}`,
    dataType: 'Boolean',
    engineeringUnit: null,
    tagReference: Object.freeze({
      tagId: baseReference.tagId,
      selector: Object.freeze({ kind: 'bit', index: bitIndex })
    }),
    selectorCapability: null,
    bindable: true
  });
}

export function resolveBindingSourceReference(
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[],
  rawReference: string
): BindingSourceResolution {
  const candidate = rawReference.trim();
  if (!candidate) return Object.freeze({ status: 'notFound' });
  const normalized = normalizeBindingSourceCatalog(sourceCatalog);

  const exact = normalized.filter(source => source.target === candidate || source.label === candidate);
  if (exact.length === 1) return Object.freeze({ status: 'found', source: exact[0] });
  if (exact.length > 1) return Object.freeze({ status: 'ambiguous' });

  const bitMatch = /^(.*)\.(\d{1,2})$/.exec(candidate);
  if (!bitMatch) return Object.freeze({ status: 'notFound' });
  const baseText = bitMatch[1];
  const bitIndex = Number(bitMatch[2]);
  const derived = normalized
    .filter(source => source.target === baseText || source.label === baseText)
    .map(source => {
      try { return createTagBitBindingSource(source, bitIndex); }
      catch { return null; }
    })
    .filter((source): source is VisualEditorBindingSourceCatalogItem => source !== null);

  if (derived.length === 1) return Object.freeze({ status: 'found', source: derived[0] });
  if (derived.length > 1) return Object.freeze({ status: 'ambiguous' });
  return Object.freeze({ status: 'notFound' });
}

export function findBindingSourceForBinding(
  binding: BindingEngineering,
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[]
): VisualEditorBindingSourceCatalogItem | undefined {
  const normalized = normalizeBindingSourceCatalog(sourceCatalog);
  if (binding.kind === 'Tag' && binding.tagReference?.tagId) {
    const tagId = binding.tagReference.tagId.toLocaleLowerCase();
    return normalized.find(source =>
      source.kind === 'Tag' &&
      source.tagReference?.tagId?.toLocaleLowerCase() === tagId &&
      !source.tagReference?.selector
    );
  }
  const kind = binding.kind.trim().toLocaleLowerCase();
  return normalized.find(source =>
    source.kind.trim().toLocaleLowerCase() === kind && source.target === binding.target
  );
}

export function findVisualBinding(
  element: Pick<VisualElementEngineering, 'bindings'>,
  propertyKey: string
): BindingEngineering | undefined {
  return element.bindings?.find(binding => binding.key === propertyKey);
}

export function createBindingSetIntent(
  element: Pick<VisualElementEngineering, 'id' | 'type'>,
  propertyKey: string,
  source: VisualEditorBindingSourceCatalogItem,
  direction?: string | null
): Extract<VisualEditorMutationIntent, { kind: 'binding.set' }> {
  const objectId = requireElementIdentity(element.id);
  const destination = requireBindableDestination(element.type, propertyKey);
  const kind = requireCanonicalKind(source.kind);
  const target = requireStableReference(source.target, 'binding source target');
  const tagReference = normalizeTagValueReference(source, kind);
  if (!isBindingSourceCompatible({ key: propertyKey, type: destination.type }, source)) {
    throw new VisualBindingEditorError(
      'binding.source.typeMismatch',
      `Binding source '${target}' data type '${source.dataType ?? 'unknown'}' is not compatible with visual property '${propertyKey}' type '${destination.type}'.`,
      propertyKey
    );
  }
  const normalizedDirection = normalizeDirection(
    direction ?? (element.type === 'core.slider' && propertyKey === 'value'
      ? source.writable === true ? 'readWrite' : 'read'
      : undefined)
  );
  const scalarText = propertyKey === 'text' && source.dataType != null;

  const binding: BindingEngineering = {
    key: propertyKey,
    kind,
    target,
    ...(normalizedDirection !== undefined ? { direction: normalizedDirection } : {}),
    ...(scalarText ? {
      metadata: {
        presentationMode: 'scalar-text',
        sourceDataType: source.dataType!,
        ...(source.engineeringUnit ? { engineeringUnit: source.engineeringUnit } : {})
      }
    } : {}),
    ...(tagReference !== undefined ? { tagReference } : {})
  };

  return Object.freeze({
    kind: 'binding.set',
    objectId,
    binding: Object.freeze(binding)
  });
}

export function createBindingRemoveIntent(
  element: Pick<VisualElementEngineering, 'id' | 'type'>,
  propertyKey: string
): Extract<VisualEditorMutationIntent, { kind: 'binding.remove' }> {
  const objectId = requireElementIdentity(element.id);
  requireBindableDestination(element.type, propertyKey);
  return Object.freeze({ kind: 'binding.remove', objectId, propertyKey });
}

export function filterBindingSourceCatalog(
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[],
  query: string
): readonly VisualEditorBindingSourceCatalogItem[] {
  const normalized = normalizeBindingSourceCatalog(sourceCatalog);
  const needle = query.trim().toLocaleLowerCase();
  if (!needle) return normalized;

  return Object.freeze(normalized.filter(item =>
    item.label.toLocaleLowerCase().includes(needle) ||
    item.target.toLocaleLowerCase().includes(needle) ||
    String(item.kind).toLocaleLowerCase().includes(needle) ||
    (item.dataType ?? '').toLocaleLowerCase().includes(needle) ||
    (item.tagReference?.tagId ?? '').toLocaleLowerCase().includes(needle)
  ));
}

function sourceIdentity(
  kind: CanonicalVisualBindingKind,
  target: string,
  tagReference: TagValueReferenceEngineering | undefined
): string {
  if (kind === 'Tag' && tagReference?.tagId) {
    const selector = tagReference.selector;
    return selector
      ? `Tag\u0000${tagReference.tagId.toLocaleLowerCase()}\u0000${selector.kind}\u0000${selector.index}`
      : `Tag\u0000${tagReference.tagId.toLocaleLowerCase()}`;
  }
  return `${kind}\u0000${target}`;
}

function normalizeTagValueReference(
  source: Pick<VisualEditorBindingSourceCatalogItem, 'tagReference'>,
  kind: CanonicalVisualBindingKind
): TagValueReferenceEngineering | undefined {
  const reference = source.tagReference;
  if (reference === undefined || reference === null) return undefined;
  if (kind !== 'Tag') {
    throw new VisualBindingEditorError(
      'binding.source.tagReferenceKind',
      'A canonical TAG value reference can only be attached to a Tag binding source.'
    );
  }

  const tagId = requireStableReference(reference.tagId, 'TAG identity');
  const selector = reference.selector;
  if (selector === undefined || selector === null) {
    return Object.freeze({ tagId });
  }
  if (selector.kind !== 'bit' || !Number.isInteger(selector.index) || selector.index < 0) {
    throw new VisualBindingEditorError(
      'binding.source.selector',
      'TAG selector must be a non-negative integer bit selector.'
    );
  }

  return Object.freeze({
    tagId,
    selector: Object.freeze({ kind: 'bit', index: selector.index })
  });
}

function normalizeSelectorCapability(
  capability: VisualEditorBindingSelectorCapability | null | undefined,
  kind: CanonicalVisualBindingKind
): VisualEditorBindingSelectorCapability | undefined {
  if (capability === undefined || capability === null) return undefined;
  if (kind !== 'Tag' || capability.kind !== 'bit' ||
      !Number.isInteger(capability.minIndex) || !Number.isInteger(capability.maxIndex) ||
      capability.minIndex < 0 || capability.maxIndex < capability.minIndex) {
    throw new VisualBindingEditorError(
      'binding.source.selectorCapability',
      'TAG bit selector capability must declare a valid non-negative index range.'
    );
  }
  return Object.freeze({ kind: 'bit', minIndex: capability.minIndex, maxIndex: capability.maxIndex });
}

function requireBindableDestination(objectType: string, propertyKey: string): VisualPropertyDefinition {
  const schema = requireBuiltinSchema(objectType);
  if (!schema.declares(propertyKey)) {
    throw new VisualBindingEditorError(
      'binding.destination.unregistered',
      `Visual property '${propertyKey}' is not declared by '${objectType}'.`,
      propertyKey
    );
  }

  const definition = schema.getRequired(propertyKey);
  if (!definition.supportsBinding) {
    throw new VisualBindingEditorError(
      'binding.destination.unsupported',
      `Visual property '${propertyKey}' does not support bindings.`,
      propertyKey
    );
  }
  return definition;
}

function requireBuiltinSchema(objectType: string) {
  try {
    return getBuiltinVisualObjectSchema(objectType);
  } catch {
    throw new VisualBindingEditorError(
      'binding.objectType.unsupported',
      `Visual object type '${objectType}' is not a registered Wave 08 built-in.`
    );
  }
}

function requireCanonicalKind(value: string): CanonicalVisualBindingKind {
  if ((CANONICAL_VISUAL_BINDING_KINDS as readonly string[]).includes(value)) {
    return value as CanonicalVisualBindingKind;
  }
  throw new VisualBindingEditorError(
    'binding.source.kind',
    `Binding kind '${value}' is not canonical.`
  );
}

function requireStableReference(value: string, label: string): string {
  if (!value.trim() || value !== value.trim() || /[\u0000-\u001F\u007F]/.test(value)) {
    throw new VisualBindingEditorError('binding.source.target', `${label} must be a stable non-empty reference.`);
  }
  return value;
}

function requireDisplayLabel(value: string): string {
  const normalized = value.trim();
  if (!normalized) {
    throw new VisualBindingEditorError('binding.source.label', 'Binding source label is required.');
  }
  return normalized;
}

function requireElementIdentity(value: string | null | undefined): string {
  if (!value?.trim()) {
    throw new VisualBindingEditorError(
      'binding.object.identity',
      'A stable visual object ID is required before authoring a canonical binding.'
    );
  }
  return value;
}

function normalizeDirection(value: string | null | undefined): string | null | undefined {
  if (value === undefined) return undefined;
  if (value === null) return null;
  const normalized = value.trim();
  return normalized || null;
}
