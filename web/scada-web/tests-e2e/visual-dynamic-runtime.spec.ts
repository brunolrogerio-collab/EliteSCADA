import { expect, test } from '@playwright/test';
import { resolveVisualDynamicState, visualTagSampleKey } from '../src/engineering/visual-editor/visualDynamicRuntime';
import type { VisualElementEngineering } from '../src/engineering/types';

const visibleBase = Object.freeze({ visible: true, x: 10, opacity: 1, fillColor: '#202020' });

function sample(tagId: string, value: unknown, dataType: string, quality: string | number = 'Good') {
  return Object.freeze({
    reference: tagId,
    tagId,
    value,
    dataType,
    quality
  });
}

test('canonical integer TAG bit binding drives visible without using friendly .NN as identity', () => {
  const tagId = '11111111-1111-1111-1111-111111111111';
  const element: VisualElementEngineering = {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    key: 'Pump',
    type: 'core.rectangle',
    bindings: [{
      key: 'visible',
      kind: 'Tag',
      target: 'Plant.RenamedStatus.03',
      tagReference: { tagId, selector: { kind: 'bit', index: 3 } }
    }]
  };
  const samples = new Map([[visualTagSampleKey(tagId), sample(tagId, 0b1000, 'Int16')]]);

  const resolved = resolveVisualDynamicState(element, visibleBase, samples);

  expect(resolved.values.visible).toBe(true);
  expect(resolved.diagnostics).toEqual([]);
});

test('bad quality binding falls back to Engineering value and exposes a diagnostic', () => {
  const tagId = '22222222-2222-2222-2222-222222222222';
  const element: VisualElementEngineering = {
    key: 'Pump',
    type: 'core.rectangle',
    bindings: [{
      key: 'visible',
      kind: 'Tag',
      target: 'Plant.Status',
      tagReference: { tagId }
    }]
  };
  const samples = new Map([[visualTagSampleKey(tagId), sample(tagId, false, 'Boolean', 'BadCommunication')]]);

  const resolved = resolveVisualDynamicState(element, visibleBase, samples);

  expect(resolved.values.visible).toBe(true);
  expect(resolved.diagnostics).toHaveLength(1);
  expect(resolved.diagnostics[0].message).toContain('BadCommunication');
});

test('typed expression drives numeric public property through canonical dependencies', () => {
  const levelA = '33333333-3333-3333-3333-333333333333';
  const levelB = '44444444-4444-4444-4444-444444444444';
  const element: VisualElementEngineering = {
    key: 'Bar',
    type: 'core.rectangle',
    propertyExpressions: [{
      propertyKey: 'x',
      expression: {
        text: '(nivel1 + nivel2) * 3',
        resultType: 'Number',
        dependencies: [
          { symbol: 'nivel1', kind: 'Tag', valueType: 'Number', tagReference: { tagId: levelA }, target: 'Plant.Level1' },
          { symbol: 'nivel2', kind: 'Tag', valueType: 'Number', tagReference: { tagId: levelB }, target: 'Plant.Level2' }
        ]
      }
    }]
  };
  const samples = new Map([
    [visualTagSampleKey(levelA), sample(levelA, 2, 'Double')],
    [visualTagSampleKey(levelB), sample(levelB, 5, 'Double')]
  ]);

  const resolved = resolveVisualDynamicState(element, visibleBase, samples);

  expect(resolved.values.x).toBe(21);
  expect(resolved.diagnostics).toEqual([]);
});

test('numeric interval condition drives visible and honors outside/negate semantics', () => {
  const level = '55555555-5555-5555-5555-555555555555';
  const element: VisualElementEngineering = {
    key: 'Alarm',
    type: 'core.rectangle',
    booleanConditions: [{
      propertyKey: 'visible',
      kind: 'NumericInterval',
      source: { kind: 'Tag', valueType: 'Number', target: 'Plant.Level', tagReference: { tagId: level } },
      minimum: 20,
      maximum: 80,
      minimumInclusive: true,
      maximumInclusive: true,
      intervalMode: 'Outside',
      negate: false
    }]
  };
  const samples = new Map([[visualTagSampleKey(level), sample(level, 90, 'Double')]]);

  const resolved = resolveVisualDynamicState(element, visibleBase, samples);

  expect(resolved.values.visible).toBe(true);
  expect(resolved.diagnostics).toEqual([]);
});

test('analog fill resolves numeric source into canonical presentation without persisting percentage', () => {
  const level = '66666666-6666-6666-6666-666666666666';
  const element: VisualElementEngineering = {
    key: 'Tank',
    type: 'core.rectangle',
    analogFill: {
      source: { kind: 'Tag', valueType: 'Number', target: 'Plant.Level', tagReference: { tagId: level } },
      inputMinimum: 0,
      inputMaximum: 100,
      fillColor: '#00AAFF',
      clamp: true,
      invertScale: false,
      direction: 'BottomToTop'
    }
  };
  const samples = new Map([[visualTagSampleKey(level), sample(level, 25, 'Double')]]);

  const resolved = resolveVisualDynamicState(element, visibleBase, samples);

  expect(resolved.analogFill?.presentation.percent).toBe(25);
  expect(resolved.analogFill?.presentation.clipPath).toBe('inset(75% 0 0 0)');
  expect(resolved.analogFill?.fillColor).toBe('#00AAFF');
  expect(resolved.diagnostics).toEqual([]);
});
