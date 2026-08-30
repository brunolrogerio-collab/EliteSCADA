import { expect, test } from '@playwright/test';
import {
  normalizeHistoricalQueryResponse,
  type HistoricalQueryResponse
} from '../src/runtime/historical-browser/historicalQueryApi';
import {
  buildHistoricalQueryRequest,
  formatHistoricalQueryValue,
  projectHistoricalQueryResponse
} from '../src/runtime/historical-browser/historicalBrowserQueryAdapter';
import { createHistoricalBrowserDraft } from '../src/runtime/historical-browser/historicalBrowserPresentation';

test('Historical Browser projects the integrated relative query wire contract without private DTO fields', () => {
  const request = buildHistoricalQueryRequest(createHistoricalBrowserDraft(), {
    search: ' Pump  ',
    sort: { field: 'timestamp', direction: 'descending' },
    cursor: 'opaque-cursor'
  });

  expect(request).toEqual({
    version: 1,
    datasetKey: 'historian.samples',
    timeRange: { kind: 'relative', durationSeconds: 3600, anchor: 'now' },
    search: 'Pump',
    orderBy: [{ field: 'timestamp', direction: 'descending' }],
    page: { limit: 100, cursor: 'opaque-cursor' }
  });
  expect(JSON.stringify(request)).not.toContain('offset');
  expect(JSON.stringify(request)).not.toContain('table');
  expect(JSON.stringify(request)).not.toContain('sql');
});

test('Historical Browser converts absolute local inputs to explicit UTC instants before request transport', () => {
  const request = buildHistoricalQueryRequest({
    ...createHistoricalBrowserDraft(),
    timeMode: 'absolute',
    absoluteFromLocal: '2026-08-29T18:00',
    absoluteToLocal: '2026-08-29T19:00'
  });

  expect(request.timeRange.kind).toBe('absolute');
  if (request.timeRange.kind !== 'absolute') throw new Error('Expected absolute range.');
  expect(request.timeRange.fromUtc).toMatch(/Z$/);
  expect(request.timeRange.toUtc).toMatch(/Z$/);
  expect(Date.parse(request.timeRange.fromUtc)).toBeLessThan(Date.parse(request.timeRange.toUtc));
});

test('Historical Browser keeps canonical Int64 query values as exact decimal text', () => {
  expect(formatHistoricalQueryValue({ kind: 'int64', value: '9223372036854775807' })).toBe('9223372036854775807');
  expect(formatHistoricalQueryValue({ kind: 'int64', value: '-9223372036854775808' })).toBe('-9223372036854775808');
  expect(formatHistoricalQueryValue({ kind: 'int64', value: '9.5' })).toBe('Unavailable');
  expect(formatHistoricalQueryValue({ kind: 'boolean', value: 'true' })).toBe('True');
  expect(formatHistoricalQueryValue({ kind: 'null', value: null })).toBe('—');
});

test('Historical Browser validates and projects canonical response columns, operators, rows and opaque cursor', () => {
  const payload = {
    version: 1,
    datasetKey: 'historian.samples',
    columns: [
      { field: 'tag.id', type: 'guid', operators: ['eq', 'notEq', 'in'], filterable: true, sortable: false, searchable: false },
      { field: 'value', type: 'scalar', operators: ['eq', 'notEq', 'in'], filterable: true, sortable: false, searchable: false },
      { field: 'timestamp', type: 'dateTime', operators: ['eq', 'notEq', 'in', 'gt', 'gte', 'lt', 'lte'], filterable: true, sortable: true, searchable: false }
    ],
    rows: [{
      cells: {
        'tag.id': { kind: 'guid', value: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' },
        value: { kind: 'int64', value: '9223372036854775807' },
        timestamp: { kind: 'dateTime', value: '2026-08-30T01:00:00.0000000+00:00' }
      }
    }],
    fromUtc: '2026-08-30T00:00:00.0000000+00:00',
    toUtc: '2026-08-30T01:00:00.0000000+00:00',
    nextCursor: 'opaque-do-not-parse',
    pageSize: 100
  };

  const response = normalizeHistoricalQueryResponse(payload);
  const page = projectHistoricalQueryResponse(response);

  expect(response.columns[0].operators).toEqual(['eq', 'notEq', 'in']);
  expect(page.nextCursor).toBe('opaque-do-not-parse');
  expect(page.rows).toHaveLength(1);
  expect(page.rows[0].cells.value).toBe('9223372036854775807');
  expect(page.columns.find(column => column.key === 'timestamp')?.sortable).toBe(true);
});

test('Historical Browser rejects malformed or unsupported query responses fail-closed', () => {
  expect(() => normalizeHistoricalQueryResponse({ version: 2 })).toThrow('version is unsupported');
  expect(() => normalizeHistoricalQueryResponse({
    version: 1,
    datasetKey: 'raw.table',
    columns: [],
    rows: [],
    fromUtc: '2026-08-30T00:00:00Z',
    toUtc: '2026-08-30T01:00:00Z',
    nextCursor: null,
    pageSize: 100
  })).toThrow('dataset is not allowlisted');

  expect(() => normalizeHistoricalQueryResponse({
    version: 1,
    datasetKey: 'historian.samples',
    columns: [{ field: 'tag.id', type: 'guid', operators: [], filterable: true, sortable: false, searchable: false }],
    rows: [],
    fromUtc: '2026-08-30T00:00:00Z',
    toUtc: '2026-08-30T01:00:00Z',
    nextCursor: null,
    pageSize: 100
  })).toThrow('filter capability is inconsistent');
});

test('Historical Browser projected page remains read-only presentation data', () => {
  const response: HistoricalQueryResponse = {
    version: 1,
    datasetKey: 'alarm.events',
    columns: [
      { field: 'alarm.id', type: 'guid', operators: ['eq', 'notEq', 'in'], filterable: true, sortable: false, searchable: false },
      { field: 'state', type: 'enum', operators: ['eq', 'notEq', 'in'], filterable: true, sortable: true, searchable: false },
      { field: 'timestamp', type: 'dateTime', operators: ['eq', 'notEq', 'in', 'gt', 'gte', 'lt', 'lte'], filterable: true, sortable: true, searchable: false }
    ],
    rows: [{ cells: {
      'alarm.id': { kind: 'guid', value: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' },
      state: { kind: 'enum', value: 'Active' },
      timestamp: { kind: 'dateTime', value: '2026-08-30T01:00:00Z' }
    }}],
    fromUtc: '2026-08-30T00:00:00Z',
    toUtc: '2026-08-30T01:00:00Z',
    nextCursor: null,
    pageSize: 100
  };

  const page = projectHistoricalQueryResponse(response);
  expect(page.rows[0].detail.map(fact => fact.label)).toContain('Alarm / id');
  expect(JSON.stringify(page)).not.toMatch(/acknowledge|shelve|command/i);
});
