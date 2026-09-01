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

const levelSource = {
  kind: 'Tag', valueType: 'Number', target: 'Plant.Level',
  tagReference: { tagId: '22222222-2222-2222-2222-222222222222' }, version: 1
} as const;

const boolSource = {
  kind: 'Tag', valueType: 'Boolean', target: 'Plant.Running',
  tagReference: { tagId: '11111111-1111-1111-1111-111111111111' }, version: 1
} as const;

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

test('Dynamo library insertion creates a reusable instance with equipment context and canonical bounds', () => {
  const original = screen([]);
  const next = applyVisualEditorMutationIntent(original, {
    kind: 'dynamo.add',
    dynamoKey: 'process.motor.standard',
    equipmentPath: ' Plant.M01 ',
    at: { x: 80, y: 96 },
    defaultWidth: 106,
    defaultHeight: 92
  }, { createObjectId: idGenerator('dynamo-instance-1') });

  expect(next.elements).toHaveLength(1);
  expect(next.elements?.[0]).toMatchObject({
    id: 'dynamo-instance-1',
    key: 'standard',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    dynamoKey: 'process.motor.standard',
    equipmentPath: 'Plant.M01',
    properties: { x: 80, y: 96, width: 106, height: 92 }
  });
  expect(original.elements).toEqual([]);

  expect(() => applyVisualEditorMutationIntent(original, {
    kind: 'dynamo.add', dynamoKey: 'process.motor.standard', defaultWidth: 0
  }, { createObjectId: idGenerator('invalid-dynamo') })).toThrow(/positive finite/);
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

test('FOLLOW-B expression and condition set/remove are immutable, deterministic and type-checked', () => {
  const base = screen([{ id: 'rect-1', key: 'rect', type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, properties: {} }]);
  const expressionA = {
    propertyKey: 'visible',
    expression: {
      text: 'running', resultType: 'Boolean',
      dependencies: [{ symbol: 'running', kind: 'Tag', valueType: 'Boolean', tagReference: boolSource.tagReference, target: boolSource.target, version: 1 }],
      version: 1
    }, version: 1
  } as const;
  const expressionB = { ...expressionA, expression: { ...expressionA.expression, text: 'not running' } } as const;

  const withExpression = applyVisualEditorMutationIntent(base, {
    kind: 'propertyExpression.set', objectId: 'rect-1', configuration: expressionA
  });
  const replacedExpression = applyVisualEditorMutationIntent(withExpression, {
    kind: 'propertyExpression.set', objectId: 'rect-1', configuration: expressionB
  });
  expect(withExpression).not.toBe(base);
  expect(base.elements?.[0].propertyExpressions).toBeUndefined();
  expect(replacedExpression.elements?.[0].propertyExpressions).toHaveLength(1);
  expect(replacedExpression.elements?.[0].propertyExpressions?.[0].expression.text).toBe('not running');

  const condition = {
    propertyKey: 'visible', kind: 'NumericInterval', source: levelSource,
    minimum: 20, maximum: 80, minimumInclusive: true, maximumInclusive: true,
    intervalMode: 'Inside', negate: false, version: 1
  } as const;
  const withCondition = applyVisualEditorMutationIntent(replacedExpression, {
    kind: 'booleanCondition.set', objectId: 'rect-1', configuration: condition
  });
  expect(withCondition.elements?.[0].booleanConditions).toHaveLength(1);

  const removed = applyVisualEditorMutationIntent(
    applyVisualEditorMutationIntent(withCondition, {
      kind: 'propertyExpression.remove', objectId: 'rect-1', propertyKey: 'visible'
    }),
    { kind: 'booleanCondition.remove', objectId: 'rect-1', propertyKey: 'visible' }
  );
  expect(removed.elements?.[0].propertyExpressions).toEqual([]);
  expect(removed.elements?.[0].booleanConditions).toEqual([]);
  expect(applyVisualEditorMutationIntent(removed, {
    kind: 'booleanCondition.remove', objectId: 'rect-1', propertyKey: 'visible'
  }).elements?.[0].booleanConditions).toEqual([]);

  expect(() => applyVisualEditorMutationIntent(base, {
    kind: 'propertyExpression.set', objectId: 'rect-1',
    configuration: { ...expressionA, propertyKey: 'x' }
  })).toThrow(/incompatible/);
});

test('FOLLOW-B Analog Fill uses shared eligibility and supports idempotent removal', () => {
  const base = screen([
    { id: 'rect-1', key: 'rect', type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle, properties: {} },
    { id: 'text-1', key: 'text', type: BUILTIN_VISUAL_OBJECT_TYPES.text, properties: {} }
  ]);
  const fill = {
    source: levelSource, inputMinimum: 0, inputMaximum: 100,
    fillColor: '#00AAFF', clamp: true, invertScale: false, direction: 'BottomToTop', version: 1
  } as const;

  const withFill = applyVisualEditorMutationIntent(base, {
    kind: 'analogFill.set', objectId: 'rect-1', configuration: fill
  });
  expect(withFill.elements?.[0].analogFill).toEqual(fill);
  const removed = applyVisualEditorMutationIntent(withFill, { kind: 'analogFill.remove', objectId: 'rect-1' });
  expect(removed.elements?.[0].analogFill).toBeNull();
  expect(applyVisualEditorMutationIntent(removed, { kind: 'analogFill.remove', objectId: 'rect-1' }).elements?.[0].analogFill).toBeNull();

  expect(() => applyVisualEditorMutationIntent(base, {
    kind: 'analogFill.set', objectId: 'text-1', configuration: fill
  })).toThrow(/does not support Analog Fill/);
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
