import type {
  HistoricalColumn,
  HistoricalFieldType,
  HistoricalFilter,
  HistoricalFilterOperator,
  HistoricalQueryValue,
  HistoricalValueKind
} from './historicalQueryApi';

export const HISTORICAL_SCALAR_FILTER_KINDS = Object.freeze([
  'string', 'enum', 'int16', 'int32', 'int64', 'float', 'double', 'number', 'boolean', 'dateTime'
] as const satisfies readonly HistoricalValueKind[]);

export type HistoricalFilterDraft = Readonly<{
  field: string;
  operator: HistoricalFilterOperator | '';
  valueText: string;
  scalarKind: Exclude<(typeof HISTORICAL_SCALAR_FILTER_KINDS)[number], 'null'>;
}>;

export function createHistoricalFilterDraft(columns: readonly HistoricalColumn[]): HistoricalFilterDraft {
  const column = columns.find(candidate => candidate.filterable && candidate.operators.length > 0);
  return Object.freeze({
    field: column?.field ?? '',
    operator: column?.operators[0] ?? '',
    valueText: '',
    scalarKind: 'string'
  });
}

export function filterableHistoricalColumns(columns: readonly HistoricalColumn[]): readonly HistoricalColumn[] {
  return columns.filter(column => column.filterable && column.operators.length > 0);
}

export function operatorsForHistoricalField(
  columns: readonly HistoricalColumn[],
  field: string
): readonly HistoricalFilterOperator[] {
  return columns.find(column => column.field === field)?.operators ?? Object.freeze([]);
}

export function buildHistoricalFilter(
  columns: readonly HistoricalColumn[],
  draft: HistoricalFilterDraft
): HistoricalFilter {
  const column = columns.find(candidate => candidate.field === draft.field && candidate.filterable);
  if (!column) throw new Error('Select a filterable field exposed by the historical dataset schema.');
  if (!draft.operator || !column.operators.includes(draft.operator)) {
    throw new Error('Select an operator allowed by the historical dataset schema.');
  }

  const rawValues = draft.operator === 'in'
    ? draft.valueText.split(',').map(value => value.trim()).filter(Boolean)
    : [draft.valueText.trim()];
  if (rawValues.length === 0) throw new Error('Historical filter value is required.');
  if (rawValues.length > 64) throw new Error('Historical membership filter cannot contain more than 64 values.');

  const values = rawValues.map(value => parseHistoricalFilterValue(column.type, value, draft.scalarKind));
  return Object.freeze({
    field: column.field,
    operator: draft.operator,
    values: Object.freeze(values)
  });
}

export function summarizeHistoricalFilter(filter: HistoricalFilter): string {
  return `${filter.field} ${filter.operator} ${filter.values.map(value => value.value ?? '—').join(', ')}`;
}

function parseHistoricalFilterValue(
  fieldType: HistoricalFieldType,
  text: string,
  scalarKind: HistoricalFilterDraft['scalarKind']
): HistoricalQueryValue {
  if (!text) throw new Error('Historical filter value is required.');

  switch (fieldType) {
    case 'guid':
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(text)) {
        throw new Error('Historical GUID filter value is invalid.');
      }
      return value('guid', text.toLowerCase());
    case 'string': return value('string', text);
    case 'enum': return value('enum', text);
    case 'number': return numericValue('number', text);
    case 'boolean': return booleanValue(text);
    case 'dateTime': return dateTimeValue(text);
    case 'int64': return integerValue('int64', text, -9223372036854775808n, 9223372036854775807n);
    case 'scalar': return parseScalarValue(scalarKind, text);
  }
}

function parseScalarValue(kind: HistoricalFilterDraft['scalarKind'], text: string): HistoricalQueryValue {
  switch (kind) {
    case 'string': return value('string', text);
    case 'enum': return value('enum', text);
    case 'int16': return integerValue('int16', text, -32768n, 32767n);
    case 'int32': return integerValue('int32', text, -2147483648n, 2147483647n);
    case 'int64': return integerValue('int64', text, -9223372036854775808n, 9223372036854775807n);
    case 'float': return numericValue('float', text);
    case 'double': return numericValue('double', text);
    case 'number': return numericValue('number', text);
    case 'boolean': return booleanValue(text);
    case 'dateTime': return dateTimeValue(text);
  }
}

function value(kind: HistoricalValueKind, text: string): HistoricalQueryValue {
  return Object.freeze({ kind, value: text });
}

function integerValue(kind: 'int16' | 'int32' | 'int64', text: string, minimum: bigint, maximum: bigint): HistoricalQueryValue {
  if (!/^-?\d+$/.test(text)) throw new Error(`Historical ${kind} filter value must be an integer.`);
  const parsed = BigInt(text);
  if (parsed < minimum || parsed > maximum) throw new Error(`Historical ${kind} filter value is out of range.`);
  return value(kind, parsed.toString());
}

function numericValue(kind: 'float' | 'double' | 'number', text: string): HistoricalQueryValue {
  const parsed = Number(text);
  if (!Number.isFinite(parsed)) throw new Error(`Historical ${kind} filter value must be finite.`);
  return value(kind, text);
}

function booleanValue(text: string): HistoricalQueryValue {
  const normalized = text.toLowerCase();
  if (normalized !== 'true' && normalized !== 'false') throw new Error('Historical boolean filter value must be true or false.');
  return value('boolean', normalized);
}

function dateTimeValue(text: string): HistoricalQueryValue {
  const parsed = new Date(text);
  if (Number.isNaN(parsed.getTime())) throw new Error('Historical date/time filter value is invalid.');
  return value('dateTime', parsed.toISOString());
}
