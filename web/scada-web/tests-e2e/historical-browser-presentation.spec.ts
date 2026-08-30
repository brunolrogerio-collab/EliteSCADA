import { expect, test } from '@playwright/test';
import {
  HISTORICAL_BROWSER_DATASET_KEYS,
  HISTORICAL_BROWSER_RELATIVE_PRESETS,
  createHistoricalBrowserDraft,
  formatHistoricalScalar,
  historicalDatasetLabel,
  historicalTimeSummary,
  validateHistoricalBrowserDraft
} from '../src/runtime/historical-browser/historicalBrowserPresentation';

test('Historical Browser exposes only the two canonical Wave 09 dataset keys', () => {
  expect(HISTORICAL_BROWSER_DATASET_KEYS).toEqual(['historian.samples', 'alarm.events']);
  expect(historicalDatasetLabel('historian.samples')).toBe('Historian samples');
  expect(historicalDatasetLabel('alarm.events')).toBe('Alarm events');
});

test('Historical Browser transient draft defaults to a bounded relative period without becoming a query DTO', () => {
  const draft = createHistoricalBrowserDraft();
  expect(draft).toMatchObject({
    datasetKey: 'historian.samples',
    timeMode: 'relative',
    relativeDurationSeconds: 3600
  });
  expect(validateHistoricalBrowserDraft(draft)).toEqual({ ok: true, diagnostics: [] });
  expect(historicalTimeSummary(draft)).toBe('Last 1 h');
  expect(HISTORICAL_BROWSER_RELATIVE_PRESETS.map(item => item.seconds)).toEqual([
    900,
    3600,
    28800,
    86400,
    604800
  ]);
});

test('Historical Browser preflight rejects invalid local periods before a future shared query request', () => {
  expect(validateHistoricalBrowserDraft({
    ...createHistoricalBrowserDraft(),
    relativeDurationSeconds: 0
  })).toEqual({
    ok: false,
    diagnostics: ['Relative period must be a positive whole number of seconds.']
  });

  expect(validateHistoricalBrowserDraft({
    ...createHistoricalBrowserDraft(),
    timeMode: 'absolute',
    absoluteFromLocal: '2026-08-29T20:00',
    absoluteToLocal: '2026-08-29T19:00'
  })).toEqual({
    ok: false,
    diagnostics: ['Absolute period start must be before end.']
  });
});

test('Historical Browser preserves exact Int64 wire text without JavaScript Number precision loss', () => {
  expect(formatHistoricalScalar('9223372036854775807', 'Int64')).toBe('9223372036854775807');
  expect(formatHistoricalScalar('-9223372036854775808', 'Int64')).toBe('-9223372036854775808');
  expect(formatHistoricalScalar(9223372036854775807n, 'Int64')).toBe('Unavailable');
  expect(formatHistoricalScalar('1.5', 'Int64')).toBe('Unavailable');
});

test('Historical Browser scalar presentation remains typed and fail-closed', () => {
  expect(formatHistoricalScalar(true, 'Boolean')).toBe('True');
  expect(formatHistoricalScalar(false, 'Boolean')).toBe('False');
  expect(formatHistoricalScalar('false', 'Boolean')).toBe('Unavailable');
  expect(formatHistoricalScalar(42, 'Int32')).toBe('42');
  expect(formatHistoricalScalar(1.25, 'Int32')).toBe('Unavailable');
  expect(formatHistoricalScalar(Number.POSITIVE_INFINITY, 'Double')).toBe('Unavailable');
  expect(formatHistoricalScalar(null, 'String')).toBe('—');
  expect(formatHistoricalScalar('2026-08-29T23:00:00Z', 'DateTime')).toBe('2026-08-29T23:00:00Z');
});
