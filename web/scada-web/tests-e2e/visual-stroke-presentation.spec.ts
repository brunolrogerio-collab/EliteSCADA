import { expect, test } from '@playwright/test';
import {
  cssStrokeStyle,
  effectiveStrokeWidth,
  normalizeCanonicalStrokeStyle,
  svgStrokeDasharray
} from '../src/engineering/visual-editor/visualStrokePresentation';

test('canonical stroke none disables rendered border or line regardless of configured width', () => {
  const style = normalizeCanonicalStrokeStyle('none');
  expect(cssStrokeStyle(style)).toBe('none');
  expect(effectiveStrokeWidth(style, 5)).toBe(0);
  expect(svgStrokeDasharray(style)).toBeUndefined();
});

test('canonical patterned strokes have deterministic box and SVG presentations', () => {
  expect(cssStrokeStyle(normalizeCanonicalStrokeStyle('solid'))).toBe('solid');
  expect(cssStrokeStyle(normalizeCanonicalStrokeStyle('dotted'))).toBe('dotted');
  expect(cssStrokeStyle(normalizeCanonicalStrokeStyle('dash-dot'))).toBe('dashed');
  expect(cssStrokeStyle(normalizeCanonicalStrokeStyle('dash-dot-dot'))).toBe('dashed');

  expect(svgStrokeDasharray(normalizeCanonicalStrokeStyle('dashed'))).toBe('8 5');
  expect(svgStrokeDasharray(normalizeCanonicalStrokeStyle('dotted'))).toBe('2 4');
  expect(svgStrokeDasharray(normalizeCanonicalStrokeStyle('dash-dot'))).toBe('8 4 2 4');
  expect(svgStrokeDasharray(normalizeCanonicalStrokeStyle('dash-dot-dot'))).toBe('8 4 2 4 2 4');
});

test('unknown stroke presentation fails to the registered default presentation', () => {
  expect(normalizeCanonicalStrokeStyle('future-style')).toBe('solid');
  expect(effectiveStrokeWidth(normalizeCanonicalStrokeStyle('solid'), Number.NaN)).toBe(0);
});
