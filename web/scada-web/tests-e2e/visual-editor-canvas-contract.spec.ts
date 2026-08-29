import { expect, test } from '@playwright/test';
import {
  DEFAULT_CANVAS_GRID_SIZE,
  MAX_CANVAS_ZOOM,
  MIN_CANVAS_ZOOM,
  clientDeltaToCanvas,
  nextSelection,
  normalizeSelection,
  normalizeViewport,
  panViewport,
  projectCanvasElements,
  resizeBounds,
  rotationDeltaDegrees,
  selectionModeFromModifiers,
  snapPoint,
  zoomViewport
} from '../src/engineering/visual-editor/canvas/canvasInteractionModel';
import type { ScreenEngineering } from '../src/engineering/types';

test('canvas projection uses canonical registry defaults without mutating supplied Engineering', () => {
  const screen: ScreenEngineering = {
    id: 'screen-1',
    key: 'main',
    name: 'Main',
    elements: [
      {
        id: 'parent-1',
        key: 'parent',
        type: 'core.group',
        properties: { x: 20, y: 30 },
        children: [
          {
            id: 'child-1',
            key: 'child',
            type: 'core.rectangle',
            properties: { width: 250 }
          }
        ]
      }
    ]
  };
  const before = JSON.stringify(screen);

  const projection = projectCanvasElements(screen);

  expect(projection).toHaveLength(1);
  expect(projection[0].objectId).toBe('parent-1');
  expect(projection[0].geometry).toMatchObject({
    x: 20,
    y: 30,
    width: 100,
    height: 100,
    rotation: 0,
    zIndex: 0,
    visible: true
  });
  expect(projection[0].children[0].objectId).toBe('child-1');
  expect(projection[0].children[0].geometry.width).toBe(250);
  expect(projection[0].children[0].geometry.height).toBe(100);
  expect(JSON.stringify(screen)).toBe(before);
});

test('missing and duplicate stable IDs fail closed for Canvas interaction identity', () => {
  const screen: ScreenEngineering = {
    key: 'identity',
    name: 'Identity',
    elements: [
      { key: 'missing', type: 'core.rectangle' },
      { id: 'duplicate', key: 'a', type: 'core.rectangle' },
      { id: 'duplicate', key: 'b', type: 'core.ellipse' }
    ]
  };

  const projection = projectCanvasElements(screen);

  expect(projection[0].objectId).toBeNull();
  expect(projection[0].identityIssue).toBe('missing-id');
  expect(projection[1].objectId).toBeNull();
  expect(projection[1].identityIssue).toBe('duplicate-id');
  expect(projection[2].objectId).toBeNull();
  expect(projection[2].identityIssue).toBe('duplicate-id');
});

test('selection is deterministic for replace add and toggle multiselect modes', () => {
  expect(normalizeSelection(['a', 'a', '', ' b '])).toEqual(['a', 'b']);
  expect(nextSelection(['a', 'b'], 'c', 'replace')).toEqual(['c']);
  expect(nextSelection(['a', 'b'], 'c', 'add')).toEqual(['a', 'b', 'c']);
  expect(nextSelection(['a', 'b'], 'b', 'add')).toEqual(['a', 'b']);
  expect(nextSelection(['a', 'b'], 'b', 'toggle')).toEqual(['a']);
  expect(nextSelection(['a'], 'b', 'toggle')).toEqual(['a', 'b']);
  expect(selectionModeFromModifiers({ shiftKey: false, ctrlKey: false, metaKey: false })).toBe('replace');
  expect(selectionModeFromModifiers({ shiftKey: true, ctrlKey: false, metaKey: false })).toBe('add');
  expect(selectionModeFromModifiers({ shiftKey: false, ctrlKey: true, metaKey: false })).toBe('toggle');
});

test('grid snap and viewport math are finite bounded and deterministic', () => {
  expect(snapPoint({ x: 14.9, y: -15.1 })).toEqual({ x: 10, y: -20 });
  expect(snapPoint({ x: 14.9, y: -15.1 }, 0)).toEqual({ x: 14.9, y: -15.1 });

  expect(normalizeViewport({ zoom: Number.NaN, panX: Number.POSITIVE_INFINITY, panY: 5 }))
    .toEqual({ zoom: 1, panX: 0, panY: 5 });
  expect(zoomViewport({ zoom: MAX_CANVAS_ZOOM, panX: 2, panY: 3 }, 2).zoom).toBe(MAX_CANVAS_ZOOM);
  expect(zoomViewport({ zoom: MIN_CANVAS_ZOOM, panX: 2, panY: 3 }, 0.1).zoom).toBe(MIN_CANVAS_ZOOM);
  expect(panViewport({ zoom: 2, panX: 10, panY: 20 }, { x: -4, y: 6 }))
    .toEqual({ zoom: 2, panX: 6, panY: 26 });

  expect(clientDeltaToCanvas(
    { x: 25, y: 39 },
    { zoom: 2, panX: 0, panY: 0 },
    true,
    DEFAULT_CANVAS_GRID_SIZE
  )).toEqual({ x: 10, y: 20 });
});

test('resize handles produce absolute bounds and enforce positive size', () => {
  const start = { x: 10, y: 20, width: 100, height: 80 };

  expect(resizeBounds(start, { x: 20, y: 10 }, 'southEast'))
    .toEqual({ x: 10, y: 20, width: 120, height: 90 });
  expect(resizeBounds(start, { x: 20, y: 10 }, 'northWest'))
    .toEqual({ x: 30, y: 30, width: 80, height: 70 });
  expect(resizeBounds(start, { x: 500, y: 500 }, 'northWest'))
    .toEqual({ x: 109, y: 99, width: 1, height: 1 });
});

test('rotation delta is normalized across the angle wrap boundary', () => {
  const center = { x: 0, y: 0 };
  const start = { x: -1, y: 0.01 };
  const current = { x: -1, y: -0.01 };
  const delta = rotationDeltaDegrees(center, start, current);

  expect(Math.abs(delta)).toBeLessThan(2);
});
