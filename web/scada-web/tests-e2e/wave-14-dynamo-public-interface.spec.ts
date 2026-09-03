import { expect, test } from '@playwright/test';
import type {
  DynamoEngineering,
  VisualElementEngineering
} from '../src/engineering/types';
import {
  DynamoPublicInterfaceError,
  listDynamoPublicParameterValues,
  listDynamoPublicParameters,
  removeDynamoPublicParameterValue,
  resolveDynamoParameterEditorKind,
  setDynamoPublicParameterValue
} from '../src/engineering/visual-editor/dynamo/dynamoPublicInterfaceModel';

const definition: DynamoEngineering = {
  id: '43000000-0000-0000-0000-000000000001',
  key: 'dynamo.pump.standard',
  name: 'Bomba centrífuga',
  parameters: [
    { key: 'equipmentPath', kind: 'EquipmentPath' },
    { key: 'running', kind: 'TagReference' },
    { key: 'fault', kind: 'TagReference' },
    { key: 'startCommandKey', kind: 'String' }
  ],
  elements: [
    {
      id: '43100000-0000-0000-0000-000000000001',
      key: 'body',
      type: 'core.ellipse',
      properties: { x: 0, y: 0, width: 40, height: 40 }
    }
  ]
};

function instance(overrides: Partial<VisualElementEngineering> = {}): VisualElementEngineering {
  return {
    id: 'instance-1',
    key: 'P-101',
    type: 'core.group',
    dynamoKey: definition.key,
    equipmentPath: 'Plant.P101',
    properties: { x: 10, y: 10, width: 132, height: 92 },
    ...overrides
  };
}

test('lists only the definition-declared public interface and maps editor kinds', () => {
  const parameters = listDynamoPublicParameters(definition);

  expect(parameters.map(parameter => parameter.key)).toEqual([
    'equipmentPath',
    'running',
    'fault',
    'startCommandKey'
  ]);
  expect(resolveDynamoParameterEditorKind(parameters[0].kind)).toBe('equipment-path');
  expect(resolveDynamoParameterEditorKind(parameters[1].kind)).toBe('tag-reference');
  expect(resolveDynamoParameterEditorKind(parameters[3].kind)).toBe('text');
  expect(Object.isFrozen(parameters)).toBe(true);
});

test('projects legacy equipmentPath through the public interface without mutating the instance', () => {
  const current = instance({ equipmentPath: '  Plant.P101  ', dynamoParameters: [] });
  const values = listDynamoPublicParameterValues(current, definition);

  expect(values).toEqual([
    {
      key: 'equipmentPath',
      kind: 'EquipmentPath',
      value: 'Plant.P101',
      version: undefined
    }
  ]);
  expect(current.equipmentPath).toBe('  Plant.P101  ');
});

test('setting equipmentPath updates the typed parameter and legacy fallback together', () => {
  const updated = setDynamoPublicParameterValue(instance(), definition, {
    key: 'equipmentPath',
    kind: 'EquipmentPath',
    value: ' Area01.Pump02 '
  });

  expect(updated.equipmentPath).toBe('Area01.Pump02');
  expect(updated.dynamoParameters).toEqual([
    {
      key: 'equipmentPath',
      kind: 'EquipmentPath',
      value: ' Area01.Pump02 '
    }
  ]);
});

test('TAG references are authored through the public interface without exposing or rewriting definition children', () => {
  const originalElements = definition.elements;
  const updated = setDynamoPublicParameterValue(instance(), definition, {
    key: 'running',
    kind: 'TagReference',
    tagReference: { tagId: 'tag-running', selector: { kind: 'bit', index: 2 } }
  });

  expect(updated.dynamoParameters).toEqual([
    {
      key: 'running',
      kind: 'TagReference',
      tagReference: { tagId: 'tag-running', selector: { kind: 'bit', index: 2 } }
    }
  ]);
  expect(definition.elements).toBe(originalElements);
  expect(definition.elements?.[0].key).toBe('body');
});

test('unknown and mismatched parameters fail closed', () => {
  expect(() => setDynamoPublicParameterValue(instance(), definition, {
    key: 'privateChildBinding',
    kind: 'String',
    value: 'nope'
  })).toThrow(DynamoPublicInterfaceError);

  expect(() => setDynamoPublicParameterValue(instance(), definition, {
    key: 'running',
    kind: 'Boolean',
    value: true
  })).toThrow(/expects TagReference/);

  expect(() => setDynamoPublicParameterValue(instance(), definition, {
    key: 'equipmentPath',
    kind: 'EquipmentPath',
    value: '   '
  })).toThrow(/invalid EquipmentPath value/);
});

test('removing equipmentPath clears both the typed value and legacy fallback', () => {
  const current = instance({
    dynamoParameters: [
      { key: 'equipmentPath', kind: 'EquipmentPath', value: 'Plant.P101' },
      { key: 'fault', kind: 'TagReference', tagReference: { tagId: 'tag-fault' } }
    ]
  });
  const updated = removeDynamoPublicParameterValue(current, definition, 'equipmentPath');

  expect(updated.equipmentPath).toBeNull();
  expect(updated.dynamoParameters).toEqual([
    { key: 'fault', kind: 'TagReference', tagReference: { tagId: 'tag-fault' } }
  ]);
});

test('required public parameters cannot be silently removed', () => {
  const requiredDefinition: DynamoEngineering = {
    ...definition,
    parameters: [{ key: 'running', kind: 'TagReference', required: true }]
  };
  const current = instance({
    dynamoParameters: [
      { key: 'running', kind: 'TagReference', tagReference: { tagId: 'tag-running' } }
    ]
  });

  expect(() => removeDynamoPublicParameterValue(current, requiredDefinition, 'running'))
    .toThrow(/cannot be removed/);
});
