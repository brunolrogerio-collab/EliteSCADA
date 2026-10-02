import { expect, test } from '@playwright/test';
import {
  HISTORICAL_BROWSER_DATASET_KEYS,
  HISTORICAL_BROWSER_RELATIVE_PRESETS,
  createHistoricalBrowserDraft,
  formatHistoricalScalar,
  historicalBrowserDurationSeconds,
  historicalDatasetLabel,
  historicalTimeSummary,
  validateHistoricalBrowserDraft
} from '../src/runtime/historical-browser/historicalBrowserPresentation';

test('Historical Browser exposes the canonical historian, alarm and operational event dataset keys', () => {
  expect(HISTORICAL_BROWSER_DATASET_KEYS).toEqual([
    'historian.samples',
    'alarm.events',
    'operational.events'
  ]);
  expect(historicalDatasetLabel('historian.samples')).toBe('Historian samples');
  expect(historicalDatasetLabel('alarm.events')).toBe('Alarm events');
  expect(historicalDatasetLabel('operational.events')).toBe('Operational events');
});

test('Historical Browser transient draft defaults to a bounded relative period without becoming a query DTO', () => {
  const draft = createHistoricalBrowserDraft();
  expect(draft).toMatchObject({
    datasetKey: 'historian.samples',
    timeMode: 'relative',
    relativeAmount: 1,
    relativeUnit: 'hours'
  });
  expect(historicalBrowserDurationSeconds(draft)).toBe(3600);
  expect(validateHistoricalBrowserDraft(draft)).toEqual({ ok: true, diagnostics: [] });
  expect(historicalTimeSummary(draft)).toBe('Last 1 h');
  expect(HISTORICAL_BROWSER_RELATIVE_PRESETS.map(item => item.seconds)).toEqual([
    900,
    3600,
    28800,
    86400
  ]);
});

test('Historical Browser preflight validates custom, oversized and reversed ranges before request', () => {
  expect(validateHistoricalBrowserDraft({
    ...createHistoricalBrowserDraft(),
    relativeAmount: 0
  })).toEqual({
    ok: false,
    diagnostics: ['Period amount must be a positive whole number.']
  });

  expect(validateHistoricalBrowserDraft({
    ...createHistoricalBrowserDraft(),
    relativeAmount: 32,
    relativeUnit: 'days'
  })).toEqual({
    ok: false,
    diagnostics: ['The period cannot exceed 31 days.']
  });

  expect(validateHistoricalBrowserDraft({
    ...createHistoricalBrowserDraft(),
    timeMode: 'absolute',
    absoluteFromLocal: '2026-08-29T20:00:00',
    absoluteToLocal: '2026-08-29T19:00:00'
  })).toEqual({
    ok: false,
    diagnostics: ['From must be before To.']
  });
});

test('Historical Browser keeps history ranges relative or custom and does not expose Live', () => {
  const relative = { ...createHistoricalBrowserDraft(), relativeAmount: 15, relativeUnit: 'minutes' as const };
  expect(historicalBrowserDurationSeconds(relative)).toBe(900);
  expect(validateHistoricalBrowserDraft(relative).ok).toBe(true);
  expect(JSON.stringify(relative)).not.toContain('"live"');
  expect(HISTORICAL_BROWSER_RELATIVE_PRESETS.map(item => item.label)).toEqual(['15 min', '1 h', '8 h', '24 h']);
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


test('Historical Browser keeps the simplified dataset vocabulary equivalent in pt-BR, en and es', () => {
  expect([
    historicalDatasetLabel('historian.samples', 'pt-BR'),
    historicalDatasetLabel('alarm.events', 'pt-BR'),
    historicalDatasetLabel('operational.events', 'pt-BR')
  ]).toEqual(['Valores de TAGs', 'Alarmes', 'Eventos']);
  expect([
    historicalDatasetLabel('historian.samples', 'en'),
    historicalDatasetLabel('alarm.events', 'en'),
    historicalDatasetLabel('operational.events', 'en')
  ]).toEqual(['TAG values', 'Alarms', 'Events']);
  expect([
    historicalDatasetLabel('historian.samples', 'es'),
    historicalDatasetLabel('alarm.events', 'es'),
    historicalDatasetLabel('operational.events', 'es')
  ]).toEqual(['Valores de TAGs', 'Alarmas', 'Eventos']);
});
