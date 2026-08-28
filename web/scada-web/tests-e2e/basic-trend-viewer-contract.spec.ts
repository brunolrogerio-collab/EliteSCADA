import { expect, test } from '@playwright/test';
import { buildTrendHistoryPath } from '../src/runtime/trendApi';
import {
  MAX_TREND_SAMPLES,
  buildBasicTrendRange,
  buildTrendPlot,
  clampTrendSampleLimit,
  summarizeTrendSamples,
  trendNumericValue,
  trendQualityTone,
  validateTrendRange
} from '../src/runtime/trendModel';
import type { RuntimeTagHistorySample, RuntimeTagListItem } from '../src/runtime/trendTypes';

function sample(
  value: unknown,
  timestamp: string,
  quality: string | number = 'Good',
  tagId = 'tag-1'
): RuntimeTagHistorySample {
  return { tagId, value, timestamp, quality, source: 'builtin.simulation' };
}

test('builds bounded live and historical windows without querying the future', () => {
  const now = new Date('2026-08-27T20:30:00Z');
  const live = buildBasicTrendRange('live', '1h', null, now);
  expect(live.from).toBe('2026-08-27T19:30:00.000Z');
  expect(live.to).toBe('2026-08-27T20:30:00.000Z');

  const historical = buildBasicTrendRange('historical', '6h', new Date('2026-08-27T18:00:00Z'), now);
  expect(historical.from).toBe('2026-08-27T12:00:00.000Z');
  expect(historical.to).toBe('2026-08-27T18:00:00.000Z');

  const future = buildBasicTrendRange('historical', '24h', new Date('2026-08-29T18:00:00Z'), now);
  expect(future.to).toBe(now.toISOString());
  expect(() => validateTrendRange('2026-08-26T20:29:59Z', now.toISOString())).toThrow(/24 hours/);
  expect(clampTrendSampleLimit(50_000)).toBe(MAX_TREND_SAMPLES);
});

test('preserves TAG quality semantics and refuses string-to-number chart coercion', () => {
  expect(trendQualityTone('Good')).toBe('good');
  expect(trendQualityTone('Uncertain')).toBe('attention');
  expect(trendQualityTone('Stale')).toBe('attention');
  expect(trendQualityTone('BadCommunication')).toBe('bad');
  expect(trendQualityTone('future-quality')).toBe('unknown');
  expect(trendNumericValue(12.5)).toBe(12.5);
  expect(trendNumericValue(true)).toBe(1);
  expect(trendNumericValue('12.5')).toBeNull();
});

test('summarizes ordered samples and builds a finite numeric plot', () => {
  const samples = [
    sample(30, '2026-08-27T18:02:00Z', 'BadCommunication'),
    sample(10, '2026-08-27T18:00:00Z', 'Good'),
    sample(20, '2026-08-27T18:01:00Z', 'Stale')
  ];

  const summary = summarizeTrendSamples(samples);
  expect(summary).toMatchObject({ total: 3, good: 1, attention: 1, bad: 1, numericCount: 3, minimum: 10, maximum: 30, latestValue: 30 });

  const plot = buildTrendPlot(samples);
  expect(plot.points).toHaveLength(3);
  expect(plot.points.map(point => point.value)).toEqual([10, 20, 30]);
  expect(plot.points.every(point => Number.isFinite(point.x) && Number.isFinite(point.y))).toBeTruthy();
});

test('history path carries an explicit bounded from/to/limit contract', () => {
  const path = buildTrendHistoryPath('10000000-0000-0000-0000-000000000001', '2026-08-27T18:00:00Z', '2026-08-27T18:15:00Z', 5000);
  const url = new URL(path, 'http://localhost');
  expect(url.pathname).toBe('/api/history/10000000-0000-0000-0000-000000000001');
  expect(url.searchParams.get('from')).toBe('2026-08-27T18:00:00Z');
  expect(url.searchParams.get('to')).toBe('2026-08-27T18:15:00Z');
  expect(url.searchParams.get('limit')).toBe(String(MAX_TREND_SAMPLES));
});

test('protected TAG catalog and Historian expose the facts required by the basic Trend', async ({ request }) => {
  const tagsResponse = await request.get('/api/tags');
  expect(tagsResponse.ok()).toBeTruthy();
  const tags = await tagsResponse.json() as RuntimeTagListItem[];
  expect(tags.length).toBeGreaterThan(0);

  const selected = tags.find(tag => tag.path === 'Demo.Tank01.Level') ?? tags[0];
  expect(selected.id).toBeTruthy();
  expect(selected.path).toBeTruthy();

  let history: RuntimeTagHistorySample[] = [];
  await expect.poll(async () => {
    const end = new Date();
    const start = new Date(end.getTime() - 15 * 60_000);
    const response = await request.get(buildTrendHistoryPath(selected.id, start.toISOString(), end.toISOString(), 200));
    if (!response.ok()) return 0;
    history = await response.json() as RuntimeTagHistorySample[];
    return history.length;
  }, { timeout: 12_000 }).toBeGreaterThan(0);

  expect(history.length).toBeLessThanOrEqual(200);
  expect(history.every(item => item.tagId === selected.id)).toBeTruthy();
  expect(history.every(item => Boolean(item.timestamp))).toBeTruthy();
  expect(history.every((item, index) => index === 0 || new Date(item.timestamp).getTime() >= new Date(history[index - 1].timestamp).getTime())).toBeTruthy();
});
