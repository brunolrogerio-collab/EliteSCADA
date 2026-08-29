import { expect, test } from '@playwright/test';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime/builtinVisualObjectSchemas';
import { VISUAL_PROPERTY_KEYS } from '../src/visual-runtime/visualPropertyRegistry';
import type { VisualElementEngineering } from '../src/engineering/types';
import {
  bindingSourceIdentity,
  compatibleBindingSources,
  createBindingRemoveIntent,
  createBindingSetIntent,
  createTagBitBindingSource,
  filterBindingSourceCatalog,
  findBindingSourceForBinding,
  findVisualBinding,
  isBindingSourceCompatible,
  isBitSelectorAuthoringSource,
  listBindableVisualProperties,
  normalizeBindingSourceCatalog,
  resolveBindingSourceReference,
  VisualBindingEditorError
} from '../src/engineering/visual-editor/binding-editor/bindingEditorModel';

const rectangle: VisualElementEngineering = {
  id: '11111111-1111-1111-1111-111111111111',
  key: 'PumpBody',
  type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
  bindings: [{ key: VISUAL_PROPERTY_KEYS.fillColor, kind: 'tag', target: 'Plant.Pump.Color' }]
};

const catalog = [
  { kind: 'Tag', target: 'Plant.Pump.Running', label: 'Pump running', dataType: 'Boolean', writable: false },
  { kind: 'Tag', target: 'Plant.Pump.Color', label: 'Pump color', dataType: 'String', writable: false },
  { kind: 'Tag', target: 'Plant.Level', label: 'Level', dataType: 'Double', writable: false },
  { kind: 'Property', target: 'Context.SelectedPump', label: 'Selected pump' },
  { kind: 'Expression', target: 'tag("Plant.Level") > 80', label: 'High level expression' }
] as const;

const baseBitSource = {
  kind: 'Tag',
  target: 'Plant.Status',
  label: 'Status',
  dataType: 'Int16',
  writable: true,
  tagReference: {
    tagId: '33333333-3333-3333-3333-333333333333'
  },
  selectorCapability: {
    kind: 'bit',
    minIndex: 0,
    maxIndex: 15
  }
} as const;

const bitSource = createTagBitBindingSource(baseBitSource, 3);

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
    VISUAL_PROPERTY_KEYS.visible,
    catalog[0]
  );

  expect(intent).toEqual({
    kind: 'binding.set',
    objectId: rectangle.id,
    binding: {
      key: VISUAL_PROPERTY_KEYS.visible,
      kind: 'Tag',
      target: 'Plant.Pump.Running'
    }
  });
  expect('driver' in intent.binding).toBe(false);
  expect('address' in intent.binding).toBe(false);
  expect(Object.isFrozen(intent)).toBe(true);
  expect(Object.isFrozen(intent.binding)).toBe(true);
});

test('integer TAG is offered for Boolean authoring only through an explicit bit selector', () => {
  expect(isBindingSourceCompatible('boolean', baseBitSource)).toBe(false);
  expect(isBitSelectorAuthoringSource('boolean', baseBitSource)).toBe(true);
  expect(compatibleBindingSources('boolean', [...catalog, baseBitSource]).map(source => source.target)).toEqual([
    'Plant.Pump.Running',
    'Context.SelectedPump',
    'tag("Plant.Level") > 80',
    'Plant.Status'
  ]);
  expect(() => createBindingSetIntent(rectangle, VISUAL_PROPERTY_KEYS.visible, baseBitSource))
    .toThrow(/not compatible/);
});

test('on-demand bit source uses friendly .NN text while persisting stable TAG identity plus selector', () => {
  expect(bitSource).toMatchObject({
    target: 'Plant.Status.03',
    label: 'Status.03',
    dataType: 'Boolean',
    tagReference: {
      tagId: '33333333-3333-3333-3333-333333333333',
      selector: { kind: 'bit', index: 3 }
    }
  });

  const intent = createBindingSetIntent(rectangle, VISUAL_PROPERTY_KEYS.visible, bitSource);
  expect(intent.binding).toEqual({
    key: VISUAL_PROPERTY_KEYS.visible,
    kind: 'Tag',
    target: 'Plant.Status.03',
    tagReference: {
      tagId: '33333333-3333-3333-3333-333333333333',
      selector: { kind: 'bit', index: 3 }
    }
  });
  expect(Object.isFrozen(intent.binding.tagReference)).toBe(true);
  expect(Object.isFrozen(intent.binding.tagReference?.selector)).toBe(true);
  expect(findBindingSourceForBinding(intent.binding, [baseBitSource])).toMatchObject({ target: 'Plant.Status' });
});

test('exact reference authoring resolves non-padded bit notation without expanding the project tree', () => {
  const resolved = resolveBindingSourceReference([baseBitSource], 'Plant.Status.3');
  expect(resolved.status).toBe('found');
  expect(resolved.source).toMatchObject({
    target: 'Plant.Status.03',
    dataType: 'Boolean',
    tagReference: { selector: { kind: 'bit', index: 3 } }
  });
  expect(resolveBindingSourceReference([baseBitSource], 'Plant.Status.16')).toEqual({ status: 'notFound' });
});

test('stable TAG source identity ignores friendly path changes but includes the bit selector', () => {
  const renamed = { ...bitSource, target: 'Plant.RenamedStatus.03', label: 'Renamed status.03' };
  const otherBit = createTagBitBindingSource(baseBitSource, 4);

  expect(bindingSourceIdentity(bitSource)).toBe(bindingSourceIdentity(renamed));
  expect(bindingSourceIdentity(bitSource)).not.toBe(bindingSourceIdentity(otherBit));
  expect(normalizeBindingSourceCatalog([bitSource, renamed])).toHaveLength(1);
});

test('typed TAG compatibility fails early instead of deferring obvious errors to Runtime', () => {
  expect(isBindingSourceCompatible('boolean', catalog[0])).toBe(true);
  expect(isBindingSourceCompatible('boolean', catalog[2])).toBe(false);
  expect(isBindingSourceCompatible('number', catalog[2])).toBe(true);
  expect(isBindingSourceCompatible('number', catalog[0])).toBe(false);
  expect(isBindingSourceCompatible('color', catalog[1])).toBe(true);
  expect(isBindingSourceCompatible('boolean', bitSource)).toBe(true);
  expect(isBindingSourceCompatible('number', bitSource)).toBe(false);

  expect(compatibleBindingSources('boolean', catalog).map(source => source.target)).toEqual([
    'Plant.Pump.Running',
    'Context.SelectedPump',
    'tag("Plant.Level") > 80'
  ]);

  expect(() => createBindingSetIntent(rectangle, VISUAL_PROPERTY_KEYS.visible, catalog[2]))
    .toThrow(/not compatible/);
  expect(() => createBindingSetIntent(rectangle, VISUAL_PROPERTY_KEYS.x, catalog[0]))
    .toThrow(/not compatible/);
});

test('binding.remove is explicit and findVisualBinding resolves only the canonical property key', () => {
  expect(findVisualBinding(rectangle, VISUAL_PROPERTY_KEYS.fillColor)).toEqual({
    key: VISUAL_PROPERTY_KEYS.fillColor,
    kind: 'tag',
    target: 'Plant.Pump.Color'
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
  expect(() => createTagBitBindingSource(baseBitSource, 16)).toThrow(/between 0 and 15/);
});

test('source catalog accepts only canonical Tag/Property/Expression references and deduplicates identities', () => {
  const normalized = normalizeBindingSourceCatalog([
    ...catalog,
    { ...catalog[0], label: 'Duplicate label does not create a second source' }
  ]);

  expect(normalized).toHaveLength(5);
  expect(normalized.map(item => item.kind)).toEqual(['Tag', 'Tag', 'Tag', 'Property', 'Expression']);
  expect(Object.isFrozen(normalized)).toBe(true);

  expect(() => normalizeBindingSourceCatalog([
    { kind: 'Driver' as never, target: 'modbus://holding/1', label: 'Direct driver browse' }
  ])).toThrow(/not canonical/);

  expect(() => normalizeBindingSourceCatalog([
    { kind: 'Tag', target: ' Plant.Level ', label: 'Bad target' }
  ])).toThrow(/stable non-empty reference/);

  expect(() => normalizeBindingSourceCatalog([
    { ...bitSource, tagReference: { tagId: bitSource.tagReference!.tagId, selector: { kind: 'bit', index: -1 } } }
  ])).toThrow(/non-negative integer bit selector/);
});

test('source catalog filtering stays within coordinator-provided canonical entries', () => {
  expect(filterBindingSourceCatalog(catalog, 'level')).toEqual([
    { ...catalog[2], bindable: true },
    { ...catalog[4], bindable: true }
  ]);
  expect(filterBindingSourceCatalog(catalog, 'boolean')).toEqual([
    { ...catalog[0], bindable: true }
  ]);
  expect(filterBindingSourceCatalog(catalog, '')).toHaveLength(5);
});
