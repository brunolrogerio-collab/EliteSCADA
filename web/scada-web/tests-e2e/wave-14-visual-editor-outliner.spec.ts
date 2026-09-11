import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import {
  buildVisualEditorOutliner,
  countVisualEditorOutlinerNodes
} from '../src/engineering/visual-editor/canvas/visualEditorOutlinerModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

function rectangle(id: string): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x: 0, y: 0, width: 20, height: 20 }
  };
}

function screen(elements: readonly VisualElementEngineering[]): ScreenEngineering {
  return {
    key: 'screen',
    name: 'Screen',
    route: '/screen',
    elements: [...elements],
    properties: {},
    context: {},
    metadata: {}
  };
}

test('outliner preserves canonical hierarchy and counts nested authoring objects', () => {
  const draft = screen([{
    id: 'group',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    children: [rectangle('child-a'), rectangle('child-b')]
  }, rectangle('peer')]);

  const nodes = buildVisualEditorOutliner(draft);
  expect(nodes.map(node => node.objectId)).toEqual(['group', 'peer']);
  expect(nodes[0].children.map(node => node.objectId)).toEqual(['child-a', 'child-b']);
  expect(countVisualEditorOutlinerNodes(nodes)).toBe(4);
});

test('outliner differentiates direct lock from lock inherited through hierarchy', () => {
  const draft = screen([{
    id: 'group',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    metadata: { 'engineering.authoring.locked': 'true' },
    children: [rectangle('child')]
  }]);

  const group = buildVisualEditorOutliner(draft)[0];
  expect(group.directLocked).toBe(true);
  expect(group.effectiveLocked).toBe(true);
  expect(group.children[0].directLocked).toBe(false);
  expect(group.children[0].effectiveLocked).toBe(true);
});

test('outliner identifies a Dynamo instance as one reusable authoring object', () => {
  const draft = screen([{
    id: 'pump-instance',
    key: 'pump-1',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    dynamoKey: 'dynamo.pump.standard',
    equipmentPath: 'Plant.P01',
    properties: { x: 20, y: 20, width: 132, height: 92 }
  }]);

  const node = buildVisualEditorOutliner(draft)[0];
  expect(node.objectId).toBe('pump-instance');
  expect(node.dynamoKey).toBe('dynamo.pump.standard');
  expect(node.children).toEqual([]);
});
