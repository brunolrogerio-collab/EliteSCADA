import { expect, test } from '@playwright/test';
import type { DynamoEngineering, VisualElementEngineering } from '../src/engineering/types';
import { compileVisualExpression, evaluateVisualExpression } from '../src/expressions/visualExpressionCore';
import { composeDynamoRuntime, type DynamoParameterValueEngineering } from '../src/runtime/visual-navigation/runtimeVisualNavigationModel';
import {
  projectDynamoRuntimeElements,
  resolveDynamoRuntimeEquipmentPath
} from '../src/runtime/visual-navigation/dynamoRuntimeBindingProjection';
import { collectRuntimeDynamoEventOnlyObjectIds } from '../src/runtime/visual-navigation/runtimeDynamoVisualProjection';
import { resolveDynamoParameterEditorKind, resolveDynamoValueSourceType, setDynamoPublicParameterValue } from '../src/engineering/visual-editor/dynamo/dynamoPublicInterfaceModel';

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

test('only persisted Dynamo roots receive event-only Runtime identities, including nested instances', () => {
  const ids = collectRuntimeDynamoEventOnlyObjectIds([
    { id: 'plain-group', key: 'group', type: 'core.group', children: [
      { id: 'pump-dynamo', key: 'pump', type: 'dynamo', dynamoKey: 'equipment.motor.axial' },
      { id: 'plain-rect', key: 'rect', type: 'core.rectangle' }
    ] },
    { id: 'lamp-dynamo', key: 'lamp', type: 'dynamo', dynamoKey: 'indicator.lamp.round' }
  ]);

  expect([...ids].sort()).toEqual(['lamp-dynamo', 'pump-dynamo']);
});

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

test('Dynamo composition accepts API camelCase EquipmentPath parameters at every entry point', () => {
  const definition = {
    id: '55000000-0000-0000-0000-000000000001',
    key: 'pump.test',
    name: 'Pump test',
    parameters: [{ key: 'equipmentPath', kind: 'equipmentPath', required: false }],
    elements: definitionElements
  } as unknown as DynamoEngineering;
  const instance = {
    id: '56000000-0000-0000-0000-000000000001',
    key: 'pump-1',
    type: 'dynamo',
    dynamoKey: 'pump.test',
    dynamoDefinitionId: definition.id,
    dynamoParameters: [{ key: 'equipmentPath', kind: 'equipmentPath', value: 'Plant.P01' }]
  } as unknown as VisualElementEngineering;

  const composed = composeDynamoRuntime(instance, definition);

  expect(composed.parameters.get('equipmentPath')).toMatchObject({
    key: 'equipmentPath', kind: 'EquipmentPath', value: 'Plant.P01'
  });
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

test('typed Dynamo expressions preserve Client Memory dependency identity', () => {
  const memoryTagId = '33333333-3333-4333-8333-333333333333';
  const element: VisualElementEngineering = {
    key: 'fault-indicator',
    type: 'core.rectangle',
    booleanConditions: [{
      propertyKey: 'visible',
      kind: 'Direct',
      source: { kind: 'Tag', valueType: 'Boolean', target: '{dynamoParameter:faultSignal}' }
    }]
  };
  const projected = projectDynamoRuntimeElements([element], parameters({
    key: 'faultSignal',
    kind: 'ValueSource',
    valueSource: {
      kind: 'Expression',
      valueType: 'Boolean',
      expression: {
        text: 'source',
        resultType: 'Boolean',
        dependencies: [{
          symbol: 'source',
          kind: 'ClientMemory',
          valueType: 'Boolean',
          tagReference: { tagId: memoryTagId },
          target: 'Signals.Fault'
        }]
      }
    }
  }), null);
  const dependency = projected[0].booleanConditions?.[0].source?.expression?.dependencies?.[0];

  expect(dependency).toMatchObject({
    kind: 'ClientMemory',
    target: 'Signals.Fault',
    tagReference: { tagId: memoryTagId }
  });
});

test('electrical contact inversion swaps the binary open/closed state without changing its TAG identity', () => {
  const tagId = '44444444-4444-4444-8444-444444444444';
  const element: VisualElementEngineering = {
    key: 'contact-blade',
    type: 'core.rectangle',
    metadata: {
      dynamoStateColorParameter: 'state',
      dynamoNumericStateInvertParameter: 'invertState'
    },
    propertyMaps: [{
      propertyKey: 'fillColor',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      rules: [{ value: '#526879', minimum: 0, maximum: 1 }, { value: '#16A34A', minimum: 1, maximum: 2 }]
    }]
  };
  const projected = projectDynamoRuntimeElements([element], parameters(
    { key: 'state', kind: 'TagReference', tagReference: { tagId } },
    { key: 'invertState', kind: 'Boolean', value: true }
  ), null);
  const source = projected[0].propertyMaps?.[0].source;

  expect(source?.kind).toBe('Expression');
  expect(source?.expression?.text).toBe('1 - source');
  expect(source?.expression?.dependencies?.[0].tagReference.tagId).toBe(tagId);
});

test('three-pole electrical contacts bind independent Boolean closure signals in runtime projection', () => {
  const tagIds = [
    '11111111-1111-4111-8111-111111111111',
    '22222222-2222-4222-8222-222222222222',
    '33333333-3333-4333-8333-333333333333'
  ];
  const elements: VisualElementEngineering[] = tagIds.map((tagId, index) => ({
    key: `contact-${index}-moving`,
    type: 'core.rectangle',
    metadata: {
      dynamoStateColorParameter: 'state',
      dynamoNumericStateInvertParameter: 'invertState',
      dynamoContactDiscreteStateModeParameter: 'useIndependentPoleSignals',
      dynamoContactPoleSignalParameter: `pole${index + 1}ClosedSignal`
    },
    propertyMaps: [
      {
        propertyKey: 'fillColor',
        source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
        rules: [{ value: '#526879', minimum: 0, maximum: 1 }, { value: '#16A34A', minimum: 1, maximum: 2 }]
      },
      {
        propertyKey: 'rotation',
        source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
        rules: [{ value: 16, minimum: 0, maximum: 1 }, { value: 0, minimum: 1, maximum: 2 }]
      }
    ]
  }));
  const projected = projectDynamoRuntimeElements(elements, parameters(
    { key: 'state', kind: 'ValueSource', valueSource: { kind: 'Tag', valueType: 'Number', tagReference: { tagId: tagIds[0] } } },
    { key: 'useIndependentPoleSignals', kind: 'Boolean', value: true },
    { key: 'invertState', kind: 'Boolean', value: true },
    ...tagIds.map((tagId, index) => ({
      key: `pole${index + 1}ClosedSignal`, kind: 'ValueSource' as const,
      valueSource: { kind: 'Tag' as const, valueType: 'Boolean' as const, tagReference: { tagId } }
    }))
  ), null);

  expect(projected).toHaveLength(3);
  for (const [index, element] of projected.entries()) {
    const colorSource = element.propertyMaps?.find(map => map.propertyKey === 'fillColor')?.source;
    const rotationSource = element.propertyMaps?.find(map => map.propertyKey === 'rotation')?.source;
    expect(colorSource).toMatchObject({
      kind: 'Expression', valueType: 'Number',
      expression: { text: 'number(not source)', dependencies: [{ tagReference: { tagId: tagIds[index] } }] }
    });
    expect(rotationSource).toEqual(colorSource);
  }
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

test('motor and valve runtime paint composes discrete state signals with fault-first priority', () => {
  const element: VisualElementEngineering = {
    key: 'equipment-state', type: 'core.svgSymbol',
    metadata: {
      dynamoStateColorParameter: 'state',
      dynamoDiscreteStateModeParameter: 'useDiscreteSignals',
      dynamoDiscreteStateSignalProfile: 'runningSignal,faultSignal,communicationBadSignal,inhibitedSignal'
    },
    propertyMaps: [{
      propertyKey: 'svg.slot.state.fill',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      rules: []
    }],
    booleanConditions: [{
      propertyKey: 'visible', kind: 'NumericInterval',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      minimum: 2, maximum: 3
    }]
  };
  const source = (tagId: string) => ({
    kind: 'Tag' as const,
    valueType: 'Boolean' as const,
    tagReference: { tagId }
  });
  const projected = projectDynamoRuntimeElements([element], parameters(
    { key: 'useDiscreteSignals', kind: 'Boolean', value: true },
    { key: 'runningSignal', kind: 'ValueSource', valueSource: source('11111111-1111-4111-8111-111111111111') },
    { key: 'faultSignal', kind: 'ValueSource', valueSource: {
      kind: 'Expression', valueType: 'Boolean', expression: {
        text: 'trip or overload', resultType: 'Boolean', dependencies: [
          { symbol: 'trip', kind: 'Tag', valueType: 'Boolean', tagReference: { tagId: '22222222-2222-4222-8222-222222222222' } },
          { symbol: 'overload', kind: 'ClientMemory', valueType: 'Boolean', tagReference: { tagId: '33333333-3333-4333-8333-333333333333' } }
        ]
      }
    } },
    { key: 'communicationBadSignal', kind: 'ValueSource', valueSource: source('44444444-4444-4444-8444-444444444444') },
    { key: 'inhibitedSignal', kind: 'ValueSource', valueSource: source('55555555-5555-4555-8555-555555555555') }
  ), null);

  const projectedMap = projected[0]?.propertyMaps?.[0];
  const expression = projectedMap?.source.expression;
  expect(projectedMap?.source.valueType).toBe('Number');
  expect(expression?.text).toContain('number(dynamo_faultSignal_dep0 or dynamo_faultSignal_dep1) * 2');
  expect(expression?.text).toContain('number((dynamo_communicationBadSignal and not (dynamo_faultSignal_dep0 or dynamo_faultSignal_dep1))) * 3');
  expect(expression?.text).toContain('number((dynamo_inhibitedSignal and not (dynamo_faultSignal_dep0 or dynamo_faultSignal_dep1 or dynamo_communicationBadSignal))) * 4');
  expect(expression?.dependencies).toHaveLength(5);
  expect(expression?.dependencies?.some(dependency => dependency.kind === 'ClientMemory')).toBe(true);
  expect(projected[0]?.booleanConditions?.[0]?.source.expression?.text).toBe(expression?.text);

  const compiled = compileVisualExpression(expression!.text, 'number', expression!.dependencies!.map(dependency => ({
    symbol: dependency.symbol,
    kind: dependency.kind === 'ClientMemory' ? 'clientMemory' as const : 'tag' as const,
    valueType: 'boolean' as const,
    tagReference: dependency.tagReference
  })));
  expect(compiled.ok).toBe(true);
  if (!compiled.ok) return;
  const evaluate = (signals: Readonly<Record<string, boolean>>) => evaluateVisualExpression(
    compiled.expression,
    dependency => ({ value: signals[dependency.symbol], dataType: 'Boolean', quality: 'Good' })
  );
  expect(evaluate({
    dynamo_runningSignal: true,
    dynamo_faultSignal_dep0: true,
    dynamo_faultSignal_dep1: false,
    dynamo_communicationBadSignal: true,
    dynamo_inhibitedSignal: true
  })).toMatchObject({ ok: true, value: 2 });
  expect(evaluate({
    dynamo_runningSignal: true,
    dynamo_faultSignal_dep0: false,
    dynamo_faultSignal_dep1: false,
    dynamo_communicationBadSignal: true,
    dynamo_inhibitedSignal: true
  })).toMatchObject({ ok: true, value: 3 });
});

test('state enable parameters collapse omitted lamp stages to the configured off color', () => {
  const element: VisualElementEngineering = {
    key: 'signal-lamp', type: 'core.svgSymbol',
    metadata: {
      dynamoStateColorProfile: 'off,running,fault',
      dynamoStateEnableParameterProfile: 'always,enableRunning,enableFault'
    },
    propertyMaps: [{
      propertyKey: 'svg.slot.state.fill',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      rules: [
        { value: '#111111', minimum: 0, maximum: 1 },
        { value: '#22aa22', minimum: 1, maximum: 2 },
        { value: '#ff0000', minimum: 2, maximum: 3 }
      ]
    }]
  };
  const projected = projectDynamoRuntimeElements([element], parameters(
    { key: 'offColor', kind: 'String', value: '#333333' },
    { key: 'runningColor', kind: 'String', value: '#00aa00' },
    { key: 'faultColor', kind: 'String', value: '#ff9900' },
    { key: 'enableRunning', kind: 'Boolean', value: true },
    { key: 'enableFault', kind: 'Boolean', value: false }
  ), null);

  expect(projected[0]?.propertyMaps?.[0]?.rules.map(rule => rule.value))
    .toEqual(['#333333', '#00aa00', '#333333']);
});

test('disabled motor and valve states also suppress their animated text labels', () => {
  const label: VisualElementEngineering = {
    key: 'state-label-fault', type: 'core.text',
    properties: { visible: false },
    metadata: {
      dynamoStateLabelIndex: '2',
      dynamoStateEnableParameterProfile: 'always,enableRunning,enableFault'
    },
    booleanConditions: [{
      propertyKey: 'visible', kind: 'NumericInterval',
      source: { kind: 'Tag', valueType: 'Number', target: '{dynamoParameter:state}' },
      minimum: 2, maximum: 3
    }]
  };
  const projected = projectDynamoRuntimeElements([label], parameters(
    { key: 'enableFault', kind: 'Boolean', value: false }
  ), null);

  expect(projected[0]?.properties?.visible).toBe(false);
  expect(projected[0]?.booleanConditions).toBeUndefined();
});

test('typed value-source parameters use the dedicated TAG/expression authoring editor', () => {
  expect(resolveDynamoParameterEditorKind('ValueSource')).toBe('value-source');
});

test('Dynamo Boolean/Number sources reject non-scalar TAG data types', () => {
  expect(resolveDynamoValueSourceType('Boolean')).toBe('Boolean');
  expect(resolveDynamoValueSourceType('UInt16')).toBe('Number');
  expect(resolveDynamoValueSourceType('Double')).toBe('Number');
  expect(resolveDynamoValueSourceType('DateTime')).toBeNull();
  expect(resolveDynamoValueSourceType('String')).toBeNull();
});

test('Dynamo script-output value source stores Client Memory identity and keeps it in Runtime composition', () => {
  const definition: DynamoEngineering = {
    id: '55000000-0000-4000-8000-000000000001',
    key: 'indicator.lamp.round',
    name: 'Sinalizador redondo',
    parameters: [{ key: 'faultSignal', kind: 'ValueSource', valueSourceType: 'Boolean' }],
    elements: []
  };
  const instance: VisualElementEngineering = {
    id: '55000000-0000-4000-8000-000000000002',
    key: 'lamp-1',
    type: 'core.group',
    dynamoKey: definition.key,
    dynamoDefinitionId: definition.id
  };
  const withOutput = setDynamoPublicParameterValue(instance, definition, {
    key: 'faultSignal',
    kind: 'ValueSource',
    valueSource: {
      kind: 'ClientMemory',
      valueType: 'Boolean',
      target: 'Signals.Fault',
      tagReference: { tagId: '55000000-0000-4000-8000-000000000003' }
    }
  });

  expect(withOutput.dynamoParameters?.[0]?.valueSource).toMatchObject({
    kind: 'ClientMemory', target: 'Signals.Fault', valueType: 'Boolean',
    tagReference: { tagId: '55000000-0000-4000-8000-000000000003' }
  });
  expect(composeDynamoRuntime(withOutput, definition).parameters.get('faultSignal')?.valueSource).toMatchObject({
    kind: 'ClientMemory', target: 'Signals.Fault',
    tagReference: { tagId: '55000000-0000-4000-8000-000000000003' }
  });
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

test('button Dynamo lets each instance select a canonical command or TAG action', () => {
  const button: VisualElementEngineering = {
    id: 'button-action-mode', key: 'button', type: 'core.rectangle',
    metadata: { dynamoActionModeParameter: 'actionMode' },
    actions: [{ eventKey: 'click', kind: 'ExecuteCommand', commandParameterKey: 'command' }]
  };
  const common = [
    { key: 'command', kind: 'Command' as const, commandId: 'command-1' },
    { key: 'targetTag', kind: 'TagReference' as const, tagReference: { tagId: 'tag-1' } },
    { key: 'analogValue', kind: 'Number' as const, value: 11.5 },
    { key: 'booleanValue', kind: 'Boolean' as const, value: true }
  ];
  const resolve = (mode: string) => projectDynamoRuntimeElements([button], parameters(
    { key: 'actionMode', kind: 'String', value: mode }, ...common
  ), null)[0]?.actions?.[0];

  expect(resolve('command')).toMatchObject({ kind: 'ExecuteCommand', commandId: 'command-1' });
  expect(resolve('set-analog')).toMatchObject({ kind: 'SetTagValue', targetKey: 'tag-1', parameters: { value: 11.5 } });
  expect(resolve('set-bool')).toMatchObject({ kind: 'SetTagValue', targetKey: 'tag-1', parameters: { value: true } });
  expect(resolve('toggle-bool')).toMatchObject({ kind: 'ToggleTagBoolean', targetKey: 'tag-1' });
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
