import { expect, test } from '@playwright/test';
import {
  decodeVisualEngineeringProperties,
  encodeVisualEngineeringProperties,
  VISUAL_PROPERTY_KEYS,
  VisualObjectPropertySchema,
  VisualPropertyContractError
} from '../src/visual-runtime';

function imageSchema() {
  return new VisualObjectPropertySchema('core.image', [
    VISUAL_PROPERTY_KEYS.x,
    VISUAL_PROPERTY_KEYS.visible,
    VISUAL_PROPERTY_KEYS.assetRef,
    VISUAL_PROPERTY_KEYS.imageFit
  ]);
}

test('canonical visual Engineering codec preserves JSON-native property types', () => {
  const decoded = decodeVisualEngineeringProperties({
    x: 12.5,
    visible: false,
    assetRef: { assetId: 'asset:logo' },
    imageFit: 'cover'
  }, imageSchema());

  expect(decoded).toEqual({
    x: 12.5,
    visible: false,
    assetRef: { assetId: 'asset:logo' },
    imageFit: 'cover'
  });

  const encoded = encodeVisualEngineeringProperties(decoded, imageSchema());
  expect(encoded).toEqual(decoded);
});

test('canonical visual Engineering codec rejects legacy string coercion on typed properties', () => {
  expect(() => decodeVisualEngineeringProperties({ x: '12.5' }, imageSchema()))
    .toThrow(VisualPropertyContractError);
  expect(() => decodeVisualEngineeringProperties({ visible: 'false' }, imageSchema()))
    .toThrow(VisualPropertyContractError);
});

test('canonical visual Engineering codec keeps null assetRef distinct from clear semantics', () => {
  const decoded = decodeVisualEngineeringProperties({ assetRef: null }, imageSchema());
  expect(decoded.assetRef).toBeNull();
});
