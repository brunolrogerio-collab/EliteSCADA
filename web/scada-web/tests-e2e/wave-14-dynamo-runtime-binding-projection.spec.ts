import { expect, test } from '@playwright/test';
import type { VisualElementEngineering } from '../src/engineering/types';
import type { DynamoParameterValueEngineering } from '../src/runtime/visual-navigation/runtimeVisualNavigationModel';
import {
  projectDynamoRuntimeElements,
  resolveDynamoRuntimeEquipmentPath
} from '../src/runtime/visual-navigation/dynamoRuntimeBindingProjection';

function parameters(...values: DynamoParameterValueEngineering[]) {
  return new Map(values.map(value => [value.key, value]));
}

const definitionElements: readonly VisualElementEngineering[] = [
  {
    id: 'lamp-running',
    key: 'running',
    type: 'core.ellipse',
    properties: { x: 0, y: 0, width: 18, height: 18 },
    bindings: [
      {
        key: 'visible',
        kind: 'Tag',
        target: '{equipmentPath}.Running',
        direction: 'read',
        metadata: {
          dynamoContext: 'equipmentPath',
          dynamoParameter: 'running'
        }
      }
    ]
  },
  {
    id: 'lamp-fault',
    key: 'fault',
    type: 'core.ellipse',
    properties: { x: 20, y: 0, width: 18, height: 18 },
    bindings: [
      {
        key: 'visible',
        kind: 'Tag',
        target: '{equipmentPath}.Fault',
        direction: 'read',
        metadata: {
          dynamoContext: 'equipmentPath',
          dynamoParameter: 'fault'
        }
      }
    ]
  }
];

test('typed equipmentPath overrides legacy instance field', () => {
  const resolved = resolveDynamoRuntimeEquipmentPath(
    'Legacy.P101',
    parameters({ key: 'equipmentPath', kind: 'EquipmentPath', value: ' Area.P202 ' })
  );

  expect(resolved).toBe('Area.P202');
});

test('legacy equipmentPath remains the fallback for existing instances', () => {
  expect(resolveDynamoRuntimeEquipmentPath(' Legacy.P101 ', new Map())).toBe('Legacy.P101');
  expect(resolveDynamoRuntimeEquipmentPath(null, new Map())).toBeNull();
});

test('public TagReference overrides the opted-in internal binding and preserves selector', () => {
  const projected = projectDynamoRuntimeElements(
    definitionElements,
    parameters({
      key: 'running',
      kind: 'TagReference',
      tagReference: { tagId: 'tag-running-id', selector: { kind: 'bit', index: 3 } }
    }),
    'Area.P202'
  );

  expect(projected[0]?.bindings?.[0]).toMatchObject({
    target: 'Area.P202.Running',
    tagReference: { tagId: 'tag-running-id', selector: { kind: 'bit', index: 3 } }
  });
});

test('missing optional TagReference preserves legacy equipmentPath binding', () => {
  const projected = projectDynamoRuntimeElements(definitionElements, new Map(), 'Area.P202');

  expect(projected[0]?.bindings?.[0]).toMatchObject({
    target: 'Area.P202.Running'
  });
  expect(projected[0]?.bindings?.[0]?.tagReference).toBeUndefined();
});

test('only the binding declaring the public parameter is overridden', () => {
  const projected = projectDynamoRuntimeElements(
    definitionElements,
    parameters({ key: 'running', kind: 'TagReference', tagReference: { tagId: 'tag-running-id' } }),
    'Area.P202'
  );

  expect(projected[0]?.bindings?.[0]?.tagReference?.tagId).toBe('tag-running-id');
  expect(projected[1]?.bindings?.[0]?.tagReference).toBeUndefined();
  expect(projected[1]?.bindings?.[0]?.target).toBe('Area.P202.Fault');
});

test('runtime projection does not mutate shared definition internals', () => {
  const originalTarget = definitionElements[0]?.bindings?.[0]?.target;
  const originalReference = definitionElements[0]?.bindings?.[0]?.tagReference;

  const projected = projectDynamoRuntimeElements(
    definitionElements,
    parameters({ key: 'running', kind: 'TagReference', tagReference: { tagId: 'tag-running-id' } }),
    'Area.P202'
  );

  expect(projected).not.toBe(definitionElements);
  expect(projected[0]).not.toBe(definitionElements[0]);
  expect(definitionElements[0]?.bindings?.[0]?.target).toBe(originalTarget);
  expect(definitionElements[0]?.bindings?.[0]?.tagReference).toBe(originalReference);
});

test('state color mapping follows the selected TAG and per-instance palette overrides', () => {
  const stateElement: VisualElementEngineering = {
    id: 'motor-body',
    key: 'body',
    type: 'core.rectangle',
    metadata: {
      dynamoStateColorParameter: 'state',
      dynamoStateColorProfile: 'stopped,running,fault'
    },
    propertyMaps: [{
      propertyKey: 'fillColor',
      source: { kind: 'Tag', valueType: 'Number', target: '{equipmentPath}.State' },
      rules: [
        { value: '#8FBF98', minimum: 0, maximum: 1 },
        { value: '#D98282', minimum: 1, maximum: 2 },
        { value: '#D8B95F', minimum: 2, maximum: 3 }
      ],
      fallback: '#8FBF98'
    }]
  };
  const projected = projectDynamoRuntimeElements(
    [stateElement],
    parameters(
      { key: 'state', kind: 'TagReference', tagReference: { tagId: 'tag-state-id' } },
      { key: 'stoppedColor', kind: 'String', value: '#21A34A' },
      { key: 'runningColor', kind: 'String', value: '#C02D20' },
      { key: 'faultColor', kind: 'String', value: '#EAB308' }
    ),
    'Area.M01'
  );

  expect(projected[0]?.propertyMaps?.[0]).toMatchObject({
    source: {
      target: 'Area.M01.State',
      tagReference: { tagId: 'tag-state-id' }
    },
    rules: [
      { value: '#21A34A' },
      { value: '#C02D20' },
      { value: '#EAB308' }
    ]
  });
});
