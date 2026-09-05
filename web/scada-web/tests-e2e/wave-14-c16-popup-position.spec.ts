import { expect, test } from '@playwright/test';
import {
  POPUP_MIN_VISIBLE_LOGICAL_PX,
  resolvePopupLogicalPosition
} from '../src/runtime/visual-navigation/runtimePopupPosition';

const DESIGN = { width: 1920, height: 1080 } as const;

test('Popup keeps authored logical coordinates inside the HMI stage', () => {
  expect(resolvePopupLogicalPosition({ x: 320, y: 180 }, DESIGN)).toEqual({ x: 320, y: 180 });
});

test('negative Popup coordinates clamp to the logical stage origin', () => {
  expect(resolvePopupLogicalPosition({ x: -500, y: -100 }, DESIGN)).toEqual({ x: 0, y: 0 });
});

test('far off-canvas Popup keeps a deterministic reachable region without inventing width or height', () => {
  expect(resolvePopupLogicalPosition({ x: 9000, y: 9000 }, DESIGN)).toEqual({
    x: DESIGN.width - POPUP_MIN_VISIBLE_LOGICAL_PX,
    y: DESIGN.height - POPUP_MIN_VISIBLE_LOGICAL_PX
  });
});

test('invalid runtime Popup coordinates fail safe to the origin', () => {
  expect(resolvePopupLogicalPosition({ x: Number.NaN, y: Number.POSITIVE_INFINITY }, DESIGN)).toEqual({ x: 0, y: 0 });
});

test('Popup logical position is viewport-independent across canonical C09 target resolutions', () => {
  for (const viewport of [
    { width: 1280, height: 720 },
    { width: 1920, height: 1080 },
    { width: 2560, height: 1440 },
    { width: 3840, height: 2160 }
  ]) {
    expect(viewport.width / viewport.height).toBeCloseTo(16 / 9, 8);
    expect(resolvePopupLogicalPosition({ x: 1440, y: 810 }, DESIGN)).toEqual({ x: 1440, y: 810 });
  }
});