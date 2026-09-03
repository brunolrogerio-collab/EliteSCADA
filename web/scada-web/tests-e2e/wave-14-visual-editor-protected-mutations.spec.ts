import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import { applyProtectedVisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorProtectedMutationModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

function rectangle(
  id: string,
  x: number,
  zIndex = 0,
  metadata: Record<string, string> = {}
): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x, y: 10, width: 20, height: 20, zIndex },
    metadata
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

test('protected reducer rejects geometry and property mutations on locked objects', () => {
  const draft = screen([
    rectangle('locked', 10, 0, { 'engineering.authoring.locked': 'true' })
  ]);

  expect(() => applyProtectedVisualEditorMutationIntent(draft, {
    kind: 'object.move', objectIds: ['locked'], delta: { x: 10, y: 0 }
  })).toThrow(/locked for Engineering authoring/);

  expect(() => applyProtectedVisualEditorMutationIntent(draft, {
    kind: 'property.set', objectIds: ['locked'], propertyKey: 'x', value: 25
  })).toThrow(/locked for Engineering authoring/);

  expect(() => applyProtectedVisualEditorMutationIntent(draft, {
    kind: 'object.duplicate', objectIds: ['locked']
  })).toThrow(/locked for Engineering authoring/);
});

test('protected reducer inherits lock from an authoring group', () => {
  const draft = screen([{
    id: 'group',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    metadata: { 'engineering.authoring.locked': 'true' },
    children: [rectangle('child', 10)]
  }]);

  expect(() => applyProtectedVisualEditorMutationIntent(draft, {
    kind: 'object.resize',
    objectId: 'child',
    bounds: { x: 10, y: 10, width: 40, height: 40 }
  })).toThrow(/locked for Engineering authoring/);
});

test('protected reducer routes legacy z-order intents through deterministic stacking', () => {
  const draft = screen([
    rectangle('one', 10, 4),
    rectangle('two', 40, 4),
    rectangle('three', 70, 4)
  ]);

  const next = applyProtectedVisualEditorMutationIntent(draft, {
    kind: 'object.zOrder', objectIds: ['one'], operation: 'bringToFront'
  });
  const values = Object.fromEntries((next.elements ?? []).map(element => [element.id!, element.properties?.zIndex]));

  expect(new Set(Object.values(values)).size).toBe(3);
  expect(Number(values.one)).toBeGreaterThan(Number(values.three));
});

test('protected reducer prevents insertion into a locked authoring group', () => {
  const draft = screen([{
    id: 'group',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    metadata: { 'engineering.authoring.locked': 'true' },
    children: []
  }]);

  expect(() => applyProtectedVisualEditorMutationIntent(draft, {
    kind: 'object.add', objectType: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, parentObjectId: 'group'
  })).toThrow(/locked for Engineering authoring/);
});
