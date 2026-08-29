import { expect, test } from '@playwright/test';
import type { VisualElementEngineering } from '../src/engineering/types';
import {
  buildPropertyInspectorModel,
  buildPropertyInspectorSetIntent,
  parsePropertyInspectorInput
} from '../src/engineering/visual-editor/property-inspector/propertyInspectorModel';

function modelFor(type: string) {
  const element: VisualElementEngineering = {
    id: 'object-1',
    key: 'object-1',
    type,
    properties: {}
  };
  return buildPropertyInspectorModel([element]);
}

test('parses boolean and enum controls through registered property definitions', () => {
  const rectangle = modelFor('core.rectangle');
  const visible = rectangle.rows.find(row => row.definition.key === 'visible')!.definition;
  const strokeStyle = rectangle.rows.find(row => row.definition.key === 'strokeStyle')!.definition;

  expect(parsePropertyInspectorInput(visible, 'true')).toEqual({ ok: true, value: true });
  expect(parsePropertyInspectorInput(visible, 'false')).toEqual({ ok: true, value: false });
  expect(parsePropertyInspectorInput(visible, 'maybe').ok).toBeFalsy();

  const dotted = parsePropertyInspectorInput(strokeStyle, 'dotted');
  expect(dotted).toEqual({ ok: true, value: 'dotted' });
  expect(dotted.ok && buildPropertyInspectorSetIntent(rectangle, 'strokeStyle', dotted.value).ok).toBeTruthy();

  const unsupported = parsePropertyInspectorInput(strokeStyle, 'future-style');
  expect(unsupported.ok).toBeTruthy();
  if (unsupported.ok) {
    expect(buildPropertyInspectorSetIntent(rectangle, 'strokeStyle', unsupported.value).ok).toBeFalsy();
  }
});

test('color and asset input remain subject to shared registry validation', () => {
  const rectangle = modelFor('core.rectangle');
  expect(buildPropertyInspectorSetIntent(rectangle, 'fillColor', '#11223344').ok).toBeTruthy();
  expect(buildPropertyInspectorSetIntent(rectangle, 'fillColor', 'red').ok).toBeFalsy();

  const image = modelFor('core.image');
  const assetRef = image.rows.find(row => row.definition.key === 'assetRef')!.definition;
  const cleared = parsePropertyInspectorInput(assetRef, '');
  expect(cleared).toEqual({ ok: true, value: null });
  expect(cleared.ok && buildPropertyInspectorSetIntent(image, 'assetRef', cleared.value).ok).toBeTruthy();

  const pathLike = parsePropertyInspectorInput(assetRef, '../unsafe.png');
  expect(pathLike.ok).toBeTruthy();
  if (pathLike.ok) {
    expect(buildPropertyInspectorSetIntent(image, 'assetRef', pathLike.value).ok).toBeFalsy();
  }
});
