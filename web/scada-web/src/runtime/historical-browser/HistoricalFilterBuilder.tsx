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
import {
  historicalBrowserCopy,
  type HistoricalBrowserCopy,
  type HistoricalBrowserLocale
} from './historicalBrowserI18n';

export type HistoricalFilterBuilderProps = Readonly<{
  locale?: HistoricalBrowserLocale;
  columns: readonly HistoricalColumn[];
  filters: readonly HistoricalFilter[];
  disabled?: boolean;
  onFiltersChange: (filters: readonly HistoricalFilter[]) => void;
}>;

export function HistoricalFilterBuilder({
  locale = 'en',
  columns,
  filters,
  disabled = false,
  onFiltersChange
}: HistoricalFilterBuilderProps) {
  const text = historicalBrowserCopy(locale);
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
    } catch {
      setDiagnostic(text.invalidFilter);
    }
  }

  return (
    <section className="historical-browser__filters" aria-label={text.filters}>
      <div className="historical-browser__filter-controls">
        <label>
          {text.filterField}
          <select
            aria-label={text.filterField}
            value={draft.field}
            disabled={disabled || filterableColumns.length === 0}
            onChange={event => {
              const field = event.target.value;
              const nextOperators = operatorsForHistoricalField(columns, field);
              updateDraft({ field, operator: nextOperators[0] ?? '', valueText: '' });
            }}
          >
            {filterableColumns.length === 0 && <option value="">{text.discoverFilterFields}</option>}
            {filterableColumns.map(column => <option key={column.field} value={column.field}>{column.field}</option>)}
          </select>
        </label>

        <label>
          {text.operator}
          <select
            aria-label={text.operator}
            value={draft.operator}
            disabled={disabled || operators.length === 0}
            onChange={event => updateDraft({ operator: event.target.value as HistoricalFilterDraft['operator'] })}
          >
            {operators.map(operator => <option key={operator} value={operator}>{operator}</option>)}
          </select>
        </label>

        {selectedColumn?.type === 'scalar' && (
          <label>
            {text.valueType}
            <select
              aria-label={text.valueType}
              value={draft.scalarKind}
              disabled={disabled}
              onChange={event => updateDraft({ scalarKind: event.target.value as HistoricalFilterDraft['scalarKind'], valueText: '' })}
            >
              {HISTORICAL_SCALAR_FILTER_KINDS.map(kind => <option key={kind} value={kind}>{kind}</option>)}
            </select>
          </label>
        )}

        <FilterValueInput
          text={text}
          type={selectedColumn?.type ?? null}
          scalarKind={draft.scalarKind}
          value={draft.valueText}
          membership={draft.operator === 'in'}
          disabled={disabled || !selectedColumn || !draft.operator}
          onChange={valueText => updateDraft({ valueText })}
        />

        <button type="button" disabled={disabled || !selectedColumn || !draft.operator} onClick={addFilter}>{text.addFilter}</button>
        <button type="button" disabled={disabled || filters.length === 0} onClick={() => onFiltersChange(Object.freeze([]))}>{text.clearFilters}</button>
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
                aria-label={text.removeFilter(index + 1)}
                onClick={() => onFiltersChange(Object.freeze(filters.filter((_, candidate) => candidate !== index)))}
              >
                {text.remove}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function FilterValueInput({
  text,
  type,
  scalarKind,
  value,
  membership,
  disabled,
  onChange
}: Readonly<{
  text: HistoricalBrowserCopy;
  type: HistoricalColumn['type'] | null;
  scalarKind: HistoricalFilterDraft['scalarKind'];
  value: string;
  membership: boolean;
  disabled: boolean;
  onChange: (value: string) => void;
}>) {
  const effectiveType = type === 'scalar' ? scalarKind : type;
  const label = membership ? text.values : text.value;
  if (effectiveType === 'boolean' && !membership) {
    return (
      <label>
        {label}
        <select aria-label={label} value={value} disabled={disabled} onChange={event => onChange(event.target.value)}>
          <option value="">{text.select}</option>
          <option value="true">{text.trueLabel}</option>
          <option value="false">{text.falseLabel}</option>
        </select>
      </label>
    );
  }

  return (
    <label>
      {label}
      <input
        aria-label={label}
        type={effectiveType === 'dateTime' && !membership ? 'datetime-local' : 'text'}
        value={value}
        disabled={disabled}
        placeholder={membership ? text.commaSeparated : undefined}
        onChange={event => onChange(event.target.value)}
      />
    </label>
  );
}
