import type {
  BindingEngineering,
  VisualElementEngineering
} from './types';
import {
  decodeVisualEngineeringProperties,
  projectVisualEngineeringDefinition,
  VisualObjectPropertySchema,
  VisualPropertyContractError,
  type VisualBindingSourceKind,
  type VisualEngineeringDefinitionProjection
} from '../visual-runtime';

/**
 * Projects one canonical Engineering visual element into the renderer-independent
 * Runtime definition. Structural visual geometry, such as core.polygon points,
 * remains in the canonical visual element and is not misclassified as a scalar
 * Visual Property Registry value.
 */
export function projectCanonicalVisualElementForRuntime(
  element: VisualElementEngineering,
  schema: VisualObjectPropertySchema,
  parentObjectId: string | null = null
): VisualEngineeringDefinitionProjection {
  if (!element.id) {
    throw new VisualPropertyContractError(
      'canonicalProjection.missingObjectId',
      `Visual element '${element.key}' has no stable object ID. Legacy views must be materialized by Engineering before Runtime projection.`
    );
  }

  const baseProperties = decodeVisualEngineeringProperties(registeredScalarProperties(element, schema), schema);
  const bindings = (element.bindings ?? []).map(projectCanonicalBinding);

  return projectVisualEngineeringDefinition({
    objectId: element.id,
    key: element.key,
    objectType: element.type,
    parentObjectId,
    baseProperties,
    bindings,
    metadata: element.metadata ?? undefined
  }, schema);
}

export function projectCanonicalVisualTreeForRuntime(
  elements: readonly VisualElementEngineering[] | null | undefined,
  resolveSchema: (objectType: string) => VisualObjectPropertySchema,
  parentObjectId: string | null = null
): readonly VisualEngineeringDefinitionProjection[] {
  const projected: VisualEngineeringDefinitionProjection[] = [];

  for (const element of elements ?? []) {
    const definition = projectCanonicalVisualElementForRuntime(element, resolveSchema(element.type), parentObjectId);
    projected.push(definition);
    projected.push(...projectCanonicalVisualTreeForRuntime(element.children, resolveSchema, definition.objectId));
  }

  return Object.freeze(projected);
}

function registeredScalarProperties(
  element: VisualElementEngineering,
  schema: VisualObjectPropertySchema
): Readonly<Record<string, unknown>> {
  const projected: Record<string, unknown> = Object.create(null) as Record<string, unknown>;
  for (const [key, value] of Object.entries(element.properties ?? {})) {
    if (schema.declares(key)) projected[key] = value;
  }
  return projected;
}

function projectCanonicalBinding(binding: BindingEngineering) {
  return Object.freeze({
    propertyKey: binding.key,
    sourceKind: toRuntimeBindingKind(binding.kind),
    sourceReference: binding.target
  });
}

function toRuntimeBindingKind(kind: string): VisualBindingSourceKind {
  const normalized = kind.trim().toLowerCase();
  switch (normalized) {
    case 'tag':
    case 'clientmemory':
    case 'property':
    case 'binding':
      return 'binding';
    case 'expression':
      return 'expression';
    default:
      throw new VisualPropertyContractError(
        'canonicalProjection.unsupportedBindingKind',
        `Canonical visual binding kind '${kind}' is not supported by the Runtime visual projection.`
      );
  }
}
