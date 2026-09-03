import { expect, test } from '@playwright/test';
import {
  calculateRuntimeLogicalTransform,
  resolveRuntimeLogicalSize,
  viewportPointToLogical
} from '../src/runtime/visual-navigation/runtimeLogicalCanvas';

const DESIGN = { width: 1920, height: 1080 } as const;

for (const viewport of [
  { width: 1280, height: 720, scale: 2 / 3 },
  { width: 1920, height: 1080, scale: 1 },
  { width: 2560, height: 1440, scale: 4 / 3 },
  { width: 3840, height: 2160, scale: 2 }
]) {
  test(`logical Runtime scales uniformly at ${viewport.width}x${viewport.height}`, () => {
    const result = calculateRuntimeLogicalTransform(
      viewport.width,
      viewport.height,
      DESIGN.width,
      DESIGN.height
    );
    expect(result.scale).toBeCloseTo(viewport.scale, 8);
    expect(result.offsetX).toBeCloseTo(0, 8);
    expect(result.offsetY).toBeCloseTo(0, 8);
    expect(result.width * result.scale).toBeCloseTo(viewport.width, 8);
    expect(result.height * result.scale).toBeCloseTo(viewport.height, 8);
  });
}

test('16:10 viewport letterboxes a 16:9 Screen without distortion', () => {
  const result = calculateRuntimeLogicalTransform(1920, 1200, DESIGN.width, DESIGN.height);
  expect(result.scale).toBe(1);
  expect(result.offsetX).toBe(0);
  expect(result.offsetY).toBe(60);
  expect(result.width / result.height).toBeCloseTo(16 / 9, 8);
});

test('logical size is authored per Screen with deterministic 1920x1080 fallback', () => {
  expect(resolveRuntimeLogicalSize({ designWidth: '1600', designHeight: '900' })).toEqual({ width: 1600, height: 900 });
  expect(resolveRuntimeLogicalSize({})).toEqual(DESIGN);
  expect(resolveRuntimeLogicalSize({ designWidth: '-1', designHeight: 'NaN' })).toEqual(DESIGN);
});

test('pointer coordinates invert the exact visual scale and letterbox offset', () => {
  const transform = calculateRuntimeLogicalTransform(1280, 800, DESIGN.width, DESIGN.height);
  const logical = viewportPointToLogical(650, 410, 10, 10, transform);
  expect(logical).not.toBeNull();
  expect(logical!.x).toBeCloseTo(960, 8);
  expect(logical!.y).toBeCloseTo(540, 8);
});
