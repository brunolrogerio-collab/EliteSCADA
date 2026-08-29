import type {
  BindingEngineering,
  VisualElementEngineering
} from '../../types';
import { getBuiltinVisualObjectSchema } from '../../../visual-runtime/builtinVisualObjectSchemas';
import type { VisualPropertyDefinition, VisualPropertyType } from '../../../visual-runtime/visualPropertyTypes';
import type {
  VisualEditorBindingSourceCatalogItem,
  VisualEditorMutationIntent
} from '../visualEditorContracts';

export const CANONICAL_VISUAL_BINDING_KINDS = ['Tag', 'Property', 'Expression'] as const;
export type CanonicalVisualBindingKind = typeof CANONICAL_VISUAL_BINDING_KINDS[number];

export type BindableVisualProperty = Readonly<{
  key: string;
  type: VisualPropertyDefinition['type'];
  category?: string;
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

export function normalizeBindingSourceCatalog(
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[]
): readonly VisualEditorBindingSourceCatalogItem[] {
  const identities = new Set<string>();
  const normalized: VisualEditorBindingSourceCatalogItem[] = [];

  for (const item of sourceCatalog) {
    const kind = requireCanonicalKind(item.kind);
    const target = requireStableReference(item.target, 'binding source target');
    const label = requireDisplayLabel(item.label);
    const identity = `${kind}\u0000${target}`;
    if (identities.has(identity)) continue;
    identities.add(identity);

    normalized.push(Object.freeze({
      kind,
      target,
      label,
      ...(item.dataType !== undefined ? { dataType: item.dataType } : {}),
      ...(item.engineeringUnit !== undefined ? { engineeringUnit: item.engineeringUnit } : {}),
      ...(item.writable !== undefined ? { writable: item.writable } : {})
    }));
  }

  return Object.freeze(normalized);
}

export function compatibleBindingSources(
  destinationType: VisualPropertyType,
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[]
): readonly VisualEditorBindingSourceCatalogItem[] {
  const normalized = normalizeBindingSourceCatalog(sourceCatalog);
  return Object.freeze(normalized.filter(source => isBindingSourceCompatible(destinationType, source)));
}

export function isBindingSourceCompatible(
  destinationType: VisualPropertyType,
  source: VisualEditorBindingSourceCatalogItem
): boolean {
  // Property/expression contracts do not yet expose a dependable result type in
  // Wave 08. They remain authorable only when a future coordinator catalog
  // supplies one. Current central composition exposes TAGs only.
  if (source.dataType === undefined || source.dataType === null || !source.dataType.trim()) {
    return source.kind !== 'Tag';
  }

  const dataType = source.dataType.trim().toLowerCase();
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
  if (!isBindingSourceCompatible(destination.type, source)) {
    throw new VisualBindingEditorError(
      'binding.source.typeMismatch',
      `Binding source '${target}' data type '${source.dataType ?? 'unknown'}' is not compatible with visual property '${propertyKey}' type '${destination.type}'.`,
      propertyKey
    );
  }
  const normalizedDirection = normalizeDirection(direction);

  const binding: BindingEngineering = {
    key: propertyKey,
    kind,
    target,
    ...(normalizedDirection !== undefined ? { direction: normalizedDirection } : {})
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
    (item.dataType ?? '').toLocaleLowerCase().includes(needle)
  ));
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