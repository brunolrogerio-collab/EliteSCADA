import { expect, test } from '@playwright/test';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema
} from '../src/visual-runtime/builtinVisualObjectSchemas';
import { VISUAL_PROPERTY_KEYS } from '../src/visual-runtime/visualPropertyRegistry';
import { insertBezierAnchor, readEditableBezierPath, removeBezierAnchor } from '../src/engineering/visual-editor/bezierGeometry';
import {
  createObjectAddIntent,
  listVisualObjectPaletteItems
} from '../src/engineering/visual-editor/object-palette/objectPaletteModel';

test('palette is derived from the complete registered built-in set', () => {
  const items = listVisualObjectPaletteItems();

  expect(items.map(item => item.objectType)).toEqual([
    BUILTIN_VISUAL_OBJECT_TYPES.group,
    BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    BUILTIN_VISUAL_OBJECT_TYPES.ellipse,
    BUILTIN_VISUAL_OBJECT_TYPES.line,
    BUILTIN_VISUAL_OBJECT_TYPES.arc,
    BUILTIN_VISUAL_OBJECT_TYPES.bezier,
    BUILTIN_VISUAL_OBJECT_TYPES.polygon,
    BUILTIN_VISUAL_OBJECT_TYPES.text,
    BUILTIN_VISUAL_OBJECT_TYPES.image,
    BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol,
    BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay,
    BUILTIN_VISUAL_OBJECT_TYPES.trend,
    BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser,
    BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser,
    BUILTIN_VISUAL_OBJECT_TYPES.button,
    BUILTIN_VISUAL_OBJECT_TYPES.slider,
    BUILTIN_VISUAL_OBJECT_TYPES.numericInput
  ]);
  expect(new Set(items.map(item => item.objectType)).size).toBe(items.length);

  for (const item of items) {
    const schema = getBuiltinVisualObjectSchema(item.objectType);
    expect(item.propertyKeys).toEqual(schema.propertyKeys);
    expect(Object.isFrozen(item)).toBe(true);
    expect(Object.isFrozen(item.propertyKeys)).toBe(true);
  }
});

test('Image and SVG symbol palette entries consume the registered assetRef contract', () => {
  const items = listVisualObjectPaletteItems();
  const image = items.find(item => item.objectType === BUILTIN_VISUAL_OBJECT_TYPES.image);
  const svgSymbol = items.find(item => item.objectType === BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol);
  expect(image).toBeDefined();
  expect(image?.supportsAssetReference).toBe(true);
  expect(image?.propertyKeys).toContain(VISUAL_PROPERTY_KEYS.assetRef);
  expect(svgSymbol?.supportsAssetReference).toBe(true);
  expect(svgSymbol?.propertyKeys).toContain(VISUAL_PROPERTY_KEYS.assetRef);

  for (const item of items.filter(item => item.objectType !== BUILTIN_VISUAL_OBJECT_TYPES.image && item.objectType !== BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol)) {
    expect(item.supportsAssetReference).toBe(false);
  }
});

test('Trend palette entry is first-class content backed by the registered scalar schema', () => {
  const trend = listVisualObjectPaletteItems().find(item => item.objectType === BUILTIN_VISUAL_OBJECT_TYPES.trend);
  expect(trend).toBeDefined();
  expect(trend?.category).toBe('content');
  expect(trend?.propertyKeys).toContain(VISUAL_PROPERTY_KEYS.trendWindowSeconds);
  expect(trend?.propertyKeys).not.toContain('pens');
});

test('object add intent delegates defaults and identity to canonical coordinator composition', () => {
  const intent = createObjectAddIntent(BUILTIN_VISUAL_OBJECT_TYPES.rectangle, {
    parentObjectId: 'group-01',
    at: { x: 12.5, y: 40 }
  });

  expect(intent).toEqual({
    kind: 'object.add',
    objectType: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    parentObjectId: 'group-01',
    at: { x: 12.5, y: 40 }
  });
  expect('initialProperties' in intent).toBe(false);
  expect('id' in intent).toBe(false);
  expect('key' in intent).toBe(false);
  expect(Object.isFrozen(intent)).toBe(true);
});

test('Bezier is an editable first-class canvas shape with a path property', () => {
  const bezier = listVisualObjectPaletteItems().find(item => item.objectType === BUILTIN_VISUAL_OBJECT_TYPES.bezier);
  expect(bezier?.propertyKeys).toContain(VISUAL_PROPERTY_KEYS.bezierPath);
  expect(createObjectAddIntent(BUILTIN_VISUAL_OBJECT_TYPES.bezier)).toMatchObject({
    kind: 'object.add',
    objectType: BUILTIN_VISUAL_OBJECT_TYPES.bezier,
    initialProperties: {
      width: 120,
      height: 80,
      bezierPath: 'M 0 50 C 20 0 80 0 100 50 C 80 100 20 100 0 50 Z'
    }
  });
});

test('Bezier anchor insertion splits a cubic segment without changing its curve and removal is bounded', () => {
  const original = 'M 0 0 C 10 10 20 20 30 30';
  const split = insertBezierAnchor(original);
  expect(split).toBe('M 0 0 C 5 5 10 10 15 15 C 20 20 25 25 30 30');
  expect(removeBezierAnchor(split!)).toBe('M 0 0 C 5 5 10 10 15 15');
  expect(removeBezierAnchor(original)).toBeNull();
  expect(insertBezierAnchor('M 0 0 L 10 0')).toBe('M 0 0 L 5 0 L 10 0');
  expect(removeBezierAnchor('M 0 0 L 5 0 L 10 0')).toBe('M 0 0 L 5 0');
  expect(insertBezierAnchor('M 0 0 Q 10 20 20 0')).toBe('M 0 0 Q 5 10 10 10 Q 15 10 20 0');
  expect(insertBezierAnchor('M 0 0 C 10 10 20 20 30 30 Z')).toBe('M 0 0 C 5 5 10 10 15 15 C 20 20 25 25 30 30 Z');
  expect(insertBezierAnchor('M 0 0 c 10 10 20 20 30 30')).toBeNull();
  expect(readEditableBezierPath('M 0 0 C 10 10 20 20 30 30')?.anchorIndexes).toEqual([0, 3]);
});

test('palette fails closed for private/unknown object types and invalid placement data', () => {
  expect(() => createObjectAddIntent('renderer.private.svg-node')).toThrow(/Unknown built-in visual object type/);
  expect(() => createObjectAddIntent(BUILTIN_VISUAL_OBJECT_TYPES.text, { at: { x: Number.NaN, y: 1 } }))
    .toThrow(/coordinates must be finite/);
  expect(() => createObjectAddIntent(BUILTIN_VISUAL_OBJECT_TYPES.text, { parentObjectId: ' parent ' }))
    .toThrow(/stable non-empty identity/);
});
