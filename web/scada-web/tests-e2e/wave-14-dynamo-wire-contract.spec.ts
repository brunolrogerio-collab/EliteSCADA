import { expect, test } from '@playwright/test';
import type {
  DynamoEngineering,
  VisualElementEngineering
} from '../src/engineering/types';
import {
  listDynamoPublicParameters,
  resolveDynamoParameterEditorKind
} from '../src/engineering/visual-editor/dynamo/dynamoPublicInterfaceModel';
import {
  normalizeDynamoDefinitionParameterContract,
  normalizeDynamoInstanceParameterContract
} from '../src/runtime/visual-navigation/dynamoParameterWireContract';
import { expandRuntimeDynamoVisuals } from '../src/runtime/visual-navigation/runtimeDynamoVisualProjection';

const wireDefinition = ({
  id: '43000000-0000-0000-0000-000000000001',
  key: 'dynamo.pump.standard',
  name: 'Bomba centrífuga',
  parameters: [
    {
      key: 'equipmentPath',
      kind: 'equipmentPath',
      required: false,
      defaultValue: null,
      defaultTagReference: null,
      version: 1
    },
    {
      key: 'running',
      kind: 'tagReference',
      required: false,
      defaultValue: null,
      defaultTagReference: null,
      version: 1
    },
    {
      key: 'startCommandKey',
      kind: 'string',
      required: false,
      defaultValue: null,
      defaultTagReference: null,
      version: 1
    }
  ],
  elements: [
    {
      id: '43100000-0000-0000-0000-000000000001',
      key: 'running',
      type: 'core.ellipse',
      properties: { x: 4, y: 4, width: 18, height: 18, visible: false },
      bindings: [
        {
          key: 'visible',
          kind: 'tag',
          target: '{equipmentPath}.Running',
          direction: 'read',
          metadata: { dynamoContext: 'equipmentPath', dynamoParameter: 'running' }
        }
      ]
    }
  ]
} as unknown) as DynamoEngineering;

function legacyInstance(overrides: Partial<VisualElementEngineering> = {}): VisualElementEngineering {
  return {
    id: '5af6bcaa-ea75-4bf5-b487-61fa7d202656',
    key: 'pump01',
    type: 'dynamo',
    dynamoKey: wireDefinition.key,
    equipmentPath: 'Demo.P01',
    properties: { x: 430, y: 160 },
    dynamoParameters: null,
    ...overrides
  };
}

test('backend camel-case parameter kinds normalize to the established browser composition kinds', () => {
  const normalized = normalizeDynamoDefinitionParameterContract(wireDefinition);

  expect(normalized.parameters?.map(parameter => parameter.kind)).toEqual([
    'EquipmentPath',
    'TagReference',
    'String'
  ]);
  expect(normalized.parameters?.[0]?.defaultValue).toBeUndefined();
  expect(normalized.parameters?.[1]?.defaultValue).toBeUndefined();
});

test('authoring public interface consumes backend wire kinds without exposing serializer casing', () => {
  const parameters = listDynamoPublicParameters(wireDefinition);

  expect(parameters[0]?.kind).toBe('EquipmentPath');
  expect(parameters[1]?.kind).toBe('TagReference');
  expect(resolveDynamoParameterEditorKind(parameters[0]!.kind)).toBe('equipment-path');
  expect(resolveDynamoParameterEditorKind(parameters[1]!.kind)).toBe('tag-reference');
});

test('legacy Dynamo equipmentPath expands against backend wire definition without a diagnostic element', () => {
  const expanded = expandRuntimeDynamoVisuals([legacyInstance()], [wireDefinition]);
  const instance = expanded[0]!;

  expect(instance.metadata?.['runtime.dynamo.expanded']).toBe('true');
  expect(instance.metadata?.['runtime.dynamo.diagnostic']).toBeUndefined();
  expect(instance.children?.[0]?.bindings?.[0]).toMatchObject({
    target: 'Demo.P01.Running'
  });
});

test('backend camel-case persisted parameter values normalize before runtime composition', () => {
  const wireInstance = legacyInstance({
    dynamoParameters: ([
      {
        key: 'running',
        kind: 'tagReference',
        tagReference: { tagId: '10000000-0000-0000-0000-000000000002' },
        version: 1
      }
    ] as unknown) as NonNullable<VisualElementEngineering['dynamoParameters']>
  });

  const normalizedInstance = normalizeDynamoInstanceParameterContract(wireInstance);
  expect(normalizedInstance.dynamoParameters?.[0]?.kind).toBe('TagReference');

  const expanded = expandRuntimeDynamoVisuals([wireInstance], [wireDefinition]);
  expect(expanded[0]?.metadata?.['runtime.dynamo.expanded']).toBe('true');
  expect(expanded[0]?.children?.[0]?.bindings?.[0]?.tagReference?.tagId)
    .toBe('10000000-0000-0000-0000-000000000002');
});
