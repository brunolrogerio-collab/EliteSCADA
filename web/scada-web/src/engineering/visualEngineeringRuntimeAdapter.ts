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
 * Wave 07 Runtime definition. Current Engineering is JSON-native and is validated
 * against the same property schema consumed by Runtime. Legacy string migration
 * is owned by the backend import/apply boundary rather than reimplemented here.
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

  const baseProperties = decodeVisualEngineeringProperties(element.properties, schema);
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

/**
 * Flattens a canonical visual tree into parent-before-child Runtime definitions.
 * The schema resolver remains external so the object palette/type registry is a
 * separate contract rather than hidden inside this adapter.
 */
export function projectCanonicalVisualTreeForRuntime(
  elements: readonly VisualElementEngineering[] | null | undefined,
  resolveSchema: (objectType: string) => VisualObjectPropertySchema,
  parentObjectId: string | null = null
): readonly VisualEngineeringDefinitionProjection[] {
  const projected: VisualEngineeringDefinitionProjection[] = [];

  for (const element of elements ?? []) {
    const definition = projectCanonicalVisualElementForRuntime(
      element,
      resolveSchema(element.type),
      parentObjectId
    );
    projected.push(definition);
    projected.push(...projectCanonicalVisualTreeForRuntime(
      element.children,
      resolveSchema,
      definition.objectId
    ));
  }

  return Object.freeze(projected);
}

function projectCanonicalBinding(binding: BindingEngineering) {
  return Object.freeze({
    propertyKey: binding.key,
    sourceKind: toRuntimeBindingKind(binding.kind),
    sourceReference: binding.target
  });
}

function toRuntimeBindingKind(kind: string): VisualBindingSourceKind {
  switch (kind) {
    case 'tag':
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
