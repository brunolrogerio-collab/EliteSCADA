import { useMemo, useState, type ReactNode } from 'react';
import { localTimeZoneLabel } from '../historicalTimeRange';
import {
  HISTORICAL_BROWSER_DATASET_KEYS,
  HISTORICAL_BROWSER_RELATIVE_PRESETS,
  applyHistoricalBrowserPreset,
  createHistoricalBrowserDraft,
  formatHistoricalScalar,
  historicalBrowserPresetSeconds,
  historicalDatasetLabel,
  historicalTimeSummary,
  validateHistoricalBrowserDraft,
  type HistoricalBrowserDraft,
  type HistoricalBrowserDatasetKey
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
  scalarType: 'Boolean' | 'Int16' | 'Int32' | 'Int64' | 'Float' | 'Double' | 'String' | 'DateTime';
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
  advancedControls?: ReactNode;
  onDraftChange?: (draft: HistoricalBrowserDraft) => void;
  onQueryRequested?: (draft: HistoricalBrowserDraft) => void;
}>;

export function HistoricalDataBrowser({
  locale = 'en',
  columns = [],
  rows = [],
  state = 'idle',
  errorMessage = null,
  filterSummary = [],
  advancedControls,
  onDraftChange,
  onQueryRequested
}: HistoricalDataBrowserProps) {
  const text = historicalBrowserCopy(locale);
  const [draft, setDraft] = useState<HistoricalBrowserDraft>(() => createHistoricalBrowserDraft());
  const [selectedRowId, setSelectedRowId] = useState<string | null>(null);
  const validation = useMemo(() => validateHistoricalBrowserDraft(draft, locale), [draft, locale]);
  const selectedRow = rows.find(row => row.id === selectedRowId) ?? null;
  const presetSeconds = historicalBrowserPresetSeconds(draft);

  function updateDraft(next: HistoricalBrowserDraft) {
    setDraft(next);
    onDraftChange?.(next);
  }

  function updateDataset(datasetKey: HistoricalBrowserDatasetKey) {
    updateDraft(Object.freeze({ ...draft, datasetKey }));
    setSelectedRowId(null);
  }

  function selectPreset(seconds: number) {
    updateDraft(applyHistoricalBrowserPreset(draft, seconds));
  }

  function selectCustom() {
    updateDraft(Object.freeze({ ...draft, timeMode: 'absolute' }));
  }

  return (
    <section className="historical-browser" data-testid="historical-data-browser">
      <header className="historical-browser__header">
        <div>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
      </header>

      <div className="historical-browser__primary">
        <fieldset className="historical-browser__choice">
          <legend>{text.dataset}</legend>
          <div className="historical-browser__segmented" role="group" aria-label={text.dataset}>
            {HISTORICAL_BROWSER_DATASET_KEYS.map(key => (
              <button
                key={key}
                type="button"
                className={draft.datasetKey === key ? 'active' : undefined}
                aria-pressed={draft.datasetKey === key}
                onClick={() => updateDataset(key)}
              >
                {historicalDatasetLabel(key, locale)}
              </button>
            ))}
          </div>
        </fieldset>

        <fieldset className="historical-browser__choice">
          <legend>{text.period}</legend>
          <div className="historical-browser__segmented" role="group" aria-label={text.period}>
            {HISTORICAL_BROWSER_RELATIVE_PRESETS.map(preset => (
              <button
                key={preset.seconds}
                type="button"
                className={draft.timeMode === 'relative' && presetSeconds === preset.seconds ? 'active' : undefined}
                aria-pressed={draft.timeMode === 'relative' && presetSeconds === preset.seconds}
                onClick={() => selectPreset(preset.seconds)}
              >
                {preset.label}
              </button>
            ))}
            <button
              type="button"
              className={draft.timeMode === 'absolute' ? 'active' : undefined}
              aria-pressed={draft.timeMode === 'absolute'}
              onClick={selectCustom}
            >
              {text.customPeriod}
            </button>
          </div>
        </fieldset>

        {draft.timeMode === 'absolute' && (
          <div className="historical-browser__absolute-period">
            <label>
              {text.start}
              <input
                aria-label={text.start}
                type="datetime-local"
                step={1}
                value={draft.absoluteFromLocal}
                onChange={event => updateDraft(Object.freeze({ ...draft, absoluteFromLocal: event.target.value }))}
              />
            </label>
            <label>
              {text.end}
              <input
                aria-label={text.end}
                type="datetime-local"
                step={1}
                value={draft.absoluteToLocal}
                onChange={event => updateDraft(Object.freeze({ ...draft, absoluteToLocal: event.target.value }))}
              />
            </label>
            <span className="historical-browser__timezone">{localTimeZoneLabel()}</span>
          </div>
        )}

        <button
          type="button"
          className="historical-browser__query"
          onClick={() => onQueryRequested?.(draft)}
          disabled={!validation.ok || state === 'loading'}
        >
          {text.query}
        </button>
      </div>

      {!validation.ok && (
        <div role="alert" className="historical-browser__validation">
          {validation.diagnostics.join(' ')}
        </div>
      )}

      {advancedControls}

      {state !== 'idle' && (
        <div className="historical-browser__summary" aria-live="polite">
          <strong>{historicalDatasetLabel(draft.datasetKey, locale)}</strong>
          <span>{historicalTimeSummary(draft, locale)}</span>
          {filterSummary.length > 0 && <span>{filterSummary.join(' · ')}</span>}
        </div>
      )}

      <HistoricalBrowserResultState state={state} errorMessage={errorMessage} rowCount={rows.length} locale={locale} />

      {(state === 'ready' || (state === 'idle' && rows.length > 0)) && rows.length > 0 && (
        <div className="historical-browser__content">
          <div className="historical-browser__table-wrap">
            <table>
              <thead>
                <tr>{columns.map(column => <th key={column.key} scope="col">{column.label}</th>)}</tr>
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
                  <div key={fact.label + '-' + index}>
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
  if (state === 'loading') return <p role="status" className="historical-browser__state">{text.loading}</p>;
  if (state === 'unauthorized') return <p role="alert" className="historical-browser__state">{text.unauthorized}</p>;
  if (state === 'error') return <p role="alert" className="historical-browser__state">{errorMessage?.trim() || text.queryFailed}</p>;
  if (state === 'empty' || (state === 'ready' && rowCount === 0)) return <p role="status" className="historical-browser__state">{text.empty}</p>;
  if (state === 'idle' && rowCount === 0) return <p role="status" className="historical-browser__state">{text.idle}</p>;
  return null;
}
