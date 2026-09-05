import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import { buildVisualEditorAuthoringToolbarState } from '../src/engineering/visual-editor/canvas/visualEditorAuthoringToolbarModel';
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
  return { key: 'screen', name: 'Screen', elements: [...elements], properties: {}, context: {}, metadata: {} };
}

test('toolbar enables sibling CAD operations by selection cardinality', () => {
  const draft = screen([rectangle('a'), rectangle('b'), rectangle('c')]);
  const state = buildVisualEditorAuthoringToolbarState(draft, ['a', 'b', 'c']);
  expect(state.canAlign).toBe(true);
  expect(state.canDistribute).toBe(true);
  expect(state.canSize).toBe(true);
  expect(state.canGroup).toBe(true);
  expect(state.referenceObjectId).toBe('a');
});

test('toolbar blocks cross-coordinate-space arrangement', () => {
  const draft = screen([{
    id: 'group', key: 'group', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    children: [rectangle('child')]
  }, rectangle('peer')]);
  const state = buildVisualEditorAuthoringToolbarState(draft, ['child', 'peer']);
  expect(state.sameParent).toBe(false);
  expect(state.canAlign).toBe(false);
  expect(state.canGroup).toBe(false);
});

test('toolbar blocks arrangement of inherited locked objects', () => {
  const draft = screen([{
    id: 'group', key: 'group', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    metadata: { 'engineering.authoring.locked': 'true' },
    children: [rectangle('a'), rectangle('b')]
  }]);
  const state = buildVisualEditorAuthoringToolbarState(draft, ['a', 'b']);
  expect(state.hasEffectiveLock).toBe(true);
  expect(state.canAlign).toBe(false);
  expect(state.canToggleLock).toBe(false);
});

test('toolbar never exposes Dynamo instances as ungroupable authoring groups', () => {
  const draft = screen([{
    id: 'pump', key: 'pump', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    dynamoKey: 'dynamo.pump.standard',
    properties: { x: 0, y: 0, width: 100, height: 100 }
  }]);
  const state = buildVisualEditorAuthoringToolbarState(draft, ['pump']);
  expect(state.canUngroup).toBe(false);
  expect(state.canToggleLock).toBe(true);
});
