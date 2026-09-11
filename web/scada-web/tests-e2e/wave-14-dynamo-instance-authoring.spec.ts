import { expect, test } from '@playwright/test';
import type {
  DynamoEngineering,
  ScreenEngineering,
  VisualElementEngineering
} from '../src/engineering/types';
import {
  applyVisualEditorSessionKeyboardCommand,
  createVisualEditorSession,
  currentVisualEditorSessionScreen,
  withVisualEditorSessionSelection
} from '../src/engineering/visual-editor/visualEditorSessionModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

const definition: DynamoEngineering = {
  id: '43000000-0000-0000-0000-000000000001',
  key: 'dynamo.pump.standard',
  name: 'Pump',
  parameters: [
    { key: 'equipmentPath', kind: 'EquipmentPath' },
    { key: 'running', kind: 'TagReference' }
  ]
};

function instance(metadata?: Record<string, string>): VisualElementEngineering {
  return {
    id: 'instance-1',
    key: 'pump-1',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    dynamoKey: definition.key,
    properties: { x: 0, y: 0, width: 100, height: 80 },
    metadata
  };
}

function screen(element: VisualElementEngineering): ScreenEngineering {
  return { key: 'screen', name: 'Screen', elements: [element], properties: {}, context: {}, metadata: {} };
}

function selected(element: VisualElementEngineering) {
  return withVisualEditorSessionSelection(createVisualEditorSession(screen(element)), ['instance-1']);
}

function currentInstance(session: ReturnType<typeof selected>): VisualElementEngineering {
  return currentVisualEditorSessionScreen(session).elements![0];
}

test('Dynamo EquipmentPath session command writes typed value and legacy compatibility projection', () => {
  let session = selected(instance());
  session = applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'dynamoParameter.set',
    objectId: 'instance-1',
    definition,
    value: { key: 'equipmentPath', kind: 'EquipmentPath', value: 'Plant.P01' }
  });

  expect(currentInstance(session).equipmentPath).toBe('Plant.P01');
  expect(currentInstance(session).dynamoParameters).toContainEqual({
    key: 'equipmentPath', kind: 'EquipmentPath', value: 'Plant.P01'
  });

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'undo' });
  expect(currentInstance(session).equipmentPath).toBeUndefined();
  expect(currentInstance(session).dynamoParameters).toBeUndefined();
});

test('Dynamo TagReference session command persists stable TAG identity', () => {
  let session = selected(instance());
  session = applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'dynamoParameter.set',
    objectId: 'instance-1',
    definition,
    value: { key: 'running', kind: 'TagReference', tagReference: { tagId: 'tag-id-running' } }
  });

  expect(currentInstance(session).dynamoParameters).toContainEqual({
    key: 'running', kind: 'TagReference', tagReference: { tagId: 'tag-id-running' }
  });
});

test('Dynamo parameter removal clears equipmentPath compatibility projection', () => {
  let session = selected({
    ...instance(),
    equipmentPath: 'Plant.P01',
    dynamoParameters: [{ key: 'equipmentPath', kind: 'EquipmentPath', value: 'Plant.P01' }]
  });
  session = applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'dynamoParameter.remove',
    objectId: 'instance-1',
    definition,
    parameterKey: 'equipmentPath'
  });
  expect(currentInstance(session).equipmentPath).toBeNull();
  expect(currentInstance(session).dynamoParameters).toEqual([]);
});

test('Dynamo public parameters cannot be edited through an authoring lock', () => {
  const session = selected(instance({ 'engineering.authoring.locked': 'true' }));
  expect(() => applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'dynamoParameter.set',
    objectId: 'instance-1',
    definition,
    value: { key: 'equipmentPath', kind: 'EquipmentPath', value: 'Plant.P02' }
  })).toThrow(/locked for Engineering authoring/);
});
