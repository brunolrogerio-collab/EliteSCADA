import { expect, test } from '@playwright/test';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema
} from '../src/visual-runtime/builtinVisualObjectSchemas';
import { VISUAL_PROPERTY_KEYS } from '../src/visual-runtime/visualPropertyRegistry';
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
    BUILTIN_VISUAL_OBJECT_TYPES.polygon,
    BUILTIN_VISUAL_OBJECT_TYPES.text,
    BUILTIN_VISUAL_OBJECT_TYPES.image,
    BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay,
    BUILTIN_VISUAL_OBJECT_TYPES.trend,
    BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser,
    BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser,
    BUILTIN_VISUAL_OBJECT_TYPES.button,
    BUILTIN_VISUAL_OBJECT_TYPES.slider
  ]);
  expect(new Set(items.map(item => item.objectType)).size).toBe(items.length);

  for (const item of items) {
    const schema = getBuiltinVisualObjectSchema(item.objectType);
    expect(item.propertyKeys).toEqual(schema.propertyKeys);
    expect(Object.isFrozen(item)).toBe(true);
    expect(Object.isFrozen(item.propertyKeys)).toBe(true);
  }
});

test('Image palette entry consumes the registered assetRef contract without inventing asset persistence', () => {
  const items = listVisualObjectPaletteItems();
  const image = items.find(item => item.objectType === BUILTIN_VISUAL_OBJECT_TYPES.image);
  expect(image).toBeDefined();
  expect(image?.supportsAssetReference).toBe(true);
  expect(image?.propertyKeys).toContain(VISUAL_PROPERTY_KEYS.assetRef);

  for (const item of items.filter(item => item.objectType !== BUILTIN_VISUAL_OBJECT_TYPES.image)) {
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

test('palette fails closed for private/unknown object types and invalid placement data', () => {
  expect(() => createObjectAddIntent('renderer.private.svg-node')).toThrow(/Unknown built-in visual object type/);
  expect(() => createObjectAddIntent(BUILTIN_VISUAL_OBJECT_TYPES.text, { at: { x: Number.NaN, y: 1 } }))
    .toThrow(/coordinates must be finite/);
  expect(() => createObjectAddIntent(BUILTIN_VISUAL_OBJECT_TYPES.text, { parentObjectId: ' parent ' }))
    .toThrow(/stable non-empty identity/);
});