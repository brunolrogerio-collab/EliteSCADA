import { expect, test } from '@playwright/test';
import {
  ANALOG_FILL_DIRECTIONS,
  computeAnalogFillPresentation
} from '../src/engineering/visual-editor/analogFillPresentation';

test('Analog Fill maps the canonical scale deterministically and clamps by default', () => {
  expect(computeAnalogFillPresentation({
    value: 0,
    inputMinimum: 0,
    inputMaximum: 100,
    direction: 'BottomToTop'
  })).toMatchObject({ normalized: 0, percent: 0 });

  expect(computeAnalogFillPresentation({
    value: 50,
    inputMinimum: 0,
    inputMaximum: 100,
    direction: 'BottomToTop'
  })).toMatchObject({ normalized: 0.5, percent: 50 });

  expect(computeAnalogFillPresentation({
    value: 150,
    inputMinimum: 0,
    inputMaximum: 100,
    direction: 'BottomToTop'
  })).toMatchObject({ normalized: 1, percent: 100 });

  expect(computeAnalogFillPresentation({
    value: -50,
    inputMinimum: 0,
    inputMaximum: 100,
    direction: 'BottomToTop'
  })).toMatchObject({ normalized: 0, percent: 0 });
});

test('Analog Fill accepts the four canonical Engineering directions', () => {
  const clipPaths = ANALOG_FILL_DIRECTIONS.map(direction => computeAnalogFillPresentation({
    value: 25,
    inputMinimum: 0,
    inputMaximum: 100,
    direction
  }).clipPath);

  expect(clipPaths).toEqual([
    'inset(75% 0 0 0)',
    'inset(0 0 75% 0)',
    'inset(0 75% 0 0)',
    'inset(0 0 0 75%)'
  ]);
});

test('Analog Fill explicit inversion reverses scale before normal clamping', () => {
  expect(computeAnalogFillPresentation({
    value: 25,
    inputMinimum: 0,
    inputMaximum: 100,
    direction: 'LeftToRight',
    invertScale: true
  })).toEqual({
    normalized: 0.75,
    percent: 75,
    clipPath: 'inset(0 25% 0 0)'
  });
});

test('Analog Fill can deliberately disable clamping without changing canonical direction semantics', () => {
  expect(computeAnalogFillPresentation({
    value: 125,
    inputMinimum: 0,
    inputMaximum: 100,
    direction: 'RightToLeft',
    clamp: false
  })).toEqual({
    normalized: 1.25,
    percent: 125,
    clipPath: 'inset(0 0 0 -25%)'
  });
});

test('Analog Fill fails closed for invalid scale and non-finite values', () => {
  expect(() => computeAnalogFillPresentation({
    value: 1,
    inputMinimum: 1,
    inputMaximum: 1,
    direction: 'BottomToTop'
  })).toThrow(/minimum and maximum must be different/);

  expect(() => computeAnalogFillPresentation({
    value: Number.NaN,
    inputMinimum: 0,
    inputMaximum: 1,
    direction: 'TopToBottom'
  })).toThrow(/must be finite/);
});
