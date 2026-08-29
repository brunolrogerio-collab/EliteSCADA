import { expect, test } from '@playwright/test';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime/builtinVisualObjectSchemas';
import { VISUAL_PROPERTY_KEYS } from '../src/visual-runtime/visualPropertyRegistry';
import type { VisualElementEngineering } from '../src/engineering/types';
import {
  createBindingRemoveIntent,
  createBindingSetIntent,
  filterBindingSourceCatalog,
  findVisualBinding,
  listBindableVisualProperties,
  normalizeBindingSourceCatalog,
  VisualBindingEditorError
} from '../src/engineering/visual-editor/binding-editor/bindingEditorModel';

const rectangle: VisualElementEngineering = {
  id: '11111111-1111-1111-1111-111111111111',
  key: 'PumpBody',
  type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
  bindings: [{ key: VISUAL_PROPERTY_KEYS.fillColor, kind: 'Tag', target: 'Plant.Pump.Running' }]
};

const catalog = [
  { kind: 'Tag', target: 'Plant.Pump.Running', label: 'Pump running', dataType: 'Boolean', writable: false },
  { kind: 'Property', target: 'Context.SelectedPump', label: 'Selected pump' },
  { kind: 'Expression', target: 'tag("Plant.Level") > 80', label: 'High level expression' }
] as const;

test('bindable destinations come only from the registered schema supportsBinding contract', () => {
  const destinations = listBindableVisualProperties(rectangle);
  expect(destinations.length).toBeGreaterThan(0);
  expect(destinations.map(item => item.key)).toContain(VISUAL_PROPERTY_KEYS.fillColor);

  const imageDestinations = listBindableVisualProperties({ type: BUILTIN_VISUAL_OBJECT_TYPES.image });
  expect(imageDestinations.map(item => item.key)).not.toContain(VISUAL_PROPERTY_KEYS.assetRef);
  expect(imageDestinations.map(item => item.key)).toContain(VISUAL_PROPERTY_KEYS.visible);
});

test('binding.set uses canonical destination/source fields and stable object identity', () => {
  const intent = createBindingSetIntent(
    rectangle,
    VISUAL_PROPERTY_KEYS.fillColor,
    catalog[0]
  );

  expect(intent).toEqual({
    kind: 'binding.set',
    objectId: rectangle.id,
    binding: {
      key: VISUAL_PROPERTY_KEYS.fillColor,
      kind: 'Tag',
      target: 'Plant.Pump.Running'
    }
  });
  expect('driver' in intent.binding).toBe(false);
  expect('address' in intent.binding).toBe(false);
  expect(Object.isFrozen(intent)).toBe(true);
  expect(Object.isFrozen(intent.binding)).toBe(true);
});

test('binding.remove is explicit and findVisualBinding resolves only the canonical property key', () => {
  expect(findVisualBinding(rectangle, VISUAL_PROPERTY_KEYS.fillColor)).toEqual({
    key: VISUAL_PROPERTY_KEYS.fillColor,
    kind: 'Tag',
    target: 'Plant.Pump.Running'
  });

  expect(createBindingRemoveIntent(rectangle, VISUAL_PROPERTY_KEYS.fillColor)).toEqual({
    kind: 'binding.remove',
    objectId: rectangle.id,
    propertyKey: VISUAL_PROPERTY_KEYS.fillColor
  });
});

test('binding authoring fails closed for unregistered/non-bindable destinations and missing object identity', () => {
  const image: VisualElementEngineering = {
    id: '22222222-2222-2222-2222-222222222222',
    key: 'Logo',
    type: BUILTIN_VISUAL_OBJECT_TYPES.image
  };

  expect(() => createBindingSetIntent(image, VISUAL_PROPERTY_KEYS.assetRef, catalog[0]))
    .toThrow(VisualBindingEditorError);
  expect(() => createBindingSetIntent(rectangle, 'rendererPrivateHandle', catalog[0]))
    .toThrow(/not declared/);
  expect(() => createBindingSetIntent({ ...rectangle, id: null }, VISUAL_PROPERTY_KEYS.visible, catalog[0]))
    .toThrow(/stable visual object ID/);
});

test('source catalog accepts only canonical Tag/Property/Expression references and deduplicates identities', () => {
  const normalized = normalizeBindingSourceCatalog([
    ...catalog,
    { ...catalog[0], label: 'Duplicate label does not create a second source' }
  ]);

  expect(normalized).toHaveLength(3);
  expect(normalized.map(item => item.kind)).toEqual(['Tag', 'Property', 'Expression']);
  expect(Object.isFrozen(normalized)).toBe(true);

  expect(() => normalizeBindingSourceCatalog([
    { kind: 'Driver' as never, target: 'modbus://holding/1', label: 'Direct driver browse' }
  ])).toThrow(/not canonical/);

  expect(() => normalizeBindingSourceCatalog([
    { kind: 'Tag', target: ' Plant.Level ', label: 'Bad target' }
  ])).toThrow(/stable non-empty reference/);
});

test('source catalog filtering stays within coordinator-provided canonical entries', () => {
  expect(filterBindingSourceCatalog(catalog, 'level')).toEqual([
    catalog[2]
  ]);
  expect(filterBindingSourceCatalog(catalog, 'boolean')).toEqual([
    catalog[0]
  ]);
  expect(filterBindingSourceCatalog(catalog, '')).toHaveLength(3);
});
