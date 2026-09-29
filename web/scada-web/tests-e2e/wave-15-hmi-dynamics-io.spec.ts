import { expect, test } from '@playwright/test';
import {
  formatNumericInputValue,
  resolveNumericInputConfiguration,
  validateNumericInputCandidate
} from '../src/engineering/visual-editor/numericInputVisualModel';
import {
  resolveVisualDynamicState,
  visualTagSampleKey
} from '../src/engineering/visual-editor/visualDynamicRuntime';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema
} from '../src/visual-runtime';
import type { VisualElementEngineering } from '../src/engineering/types';

const tagId = '11111111-1111-1111-1111-111111111111';

test('Numeric Input resolves only stable writable good-quality TAG sources', () => {
  const element: VisualElementEngineering = {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    key: 'setpoint',
    type: BUILTIN_VISUAL_OBJECT_TYPES.numericInput,
    bindings: [{
      key: 'value',
      kind: 'Tag',
      target: 'Plant.SP',
      direction: 'readWrite',
      tagReference: { tagId },
      metadata: { decimalPlaces: '2', engineeringUnit: 'bar' }
    }]
  };
  const values = {
    ...getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.numericInput).createDefaultValues(),
    value: 42,
    minimum: 0,
    maximum: 100,
    step: .5,
    interactionEnabled: true
  };
  const samples = new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.SP',
    tagId,
    value: 42,
    dataType: 'Double',
    quality: 'Good',
    readOnly: false,
    timestamp: '2026-09-28T00:00:00Z'
  }]]);

  const resolved = resolveNumericInputConfiguration(element, values, [], samples);
  expect(resolved.tagId).toBe(tagId);
  expect(resolved.writeDirection).toBe(true);
  expect(resolved.sourceAvailable).toBe(true);
  expect(resolved.sourceReadOnly).toBe(false);
  expect(resolved.precision).toBe(2);
  expect(resolved.unit).toBe('bar');
  expect(formatNumericInputValue(resolved.value, resolved.precision)).toBe('42.00');
});

test('Numeric Input rejects invalid and out-of-range buffered values', () => {
  expect(validateNumericInputCandidate('12.5', 0, 20)).toBe(12.5);
  expect(() => validateNumericInputCandidate('', 0, 20)).toThrow(/required/);
  expect(() => validateNumericInputCandidate('NaN', 0, 20)).toThrow(/finite/);
  expect(() => validateNumericInputCandidate('21', 0, 20)).toThrow(/between/);
});

test('typed property map uses canonical property type, first-match ordering and fallback', () => {
  const element: VisualElementEngineering = {
    id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    key: 'tank',
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    propertyMaps: [{
      propertyKey: 'fillColor',
      source: { kind: 'Tag', valueType: 'Number', target: 'Plant.Level', tagReference: { tagId }, version: 1 },
      rules: [
        { minimum: 0, maximum: 50, minimumInclusive: true, maximumInclusive: true, value: '#00AA00' },
        { minimum: 40, maximum: 90, minimumInclusive: true, maximumInclusive: true, value: '#FFAA00' }
      ],
      fallback: '#CC0000',
      version: 1
    }]
  };
  const base = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.rectangle).createDefaultValues();

  const mid = resolveVisualDynamicState(element, base, new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.Level', tagId, value: 45, dataType: 'Double', quality: 'Good'
  }]]));
  expect(mid.values.fillColor).toBe('#00AA00');

  const high = resolveVisualDynamicState(element, base, new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.Level', tagId, value: 95, dataType: 'Double', quality: 'Good'
  }]]));
  expect(high.values.fillColor).toBe('#CC0000');

  const bad = resolveVisualDynamicState(element, base, new Map([[visualTagSampleKey(tagId), {
    reference: 'Plant.Level', tagId, value: 25, dataType: 'Double', quality: 'BadCommunication'
  }]]));
  expect(bad.values.fillColor).toBe(base.fillColor);
  expect(bad.diagnostics.some(item => item.sourceKind === 'PropertyMap')).toBe(true);
});
