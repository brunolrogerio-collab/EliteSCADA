import {
  VisualObjectPropertySchema
} from './visualPropertyRegistry';
import {
  cloneVisualPropertyValue,
  isStableVisualToken,
  VisualPropertyContractError,
  type VisualPropertyValue
} from './visualPropertyTypes';

export type VisualBindingSourceKind = 'binding' | 'expression';

export type VisualDefinitionBindingDescriptor = Readonly<{
  propertyKey: string;
  sourceKind: VisualBindingSourceKind;
  sourceReference: string;
}>;

export type VisualDefinitionScriptEventReference = Readonly<{
  eventKey: string;
  scriptId: string;
  entryPoint: string;
}>;

export type VisualEngineeringDefinitionInput = Readonly<{
  objectId: string;
  key: string;
  objectType: string;
  parentObjectId?: string | null;
  baseProperties?: Readonly<Record<string, unknown>>;
  bindings?: readonly VisualDefinitionBindingDescriptor[];
  scriptEventReferences?: readonly VisualDefinitionScriptEventReference[];
  metadata?: Readonly<Record<string, string>>;
}>;

export type VisualEngineeringDefinitionProjection = Readonly<{
  objectId: string;
  key: string;
  objectType: string;
  parentObjectId: string | null;
  propertyKeys: readonly string[];
  baseProperties: Readonly<Record<string, VisualPropertyValue>>;
  bindings: readonly VisualDefinitionBindingDescriptor[];
  scriptEventReferences: readonly VisualDefinitionScriptEventReference[];
  metadata: Readonly<Record<string, string>>;
}>;

export function projectVisualEngineeringDefinition(
  input: VisualEngineeringDefinitionInput,
  schema: VisualObjectPropertySchema
): VisualEngineeringDefinitionProjection {
  assertDefinitionIdentity(input, schema);

  // Keep only explicitly engineered values here. Registry defaults belong to the
  // property schema and remain a distinct lower-priority Runtime source.
  const baseProperties: Record<string, VisualPropertyValue> = Object.create(null) as Record<string, VisualPropertyValue>;

  for (const [propertyKey, candidate] of Object.entries(input.baseProperties ?? {})) {
    const definition = schema.getRequired(propertyKey);
    if (!definition.engineeringEditable) {
      throw new VisualPropertyContractError(
        'projection.propertyNotEngineeringEditable',
        `Visual property '${propertyKey}' is not editable in Engineering.`,
        propertyKey
      );
    }

    const validation = schema.validate(propertyKey, candidate);
    if (!validation.ok) {
      throw new VisualPropertyContractError(
        `projection.${validation.code}`,
        `Engineering base value for '${propertyKey}' is invalid: ${validation.code}.`,
        propertyKey
      );
    }
    baseProperties[propertyKey] = cloneVisualPropertyValue(validation.value);
  }

  const bindings = Object.freeze((input.bindings ?? []).map(binding => projectBinding(binding, schema)));
  assertUniqueBindingTargets(bindings);

  const scriptEventReferences = Object.freeze(
    (input.scriptEventReferences ?? []).map(projectScriptEventReference)
  );
  assertUniqueScriptEventReferences(scriptEventReferences);

  const metadata = projectMetadata(input.metadata ?? {});

  return Object.freeze({
    objectId: input.objectId,
    key: input.key,
    objectType: input.objectType,
    parentObjectId: input.parentObjectId ?? null,
    propertyKeys: Object.freeze([...schema.propertyKeys]),
    baseProperties: Object.freeze(baseProperties),
    bindings,
    scriptEventReferences,
    metadata
  });
}

function assertDefinitionIdentity(
  input: VisualEngineeringDefinitionInput,
  schema: VisualObjectPropertySchema
): void {
  if (!isStableVisualToken(input.objectId)) {
    throw new VisualPropertyContractError('projection.invalidObjectId', 'Visual object ID is required and must be stable.');
  }
  if (!isStableVisualToken(input.key)) {
    throw new VisualPropertyContractError('projection.invalidKey', 'Visual object key is required and must be stable.');
  }
  if (!isStableVisualToken(input.objectType)) {
    throw new VisualPropertyContractError('projection.invalidObjectType', 'Visual object type is required and must be stable.');
  }
  if (input.objectType !== schema.objectTypeKey) {
    throw new VisualPropertyContractError(
      'projection.objectTypeMismatch',
      `Definition object type '${input.objectType}' does not match schema '${schema.objectTypeKey}'.`
    );
  }
  if (input.parentObjectId !== undefined && input.parentObjectId !== null && !isStableVisualToken(input.parentObjectId)) {
    throw new VisualPropertyContractError('projection.invalidParentObjectId', 'Parent object ID must be a stable reference.');
  }
}

function projectBinding(
  binding: VisualDefinitionBindingDescriptor,
  schema: VisualObjectPropertySchema
): VisualDefinitionBindingDescriptor {
  const definition = schema.getRequired(binding.propertyKey);
  if (!definition.supportsBinding) {
    throw new VisualPropertyContractError(
      'projection.bindingNotSupported',
      `Visual property '${binding.propertyKey}' does not support binding.`,
      binding.propertyKey
    );
  }
  if (binding.sourceKind !== 'binding' && binding.sourceKind !== 'expression') {
    throw new VisualPropertyContractError('projection.invalidBindingKind', 'Binding source kind is invalid.', binding.propertyKey);
  }
  if (!isStableVisualToken(binding.sourceReference)) {
    throw new VisualPropertyContractError('projection.invalidBindingReference', 'Binding source reference must be stable.', binding.propertyKey);
  }

  return Object.freeze({
    propertyKey: binding.propertyKey,
    sourceKind: binding.sourceKind,
    sourceReference: binding.sourceReference
  });
}

function projectScriptEventReference(
  reference: VisualDefinitionScriptEventReference
): VisualDefinitionScriptEventReference {
  if (!isStableVisualToken(reference.eventKey)) {
    throw new VisualPropertyContractError('projection.invalidEventKey', 'Visual event key must be stable.');
  }
  if (!isStableVisualToken(reference.scriptId)) {
    throw new VisualPropertyContractError('projection.invalidScriptId', 'Script ID must be stable.');
  }
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(reference.entryPoint)) {
    throw new VisualPropertyContractError('projection.invalidEntryPoint', 'Script entry point must be a Python identifier.');
  }

  return Object.freeze({ ...reference });
}

function assertUniqueBindingTargets(bindings: readonly VisualDefinitionBindingDescriptor[]): void {
  const targets = new Set<string>();
  for (const binding of bindings) {
    if (targets.has(binding.propertyKey)) {
      throw new VisualPropertyContractError(
        'projection.duplicateBinding',
        `Visual property '${binding.propertyKey}' has more than one definition-level binding.`,
        binding.propertyKey
      );
    }
    targets.add(binding.propertyKey);
  }
}

function assertUniqueScriptEventReferences(references: readonly VisualDefinitionScriptEventReference[]): void {
  const identities = new Set<string>();
  for (const reference of references) {
    const identity = `${reference.eventKey}\u0000${reference.scriptId}\u0000${reference.entryPoint}`;
    if (identities.has(identity)) {
      throw new VisualPropertyContractError(
        'projection.duplicateScriptEventReference',
        `Duplicate Script event reference '${reference.eventKey}/${reference.entryPoint}'.`
      );
    }
    identities.add(identity);
  }
}

function projectMetadata(metadata: Readonly<Record<string, string>>): Readonly<Record<string, string>> {
  const projected: Record<string, string> = Object.create(null) as Record<string, string>;
  for (const [key, value] of Object.entries(metadata)) {
    if (!isStableVisualToken(key)) {
      throw new VisualPropertyContractError('projection.invalidMetadataKey', 'Metadata keys must be stable non-empty strings.');
    }
    if (typeof value !== 'string') {
      throw new VisualPropertyContractError('projection.invalidMetadataValue', 'Metadata values must be strings.');
    }
    projected[key] = value;
  }
  return Object.freeze(projected);
}
