import { expect, test } from '@playwright/test';
import type { DynamoEngineering, VisualElementEngineering } from '../src/engineering/types';
import { composeDynamoRuntime, type DynamoParameterValueEngineering } from '../src/runtime/visual-navigation/runtimeVisualNavigationModel';
import {
  projectDynamoRuntimeElements,
  resolveDynamoRuntimeEquipmentPath
} from '../src/runtime/visual-navigation/dynamoRuntimeBindingProjection';
import { resolveDynamoParameterEditorKind } from '../src/engineering/visual-editor/dynamo/dynamoPublicInterfaceModel';

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

test('typed Dynamo value-source parameters project TAG expressions into canonical behavior', () => {
  const firstTagId = '11111111-1111-4111-8111-111111111111';
  const secondTagId = '22222222-2222-4222-8222-222222222222';
  const element: VisualElementEngineering = {
    key: 'running-indicator',
    type: 'core.rectangle',
    booleanConditions: [{
      propertyKey: 'visible',
      kind: 'Direct',
      source: { kind: 'Tag', valueType: 'Boolean', target: '{dynamoParameter:running}' }
    }]
  };
  const valueSource = {
    kind: 'Expression' as const,
    valueType: 'Boolean' as const,
    expression: {
      text: 'running and permitted',
      resultType: 'Boolean' as const,
      dependencies: [
        { symbol: 'running', kind: 'Tag' as const, valueType: 'Boolean' as const, tagReference: { tagId: firstTagId } },
        { symbol: 'permitted', kind: 'Tag' as const, valueType: 'Boolean' as const, tagReference: { tagId: secondTagId } }
      ]
    }
  };
  const projected = projectDynamoRuntimeElements(
    [element],
    parameters({ key: 'running', kind: 'ValueSource', valueSource }),
    null
  );
  const source = projected[0].booleanConditions?.[0].source;

  expect(source?.kind).toBe('Expression');
  expect(source?.expression?.text).toBe('running and permitted');
  expect(source?.expression?.dependencies?.map(dependency => dependency.tagReference.tagId))
    .toEqual([firstTagId, secondTagId]);
});

test('Dynamo composition applies typed value-source defaults and rejects a mismatched result type', () => {
  const source = {
    kind: 'Expression' as const,
    valueType: 'Number' as const,
    expression: {
      text: 'status',
      resultType: 'Number' as const,
      dependencies: [{
        symbol: 'status', kind: 'Tag' as const, valueType: 'Number' as const,
        tagReference: { tagId: '33333333-3333-4333-8333-333333333333' }
      }]
    }
  };
  const definition: DynamoEngineering = {
    id: 'dynamo-definition',
    key: 'indicator.lamp.round',
    name: 'Round signal lamp',
    parameters: [{ key: 'state', kind: 'ValueSource', required: true, valueSourceType: 'Number', defaultValueSource: source }],
    elements: []
  };
  const instance: VisualElementEngineering = {
    id: 'lamp-instance', key: 'lamp-instance', type: 'dynamo', dynamoKey: definition.key
  };
  const composed = composeDynamoRuntime(instance, definition);

  expect(composed.parameters.get('state')?.valueSource?.expression?.text).toBe('status');
  expect(() => composeDynamoRuntime({
    ...instance,
    dynamoParameters: [{ key: 'state', kind: 'ValueSource', valueSource: {
      kind: 'Expression', valueType: 'Boolean', expression: { text: 'ready', resultType: 'Boolean' }
    } }]
  }, definition)).toThrow(/requires a Number visual value source/);
});

test('numeric state paint converts boolean TAG and expression sources with optional inversion', () => {
  const tagId = '33333333-3333-4333-8333-333333333334';
  const element: VisualElementEngineering = {
    id: 'lamp-lens', key: 'lens', type: 'core.svgSymbol',
    properties: { x: 0, y: 0, width: 10, height: 10 },
    metadata: { dynamoStateColorParameter: 'state', dynamoStateColorProfile: 'off,running', dynamoBooleanStateInvertParameter: 'invertBoolean' },
    propertyMaps: [{
      propertyKey: 'fillColor',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      rules: [{ value: '#111111', minimum: 0, maximum: 1 }, { value: '#22aa22', minimum: 1, maximum: 2 }]
    }]
  };
  const projected = projectDynamoRuntimeElements([element], parameters(
    { key: 'state', kind: 'ValueSource', valueSource: { kind: 'Tag', valueType: 'Boolean', tagReference: { tagId } } },
    { key: 'invertBoolean', kind: 'Boolean', value: true }
  ), null);

  expect(projected[0]?.propertyMaps?.[0]?.source).toMatchObject({
    kind: 'Expression', valueType: 'Number',
    expression: { text: 'number(not source)', resultType: 'Number', dependencies: [{ symbol: 'source', kind: 'Tag', tagReference: { tagId } }] }
  });
});

test('typed value-source parameters use the dedicated TAG/expression authoring editor', () => {
  expect(resolveDynamoParameterEditorKind('ValueSource')).toBe('value-source');
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
      target: null,
      tagReference: { tagId: 'tag-state-id' }
    },
    rules: [
      { value: '#21A34A' },
      { value: '#C02D20' },
      { value: '#EAB308' }
    ]
  });
});

test('lamp outline color stays independent from state paint and can add a 3D depth effect', () => {
  const bezel: VisualElementEngineering = {
    id: 'lamp-bezel', key: 'artwork', type: 'core.svgSymbol',
    properties: { svgPaintOverrides: { version: 1, palette: {}, slots: { state: { fill: '#16A34A' } } }, shadowEnabled: false },
    metadata: { dynamoOutlineColorParameter: 'bezelColor', dynamo3dEffectParameter: 'bezel3d' }
  };
  const projected = projectDynamoRuntimeElements([bezel], parameters(
    { key: 'bezelColor', kind: 'String', value: '#26485A' },
    { key: 'bezel3d', kind: 'Boolean', value: true }
  ), null);

  expect(projected[0]?.properties).toMatchObject({
    shadowEnabled: true, shadowColor: '#24374699', shadowOffsetX: 1, shadowOffsetY: 2, shadowBlur: 2,
    svgPaintOverrides: { slots: {
      bezel: { stroke: '#26485A' }, state: { fill: '#16A34A', stroke: '#26485A' }
    } }
  });
});

test('equipment state expression source projects consistently to color maps and state labels', () => {
  const source = {
    kind: 'Expression',
    valueType: 'Number',
    expression: {
      text: 'motorState',
      resultType: 'Number',
      dependencies: [{
        symbol: 'motorState', kind: 'Tag', valueType: 'Number', tagReference: { tagId: 'tag-state-id' }
      }]
    }
  } as const;
  const body: VisualElementEngineering = {
    id: 'motor-body', key: 'state-body', type: 'core.svgSymbol',
    metadata: { dynamoStateColorParameter: 'state', dynamoStateColorProfile: 'stopped,running,fault' },
    propertyMaps: [{
      propertyKey: 'svg.slot.state.fill',
      source: { kind: 'Tag', valueType: 'Number', target: '{equipmentPath}.State' },
      rules: [{ value: '#777777', minimum: 0, maximum: 1 }]
    }]
  };
  const label: VisualElementEngineering = {
    id: 'motor-label-1', key: 'state-label-1', type: 'core.text',
    metadata: { dynamoStateLabelIndex: '1' },
    booleanConditions: [{
      propertyKey: 'visible', kind: 'NumericInterval',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' }, minimum: 1, maximum: 2
    }]
  };

  const projected = projectDynamoRuntimeElements([body, label], parameters(
    { key: 'state', kind: 'ValueSource', valueSource: source }
  ), 'Area.M01');
  expect(projected[0]?.propertyMaps?.[0]?.source).toMatchObject({
    kind: 'Expression', valueType: 'Number', expression: { text: 'motorState' }
  });
  expect(projected[1]?.booleanConditions?.[0]?.source).toMatchObject({
    kind: 'Expression', valueType: 'Number', expression: { text: 'motorState' }
  });
});

test('button Dynamo action tokens resolve to canonical TAG identity and typed scalar values', () => {
  const button: VisualElementEngineering = {
    id: 'button', key: 'button', type: 'core.rectangle',
    actions: [{
      eventKey: 'click', kind: 'SetTagValue', targetKey: '{targetTag}',
      parameters: { value: '{analogValue}' }
    }]
  };
  const projected = projectDynamoRuntimeElements([button], parameters(
    { key: 'targetTag', kind: 'TagReference', tagReference: { tagId: 'stable-tag-id' } },
    { key: 'analogValue', kind: 'Number', value: 47.25 }
  ), null);

  expect(projected[0]?.actions?.[0]).toMatchObject({
    eventKey: 'click', kind: 'SetTagValue', targetKey: 'stable-tag-id', parameters: { value: 47.25 }
  });
});

test('button pressed feedback accepts an inverted Boolean expression source', () => {
  const button: VisualElementEngineering = {
    id: 'button-feedback', key: 'artwork', type: 'core.svgSymbol',
    metadata: {
      dynamoStateColorParameter: 'state', dynamoStateColorProfile: 'released,pressed',
      dynamoBooleanStateInvertParameter: 'invertBoolean'
    },
    propertyMaps: [{
      propertyKey: 'svg.slot.state.fill',
      source: { kind: 'Tag', valueType: 'Number', target: '{equipmentPath}.State' },
      rules: [{ value: '#7B8E9B', minimum: 0, maximum: 1 }, { value: '#1687C9', minimum: 1, maximum: 2 }]
    }]
  };
  const projected = projectDynamoRuntimeElements([button], parameters(
    { key: 'state', kind: 'ValueSource', valueSource: {
      kind: 'Expression', valueType: 'Boolean',
      expression: { text: 'isPressed', resultType: 'Boolean', dependencies: [{
        symbol: 'isPressed', kind: 'Tag', valueType: 'Boolean', tagReference: { tagId: 'tag-pressed' }
      }] }
    } },
    { key: 'invertBoolean', kind: 'Boolean', value: true }
  ), null);

  expect(projected[0]?.propertyMaps?.[0]?.source).toMatchObject({
    kind: 'Expression', valueType: 'Number', expression: { text: 'number(not (isPressed))' }
  });
});

test('animated SVG Dynamo can pin one state and preserve it as a static paint override', () => {
  const symbol: VisualElementEngineering = {
    id: 'symbol', key: 'artwork', type: 'core.svgSymbol',
    properties: { svgPaintOverrides: { version: 1, palette: {}, slots: {} } },
    metadata: {
      dynamoStateColorParameter: 'state',
      dynamoStateColorProfile: 'stopped,running,fault',
      dynamoAnimationEnabledParameter: 'animationEnabled',
      dynamoFixedStateParameter: 'fixedState'
    },
    propertyMaps: [{
      propertyKey: 'svg.slot.state.fill',
      source: { kind: 'Tag', valueType: 'Number', target: '{equipmentPath}.State' },
      rules: [{ value: '#8FBF98', minimum: 0, maximum: 1 }, { value: '#D98282', minimum: 1, maximum: 2 }, { value: '#D8B95F', minimum: 2, maximum: 3 }],
      fallback: '#8FBF98'
    }]
  };
  const projected = projectDynamoRuntimeElements([symbol], parameters(
    { key: 'animationEnabled', kind: 'Boolean', value: false },
    { key: 'fixedState', kind: 'Number', value: 2 },
    { key: 'faultColor', kind: 'String', value: '#E20D21' }
  ), null);

  expect(projected[0]?.propertyMaps).toBeUndefined();
  expect(projected[0]?.properties?.svgPaintOverrides).toMatchObject({ slots: { state: { fill: '#E20D21' } } });
});

test('Dynamo equipment state labels bind to a stable TAG and pin text when animation is disabled', () => {
  const label: VisualElementEngineering = {
    id: 'label-3', key: 'label-3', type: 'core.text',
    properties: { text: '{communicationBadText}', visible: false },
    metadata: {
      dynamoStateLabelIndex: '3',
      dynamoFixedStateParameter: 'fixedState',
      dynamoAnimationEnabledParameter: 'animationEnabled'
    },
    booleanConditions: [{
      propertyKey: 'visible', kind: 'NumericInterval',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      minimum: 3, maximum: 4
    }]
  };
  const tagReference = { tagId: 'tag-state-id' };
  const fixed = projectDynamoRuntimeElements([label], parameters(
    { key: 'communicationBadText', kind: 'String', value: 'SEM COMUNICAÇÃO' },
    { key: 'animationEnabled', kind: 'Boolean', value: false },
    { key: 'fixedState', kind: 'Number', value: 3 }
  ), null);

  expect(fixed[0]?.properties).toMatchObject({ text: 'SEM COMUNICAÇÃO', visible: true });
  expect(fixed[0]?.booleanConditions).toBeUndefined();

  const animated = projectDynamoRuntimeElements([label], parameters(
    { key: 'state', kind: 'TagReference', tagReference }
  ), null);
  expect(animated[0]?.booleanConditions?.[0]?.source).toMatchObject({ tagReference, target: '{dynamoParameter:state}' });
});
