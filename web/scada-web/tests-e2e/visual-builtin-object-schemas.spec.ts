import { expect, test } from '@playwright/test';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema,
  listBuiltinVisualObjectSchemas,
  supportsAnalogFill,
  VISUAL_PROPERTY_KEYS
} from '../src/visual-runtime';

const expectedTypes = [
  'core.group',
  'core.rectangle',
  'core.ellipse',
  'core.line',
  'core.polygon',
  'core.text',
  'core.image',
  'core.valueDisplay',
  'core.button',
  'core.slider'
];

test('built-in visual object types are stable and unique', () => {
  expect(listBuiltinVisualObjectSchemas().map(schema => schema.objectTypeKey)).toEqual(expectedTypes);
  expect(new Set(expectedTypes).size).toBe(expectedTypes.length);
});

test('built-in schemas expose only relevant shared visual properties', () => {
  const rectangle = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.rectangle);
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.fillStyle)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.fillColor)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.fillSecondaryColor)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.gradientDirection)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.strokeStyle)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.shadowEnabled)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(rectangle.declares(VISUAL_PROPERTY_KEYS.assetRef)).toBeFalsy();

  const line = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.line);
  expect(line.declares(VISUAL_PROPERTY_KEYS.strokeStyle)).toBeTruthy();
  expect(line.declares(VISUAL_PROPERTY_KEYS.shadowEnabled)).toBeTruthy();
  expect(line.declares(VISUAL_PROPERTY_KEYS.fillStyle)).toBeFalsy();

  const image = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.image);
  expect(image.declares(VISUAL_PROPERTY_KEYS.assetRef)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.imageFit)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.imagePositionX)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.shadowEnabled)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.text)).toBeFalsy();

  const text = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.text);
  expect(text.declares(VISUAL_PROPERTY_KEYS.fontFamily)).toBeTruthy();
  expect(text.declares(VISUAL_PROPERTY_KEYS.horizontalAlignment)).toBeTruthy();
  expect(text.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(text.declares(VISUAL_PROPERTY_KEYS.assetRef)).toBeFalsy();

  const button = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.button);
  expect(button.declares(VISUAL_PROPERTY_KEYS.backgroundColor)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.cornerRadius)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.text)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.fillStyle)).toBeFalsy();

  const slider = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.slider);
  expect(slider.declares(VISUAL_PROPERTY_KEYS.value)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.minimum)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.maximum)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.step)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.interactionEnabled)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.orientation)).toBeTruthy();
});

test('Analog Fill eligibility is explicit in the shared object capability contract', () => {
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.rectangle)).toBe(true);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.ellipse)).toBe(true);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.polygon)).toBe(false);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.line)).toBe(false);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.text)).toBe(false);
  expect(supportsAnalogFill('core.unknown')).toBe(false);
});

test('unknown built-in type fails closed rather than falling back to a generic object', () => {
  expect(() => getBuiltinVisualObjectSchema('core.mystery')).toThrow(/not registered|Unknown/);
});
