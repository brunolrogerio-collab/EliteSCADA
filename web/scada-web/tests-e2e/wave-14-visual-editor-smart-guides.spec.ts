import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import { resolveVisualEditorMoveGuides } from '../src/engineering/visual-editor/canvas/visualEditorSmartGuidesModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

function rectangle(id: string, x: number, y: number, width = 20, height = 20): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x, y, width, height, visible: true }
  };
}

function screen(elements: readonly VisualElementEngineering[]): ScreenEngineering {
  return {
    key: 'screen',
    name: 'Screen',
    route: '/screen',
    elements: [...elements],
    properties: {},
    context: {},
    metadata: {}
  };
}

test('smart guide adjusts a grid-snapped move to the nearest sibling edge', () => {
  const draft = screen([
    rectangle('moving', 10, 10),
    rectangle('target', 50, 10)
  ]);

  const result = resolveVisualEditorMoveGuides(draft, ['moving'], { x: 19, y: 0 }, 5);
  expect(result.delta).toEqual({ x: 20, y: 0 });
  expect(result.verticalGuide).toMatchObject({
    axis: 'vertical',
    position: 50,
    sourceAnchor: 'end',
    targetAnchor: 'start',
    targetObjectId: 'target',
    adjustment: 1
  });
});

test('smart guide treats multi-selection as one moving bounding box', () => {
  const draft = screen([
    rectangle('a', 0, 0),
    rectangle('b', 30, 0),
    rectangle('target', 80, 0)
  ]);

  const result = resolveVisualEditorMoveGuides(draft, ['a', 'b'], { x: 29, y: 0 }, 5);
  expect(result.delta.x).toBe(30);
  expect(result.verticalGuide?.position).toBe(80);
});

test('smart guide does not align objects from different canonical coordinate spaces', () => {
  const draft = screen([
    {
      id: 'group',
      key: 'group',
      type: BUILTIN_VISUAL_OBJECT_TYPES.group,
      properties: { x: 0, y: 0, width: 100, height: 100 },
      children: [rectangle('child', 10, 10)]
    },
    rectangle('peer', 50, 10)
  ]);

  const result = resolveVisualEditorMoveGuides(draft, ['child', 'peer'], { x: 3, y: 4 }, 5);
  expect(result.delta).toEqual({ x: 3, y: 4 });
  expect(result.verticalGuide).toBeNull();
  expect(result.horizontalGuide).toBeNull();
});

test('smart guide candidate selection is deterministic when distances tie', () => {
  const draft = screen([
    rectangle('moving', 20, 20),
    rectangle('first', 0, 20),
    rectangle('second', 60, 20)
  ]);

  const result = resolveVisualEditorMoveGuides(draft, ['moving'], { x: 0, y: 0 }, 25);
  expect(result.verticalGuide?.targetObjectId).toBe('first');
});
