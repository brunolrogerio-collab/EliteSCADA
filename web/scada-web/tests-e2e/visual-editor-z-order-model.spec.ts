import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';
import { applyVisualEditorAuthoringOperation } from '../src/engineering/visual-editor/visualEditorAuthoringModel';
import { applyVisualEditorZOrderOperation } from '../src/engineering/visual-editor/visualEditorZOrderModel';

function rectangle(id: string, zIndex: number): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x: 0, y: 0, width: 20, height: 20, zIndex }
  };
}

function screen(elements: VisualElementEngineering[]): ScreenEngineering {
  return { key: 'screen', name: 'Screen', route: '/screen', elements };
}

function stackOrder(value: ScreenEngineering): string[] {
  return [...(value.elements ?? [])]
    .sort((left, right) => Number(left.properties?.zIndex ?? 0) - Number(right.properties?.zIndex ?? 0))
    .map(element => element.id!);
}

test('bring to front moves a multi-selection as a stable block and removes zIndex ties', () => {
  const base = screen([
    rectangle('a', 5),
    rectangle('b', 5),
    rectangle('c', 20),
    rectangle('d', 50)
  ]);

  const next = applyVisualEditorZOrderOperation(base, ['a', 'c'], 'front');
  expect(stackOrder(next)).toEqual(['b', 'd', 'a', 'c']);
  expect(next.elements?.map(element => element.properties?.zIndex)).toEqual([7, 5, 8, 6]);
  expect(new Set(next.elements?.map(element => element.properties?.zIndex)).size).toBe(4);
});

test('send to back preserves selected relative order', () => {
  const base = screen([
    rectangle('a', 0), rectangle('b', 1), rectangle('c', 2), rectangle('d', 3)
  ]);

  const next = applyVisualEditorZOrderOperation(base, ['b', 'd'], 'back');
  expect(stackOrder(next)).toEqual(['b', 'd', 'a', 'c']);
});

test('forward and backward move a selected block exactly one neighboring layer', () => {
  const base = screen([
    rectangle('a', 0), rectangle('b', 1), rectangle('c', 2), rectangle('d', 3)
  ]);

  const forward = applyVisualEditorZOrderOperation(base, ['b', 'c'], 'forward');
  expect(stackOrder(forward)).toEqual(['a', 'd', 'b', 'c']);

  const backward = applyVisualEditorZOrderOperation(base, ['b', 'c'], 'backward');
  expect(stackOrder(backward)).toEqual(['b', 'c', 'a', 'd']);
});

test('z-order works inside one group but rejects mixed stacking contexts', () => {
  const base = screen([
    rectangle('outside', 0),
    {
      id: 'group',
      key: 'group',
      type: BUILTIN_VISUAL_OBJECT_TYPES.group,
      properties: { x: 0, y: 0, width: 100, height: 100, zIndex: 1 },
      children: [rectangle('inside-a', 0), rectangle('inside-b', 1)]
    }
  ]);

  const inside = applyVisualEditorZOrderOperation(base, ['inside-a'], 'front');
  expect(inside.elements?.[1].children?.map(element => element.properties?.zIndex)).toEqual([1, 0]);

  expect(() => applyVisualEditorZOrderOperation(base, ['outside', 'inside-a'], 'front'))
    .toThrow(/same parent stacking context/);
});

test('locked authoring objects cannot be reordered', () => {
  const base = screen([rectangle('a', 0), rectangle('b', 1)]);
  const locked = applyVisualEditorAuthoringOperation(base, {
    kind: 'lock', objectIds: ['a'], locked: true
  });

  expect(() => applyVisualEditorZOrderOperation(locked, ['a'], 'front'))
    .toThrow(/locked for Engineering authoring/);
});
