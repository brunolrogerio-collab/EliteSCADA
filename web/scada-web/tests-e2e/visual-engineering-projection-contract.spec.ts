import { expect, test } from '@playwright/test';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  RuntimeVisualInstance,
  VISUAL_PROPERTY_KEYS,
  VisualObjectPropertySchema,
  VisualPropertyRegistry,
  projectVisualEngineeringDefinition,
  type VisualPropertyDefinition
} from '../src/visual-runtime';

test('Engineering projection preserves only explicit validated base values while registry defaults stay separate', () => {
  const schema = new VisualObjectPropertySchema('basic.rectangle', [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.y,
    VISUAL_PROPERTY_KEYS.width,
    VISUAL_PROPERTY_KEYS.height,
    VISUAL_PROPERTY_KEYS.visible,
    VISUAL_PROPERTY_KEYS.opacity,
    VISUAL_PROPERTY_KEYS.fillColor,
    VISUAL_PROPERTY_KEYS.text
  ]);

  const projection = projectVisualEngineeringDefinition({
    objectId: 'object:rect-1',
    key: 'pumpStatus',
    objectType: 'basic.rectangle',
    baseProperties: {
      x: 12.5,
      y: 40,
      opacity: 0.65,
      fillColor: '#336699CC',
      text: 'Pump A'
    },
    bindings: [
      { propertyKey: 'visible', sourceKind: 'binding', sourceReference: 'tag:pump-running' }
    ],
    scriptEventReferences: [
      { eventKey: 'click', scriptId: 'script:open-pump', entryPoint: 'on_click' }
    ],
    metadata: {
      area: 'North'
    }
  }, schema);

  expect(projection.objectId).toBe('object:rect-1');
  expect(projection.objectType).toBe('basic.rectangle');
  expect({ ...projection.baseProperties }).toEqual({
    x: 12.5,
    y: 40,
    opacity: 0.65,
    fillColor: '#336699CC',
    text: 'Pump A'
  });
  expect(schema.createDefaultValues()).toMatchObject({
    width: 100,
    height: 100,
    visible: true
  });

  const runtime = new RuntimeVisualInstance({
    definition: projection,
    schema,
    runtimeInstanceId: 'runtime:rect-1'
  });
  expect(runtime.readEffective('x')).toEqual({ propertyKey: 'x', value: 12.5, source: 'engineering' });
  expect(runtime.readEffective('width')).toEqual({ propertyKey: 'width', value: 100, source: 'default' });
  expect(runtime.readEffective('visible')).toEqual({ propertyKey: 'visible', value: true, source: 'default' });

  expect(projection.bindings).toEqual([
    { propertyKey: 'visible', sourceKind: 'binding', sourceReference: 'tag:pump-running' }
  ]);
  expect(projection.scriptEventReferences).toEqual([
    { eventKey: 'click', scriptId: 'script:open-pump', entryPoint: 'on_click' }
  ]);
  expect(projection.metadata).toEqual({ area: 'North' });
});

test('AssetReference is copied into immutable Engineering base projection and carries only stable identity', () => {
  const schema = new VisualObjectPropertySchema('basic.image', [
    VISUAL_PROPERTY_KEYS.assetRef,
    VISUAL_PROPERTY_KEYS.imageFit,
    VISUAL_PROPERTY_KEYS.opacity
  ]);
  const asset = { assetId: 'asset:logo-1' };

  const projection = projectVisualEngineeringDefinition({
    objectId: 'object:image-1',
    key: 'companyLogo',
    objectType: 'basic.image',
    baseProperties: {
      assetRef: asset,
      imageFit: 'native'
    }
  }, schema);

  asset.assetId = 'asset:changed-outside';
  expect(projection.baseProperties.assetRef).toEqual({
    assetId: 'asset:logo-1'
  });
  expect(Object.isFrozen(projection.baseProperties)).toBeTruthy();
  expect(Object.isFrozen(projection.baseProperties.assetRef)).toBeTruthy();

  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:image-2',
    key: 'unsafeLogo',
    objectType: 'basic.image',
    baseProperties: {
      assetRef: { assetId: '/tmp/logo.png' }
    }
  }, schema)).toThrow(/invalid/);

  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:image-3',
    key: 'metadataLogo',
    objectType: 'basic.image',
    baseProperties: {
      assetRef: { assetId: 'asset:logo-2', name: 'Duplicated asset metadata' }
    }
  }, schema)).toThrow(/invalid/);
});

test('projection rejects undeclared properties, unsupported bindings and duplicate writers', () => {
  const schema = new VisualObjectPropertySchema('basic.image', [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.assetRef
  ]);

  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:image-3',
    key: 'image3',
    objectType: 'basic.image',
    baseProperties: { opacity: 0.5 }
  }, schema)).toThrow(/does not declare 'opacity'/);

  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:image-4',
    key: 'image4',
    objectType: 'basic.image',
    bindings: [
      { propertyKey: 'assetRef', sourceKind: 'binding', sourceReference: 'asset-selector' }
    ]
  }, schema)).toThrow(/does not support binding/);

  const xOnly = new VisualObjectPropertySchema('basic.x-only', [VISUAL_PROPERTY_KEYS.x]);
  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:x-1',
    key: 'x1',
    objectType: 'basic.x-only',
    bindings: [
      { propertyKey: 'x', sourceKind: 'binding', sourceReference: 'tag:x' },
      { propertyKey: 'x', sourceKind: 'expression', sourceReference: 'expression:x' }
    ]
  }, xOnly)).toThrow(/more than one definition-level binding/);
});

test('projection respects Engineering editability without silently materializing registry defaults', () => {
  const lockedDefinition = {
    key: 'internalValue',
    type: 'number',
    defaultValue: 1,
    engineeringEditable: false,
    runtimeReadable: true,
    runtimeWritable: false,
    supportsBinding: false,
    animatable: false
  } satisfies VisualPropertyDefinition;
  const registry = new VisualPropertyRegistry([lockedDefinition]);
  const schema = new VisualObjectPropertySchema('internal.object', ['internalValue'], registry);

  const defaults = projectVisualEngineeringDefinition({
    objectId: 'object:internal-1',
    key: 'internal1',
    objectType: 'internal.object'
  }, schema);
  expect({ ...defaults.baseProperties }).toEqual({});
  expect(schema.createDefaultValues().internalValue).toBe(1);

  const runtime = new RuntimeVisualInstance({
    definition: defaults,
    schema,
    runtimeInstanceId: 'runtime:internal-1'
  });
  expect(runtime.readEffective('internalValue')).toEqual({
    propertyKey: 'internalValue',
    value: 1,
    source: 'default'
  });

  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:internal-2',
    key: 'internal2',
    objectType: 'internal.object',
    baseProperties: { internalValue: 2 }
  }, schema)).toThrow(/not editable in Engineering/);
});

test('projection validates identity and Script event references without renderer or DOM handles', () => {
  const schema = new VisualObjectPropertySchema('basic.rectangle', [VISUAL_PROPERTY_KEYS.visible]);

  expect(() => projectVisualEngineeringDefinition({
    objectId: ' ',
    key: 'bad',
    objectType: 'basic.rectangle'
  }, schema)).toThrow(/object ID/);

  expect(() => projectVisualEngineeringDefinition({
    objectId: 'object:rect-2',
    key: 'rect2',
    objectType: 'basic.rectangle',
    scriptEventReferences: [
      { eventKey: 'click', scriptId: 'script:1', entryPoint: 'not valid' }
    ]
  }, schema)).toThrow(/Python identifier/);

  expect(COMMON_VISUAL_PROPERTY_REGISTRY.has('domNode')).toBeFalsy();
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.has('renderer')).toBeFalsy();
});
