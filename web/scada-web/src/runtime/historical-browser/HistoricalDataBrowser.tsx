import { useMemo, useState } from 'react';
import {
  HISTORICAL_BROWSER_DATASET_KEYS,
  HISTORICAL_BROWSER_RELATIVE_PRESETS,
  createHistoricalBrowserDraft,
  formatHistoricalScalar,
  historicalDatasetLabel,
  historicalTimeSummary,
  validateHistoricalBrowserDraft,
  type HistoricalBrowserDraft,
  type HistoricalBrowserDatasetKey,
  type HistoricalScalarType
} from './historicalBrowserPresentation';
import {
  historicalBrowserCopy,
  type HistoricalBrowserLocale
} from './historicalBrowserI18n';
import './historical-data-browser.css';

export type HistoricalBrowserViewState = 'idle' | 'loading' | 'ready' | 'empty' | 'error' | 'unauthorized';

export type HistoricalBrowserColumn = Readonly<{
  key: string;
  label: string;
  scalarType: HistoricalScalarType;
}>;

export type HistoricalBrowserRow = Readonly<{
  id: string;
  cells: Readonly<Record<string, unknown>>;
  detail?: readonly HistoricalBrowserDetailFact[];
}>;

export type HistoricalBrowserDetailFact = Readonly<{
  label: string;
  value: string;
}>;

export type HistoricalDataBrowserProps = Readonly<{
  locale?: HistoricalBrowserLocale;
  columns?: readonly HistoricalBrowserColumn[];
  rows?: readonly HistoricalBrowserRow[];
  state?: HistoricalBrowserViewState;
  errorMessage?: string | null;
  filterSummary?: readonly string[];
  onDraftChange?: (draft: HistoricalBrowserDraft) => void;
  onQueryRequested?: (draft: HistoricalBrowserDraft) => void;
  onRefreshRequested?: () => void;
}>;

/**
 * Presentation shell for the Historical Data Browser. It owns only transient
 * view state; the shared Historical Query v1 contract remains API authority.
 */
export function HistoricalDataBrowser({
  locale = 'en',
  columns = [],
  rows = [],
  state = 'idle',
  errorMessage = null,
  filterSummary = [],
  onDraftChange,
  onQueryRequested,
  onRefreshRequested
}: HistoricalDataBrowserProps) {
  const text = historicalBrowserCopy(locale);
  const [draft, setDraft] = useState<HistoricalBrowserDraft>(() => createHistoricalBrowserDraft());
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const validation = useMemo(() => validateHistoricalBrowserDraft(draft, locale), [draft, locale]);
  const selectedRow = rows.find(row => row.id === selectedRowId) ?? null;

  function updateDraft(next: HistoricalBrowserDraft) {
    setDraft(next);
    onDraftChange?.(next);
  }

  function updateDataset(datasetKey: HistoricalBrowserDatasetKey) {
    updateDraft(Object.freeze({ ...draft, datasetKey }));
    setSelectedRowId(null);
  }

  return (
    <section className="historical-browser" data-testid="historical-data-browser">
      <header className="historical-browser__header">
        <div>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <button type="button" onClick={() => onRefreshRequested?.()} disabled={state === 'loading'}>
          {text.refresh}
        </button>
      </header>

      <div className="historical-browser__controls">
        <label>
          {text.dataset}
          <select
            aria-label={text.dataset}
            value={draft.datasetKey}
            onChange={event => updateDataset(event.target.value as HistoricalBrowserDatasetKey)}
          >
            {HISTORICAL_BROWSER_DATASET_KEYS.map(key => (
              <option key={key} value={key}>{historicalDatasetLabel(key, locale)}</option>
            ))}
          </select>
        </label>

        <fieldset>
          <legend>{text.period}</legend>
          <label>
            <input
              type="radio"
              name="historical-time-mode"
              checked={draft.timeMode === 'relative'}
              onChange={() => updateDraft(Object.freeze({ ...draft, timeMode: 'relative' }))}
            />
            {text.relative}
          </label>
          <label>
            <input
              type="radio"
              name="historical-time-mode"
              checked={draft.timeMode === 'absolute'}
              onChange={() => updateDraft(Object.freeze({ ...draft, timeMode: 'absolute' }))}
            />
            {text.absolute}
          </label>
        </fieldset>

        {draft.timeMode === 'relative' ? (
          <label>
            {text.relativePeriod}
            <select
              aria-label={text.relativePeriod}
              value={draft.relativeDurationSeconds}
              onChange={event => updateDraft(Object.freeze({ ...draft, relativeDurationSeconds: Number(event.target.value) }))}
            >
              {HISTORICAL_BROWSER_RELATIVE_PRESETS.map(preset => (
                <option key={preset.seconds} value={preset.seconds}>{preset.label}</option>
              ))}
            </select>
          </label>
        ) : (
          <div className="historical-browser__absolute-period">
            <label>
              {text.start}
              <input
                aria-label={text.start}
                type="datetime-local"
                value={draft.absoluteFromLocal}
                onChange={event => updateDraft(Object.freeze({ ...draft, absoluteFromLocal: event.target.value }))}
              />
            </label>
            <label>
              {text.end}
              <input
                aria-label={text.end}
                type="datetime-local"
                value={draft.absoluteToLocal}
                onChange={event => updateDraft(Object.freeze({ ...draft, absoluteToLocal: event.target.value }))}
              />
            </label>
          </div>
        )}

        <button
          type="button"
          onClick={() => onQueryRequested?.(draft)}
          disabled={!validation.ok || state === 'loading'}
        >
          {text.query}
        </button>
      </div>

      <div className="historical-browser__summary" aria-live="polite">
        <strong>{historicalDatasetLabel(draft.datasetKey, locale)}</strong>
        <span>{historicalTimeSummary(draft, locale)}</span>
        {filterSummary.length > 0 && <span>{filterSummary.join(' · ')}</span>}
      </div>

      {!validation.ok && (
        <div role="alert" className="historical-browser__validation">
          {validation.diagnostics.join(' ')}
        </div>
      )}

      <HistoricalBrowserResultState
        state={state}
        errorMessage={errorMessage}
        rowCount={rows.length}
        locale={locale}
      />

      {(state === 'ready' || (state === 'idle' && rows.length > 0)) && rows.length > 0 && (
        <div className="historical-browser__content">
          <div className="historical-browser__table-wrap">
            <table>
              <thead>
                <tr>
                  {columns.map(column => <th key={column.key} scope="col">{column.label}</th>)}
                </tr>
              </thead>
              <tbody>
                {rows.map(row => (
                  <tr
                    key={row.id}
                    tabIndex={0}
                    aria-selected={row.id === selectedRowId}
                    onClick={() => setSelectedRowId(row.id)}
                    onKeyDown={event => {
                      if (event.key === 'Enter' || event.key === ' ') setSelectedRowId(row.id);
                    }}
                  >
                    {columns.map(column => (
                      <td key={column.key}>{formatHistoricalScalar(row.cells[column.key], column.scalarType, locale)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {selectedRow?.detail && selectedRow.detail.length > 0 && (
            <aside className="historical-browser__detail" data-testid="historical-row-detail">
              <h3>{text.historicalRecord}</h3>
              <p className="historical-browser__readonly-note">{text.readonlyNote}</p>
              <dl>
                {selectedRow.detail.map((fact, index) => (
                  <div key={`${fact.label}-${index}`}>
                    <dt>{fact.label}</dt>
                    <dd>{fact.value}</dd>
                  </div>
                ))}
              </dl>
            </aside>
          )}
        </div>
      )}
    </section>
  );
}

function HistoricalBrowserResultState({
  state,
  errorMessage,
  rowCount,
  locale
}: Readonly<{
  state: HistoricalBrowserViewState;
  errorMessage: string | null;
  rowCount: number;
  locale: HistoricalBrowserLocale;
}>) {
  const text = historicalBrowserCopy(locale);
  if (state === 'loading') return <p role="status">{text.loading}</p>;
  if (state === 'unauthorized') return <p role="alert">{text.unauthorized}</p>;
  if (state === 'error') return <p role="alert">{errorMessage?.trim() || text.queryFailed}</p>;
  if (state === 'empty' || (state === 'ready' && rowCount === 0)) return <p role="status">{text.empty}</p>;
  if (state === 'idle' && rowCount === 0) return <p role="status">{text.idle}</p>;
  return null;
}
