import { expect, test } from '@playwright/test';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  IMAGE_FIT_VALUES,
  VISUAL_PROPERTY_KEYS,
  VisualObjectPropertySchema,
  VisualPropertyRegistry,
  type VisualPropertyDefinition
} from '../src/visual-runtime';

const expectedCommonKeys = [
  'x',
  'y',
  'width',
  'height',
  'rotation',
  'scaleX',
  'scaleY',
  'zIndex',
  'visible',
  'opacity',
  'fillColor',
  'strokeColor',
  'strokeWidth',
  'cornerRadius',
  'text',
  'textColor',
  'fontSize',
  'assetRef',
  'imageFit'
];

test('common registry exposes the locked Wave 07 property family and image fit enum', () => {
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.list().map(definition => definition.key)).toEqual(expectedCommonKeys);
  expect(IMAGE_FIT_VALUES).toEqual(['contain', 'cover', 'fill', 'native']);

  const assetDefinition = COMMON_VISUAL_PROPERTY_REGISTRY.getRequired(VISUAL_PROPERTY_KEYS.assetRef);
  expect(assetDefinition.type).toBe('assetRef');
  expect(assetDefinition.runtimeReadable).toBeTruthy();
  expect(assetDefinition.runtimeWritable).toBeFalsy();
  expect(assetDefinition.supportsBinding).toBeFalsy();
});

test('numeric constraints reject non-finite, fractional integer-only and out-of-range values without coercion', () => {
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.opacity, 0.5))
    .toMatchObject({ ok: true, value: 0.5 });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.opacity, Number.NaN))
    .toMatchObject({ ok: false, code: 'number.nonFinite' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.opacity, Number.POSITIVE_INFINITY))
    .toMatchObject({ ok: false, code: 'number.nonFinite' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.opacity, 1.01))
    .toMatchObject({ ok: false, code: 'number.maximum' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.width, -1))
    .toMatchObject({ ok: false, code: 'number.minimum' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.width, '100'))
    .toMatchObject({ ok: false, code: 'value.type' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, 3))
    .toMatchObject({ ok: true, value: 3 });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, 3.5))
    .toMatchObject({ ok: false, code: 'number.integer' });
});

test('color, enum and AssetReference values fail closed', () => {
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.fillColor, '#12A0CC80'))
    .toMatchObject({ ok: true });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.fillColor, 'rgba(1,2,3,.5)'))
    .toMatchObject({ ok: false, code: 'color.format' });

  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.imageFit, 'native'))
    .toMatchObject({ ok: true, value: 'native' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.imageFit, 'none'))
    .toMatchObject({ ok: false, code: 'enum.value' });

  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.assetRef, {
    assetId: 'asset:plant-logo',
    name: 'Plant logo',
    mediaType: 'image/png'
  })).toMatchObject({ ok: true });

  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.assetRef, {
    assetId: '550e8400-e29b-41d4-a716-446655440000',
    mediaType: 'image/jpeg'
  })).toMatchObject({ ok: true });

  const invalidAssets = [
    { assetId: 'https://example.invalid/logo.png' },
    { assetId: 'http:example.invalid' },
    { assetId: 'file:logo.png' },
    { assetId: 'C:\\plant\\logo.png' },
    { assetId: 'asset:plant-logo', url: 'https://example.invalid/logo.png' },
    { assetId: 'asset:plant-logo', mediaType: 'text/html' },
    { assetId: 'asset:plant-logo', name: ' Plant logo ' }
  ];

  for (const candidate of invalidAssets) {
    expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.assetRef, candidate))
      .toMatchObject({ ok: false, code: 'assetRef.shape' });
  }
});

test('object schema selects registered properties and defaults without creating an arbitrary property bag', () => {
  const schema = new VisualObjectPropertySchema('basic.image', [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.y,
    VISUAL_PROPERTY_KEYS.width,
    VISUAL_PROPERTY_KEYS.height,
    VISUAL_PROPERTY_KEYS.assetRef,
    VISUAL_PROPERTY_KEYS.imageFit
  ]);

  expect(schema.propertyKeys).toEqual(['x', 'y', 'width', 'height', 'assetRef', 'imageFit']);
  expect(schema.declares('opacity')).toBeFalsy();
  expect(schema.createDefaultBaseValues()).toEqual({
    x: 0,
    y: 0,
    width: 100,
    height: 100,
    assetRef: { assetId: 'asset:none' },
    imageFit: 'contain'
  });
  expect(schema.validate('opacity', 1)).toMatchObject({ ok: false, code: 'property.unregistered' });
});

test('registry rejects invalid definitions instead of accepting ambiguous semantics', () => {
  const invalidEnum = {
    key: 'mode',
    type: 'enum',
    defaultValue: 'missing',
    allowedValues: ['a', 'b'],
    engineeringEditable: true,
    runtimeReadable: true,
    runtimeWritable: true,
    supportsBinding: true,
    animatable: false
  } satisfies VisualPropertyDefinition;

  expect(() => new VisualPropertyRegistry([invalidEnum])).toThrow(/Default value/);

  const invalidIntegerDefault = {
    key: 'stack',
    type: 'number',
    defaultValue: 1.5,
    integer: true,
    engineeringEditable: true,
    runtimeReadable: true,
    runtimeWritable: true,
    supportsBinding: true,
    animatable: false
  } satisfies VisualPropertyDefinition;

  expect(() => new VisualPropertyRegistry([invalidIntegerDefault])).toThrow(/Default value/);

  const duplicate = {
    key: 'enabled',
    type: 'boolean',
    defaultValue: true,
    engineeringEditable: true,
    runtimeReadable: true,
    runtimeWritable: true,
    supportsBinding: true,
    animatable: false
  } satisfies VisualPropertyDefinition;

  expect(() => new VisualPropertyRegistry([duplicate, duplicate])).toThrow(/already registered/);
});
