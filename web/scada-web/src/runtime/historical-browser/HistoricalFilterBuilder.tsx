import { useEffect, useMemo, useState } from 'react';
import type { HistoricalColumn, HistoricalFilter } from './historicalQueryApi';
import {
  HISTORICAL_SCALAR_FILTER_KINDS,
  buildHistoricalFilter,
  createHistoricalFilterDraft,
  filterableHistoricalColumns,
  operatorsForHistoricalField,
  summarizeHistoricalFilter,
  type HistoricalFilterDraft
} from './historicalBrowserFilters';

export type HistoricalFilterBuilderProps = Readonly<{
  columns: readonly HistoricalColumn[];
  filters: readonly HistoricalFilter[];
  disabled?: boolean;
  onFiltersChange: (filters: readonly HistoricalFilter[]) => void;
}>;

export function HistoricalFilterBuilder({
  columns,
  filters,
  disabled = false,
  onFiltersChange
}: HistoricalFilterBuilderProps) {
  const filterableColumns = useMemo(() => filterableHistoricalColumns(columns), [columns]);
  const [draft, setDraft] = useState<HistoricalFilterDraft>(() => createHistoricalFilterDraft(columns));
  const [diagnostic, setDiagnostic] = useState<string | null>(null);
  const selectedColumn = columns.find(column => column.field === draft.field) ?? null;
  const operators = operatorsForHistoricalField(columns, draft.field);

  useEffect(() => {
    setDraft(createHistoricalFilterDraft(columns));
    setDiagnostic(null);
  }, [columns]);

  function updateDraft(patch: Partial<HistoricalFilterDraft>) {
    setDraft(current => Object.freeze({ ...current, ...patch }));
    setDiagnostic(null);
  }

  function addFilter() {
    try {
      const filter = buildHistoricalFilter(columns, draft);
      onFiltersChange(Object.freeze([...filters, filter]));
      setDraft(current => Object.freeze({ ...current, valueText: '' }));
      setDiagnostic(null);
    } catch (error) {
      setDiagnostic(error instanceof Error ? error.message : 'Historical filter is invalid.');
    }
  }

  return (
    <section className="historical-browser__filters" aria-label="Historical filters">
      <div className="historical-browser__filter-controls">
        <label>
          Filter field
          <select
            aria-label="Historical filter field"
            value={draft.field}
            disabled={disabled || filterableColumns.length === 0}
            onChange={event => {
              const field = event.target.value;
              const nextOperators = operatorsForHistoricalField(columns, field);
              updateDraft({ field, operator: nextOperators[0] ?? '', valueText: '' });
            }}
          >
            {filterableColumns.length === 0 && <option value="">Run a query to discover filterable fields</option>}
            {filterableColumns.map(column => <option key={column.field} value={column.field}>{column.field}</option>)}
          </select>
        </label>

        <label>
          Operator
          <select
            aria-label="Historical filter operator"
            value={draft.operator}
            disabled={disabled || operators.length === 0}
            onChange={event => updateDraft({ operator: event.target.value as HistoricalFilterDraft['operator'] })}
          >
            {operators.map(operator => <option key={operator} value={operator}>{operator}</option>)}
          </select>
        </label>

        {selectedColumn?.type === 'scalar' && (
          <label>
            Value type
            <select
              aria-label="Historical scalar filter type"
              value={draft.scalarKind}
              disabled={disabled}
              onChange={event => updateDraft({ scalarKind: event.target.value as HistoricalFilterDraft['scalarKind'], valueText: '' })}
            >
              {HISTORICAL_SCALAR_FILTER_KINDS.map(kind => <option key={kind} value={kind}>{kind}</option>)}
            </select>
          </label>
        )}

        <FilterValueInput
          type={selectedColumn?.type ?? null}
          scalarKind={draft.scalarKind}
          value={draft.valueText}
          membership={draft.operator === 'in'}
          disabled={disabled || !selectedColumn || !draft.operator}
          onChange={valueText => updateDraft({ valueText })}
        />

        <button type="button" disabled={disabled || !selectedColumn || !draft.operator} onClick={addFilter}>Add filter</button>
        <button type="button" disabled={disabled || filters.length === 0} onClick={() => onFiltersChange(Object.freeze([]))}>Clear filters</button>
      </div>

      {diagnostic && <p role="alert">{diagnostic}</p>}

      {filters.length > 0 && (
        <ul className="historical-browser__filter-list">
          {filters.map((filter, index) => (
            <li key={`${filter.field}-${filter.operator}-${index}`}>
              <span>{summarizeHistoricalFilter(filter)}</span>
              <button
                type="button"
                disabled={disabled}
                aria-label={`Remove historical filter ${index + 1}`}
                onClick={() => onFiltersChange(Object.freeze(filters.filter((_, candidate) => candidate !== index)))}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function FilterValueInput({
  type,
  scalarKind,
  value,
  membership,
  disabled,
  onChange
}: Readonly<{
  type: HistoricalColumn['type'] | null;
  scalarKind: HistoricalFilterDraft['scalarKind'];
  value: string;
  membership: boolean;
  disabled: boolean;
  onChange: (value: string) => void;
}>) {
  const effectiveType = type === 'scalar' ? scalarKind : type;
  if (effectiveType === 'boolean' && !membership) {
    return (
      <label>
        Value
        <select aria-label="Historical filter value" value={value} disabled={disabled} onChange={event => onChange(event.target.value)}>
          <option value="">Select</option>
          <option value="true">true</option>
          <option value="false">false</option>
        </select>
      </label>
    );
  }

  return (
    <label>
      Value{membership ? 's' : ''}
      <input
        aria-label="Historical filter value"
        type={effectiveType === 'dateTime' && !membership ? 'datetime-local' : 'text'}
        value={value}
        disabled={disabled}
        placeholder={membership ? 'Comma-separated values' : undefined}
        onChange={event => onChange(event.target.value)}
      />
    </label>
  );
}
