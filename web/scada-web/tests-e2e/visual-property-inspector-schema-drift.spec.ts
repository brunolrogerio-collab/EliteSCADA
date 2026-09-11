import { expect, test } from '@playwright/test';
import type { VisualElementEngineering } from '../src/engineering/types';
import { buildPropertyInspectorModel } from '../src/engineering/visual-editor/property-inspector/propertyInspectorModel';
import { listBuiltinVisualObjectSchemas } from '../src/visual-runtime';

function elementFor(objectType: string): VisualElementEngineering {
  return {
    id: `test-${objectType.replace(/[^A-Za-z0-9]+/g, '-')}`,
    key: objectType,
    type: objectType,
    properties: {}
  };
}

test('Property Inspector exposes every and only Engineering-editable property declared by each canonical schema', () => {
  for (const schema of listBuiltinVisualObjectSchemas()) {
    const expected = schema
      .definitions()
      .filter(definition => definition.engineeringEditable)
      .map(definition => definition.key);

    const model = buildPropertyInspectorModel([elementFor(schema.objectTypeKey)]);

    expect(model.error, schema.objectTypeKey).toBeUndefined();
    expect(model.rows.map(row => row.definition.key), schema.objectTypeKey).toEqual(expected);
    expect(model.rows.every(row => row.definition.engineeringEditable), schema.objectTypeKey).toBe(true);
  }
});

test('Property Inspector multi-selection exposes the editable schema intersection without a manual allow-list', () => {
  const schemas = listBuiltinVisualObjectSchemas();
  const rectangle = schemas.find(schema => schema.objectTypeKey === 'core.rectangle')!;
  const text = schemas.find(schema => schema.objectTypeKey === 'core.text')!;

  const expected = rectangle
    .definitions()
    .filter(definition => definition.engineeringEditable && text.declares(definition.key))
    .map(definition => definition.key);

  const model = buildPropertyInspectorModel([
    elementFor(rectangle.objectTypeKey),
    elementFor(text.objectTypeKey)
  ]);

  expect(model.rows.map(row => row.definition.key)).toEqual(expected);
});
