import { expect, test } from '@playwright/test';
import type { ScreenEngineering } from '../src/engineering/types';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../src/visual-runtime';
import { applyVisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorCanonicalModel';

function screen(elements: ScreenEngineering['elements']): ScreenEngineering {
  return { key: 'screen', name: 'Screen', route: '/screen', elements };
}

function idGenerator(...ids: string[]): () => string {
  let index = 0;
  return () => {
    const value = ids[index];
    if (!value) throw new Error('test identity generator exhausted');
    index += 1;
    return value;
  };
}

test('canonical reducer adds registered objects without materializing unrelated defaults', () => {
  const original = screen([]);
  const next = applyVisualEditorMutationIntent(original, {
    kind: 'object.add',
    objectType: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    at: { x: 24, y: 36 },
    initialProperties: { fillColor: '#112233' }
  }, { createObjectId: idGenerator('object-1') });

  expect(original.elements).toEqual([]);
  expect(next.elements).toHaveLength(1);
  expect(next.elements?.[0]).toMatchObject({
    id: 'object-1',
    key: 'rectangle',
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x: 24, y: 36, fillColor: '#112233' }
  });
  expect(next.elements?.[0].properties?.width).toBeUndefined();
  expect(next.elements?.[0].properties?.height).toBeUndefined();

  expect(() => applyVisualEditorMutationIntent(original, {
    kind: 'object.add', objectType: 'core.unknown'
  }, { createObjectId: idGenerator('object-2') })).toThrow(/Unknown built-in visual object type/);
});

test('geometry intents resolve registry defaults only when an interaction changes them', () => {
  const original = screen([{ id: 'rect-1', key: 'rect', type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, properties: {} }]);

  const moved = applyVisualEditorMutationIntent(original, {
    kind: 'object.move', objectIds: ['rect-1'], delta: { x: 7, y: 9 }
  });
  expect(moved.elements?.[0].properties).toMatchObject({ x: 7, y: 9 });
  expect(moved.elements?.[0].properties?.width).toBeUndefined();

  const resized = applyVisualEditorMutationIntent(moved, {
    kind: 'object.resize', objectId: 'rect-1', bounds: { x: 10, y: 12, width: 180, height: 90 }
  });
  expect(resized.elements?.[0].properties).toMatchObject({ x: 10, y: 12, width: 180, height: 90 });

  const rotated = applyVisualEditorMutationIntent(resized, {
    kind: 'object.rotate', objectIds: ['rect-1'], deltaDegrees: 45
  });
  expect(rotated.elements?.[0].properties?.rotation).toBe(45);
  expect(original.elements?.[0].properties).toEqual({});
});

test('property and binding intents stay behind the shared schema authority', () => {
  const base = screen([
    { id: 'text-1', key: 'label', type: BUILTIN_VISUAL_OBJECT_TYPES.text, properties: {} },
    { id: 'image-1', key: 'image', type: BUILTIN_VISUAL_OBJECT_TYPES.image, properties: {} }
  ]);

  const withText = applyVisualEditorMutationIntent(base, {
    kind: 'property.set', objectIds: ['text-1'], propertyKey: VISUAL_PROPERTY_KEYS.text, value: 'Pump P01'
  });
  expect(withText.elements?.[0].properties?.text).toBe('Pump P01');

  const withBinding = applyVisualEditorMutationIntent(withText, {
    kind: 'binding.set',
    objectId: 'text-1',
    binding: { key: VISUAL_PROPERTY_KEYS.text, kind: 'tag', target: 'Demo.P01.Status' }
  });
  expect(withBinding.elements?.[0].bindings).toEqual([
    { key: VISUAL_PROPERTY_KEYS.text, kind: 'tag', target: 'Demo.P01.Status' }
  ]);

  const withoutText = applyVisualEditorMutationIntent(withBinding, {
    kind: 'property.remove', objectIds: ['text-1'], propertyKey: VISUAL_PROPERTY_KEYS.text
  });
  expect(withoutText.elements?.[0].properties?.text).toBeUndefined();
  expect(withoutText.elements?.[0].bindings).toHaveLength(1);

  expect(() => applyVisualEditorMutationIntent(base, {
    kind: 'binding.set',
    objectId: 'image-1',
    binding: { key: VISUAL_PROPERTY_KEYS.assetRef, kind: 'tag', target: 'Demo.Image' }
  })).toThrow(/does not support canonical binding/);

  expect(() => applyVisualEditorMutationIntent(base, {
    kind: 'property.set', objectIds: ['text-1'], propertyKey: VISUAL_PROPERTY_KEYS.assetRef, value: null
  })).toThrow(/does not declare|not registered/);
});

test('duplicate and delete preserve hierarchy while minting new canonical identities centrally', () => {
  const original = screen([{
    id: 'group-1',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 20, y: 30 },
    children: [{ id: 'child-1', key: 'child', type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, properties: { x: 5, y: 6 } }]
  }]);

  const duplicated = applyVisualEditorMutationIntent(original, {
    kind: 'object.duplicate', objectIds: ['group-1', 'child-1']
  }, { createObjectId: idGenerator('group-2', 'child-2'), duplicateOffset: 10 });

  expect(duplicated.elements).toHaveLength(2);
  expect(duplicated.elements?.[0].id).toBe('group-1');
  expect(duplicated.elements?.[1]).toMatchObject({
    id: 'group-2',
    key: 'group-copy',
    properties: { x: 30, y: 40 }
  });
  expect(duplicated.elements?.[1].children).toHaveLength(1);
  expect(duplicated.elements?.[1].children?.[0]).toMatchObject({ id: 'child-2', key: 'child-copy', properties: { x: 5, y: 6 } });
  expect(original.elements?.[0].properties).toEqual({ x: 20, y: 30 });

  const deleted = applyVisualEditorMutationIntent(duplicated, {
    kind: 'object.delete', objectIds: ['child-1']
  });
  expect(deleted.elements?.[0].children).toEqual([]);
  expect(deleted.elements?.[1].children).toHaveLength(1);
});

test('z-order operations persist only the explicit zIndex interaction result', () => {
  const base = screen([
    { id: 'a', key: 'a', type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, properties: {} },
    { id: 'b', key: 'b', type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, properties: { zIndex: 4 } }
  ]);

  const front = applyVisualEditorMutationIntent(base, {
    kind: 'object.zOrder', objectIds: ['a'], operation: 'bringToFront'
  });
  expect(front.elements?.[0].properties?.zIndex).toBe(5);
  expect(front.elements?.[1].properties?.zIndex).toBe(4);
  expect(base.elements?.[0].properties?.zIndex).toBeUndefined();
});
