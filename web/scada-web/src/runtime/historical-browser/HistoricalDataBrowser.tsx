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
 * Presentation shell for Wave 09 Historical Data Browser.
 *
 * The component intentionally owns only transient Runtime view state. The draft
 * emitted by callbacks is not a Historical Query DTO. Once DEV 1's shared API is
 * integrated, an adapter must project this view state into that canonical
 * contract. No endpoint, cursor or filter authority is defined here.
 */
export function HistoricalDataBrowser({
  columns = [],
  rows = [],
  state = 'idle',
  errorMessage = null,
  filterSummary = [],
  onDraftChange,
  onQueryRequested,
  onRefreshRequested
}: HistoricalDataBrowserProps) {
  const [draft, setDraft] = useState<HistoricalBrowserDraft>(() => createHistoricalBrowserDraft());
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const validation = useMemo(() => validateHistoricalBrowserDraft(draft), [draft]);
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
          <h2>Historical Data Browser</h2>
          <p>Read-only exploration of persisted historian samples and alarm events.</p>
        </div>
        <button type="button" onClick={() => onRefreshRequested?.()} disabled={state === 'loading'}>
          Refresh
        </button>
      </header>

      <div className="historical-browser__controls">
        <label>
          Dataset
          <select
            aria-label="Historical dataset"
            value={draft.datasetKey}
            onChange={event => updateDataset(event.target.value as HistoricalBrowserDatasetKey)}
          >
            {HISTORICAL_BROWSER_DATASET_KEYS.map(key => (
              <option key={key} value={key}>{historicalDatasetLabel(key)}</option>
            ))}
          </select>
        </label>

        <fieldset>
          <legend>Period</legend>
          <label>
            <input
              type="radio"
              name="historical-time-mode"
              checked={draft.timeMode === 'relative'}
              onChange={() => updateDraft(Object.freeze({ ...draft, timeMode: 'relative' }))}
            />
            Relative
          </label>
          <label>
            <input
              type="radio"
              name="historical-time-mode"
              checked={draft.timeMode === 'absolute'}
              onChange={() => updateDraft(Object.freeze({ ...draft, timeMode: 'absolute' }))}
            />
            Absolute
          </label>
        </fieldset>

        {draft.timeMode === 'relative' ? (
          <label>
            Relative period
            <select
              aria-label="Relative period"
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
              Start
              <input
                aria-label="Absolute start"
                type="datetime-local"
                value={draft.absoluteFromLocal}
                onChange={event => updateDraft(Object.freeze({ ...draft, absoluteFromLocal: event.target.value }))}
              />
            </label>
            <label>
              End
              <input
                aria-label="Absolute end"
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
          Query
        </button>
      </div>

      <div className="historical-browser__summary" aria-live="polite">
        <strong>{historicalDatasetLabel(draft.datasetKey)}</strong>
        <span>{historicalTimeSummary(draft)}</span>
        {filterSummary.length > 0 && <span>{filterSummary.join(' · ')}</span>}
      </div>

      {!validation.ok && (
        <div role="alert" className="historical-browser__validation">
          {validation.diagnostics.join(' ')}
        </div>
      )}

      <HistoricalBrowserResultState state={state} errorMessage={errorMessage} rowCount={rows.length} />

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
                      <td key={column.key}>{formatHistoricalScalar(row.cells[column.key], column.scalarType)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {selectedRow?.detail && selectedRow.detail.length > 0 && (
            <aside className="historical-browser__detail" data-testid="historical-row-detail">
              <h3>Historical record</h3>
              <p className="historical-browser__readonly-note">Read-only context. Operational alarm commands are not available here.</p>
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
  rowCount
}: Readonly<{ state: HistoricalBrowserViewState; errorMessage: string | null; rowCount: number }>) {
  if (state === 'loading') return <p role="status">Loading historical data…</p>;
  if (state === 'unauthorized') return <p role="alert">Not authorized to query this historical dataset.</p>;
  if (state === 'error') return <p role="alert">{errorMessage?.trim() || 'Historical query failed.'}</p>;
  if (state === 'empty' || (state === 'ready' && rowCount === 0)) return <p role="status">No historical records matched the current view.</p>;
  if (state === 'idle' && rowCount === 0) return <p role="status">Choose a dataset and period, then run a query.</p>;
  return null;
}
