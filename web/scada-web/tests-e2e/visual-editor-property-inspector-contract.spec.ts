import { expect, test } from '@playwright/test';
import type { VisualElementEngineering } from '../src/engineering/types';
import {
  buildPropertyInspectorModel,
  buildPropertyInspectorRemoveIntent,
  buildPropertyInspectorSetIntent,
  parsePropertyInspectorInput
} from '../src/engineering/visual-editor/property-inspector/propertyInspectorModel';

function element(
  id: string | null,
  type: string,
  properties?: VisualElementEngineering['properties'],
  key = 'object-1'
): VisualElementEngineering {
  return { id, key, type, properties };
}

test('distinguishes registry defaults from explicit Engineering base values', () => {
  const defaultModel = buildPropertyInspectorModel([
    element('rect-1', 'core.rectangle', {})
  ]);
  const defaultX = defaultModel.rows.find(row => row.definition.key === 'x');
  expect(defaultX).toMatchObject({ state: 'default', value: 0, defaultValue: 0, explicitCount: 0 });

  const explicitModel = buildPropertyInspectorModel([
    element('rect-1', 'core.rectangle', { x: 0 })
  ]);
  const explicitX = explicitModel.rows.find(row => row.definition.key === 'x');
  expect(explicitX).toMatchObject({ state: 'engineered', value: 0, defaultValue: 0, explicitCount: 1 });
});

test('multiselect exposes only common registered properties and preserves ambiguity', () => {
  const model = buildPropertyInspectorModel([
    element('rect-1', 'core.rectangle', { x: 10, fillColor: '#112233' }, 'rectangle'),
    element('text-1', 'core.text', { x: 20, text: 'Pump' }, 'label')
  ]);

  const keys = model.rows.map(row => row.definition.key);
  expect(keys).toContain('x');
  expect(keys).toContain('opacity');
  expect(keys).not.toContain('fillColor');
  expect(keys).not.toContain('text');
  expect(model.rows.find(row => row.definition.key === 'x')?.state).toBe('mixed');
});

test('mixed explicit/default state is not silently collapsed even when effective values match', () => {
  const before = [
    element('rect-1', 'core.rectangle', { x: 0 }, 'first'),
    element('rect-2', 'core.rectangle', {}, 'second')
  ];
  const snapshot = JSON.stringify(before);
  const model = buildPropertyInspectorModel(before);

  expect(model.rows.find(row => row.definition.key === 'x')).toMatchObject({
    state: 'mixed',
    explicitCount: 1,
    selectionCount: 2
  });
  expect(JSON.stringify(before)).toBe(snapshot);
});

test('emits only shared property intents after schema validation', () => {
  const model = buildPropertyInspectorModel([
    element('rect-1', 'core.rectangle', {}),
    element('rect-2', 'core.rectangle', {})
  ]);

  const valid = buildPropertyInspectorSetIntent(model, 'opacity', 0.5);
  expect(valid).toEqual({
    ok: true,
    intent: {
      kind: 'property.set',
      objectIds: ['rect-1', 'rect-2'],
      propertyKey: 'opacity',
      value: 0.5
    }
  });

  const invalid = buildPropertyInspectorSetIntent(model, 'opacity', 2);
  expect(invalid.ok).toBeFalsy();
  if (!invalid.ok) expect(invalid.validation?.code).toBe('number.maximum');

  const remove = buildPropertyInspectorRemoveIntent(model, 'opacity');
  expect(remove).toEqual({
    ok: true,
    intent: {
      kind: 'property.remove',
      objectIds: ['rect-1', 'rect-2'],
      propertyKey: 'opacity'
    }
  });
});

test('fails closed for unknown types, missing stable ids and non-common properties', () => {
  expect(buildPropertyInspectorModel([
    element('legacy-1', 'legacy.rectangle', {})
  ]).error).toMatch(/not registered/);

  expect(buildPropertyInspectorModel([
    element(null, 'core.rectangle', {})
  ]).error).toMatch(/stable canonical id/);

  const mixed = buildPropertyInspectorModel([
    element('rect-1', 'core.rectangle', {}),
    element('text-1', 'core.text', {})
  ]);
  const result = buildPropertyInspectorSetIntent(mixed, 'text', 'unsafe overwrite');
  expect(result.ok).toBeFalsy();
});

test('supports typed Wave 08 value families without inventing editor-private validation', () => {
  const rectangle = buildPropertyInspectorModel([element('rect-1', 'core.rectangle', {})]);
  const width = rectangle.rows.find(row => row.definition.key === 'width')!.definition;
  expect(parsePropertyInspectorInput(width, '125.5')).toEqual({ ok: true, value: 125.5 });
  expect(buildPropertyInspectorSetIntent(rectangle, 'width', -1).ok).toBeFalsy();

  const image = buildPropertyInspectorModel([element('img-1', 'core.image', {})]);
  const assetRef = image.rows.find(row => row.definition.key === 'assetRef')!.definition;
  expect(parsePropertyInspectorInput(assetRef, 'asset:pump-photo')).toEqual({
    ok: true,
    value: { assetId: 'asset:pump-photo' }
  });
  expect(buildPropertyInspectorSetIntent(image, 'assetRef', { assetId: 'asset:pump-photo' }).ok).toBeTruthy();
  expect(buildPropertyInspectorSetIntent(image, 'assetRef', { assetId: '../pump.png' }).ok).toBeFalsy();
});
