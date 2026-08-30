import { expect, test } from '@playwright/test';
import {
  buildHistoricalFilter,
  createHistoricalFilterDraft,
  operatorsForHistoricalField
} from '../src/runtime/historical-browser/historicalBrowserFilters';
import type { HistoricalColumn } from '../src/runtime/historical-browser/historicalQueryApi';

const columns: readonly HistoricalColumn[] = Object.freeze([
  Object.freeze({ field: 'tag.id', type: 'guid', operators: Object.freeze(['eq', 'notEq', 'in']), filterable: true, sortable: false, searchable: false }),
  Object.freeze({ field: 'tag.path', type: 'string', operators: Object.freeze(['eq', 'notEq', 'in', 'contains', 'startsWith']), filterable: true, sortable: false, searchable: true }),
  Object.freeze({ field: 'priority', type: 'number', operators: Object.freeze(['eq', 'notEq', 'in', 'gt', 'gte', 'lt', 'lte']), filterable: true, sortable: true, searchable: false }),
  Object.freeze({ field: 'value', type: 'scalar', operators: Object.freeze(['eq', 'notEq', 'in']), filterable: true, sortable: false, searchable: false })
]);

test('Historical filter builder consumes operators directly from server column metadata', () => {
  expect(operatorsForHistoricalField(columns, 'tag.path')).toEqual(['eq', 'notEq', 'in', 'contains', 'startsWith']);
  expect(createHistoricalFilterDraft(columns)).toMatchObject({ field: 'tag.id', operator: 'eq' });
});

test('Historical filter builder emits typed values without implicit coercion', () => {
  expect(buildHistoricalFilter(columns, {
    field: 'priority', operator: 'gte', valueText: '800', scalarKind: 'string'
  })).toEqual({
    field: 'priority', operator: 'gte', values: [{ kind: 'number', value: '800' }]
  });

  expect(buildHistoricalFilter(columns, {
    field: 'value', operator: 'eq', valueText: '9223372036854775807', scalarKind: 'int64'
  })).toEqual({
    field: 'value', operator: 'eq', values: [{ kind: 'int64', value: '9223372036854775807' }]
  });
});

test('Historical filter builder rejects operators absent from schema and unsafe Int64 input', () => {
  expect(() => buildHistoricalFilter(columns, {
    field: 'tag.id', operator: 'contains', valueText: 'abc', scalarKind: 'string'
  })).toThrow('operator allowed by the historical dataset schema');

  expect(() => buildHistoricalFilter(columns, {
    field: 'value', operator: 'eq', valueText: '9223372036854775808', scalarKind: 'int64'
  })).toThrow('out of range');
});

test('Historical membership filter remains bounded and typed', () => {
  expect(buildHistoricalFilter(columns, {
    field: 'tag.path', operator: 'in', valueText: 'Demo.Flow, Demo.Level', scalarKind: 'string'
  })).toEqual({
    field: 'tag.path', operator: 'in', values: [
      { kind: 'string', value: 'Demo.Flow' },
      { kind: 'string', value: 'Demo.Level' }
    ]
  });
});
