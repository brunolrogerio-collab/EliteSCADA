import { expect, test } from '@playwright/test';
import type {
  VisualElementEngineering,
  VisualEngineeringPropertyValue
} from '../src/engineering/types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';
import {
  normalizeVisualEditorMarquee,
  pickTopmostVisualEditorObjectAtPoint,
  resolveVisualEditorMarqueeSelection
} from '../src/engineering/visual-editor/canvas/visualEditorSelectionModel';

function rectangle(
  id: string,
  x: number,
  y: number,
  width: number,
  height: number,
  properties: Record<string, VisualEngineeringPropertyValue> = {}
): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x, y, width, height, ...properties }
  };
}

test('normalizes marquee drags independently of drag direction', () => {
  expect(normalizeVisualEditorMarquee({ x: 100, y: 80 }, { x: 20, y: 10 })).toEqual({
    x: 20,
    y: 10,
    width: 80,
    height: 70,
    right: 100,
    bottom: 80
  });
});

test('marquee supports intersection and containment in logical coordinates', () => {
  const elements = [
    rectangle('inside', 10, 10, 20, 20),
    rectangle('partial', 45, 45, 20, 20),
    rectangle('outside', 100, 100, 20, 20)
  ];
  const marquee = normalizeVisualEditorMarquee({ x: 0, y: 0 }, { x: 50, y: 50 });

  expect(resolveVisualEditorMarqueeSelection(elements, marquee, 'intersect')).toEqual(['inside', 'partial']);
  expect(resolveVisualEditorMarqueeSelection(elements, marquee, 'contain')).toEqual(['inside']);
});

test('hidden objects are not selectable through marquee or point hit testing', () => {
  const elements = [
    rectangle('visible', 0, 0, 50, 50, { zIndex: 1 }),
    rectangle('hidden', 0, 0, 50, 50, { zIndex: 100, visible: false })
  ];
  const marquee = normalizeVisualEditorMarquee({ x: -10, y: -10 }, { x: 60, y: 60 });

  expect(resolveVisualEditorMarqueeSelection(elements, marquee)).toEqual(['visible']);
  expect(pickTopmostVisualEditorObjectAtPoint(elements, { x: 10, y: 10 })).toBe('visible');
});

test('overlap picking is deterministic by zIndex then painter document order', () => {
  const elements = [
    rectangle('back', 0, 0, 100, 100, { zIndex: 2 }),
    rectangle('front-first', 0, 0, 100, 100, { zIndex: 5 }),
    rectangle('front-last', 0, 0, 100, 100, { zIndex: 5 })
  ];

  expect(pickTopmostVisualEditorObjectAtPoint(elements, { x: 50, y: 50 })).toBe('front-last');
});

test('marquee bounds account for canonical rotation and scale without viewport zoom', () => {
  const elements = [
    rectangle('rotated', 40, 40, 20, 40, { rotation: 90, scaleX: 2, scaleY: 1 })
  ];

  const wideMarquee = normalizeVisualEditorMarquee({ x: 25, y: 25 }, { x: 75, y: 75 });
  const tooNarrow = normalizeVisualEditorMarquee({ x: 45, y: 25 }, { x: 55, y: 75 });

  expect(resolveVisualEditorMarqueeSelection(elements, wideMarquee, 'intersect')).toEqual(['rotated']);
  expect(resolveVisualEditorMarqueeSelection(elements, tooNarrow, 'contain')).toEqual([]);
});

test('groups stay encapsulated at the current authoring level', () => {
  const group: VisualElementEngineering = {
    id: 'group',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 10, y: 10, width: 100, height: 100 },
    children: [rectangle('child', 5, 5, 10, 10)]
  };
  const marquee = normalizeVisualEditorMarquee({ x: 0, y: 0 }, { x: 200, y: 200 });

  expect(resolveVisualEditorMarqueeSelection([group], marquee)).toEqual(['group']);
  expect(resolveVisualEditorMarqueeSelection(group.children ?? [], marquee)).toEqual(['child']);
});