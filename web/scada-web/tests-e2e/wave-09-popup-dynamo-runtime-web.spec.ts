import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import type { EngineeringPackageView } from '../src/engineering/types';
import {
  composeDynamoRuntime,
  createRuntimeVisualCatalog,
  createRuntimeVisualNavigationState,
  executeVisualNavigationAction,
  resolveVisualNavigationAction,
  RuntimeVisualCompositionError,
  runtimeDynamoElementIdentity,
  type CanonicalDynamoEngineering,
  type CanonicalVisualElementEngineering,
  type VisualNavigationActionEngineering
} from '../src/runtime/visual-navigation/runtimeVisualNavigationModel';

const screenAId = '11111111-1111-1111-1111-111111111111';
const screenBId = '22222222-2222-2222-2222-222222222222';
const popupId = '33333333-3333-3333-3333-333333333333';
const dynamoDefinitionId = '44444444-4444-4444-4444-444444444444';
const dynamoInstanceId = '55555555-5555-5555-5555-555555555555';
const dynamoChildId = '66666666-6666-6666-6666-666666666666';
const tagId = '77777777-7777-7777-7777-777777777777';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

function packageView(): EngineeringPackageView {
  const dynamo: CanonicalDynamoEngineering = {
    id: dynamoDefinitionId,
    key: 'dynamo.pump',
    name: 'Pump',
    parameters: [
      { key: 'caption', kind: 'String', defaultValue: 'Pump default', version: 1 },
      { key: 'running', kind: 'TagReference', required: true, version: 1 }
    ],
    elements: [
      {
        id: dynamoChildId,
        key: 'body',
        type: 'core.rectangle',
        bindings: [{ key: 'visible', kind: 'tag', target: '{equipmentPath}.Running', direction: 'read' }],
        properties: { x: 4, y: 5, width: 80, height: 40, fillColor: '#445566' }
      }
    ]
  } as CanonicalDynamoEngineering;

  const dynamoInstance: CanonicalVisualElementEngineering = {
    id: dynamoInstanceId,
    key: 'pump01',
    type: 'dynamo',
    dynamoKey: dynamo.key,
    equipmentPath: ' Plant.P01 ',
    properties: { x: 10, y: 20, width: 120, height: 80 },
    dynamoParameters: [
      { key: 'running', kind: 'TagReference', tagReference: { tagId, selector: { kind: 'Bit', index: 3 } }, version: 1 }
    ],
    actions: [
      { eventKey: 'click', kind: 'OpenPopup', targetKey: 'popup.pump', parameters: { equipmentPath: 'Plant.P01' }, version: 1 }
    ]
  };

  const closeElement: CanonicalVisualElementEngineering = {
    id: '88888888-8888-8888-8888-888888888888',
    key: 'close',
    type: 'core.button',
    properties: { text: 'Close' },
    actions: [{ eventKey: 'click', kind: 'ClosePopup', version: 1 }]
  };

  return {
    schema: 'scada.engineering',
    schemaVersion: 13,
    exportedAt: '2026-08-30T00:00:00Z',
    tags: [],
    alarms: [],
    dynamos: [dynamo],
    screens: [
      { id: screenAId, key: 'screen.a', name: 'Screen A', route: '/a', elements: [dynamoInstance] },
      { id: screenBId, key: 'screen.b', name: 'Screen B', route: '/b', elements: [] }
    ],
    popups: [
      { id: popupId, key: 'popup.pump', name: 'Pump Details', elements: [closeElement] }
    ]
  } as EngineeringPackageView;
}

test('canonical navigation resolves Screen and Popup targets and closes only the originating Popup instance', () => {
  const project = packageView();
  const catalog = createRuntimeVisualCatalog(project);
  let state = createRuntimeVisualNavigationState(catalog, 'SCREEN.A');

  const instance = project.screens![0].elements![0];
  const open = resolveVisualNavigationAction(instance, 'CLICK');
  expect(open).not.toBeNull();

  state = executeVisualNavigationAction(catalog, state, open!, {
    popupIdFactory: () => 'popup-runtime-1'
  });
  expect(state.activeScreenKey).toBe('screen.a');
  expect(state.popups).toEqual([
    {
      runtimeInstanceId: 'popup-runtime-1',
      definitionKey: 'popup.pump',
      parameters: { equipmentPath: 'Plant.P01' }
    }
  ]);

  const navigate: VisualNavigationActionEngineering = {
    eventKey: 'click', kind: 'NavigateScreen', targetKey: 'SCREEN.B', version: 1
  };
  state = executeVisualNavigationAction(catalog, state, navigate);
  expect(state.activeScreenKey).toBe('screen.b');
  expect(state.popups).toHaveLength(1);

  const close = project.popups![0].elements![0] as CanonicalVisualElementEngineering;
  const closeAction = resolveVisualNavigationAction(close, 'click');
  state = executeVisualNavigationAction(catalog, state, closeAction!, {
    popupRuntimeInstanceId: 'popup-runtime-1'
  });
  expect(state.popups).toEqual([]);
});

test('navigation failures are explicit and fail before mutating the caller-owned state', () => {
  const catalog = createRuntimeVisualCatalog(packageView());
  const state = createRuntimeVisualNavigationState(catalog, 'screen.a');

  expectRuntimeCode(
    () => executeVisualNavigationAction(catalog, state, {
      eventKey: 'click', kind: 'OpenPopup', targetKey: 'popup.missing', version: 1
    }),
    'VISUAL_RUNTIME_POPUP_NOT_FOUND'
  );
  expect(state.activeScreenKey).toBe('screen.a');
  expect(state.popups).toEqual([]);

  expectRuntimeCode(
    () => executeVisualNavigationAction(catalog, state, {
      eventKey: 'click', kind: 'ClosePopup', version: 1
    }),
    'VISUAL_RUNTIME_CLOSE_POPUP_CONTEXT_REQUIRED'
  );

  expectRuntimeCode(
    () => createRuntimeVisualCatalog({
      screens: [
        { id: screenAId, key: 'Screen.A', name: 'One' },
        { id: screenBId, key: 'screen.a', name: 'Duplicate' }
      ],
      popups: [],
      dynamos: []
    }),
    'VISUAL_RUNTIME_ENTITY_KEY_DUPLICATE'
  );
});

test('Dynamo Runtime composition mirrors canonical defaults, stable TAG reference and composite child identity', () => {
  const project = packageView();
  const instance = project.screens![0].elements![0];
  const definition = project.dynamos![0];
  const composition = composeDynamoRuntime(instance, definition);

  expect(composition.instanceId).toBe(dynamoInstanceId);
  expect(composition.definitionId).toBe(dynamoDefinitionId);
  expect(composition.definitionKey).toBe('dynamo.pump');
  expect(composition.parameters.get('caption')).toEqual({
    key: 'caption', kind: 'String', value: 'Pump default', version: 1, tagReference: undefined
  });
  expect(composition.parameters.get('running')).toEqual({
    key: 'running',
    kind: 'TagReference',
    tagReference: { tagId, selector: { kind: 'Bit', index: 3 } },
    version: 1,
    value: undefined
  });
  expect(composition.elements[0].id).toBe(dynamoChildId);
  expect(composition.elements[0].bindings?.[0].target).toBe('Plant.P01.Running');
  expect(definition.elements?.[0].bindings?.[0].target).toBe('{equipmentPath}.Running');
  expect(runtimeDynamoElementIdentity(dynamoInstanceId, dynamoChildId))
    .toBe(`${dynamoInstanceId}/${dynamoChildId}`);
});

test('Dynamo Runtime rejects missing required, unknown, mismatched and nested parameter/composition state', () => {
  const project = packageView();
  const definition = project.dynamos![0] as CanonicalDynamoEngineering;
  const base = project.screens![0].elements![0] as CanonicalVisualElementEngineering;

  expectRuntimeCode(
    () => composeDynamoRuntime({ ...base, dynamoParameters: [] }, definition),
    'VISUAL_RUNTIME_DYNAMO_PARAMETER_REQUIRED'
  );
  expectRuntimeCode(
    () => composeDynamoRuntime({
      ...base,
      dynamoParameters: [
        ...(base.dynamoParameters ?? []),
        { key: 'unexpected', kind: 'String', value: 'x', version: 1 }
      ]
    }, definition),
    'VISUAL_RUNTIME_DYNAMO_PARAMETER_UNKNOWN'
  );
  expectRuntimeCode(
    () => composeDynamoRuntime({
      ...base,
      dynamoParameters: [{ key: 'running', kind: 'String', value: 'true', version: 1 }]
    }, definition),
    'VISUAL_RUNTIME_DYNAMO_PARAMETER_KIND_MISMATCH'
  );
  expectRuntimeCode(
    () => composeDynamoRuntime(base, {
      ...definition,
      elements: [{ id: dynamoChildId, key: 'nested', type: 'dynamo', dynamoKey: 'dynamo.other' }]
    }),
    'VISUAL_RUNTIME_DYNAMO_NESTING_NOT_SUPPORTED'
  );
});

test('canonical renderer expands Dynamo only when definitions are supplied and keeps definition identity separate from runtime identity', async () => {
  const renderer = await source('../src/engineering/visual-editor/CanonicalVisualRenderer.tsx');

  expect(renderer).toContain('if (element.dynamoKey && dynamoDefinitions)');
  expect(renderer).toContain('composeDynamoRuntime(element, definition)');
  expect(renderer).toContain('data-dynamo-definition-id={composition.definitionId}');
  expect(renderer).toContain('data-dynamo-instance-id={composition.instanceId}');
  expect(renderer).toContain('runtimeIdentityPrefix={composition.instanceId}');
  expect(renderer).toContain('return runtimeDynamoElementIdentity(runtimeIdentityPrefix, element.id ?? \'\');');
  expect(renderer).toContain('<LegacyCompatibilityElement');
});

test('RuntimeVisualNavigator consumes canonical actions and Popup context without introducing route or DOM persistence authority', async () => {
  const navigator = await source('../src/runtime/visual-navigation/RuntimeVisualNavigator.tsx');
  const model = await source('../src/runtime/visual-navigation/runtimeVisualNavigationModel.ts');

  expect(navigator).toContain('executeVisualNavigationAction(catalog, state, action');
  expect(navigator).toContain('popupRuntimeInstanceId');
  expect(navigator).toContain('dynamoDefinitions={engineeringPackage.dynamos}');
  expect(navigator).toContain('data-diagnostic-code={diagnostic.code}');

  expect(model).toContain("case 'NavigateScreen':");
  expect(model).toContain("case 'OpenPopup':");
  expect(model).toContain("case 'ClosePopup':");
  expect(model).toContain('definitionKey: popup.key');
  expect(model).toContain('runtimeInstanceId');
  expect(model).not.toContain('window.location');
  expect(model).not.toContain('document.querySelector');
  expect(model).not.toContain('localStorage');
  expect(model).not.toContain('sessionStorage');
});

function expectRuntimeCode(operation: () => unknown, code: string) {
  try {
    operation();
    throw new Error(`Expected RuntimeVisualCompositionError '${code}'.`);
  } catch (reason) {
    expect(reason).toBeInstanceOf(RuntimeVisualCompositionError);
    expect((reason as RuntimeVisualCompositionError).code).toBe(code);
  }
}
