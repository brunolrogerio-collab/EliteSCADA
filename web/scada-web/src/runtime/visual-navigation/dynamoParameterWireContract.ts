import type {
  DynamoEngineering,
  VisualElementEngineering,
  VisualValueSourceEngineering
} from '../../engineering/types';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterKindEngineering,
  DynamoParameterValueEngineering
} from './runtimeVisualNavigationModel';
import {
  normalizeVisualExpressionDependencyKind,
  normalizeVisualExpressionValueType,
  normalizeVisualValueSourceKind
} from '../../engineering/visual-editor/visualDynamicWireContract';

export class DynamoParameterWireContractError extends Error {
  constructor(
    public readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'DynamoParameterWireContractError';
  }
}

/**
 * System.Text.Json emits Engineering enum values using the API camel-case wire
 * convention. The browser keeps the established PascalCase composition model
 * internally, so all API-originated Dynamo parameter kinds cross this seam.
 */
export function normalizeDynamoParameterKind(
  value: unknown
): DynamoParameterKindEngineering {
  switch (String(value ?? '').trim()) {
    case 'Boolean':
    case 'boolean':
      return 'Boolean';
    case 'Number':
    case 'number':
      return 'Number';
    case 'String':
    case 'string':
      return 'String';
    case 'EquipmentPath':
    case 'equipmentPath':
      return 'EquipmentPath';
    case 'TagReference':
    case 'tagReference':
      return 'TagReference';
    case 'Command':
    case 'command':
      return 'Command';
    case 'ValueSource':
    case 'valueSource':
      return 'ValueSource';
    default:
      throw new DynamoParameterWireContractError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_KIND_UNSUPPORTED',
        `Unsupported Dynamo parameter kind '${String(value)}'.`
      );
  }
}

/**
 * Nullable JsonElement defaults are serialized as JSON null by the backend when
 * no default was declared. Scalar Dynamo kinds do not admit null values, so null
 * means "no default", not an explicit parameter value.
 */
export function normalizeDynamoParameterDefinition(
  parameter: DynamoParameterDefinitionEngineering
): DynamoParameterDefinitionEngineering {
  const wire = parameter as DynamoParameterDefinitionEngineering & Readonly<{ kind: unknown }>;
  const valueSourceType = parameter.valueSourceType === null || parameter.valueSourceType === undefined
    ? parameter.valueSourceType
    : normalizeValueType(parameter.valueSourceType);
  return Object.freeze({
    ...parameter,
    kind: normalizeDynamoParameterKind(wire.kind),
    valueSourceType,
    defaultValue: parameter.defaultValue === null ? undefined : parameter.defaultValue,
    defaultTagReference: parameter.defaultTagReference
      ? Object.freeze({
          ...parameter.defaultTagReference,
          selector: parameter.defaultTagReference.selector
            ? Object.freeze({ ...parameter.defaultTagReference.selector })
            : parameter.defaultTagReference.selector
        })
      : parameter.defaultTagReference,
    defaultValueSource: parameter.defaultValueSource ? normalizeValueSource(parameter.defaultValueSource) : parameter.defaultValueSource
  });
}

/**
 * Instance values cross the same nullable JsonElement boundary as definition
 * defaults. A persisted JSON null therefore represents an absent scalar value;
 * genuine non-null mixed scalar + TagReference payloads remain intact so the
 * compositor can reject them fail-closed.
 */
export function normalizeDynamoParameterValue(
  parameter: DynamoParameterValueEngineering
): DynamoParameterValueEngineering {
  const wire = parameter as DynamoParameterValueEngineering & Readonly<{ kind: unknown }>;
  return Object.freeze({
    ...parameter,
    kind: normalizeDynamoParameterKind(wire.kind),
    value: parameter.value === null ? undefined : parameter.value,
    commandId: parameter.commandId === null ? undefined : parameter.commandId,
    tagReference: parameter.tagReference
      ? Object.freeze({
          ...parameter.tagReference,
          selector: parameter.tagReference.selector
            ? Object.freeze({ ...parameter.tagReference.selector })
            : parameter.tagReference.selector
        })
      : parameter.tagReference,
    valueSource: parameter.valueSource ? normalizeValueSource(parameter.valueSource) : parameter.valueSource
  });
}

function normalizeValueSource(source: NonNullable<DynamoParameterValueEngineering['valueSource']>) {
  const kind = normalizeVisualValueSourceKind(source.kind);
  if (!kind.ok) {
    throw new DynamoParameterWireContractError(
      'VISUAL_RUNTIME_DYNAMO_PARAMETER_VALUE_SOURCE_INVALID',
      kind.message
    );
  }
  const valueType = normalizeValueType(source.valueType);

  const expression = source.expression ? normalizeExpression(source.expression) : source.expression;
  return Object.freeze({
    ...source,
    kind: kind.value,
    valueType,
    tagReference: source.tagReference
      ? Object.freeze({
          ...source.tagReference,
          selector: source.tagReference.selector ? Object.freeze({ ...source.tagReference.selector }) : source.tagReference.selector
        })
      : source.tagReference,
    expression
  });
}

function normalizeExpression(expression: NonNullable<VisualValueSourceEngineering['expression']>) {
  const resultType = normalizeValueType(expression.resultType);

  return Object.freeze({
    ...expression,
    resultType,
    dependencies: expression.dependencies
      ? Object.freeze(expression.dependencies.map(dependency => {
          const kind = normalizeVisualExpressionDependencyKind(dependency.kind);
          if (!kind.ok) {
            throw new DynamoParameterWireContractError(
              'VISUAL_RUNTIME_DYNAMO_PARAMETER_VALUE_SOURCE_INVALID',
              kind.message
            );
          }
          const valueType = normalizeValueType(dependency.valueType);
          return Object.freeze({
            ...dependency,
            kind: kind.value,
            valueType,
            tagReference: Object.freeze({
              ...dependency.tagReference,
              selector: dependency.tagReference.selector ? Object.freeze({ ...dependency.tagReference.selector }) : dependency.tagReference.selector
            })
          });
        }))
      : expression.dependencies
  });
}

function normalizeValueType(value: unknown): 'Boolean' | 'Number' {
  const normalized = normalizeVisualExpressionValueType(value);
  if (!normalized.ok) {
    throw new DynamoParameterWireContractError(
      'VISUAL_RUNTIME_DYNAMO_PARAMETER_VALUE_SOURCE_INVALID',
      normalized.message
    );
  }
  return normalized.value;
}

export function normalizeDynamoDefinitionParameterContract(
  definition: DynamoEngineering
): DynamoEngineering {
  if (!definition.parameters?.length) return definition;
  return Object.freeze({
    ...definition,
    parameters: Object.freeze(definition.parameters.map(normalizeDynamoParameterDefinition))
  });
}

export function normalizeDynamoInstanceParameterContract(
  instance: VisualElementEngineering
): VisualElementEngineering {
  if (!instance.dynamoParameters?.length) return instance;
  return Object.freeze({
    ...instance,
    dynamoParameters: Object.freeze(instance.dynamoParameters.map(normalizeDynamoParameterValue))
  });
}
