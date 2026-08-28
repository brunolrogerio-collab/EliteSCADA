import { expect, test } from '@playwright/test';
import {
  decodeLegacyVisualEngineeringProperties,
  encodeLegacyVisualEngineeringProperties,
  VISUAL_PROPERTY_KEYS,
  VisualObjectPropertySchema,
  VisualPropertyContractError
} from '../src/visual-runtime';

function createSchema() {
  return new VisualObjectPropertySchema('basic.image', [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.zIndex,
    VISUAL_PROPERTY_KEYS.visible,
    VISUAL_PROPERTY_KEYS.fillColor,
    VISUAL_PROPERTY_KEYS.assetRef,
    VISUAL_PROPERTY_KEYS.imageFit
  ]);
}

test('legacy visual property codec decodes only according to the declared schema', () => {
  const decoded = decodeLegacyVisualEngineeringProperties({
    x: '12.5',
    zIndex: '3',
    visible: 'false',
    fillColor: '#11223344',
    assetRef: 'asset:plant-logo',
    imageFit: 'native'
  }, createSchema());

  expect(decoded).toEqual({
    x: 12.5,
    zIndex: 3,
    visible: false,
    fillColor: '#11223344',
    assetRef: { assetId: 'asset:plant-logo' },
    imageFit: 'native'
  });
});

test('legacy visual property codec encodes typed values canonically and omits null assetRef', () => {
  const encoded = encodeLegacyVisualEngineeringProperties({
    x: 12.5,
    zIndex: 3,
    visible: false,
    assetRef: null
  }, createSchema());

  expect(encoded).toEqual({
    x: '12.5',
    zIndex: '3',
    visible: 'false'
  });
});

test('legacy codec rejects guessed or undeclared values instead of coercing them', () => {
  for (const visible of ['TRUE', 'False', '1']) {
    expect(() => decodeLegacyVisualEngineeringProperties({ visible }, createSchema()))
      .toThrow(VisualPropertyContractError);
  }

  expect(() => decodeLegacyVisualEngineeringProperties({ x: ' 12.5 ' }, createSchema()))
    .toThrow(VisualPropertyContractError);
  expect(() => decodeLegacyVisualEngineeringProperties({ zIndex: '3.5' }, createSchema()))
    .toThrow(VisualPropertyContractError);
  expect(() => decodeLegacyVisualEngineeringProperties({ privateEditorValue: '1' }, createSchema()))
    .toThrow(VisualPropertyContractError);
  expect(() => decodeLegacyVisualEngineeringProperties({ assetRef: 'https://example.invalid/logo.png' }, createSchema()))
    .toThrow(VisualPropertyContractError);
});
