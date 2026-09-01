import { expect, test } from '@playwright/test';
import { resolveSliderConfiguration, quantizeAndClamp } from '../src/engineering/visual-editor/sliderVisualModel';
import { visualTagSampleKey } from '../src/engineering/visual-editor/visualDynamicRuntime';
import { getBuiltinVisualObjectSchema, BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';
import type { VisualElementEngineering } from '../src/engineering/types';

const tagId = '11111111-1111-1111-1111-111111111111';

function values(overrides: Record<string, unknown> = {}) {
  return {
    ...getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.slider).createDefaultValues(),
    ...overrides
  };
}

test('Slider clamps and quantizes deterministically from its engineering minimum', () => {
  expect(quantizeAndClamp(9.76, 0, 10, .5)).toBe(10);
  expect(quantizeAndClamp(-3, -2, 8, .25)).toBe(-2);
  expect(quantizeAndClamp(1.24, 1, 2, .1)).toBe(1.2);
  expect(() => quantizeAndClamp(1, 5, 5, 1)).toThrow(/invalid/);
});

test('passive Slider displays a good bound TAG but cannot request writes', () => {
  const element: VisualElementEngineering = {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    key: 'setpoint',
    type: 'core.slider',
    bindings: [{
      key: 'value', kind: 'Tag', target: 'Plant.SP', direction: 'read', tagReference: { tagId }
    }]
  };
  const samples = new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.SP', tagId, value: 42, dataType: 'Double', quality: 'Good', readOnly: false
  }]]);

  const resolved = resolveSliderConfiguration(element, values({ value: 42 }), [], samples);
  expect(resolved.value).toBe(42);
  expect(resolved.sourceAvailable).toBe(true);
  expect(resolved.interactionEnabled).toBe(false);
  expect(resolved.writeDirection).toBe(false);
});

test('interactive Slider requires stable writable TAG binding and good source', () => {
  const element: VisualElementEngineering = {
    id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    key: 'setpoint',
    type: 'core.slider',
    bindings: [{
      key: 'value', kind: 'Tag', target: 'Plant.SP', direction: 'readWrite', tagReference: { tagId }
    }]
  };
  const good = new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.SP', tagId, value: 55, dataType: 'Double', quality: 'Good', readOnly: false
  }]]);
  const bad = new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.SP', tagId, value: 55, dataType: 'Double', quality: 'BadCommunication', readOnly: false
  }]]);

  const interactive = resolveSliderConfiguration(element, values({ interactionEnabled: true, value: 55 }), [], good);
  expect(interactive.tagId).toBe(tagId);
  expect(interactive.writeDirection).toBe(true);
  expect(interactive.sourceAvailable).toBe(true);

  const unavailable = resolveSliderConfiguration(element, values({ interactionEnabled: true, value: 55 }), [
    { propertyKey: 'value', sourceKind: 'Binding', message: 'BadCommunication' }
  ], bad);
  expect(unavailable.sourceAvailable).toBe(false);
});
