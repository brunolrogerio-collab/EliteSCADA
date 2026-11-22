import { expect, test } from '@playwright/test';
import {
  HISTORICAL_MAX_RANGE_SECONDS,
  applyHistoricalPreset,
  createHistoricalTimeRangeState,
  historicalDurationSeconds,
  historicalTimeRangeToQuery,
  resolveHistoricalTimeRange,
  validateHistoricalTimeRange
} from '../src/runtime/historicalTimeRange';

test('shared time-range presets cover live/relative quick ranges including seven days', () => {
  let range = createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: 15 * 60, now: new Date('2026-09-30T12:00:00Z') });
  expect(historicalDurationSeconds(range)).toBe(900);
  range = applyHistoricalPreset(range, 7 * 24 * 60 * 60);
  expect(historicalDurationSeconds(range)).toBe(604800);
  expect(validateHistoricalTimeRange(range).ok).toBe(true);
});

test('shared relative query is bounded and resolves deterministically at a supplied now', () => {
  const range = createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: 6 * 60 * 60 });
  expect(historicalTimeRangeToQuery(range)).toEqual({ kind: 'relative', durationSeconds: 21600, anchor: 'now' });
  expect(resolveHistoricalTimeRange(range, new Date('2026-09-30T12:00:00Z'))).toEqual({
    from: '2026-09-30T06:00:00.000Z',
    to: '2026-09-30T12:00:00.000Z'
  });
});

test('shared absolute range accepts minute precision and defaults missing seconds to zero', () => {
  const range = {
    mode: 'absolute' as const,
    relativeAmount: 1,
    relativeUnit: 'hours' as const,
    absoluteFromLocal: '2026-08-29T18:00',
    absoluteToLocal: '2026-08-29T19:00'
  };

  expect(validateHistoricalTimeRange(range).ok).toBe(true);
  const query = historicalTimeRangeToQuery(range);
  expect(query.kind).toBe('absolute');
  if (query.kind !== 'absolute') throw new Error('Expected absolute range.');
  expect(Date.parse(query.toUtc) - Date.parse(query.fromUtc)).toBe(60 * 60 * 1000);
});

test('shared validation blocks equal/reversed absolute boundaries and oversized ranges before query', () => {
  const equal = {
    mode: 'absolute' as const,
    relativeAmount: 1,
    relativeUnit: 'hours' as const,
    absoluteFromLocal: '2026-09-28T12:00:00',
    absoluteToLocal: '2026-09-28T12:00:00'
  };
  expect(validateHistoricalTimeRange(equal).issues).toContain('absolute-order');
  expect(() => historicalTimeRangeToQuery(equal)).toThrow();

  const oversized = {
    ...createHistoricalTimeRangeState({ mode: 'relative', durationSeconds: 3600 }),
    relativeAmount: HISTORICAL_MAX_RANGE_SECONDS + 1,
    relativeUnit: 'seconds' as const
  };
  expect(validateHistoricalTimeRange(oversized).issues).toContain('range-too-large');
});
