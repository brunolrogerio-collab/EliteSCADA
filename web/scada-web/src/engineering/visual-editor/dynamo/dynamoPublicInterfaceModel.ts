import type {
  DynamoEngineering,
  VisualElementEngineering
} from '../../types';
import {
  normalizeDynamoParameterDefinition,
  normalizeDynamoParameterKind,
  normalizeDynamoParameterValue
} from '../../../runtime/visual-navigation/dynamoParameterWireContract';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterKindEngineering,
  DynamoParameterValueEngineering
} from '../../../runtime/visual-navigation/runtimeVisualNavigationModel';

export type DynamoParameterEditorKind =
  | 'boolean'
  | 'number'
  | 'text'
  | 'equipment-path'
  | 'tag-reference'
  | 'value-source'
  | 'command';

export class DynamoPublicInterfaceError extends Error {
  constructor(
    public readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'DynamoPublicInterfaceError';
  }
}

export function listDynamoPublicParameters(
  definition: DynamoEngineering
): readonly DynamoParameterDefinitionEngineering[] {
  return Object.freeze((definition.parameters ?? []).map(normalizeDynamoParameterDefinition));
}

/**
 * Returns values visible to the authoring inspector. Legacy equipmentPath is
 * projected as the public equipmentPath parameter until every persisted instance
 * has migrated to the versioned parameter collection.
 */
export function listDynamoPublicParameterValues(
  instance: VisualElementEngineering,
  definition: DynamoEngineering
): readonly DynamoParameterValueEngineering[] {
  assertDynamoReference(instance, definition);
  const definitions = indexDefinitions(definition);
  const result = new Map<string, DynamoParameterValueEngineering>();

  for (const value of instance.dynamoParameters ?? []) {
    let normalizedValue = normalizeDynamoParameterValue(value);
    const key = normalizeKey(normalizedValue.key);
    const parameter = definitions.get(key);
    if (!parameter) continue;
    // The first catalog revision persisted direct TAG sources as TagReference.
    // Expose those as typed direct-TAG sources when the current definition has
    // moved to ValueSource, while leaving the persisted instance untouched.
    if (parameter.kind === 'ValueSource' && normalizedValue.kind === 'TagReference' && normalizedValue.tagReference) {
      normalizedValue = normalizeDynamoParameterValue({
        key: parameter.key,
        kind: 'ValueSource',
        version: parameter.version,
        valueSource: {
          kind: 'Tag',
          valueType: parameter.valueSourceType ?? 'Number',
          tagReference: normalizedValue.tagReference
        }
      });
    }
    result.set(key, cloneValue(normalizedValue));
  }

  const equipmentDefinition = definitions.get('equipmentpath');
  if (equipmentDefinition && !result.has('equipmentpath') && instance.equipmentPath?.trim()) {
    result.set('equipmentpath', Object.freeze({
      key: equipmentDefinition.key,
      kind: 'EquipmentPath',
      value: instance.equipmentPath.trim(),
      version: equipmentDefinition.version
    }));
  }

  return Object.freeze(
    [...definitions.keys()]
      .map(key => result.get(key))
      .filter((value): value is DynamoParameterValueEngineering => value !== undefined)
  );
}

export function resolveDynamoParameterEditorKind(
  kind: DynamoParameterKindEngineering
): DynamoParameterEditorKind {
  switch (normalizeDynamoParameterKind(kind as unknown)) {
    case 'Boolean': return 'boolean';
    case 'Number': return 'number';
    case 'String': return 'text';
    case 'EquipmentPath': return 'equipment-path';
    case 'TagReference': return 'tag-reference';
    case 'ValueSource': return 'value-source';
    case 'Command': return 'command';
  }
}

export function setDynamoPublicParameterValue(
  instance: VisualElementEngineering,
  definition: DynamoEngineering,
  nextValue: DynamoParameterValueEngineering
): VisualElementEngineering {
  assertDynamoReference(instance, definition);
  const definitions = indexDefinitions(definition);
  const definitionParameter = definitions.get(normalizeKey(nextValue.key));
  if (!definitionParameter) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_PARAMETER_UNKNOWN',
      `Dynamo '${definition.key}' does not expose public parameter '${nextValue.key}'.`
    );
  }

  const normalizedNextValue = normalizeDynamoParameterValue(nextValue);
  if (definitionParameter.kind !== normalizedNextValue.kind) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_PARAMETER_KIND_MISMATCH',
      `Dynamo parameter '${definitionParameter.key}' expects ${definitionParameter.kind} but received ${normalizedNextValue.kind}.`
    );
  }

  validateParameterValue(normalizedNextValue);
  const normalizedKey = normalizeKey(definitionParameter.key);
  const values = [...(instance.dynamoParameters ?? [])]
    .map(normalizeDynamoParameterValue)
    .filter(value => normalizeKey(value.key) !== normalizedKey);
  const canonicalValue: DynamoParameterValueEngineering = definitionParameter.kind === 'EquipmentPath'
    ? { ...normalizedNextValue, key: definitionParameter.key, value: String(normalizedNextValue.value).trim() }
    : { ...normalizedNextValue, key: definitionParameter.key };
  values.push(cloneValue(canonicalValue));

  const equipmentPath = definitionParameter.kind === 'EquipmentPath'
    ? String(canonicalValue.value)
    : instance.equipmentPath;

  return {
    ...instance,
    equipmentPath,
    dynamoParameters: Object.freeze(values)
  };
}

export function removeDynamoPublicParameterValue(
  instance: VisualElementEngineering,
  definition: DynamoEngineering,
  parameterKey: string
): VisualElementEngineering {
  assertDynamoReference(instance, definition);
  const definitions = indexDefinitions(definition);
  const definitionParameter = definitions.get(normalizeKey(parameterKey));
  if (!definitionParameter) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_PARAMETER_UNKNOWN',
      `Dynamo '${definition.key}' does not expose public parameter '${parameterKey}'.`
    );
  }
  if (definitionParameter.required === true) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_PARAMETER_REQUIRED',
      `Required Dynamo parameter '${definitionParameter.key}' cannot be removed.`
    );
  }

  const normalizedKey = normalizeKey(definitionParameter.key);
  const values = (instance.dynamoParameters ?? [])
    .map(normalizeDynamoParameterValue)
    .filter(value => normalizeKey(value.key) !== normalizedKey)
    .map(cloneValue);

  return {
    ...instance,
    equipmentPath: definitionParameter.kind === 'EquipmentPath' ? null : instance.equipmentPath,
    dynamoParameters: Object.freeze(values)
  };
}

function assertDynamoReference(
  instance: VisualElementEngineering,
  definition: DynamoEngineering
): void {
  if (!instance.dynamoKey || normalizeKey(instance.dynamoKey) !== normalizeKey(definition.key)) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_REFERENCE_MISMATCH',
      `Visual element '${instance.key}' does not reference Dynamo '${definition.key}'.`
    );
  }
}

function indexDefinitions(
  definition: DynamoEngineering
): Map<string, DynamoParameterDefinitionEngineering> {
  const result = new Map<string, DynamoParameterDefinitionEngineering>();
  for (const sourceParameter of definition.parameters ?? []) {
    const parameter = normalizeDynamoParameterDefinition(sourceParameter);
    const key = normalizeKey(parameter.key);
    if (!key) {
      throw new DynamoPublicInterfaceError(
        'DYNAMO_PUBLIC_PARAMETER_KEY_INVALID',
        `Dynamo '${definition.key}' contains an empty public parameter key.`
      );
    }
    if (result.has(key)) {
      throw new DynamoPublicInterfaceError(
        'DYNAMO_PUBLIC_PARAMETER_DUPLICATE',
        `Dynamo '${definition.key}' exposes public parameter '${parameter.key}' more than once.`
      );
    }
    result.set(key, parameter);
  }
  return result;
}

function validateParameterValue(value: DynamoParameterValueEngineering): void {
  if (value.kind === 'Command') {
    if (value.value !== undefined || value.tagReference || value.valueSource || !value.commandId?.trim()) {
      throw new DynamoPublicInterfaceError(
        'DYNAMO_PUBLIC_PARAMETER_VALUE_INVALID',
        `Command parameter '${value.key}' requires a stable Command identity and cannot carry scalar/TAG data.`
      );
    }
    return;
  }

  if (value.kind === 'TagReference') {
    if (value.value !== undefined || value.valueSource || !value.tagReference?.tagId?.trim()) {
      throw new DynamoPublicInterfaceError(
        'DYNAMO_PUBLIC_PARAMETER_VALUE_INVALID',
        `TagReference parameter '${value.key}' requires a stable TAG identity and cannot carry a scalar value.`
      );
    }
    return;
  }

  if (value.kind === 'ValueSource') {
    if (value.value !== undefined || value.tagReference || value.commandId || !value.valueSource) {
      throw new DynamoPublicInterfaceError(
        'DYNAMO_PUBLIC_PARAMETER_VALUE_INVALID',
        `ValueSource parameter '${value.key}' requires a typed source and cannot carry scalar/TAG/Command payloads.`
      );
    }
    return;
  }

  if (value.tagReference || value.commandId || value.valueSource) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_PARAMETER_VALUE_INVALID',
      `Scalar Dynamo parameter '${value.key}' cannot carry a TAG reference.`
    );
  }

  const valid = value.kind === 'Boolean'
    ? typeof value.value === 'boolean'
    : value.kind === 'Number'
      ? typeof value.value === 'number' && Number.isFinite(value.value)
      : value.kind === 'String'
        ? typeof value.value === 'string'
        : value.kind === 'EquipmentPath'
          ? typeof value.value === 'string' && value.value.trim().length > 0
          : false;

  if (!valid) {
    throw new DynamoPublicInterfaceError(
      'DYNAMO_PUBLIC_PARAMETER_VALUE_INVALID',
      `Dynamo parameter '${value.key}' has an invalid ${value.kind} value.`
    );
  }
}

function cloneValue(value: DynamoParameterValueEngineering): DynamoParameterValueEngineering {
  return Object.freeze({
    ...value,
    tagReference: value.tagReference
      ? Object.freeze({
          ...value.tagReference,
          selector: value.tagReference.selector
            ? Object.freeze({ ...value.tagReference.selector })
            : value.tagReference.selector
        })
      : value.tagReference,
    valueSource: value.valueSource
      ? normalizeDynamoParameterValue({ key: value.key, kind: 'ValueSource', valueSource: value.valueSource }).valueSource
      : value.valueSource
  });
}

function normalizeKey(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
}
