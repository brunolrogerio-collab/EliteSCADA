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
  'core.arc',
  'core.bezier',
  'core.polygon',
  'core.text',
  'core.image',
  'core.model3d',
  'core.videoPlayer',
  'core.pdfViewer',
  'core.reportLauncher',
  'core.svgSymbol',
  'core.valueDisplay',
  'core.trend',
  'core.alarmBrowser',
  'core.eventBrowser',
  'core.button',
  'core.slider',
  'core.numericInput',
  'core.measurementDial',
  'core.measurementRing',
  'core.measurementBar',
  'core.measurementThermometer',
  'core.measurementBands',
  'core.measurementTarget',
  'core.intelligencePerformanceV1B',
  'core.intelligenceProfileCoverageV1'
];

test('built-in visual object types are stable and unique', () => {
  expect(listBuiltinVisualObjectSchemas().map(schema => schema.objectTypeKey)).toEqual(expectedTypes);
  expect(new Set(expectedTypes).size).toBe(expectedTypes.length);
});

test('built-in schemas expose only relevant shared visual properties', () => {
  const model3d = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.model3d);
  expect(model3d.declares(VISUAL_PROPERTY_KEYS.assetRef)).toBeTruthy();
  expect(model3d.declares(VISUAL_PROPERTY_KEYS.visible)).toBeTruthy();

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

  const bezier = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.bezier);
  expect(bezier.declares(VISUAL_PROPERTY_KEYS.bezierPath)).toBeTruthy();
  expect(bezier.declares(VISUAL_PROPERTY_KEYS.fillStyle)).toBeTruthy();

  const image = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.image);
  expect(image.declares(VISUAL_PROPERTY_KEYS.assetRef)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.imageFit)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.imagePositionX)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.imageZoom)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.shadowEnabled)).toBeTruthy();
  expect(image.declares(VISUAL_PROPERTY_KEYS.text)).toBeFalsy();

  const text = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.text);
  expect(text.declares(VISUAL_PROPERTY_KEYS.fontFamily)).toBeTruthy();
  expect(text.declares(VISUAL_PROPERTY_KEYS.horizontalAlignment)).toBeTruthy();
  expect(text.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(text.declares(VISUAL_PROPERTY_KEYS.assetRef)).toBeFalsy();

  const trend = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.trend);
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendMode)).toBeTruthy();
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendWindowSeconds)).toBeTruthy();
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendRefreshSeconds)).toBeTruthy();
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendLegendVisible)).toBeTruthy();
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendGridVisible)).toBeTruthy();
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendAxesVisible)).toBeTruthy();
  expect(trend.declares(VISUAL_PROPERTY_KEYS.trendQualityVisible)).toBeTruthy();
  expect(trend.declares('pens')).toBeFalsy();

  const button = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.button);
  expect(button.declares(VISUAL_PROPERTY_KEYS.backgroundColor)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.cornerRadius)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.text)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(button.declares(VISUAL_PROPERTY_KEYS.fillStyle)).toBeFalsy();

  const valueDisplay = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay);
  expect(valueDisplay.declares(VISUAL_PROPERTY_KEYS.text)).toBeTruthy();
  expect(valueDisplay.declares(VISUAL_PROPERTY_KEYS.showEngineeringUnit)).toBeTruthy();
  expect(valueDisplay.getRequired(VISUAL_PROPERTY_KEYS.showEngineeringUnit).defaultValue).toBe(true);

  const performance = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.intelligencePerformanceV1B);
  for (const key of [
    VISUAL_PROPERTY_KEYS.intelligenceScopeId,
    VISUAL_PROPERTY_KEYS.intelligenceAssignmentId,
    VISUAL_PROPERTY_KEYS.intelligenceProfileId,
    VISUAL_PROPERTY_KEYS.intelligenceRuleId,
    VISUAL_PROPERTY_KEYS.intelligenceRuleVersion,
    VISUAL_PROPERTY_KEYS.intelligenceSignalIds
  ]) {
    const property = performance.getRequired(key);
    expect(property.type).toBe('string');
    expect(property.engineeringEditable).toBe(false);
    expect(property.runtimeReadable).toBe(true);
    expect(property.runtimeWritable).toBe(false);
    expect(property.supportsBinding).toBe(false);
  }

  const coverage = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1);
  for (const key of [VISUAL_PROPERTY_KEYS.intelligenceScopeId, VISUAL_PROPERTY_KEYS.intelligenceProfileId]) {
    const property = coverage.getRequired(key);
    expect(property.type).toBe('string');
    expect(property.engineeringEditable).toBe(false);
    expect(property.runtimeReadable).toBe(true);
    expect(property.runtimeWritable).toBe(false);
    expect(property.supportsBinding).toBe(false);
  }
  expect(coverage.declares(VISUAL_PROPERTY_KEYS.intelligenceAssignmentId)).toBe(false);
  expect(coverage.propertyKeys).not.toContain('result');
  expect(coverage.propertyKeys).not.toContain('window');

  const slider = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.slider);
  expect(slider.declares(VISUAL_PROPERTY_KEYS.value)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.minimum)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.maximum)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.step)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.enabled)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.interactionEnabled)).toBeTruthy();
  expect(slider.declares(VISUAL_PROPERTY_KEYS.orientation)).toBeTruthy();

  for (const objectType of [
    BUILTIN_VISUAL_OBJECT_TYPES.measurementDial,
    BUILTIN_VISUAL_OBJECT_TYPES.measurementRing,
    BUILTIN_VISUAL_OBJECT_TYPES.measurementBar,
    BUILTIN_VISUAL_OBJECT_TYPES.measurementThermometer,
    BUILTIN_VISUAL_OBJECT_TYPES.measurementBands,
    BUILTIN_VISUAL_OBJECT_TYPES.measurementTarget
  ]) {
    const measurement = getBuiltinVisualObjectSchema(objectType);
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.value)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.measurementUnit)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.minimum)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.maximum)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.measurementQualityVisible)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.measurementFreshnessVisible)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.measurementTheme)).toBeTruthy();
    expect(measurement.declares(VISUAL_PROPERTY_KEYS.interactionEnabled)).toBeFalsy();
  }
  expect(getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.measurementTarget).declares(VISUAL_PROPERTY_KEYS.measurementTarget)).toBeTruthy();
  expect(getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.measurementBands).declares(VISUAL_PROPERTY_KEYS.measurementBandOneEnd)).toBeTruthy();
  expect(getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.measurementBands).declares(VISUAL_PROPERTY_KEYS.measurementBandTwoEnd)).toBeTruthy();
});

test('Analog Fill eligibility is explicit in the shared object capability contract', () => {
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.rectangle)).toBe(true);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.ellipse)).toBe(true);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.bezier)).toBe(true);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.polygon)).toBe(false);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.trend)).toBe(false);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.line)).toBe(false);
  expect(supportsAnalogFill(BUILTIN_VISUAL_OBJECT_TYPES.text)).toBe(false);
  expect(supportsAnalogFill('core.unknown')).toBe(false);
});

test('unknown built-in type fails closed rather than falling back to a generic object', () => {
  expect(() => getBuiltinVisualObjectSchema('core.mystery')).toThrow(/not registered|Unknown/);
});
