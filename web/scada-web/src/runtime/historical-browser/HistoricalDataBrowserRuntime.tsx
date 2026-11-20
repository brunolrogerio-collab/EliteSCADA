import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { HistoricalDataBrowser, type HistoricalBrowserColumn, type HistoricalBrowserRow, type HistoricalBrowserViewState } from './HistoricalDataBrowser';
import { HistoricalFilterBuilder } from './HistoricalFilterBuilder';
import {
  HistoricalQueryApiError,
  executeHistoricalQuery,
  executeSavedHistoricalQuery,
  executeSavedAlarmHistory,
  type SavedHistoryQuery,
  type SavedHistoryView,
  type HistoricalColumn,
  type HistoricalFilter,
  type HistoricalQueryResponse,
  type HistoricalSortDirection
} from './historicalQueryApi';
import {
  buildHistoricalQueryRequest,
  canSearchHistoricalColumns,
  isInternalIdentityColumn,
  projectHistoricalQueryResponse,
  sortableHistoricalColumns
} from './historicalBrowserQueryAdapter';
import {
  createHistoricalBrowserDraft,
  formatHistoricalDateTime,
  type HistoricalBrowserDraft
} from './historicalBrowserPresentation';
import { localTimeZoneLabel } from '../historicalTimeRange';
import {
  historicalBrowserCopy,
  historicalFieldLabel,
  historicalOperatorLabel,
  type HistoricalBrowserLocale
} from './historicalBrowserI18n';

export type HistoricalQueryLoader = (
  request: ReturnType<typeof buildHistoricalQueryRequest>,
  signal?: AbortSignal
) => Promise<HistoricalQueryResponse>;

export type HistoricalDataBrowserRuntimeProps = Readonly<{
  locale?: HistoricalBrowserLocale;
  queryLoader?: HistoricalQueryLoader;
  savedQueries?: readonly SavedHistoryQuery[];
  alarmViews?: readonly SavedHistoryView[];
  alarmViewKey?: string;
  onAlarmViewChange?: (key: string) => void;
}>;

/**
 * Runtime controller for the Historical Data Browser. It consumes only the
 * integrated Historical Query v1 HTTP contract and keeps ad-hoc query choices
 * in browser state; no query settings are persisted to Engineering here.
 */
export function HistoricalDataBrowserRuntime({
  locale = 'en',
  queryLoader = executeHistoricalQuery,
  savedQueries,
  alarmViews,
  alarmViewKey = '',
  onAlarmViewChange
}: HistoricalDataBrowserRuntimeProps) {
  const text = historicalBrowserCopy(locale);
  const [draft, setDraft] = useState<HistoricalBrowserDraft>(() => createHistoricalBrowserDraft());
  const [savedQueryKey, setSavedQueryKey] = useState('');
  const [overrideRange, setOverrideRange] = useState(false);
  const [parameterValues, setParameterValues] = useState<Record<string, string>>({});
  const savedQuery = savedQueries?.find(item => item.key === savedQueryKey);
  const [state, setState] = useState<HistoricalBrowserViewState>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [responseColumns, setResponseColumns] = useState<readonly HistoricalColumn[]>([]);
  const [columns, setColumns] = useState<readonly HistoricalBrowserColumn[]>([]);
  const [rows, setRows] = useState<readonly HistoricalBrowserRow[]>([]);
  const [filters, setFilters] = useState<readonly HistoricalFilter[]>([]);
  const [search, setSearch] = useState('');
  const [sortField, setSortField] = useState('');
  const [sortDirection, setSortDirection] = useState<HistoricalSortDirection>('descending');
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [pageCursors, setPageCursors] = useState<readonly (string | null)[]>([null]);
  const [pageIndex, setPageIndex] = useState(0);
  const [resolvedRange, setResolvedRange] = useState<Readonly<{ fromUtc: string; toUtc: string }> | null>(null);
  const activeController = useRef<AbortController | null>(null);

  useEffect(() => () => activeController.current?.abort(), []);

  useEffect(() => {
    activeController.current?.abort();
    setRows([]);
    setColumns([]);
    setResponseColumns([]);
    setResolvedRange(null);
    setNextCursor(null);
    setPageCursors([null]);
    setPageIndex(0);
    setErrorMessage(null);
    setState('idle');
  }, [savedQueryKey, alarmViewKey, overrideRange, parameterValues]);

  const operatorColumns = useMemo(
    () => responseColumns.filter(column => !isInternalIdentityColumn(column)),
    [responseColumns]
  );
  const searchable = canSearchHistoricalColumns(operatorColumns);
  const sortableColumns = useMemo(() => sortableHistoricalColumns(operatorColumns), [operatorColumns]);

  const runQuery = useCallback(async (
    queryDraft: HistoricalBrowserDraft,
    cursor: string | null,
    nextPageIndex: number,
    replaceCursorStack: boolean
  ) => {
    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    setState('loading');
    setErrorMessage(null);

    try {
      const selectedSort = sortField
        ? { field: sortField, direction: sortDirection } as const
        : null;
      const request = savedQuery && !overrideRange ? undefined : buildHistoricalQueryRequest(queryDraft, {
        filters,
        search: searchable ? search : '',
        sort: selectedSort,
        cursor
      });
      const parameters = Object.fromEntries((savedQuery?.parameters ?? []).flatMap(parameter => {
        const value = parameterValues[parameter.key];
        return value === undefined || value === '' ? [] : [[parameter.key, { type: parameter.type, value }]];
      }));
      const response = savedQuery
        ? await executeSavedHistoricalQuery(savedQuery.key, request?.timeRange, cursor, parameters, controller.signal)
        : alarmViewKey && request!.datasetKey === 'alarm.events'
          ? await executeSavedAlarmHistory(alarmViewKey, request!, controller.signal)
          : await queryLoader(request!, controller.signal);
      if (controller.signal.aborted) return;

      const projected = projectHistoricalQueryResponse(response, locale);
      setResponseColumns(response.columns);
      setColumns(projected.columns.map(column => Object.freeze({
        key: column.key,
        label: column.label,
        scalarType: 'String' as const
      })));
      setRows(projected.rows.map(row => Object.freeze({
        id: row.id,
        cells: row.cells,
        cellTitles: row.cellTitles,
        detail: row.detail
      })));
      setResolvedRange(Object.freeze({ fromUtc: projected.fromUtc, toUtc: projected.toUtc }));
      setNextCursor(projected.nextCursor);
      setPageIndex(nextPageIndex);
      setPageCursors(current => replaceCursorStack ? Object.freeze([null]) : current);
      setState(projected.rows.length === 0 ? 'empty' : 'ready');
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof HistoricalQueryApiError && (error.issue === 'unauthenticated' || error.issue === 'forbidden')) {
        setState('unauthorized');
        setErrorMessage(text.unauthorized);
        return;
      }
      setState('error');
      setErrorMessage(error instanceof HistoricalQueryApiError && error.issue === 'invalid-query' ? error.message : text.queryFailed);
    }
  }, [alarmViewKey, filters, locale, overrideRange, parameterValues, queryLoader, savedQuery, search, searchable, sortDirection, sortField, text.queryFailed, text.unauthorized]);

  function runFirstPage(queryDraft = draft) {
    setPageCursors(Object.freeze([null]));
    void runQuery(queryDraft, null, 0, true);
  }

  function handleDraftChange(nextDraft: HistoricalBrowserDraft) {
    if (nextDraft.datasetKey !== draft.datasetKey) {
      activeController.current?.abort();
      setFilters(Object.freeze([]));
      setSearch('');
      setSortField('');
      setSortDirection('descending');
      setResponseColumns(Object.freeze([]));
      setColumns(Object.freeze([]));
      setRows(Object.freeze([]));
      setResolvedRange(null);
      setNextCursor(null);
      setPageCursors(Object.freeze([null]));
      setPageIndex(0);
      setErrorMessage(null);
      setState('idle');
    }
    setDraft(nextDraft);
  }

  function handleFiltersChange(nextFilters: readonly HistoricalFilter[]) {
    setFilters(nextFilters);
    setNextCursor(null);
    setPageCursors(Object.freeze([null]));
    setPageIndex(0);
  }

  function goNext() {
    if (!nextCursor || state === 'loading') return;
    const nextIndex = pageIndex + 1;
    setPageCursors(current => {
      const next = current.slice(0, nextIndex);
      next[nextIndex] = nextCursor;
      return Object.freeze(next);
    });
    void runQuery(draft, nextCursor, nextIndex, false);
  }

  function goPrevious() {
    if (pageIndex <= 0 || state === 'loading') return;
    const previousIndex = pageIndex - 1;
    const cursor = pageCursors[previousIndex] ?? null;
    void runQuery(draft, cursor, previousIndex, false);
  }

  const filterSummary = useMemo(() => {
    const summary: string[] = (savedQuery ? [] : filters).map(filter =>
      historicalFieldLabel(filter.field, locale) + ' ' +
      historicalOperatorLabel(filter.operator, locale) + ' ' +
      filter.values.map(value => value.value ?? '—').join(', ')
    );
    if (!savedQuery && searchable && search.trim()) summary.push(text.search + ': ' + search.trim());
    if (!savedQuery && sortField) summary.push(
      text.sortField + ': ' + historicalFieldLabel(sortField, locale) + ' ' +
      (sortDirection === 'ascending' ? text.ascending : text.descending)
    );
    if (resolvedRange) summary.push(
      formatHistoricalDateTime(resolvedRange.fromUtc, locale) + ' → ' +
      formatHistoricalDateTime(resolvedRange.toUtc, locale) + ' · ' + localTimeZoneLabel()
    );
    summary.push(text.page + ' ' + String(pageIndex + 1));
    return Object.freeze(summary);
  }, [filters, locale, pageIndex, resolvedRange, savedQuery, search, searchable, sortDirection, sortField, text]);

  const advancedControls = (
    <details className="historical-browser__advanced" data-testid="historical-browser-advanced">
      <summary>{text.advancedFilters}</summary>
      <div className="historical-browser__advanced-body">
        {responseColumns.length === 0 ? (
          <p className="historical-browser__advanced-note">{text.advancedDiscovery}</p>
        ) : (
          <>
            <div className="historical-browser__query-tools">
              {searchable ? (
                <label>
                  {text.search}
                  <input
                    aria-label={text.search}
                    value={search}
                    maxLength={200}
                    disabled={state === 'loading'}
                    placeholder={text.searchPlaceholder}
                    onChange={event => setSearch(event.target.value)}
                  />
                </label>
              ) : <p className="historical-browser__advanced-note">{text.searchUnavailable}</p>}
              {sortableColumns.length > 0 ? (
                <>
                  <label>
                    {text.sortField}
                    <select
                      aria-label={text.sortField}
                      value={sortField}
                      disabled={state === 'loading'}
                      onChange={event => setSortField(event.target.value)}
                    >
                      <option value="">{text.serverDefault}</option>
                      {sortableColumns.map(column => (
                        <option key={column.field} value={column.field}>
                          {historicalFieldLabel(column.field, locale)}
                        </option>
                      ))}
                    </select>
                  </label>
                  {sortField ? (
                    <label>
                      {text.direction}
                      <select
                        aria-label={text.direction}
                        value={sortDirection}
                        disabled={state === 'loading'}
                        onChange={event => setSortDirection(event.target.value as HistoricalSortDirection)}
                      >
                        <option value="descending">{text.descending}</option>
                        <option value="ascending">{text.ascending}</option>
                      </select>
                    </label>
                  ) : null}
                </>
              ) : <p className="historical-browser__advanced-note">{text.sortUnavailable}</p>}
            </div>

            <HistoricalFilterBuilder
              locale={locale}
              columns={operatorColumns}
              filters={filters}
              disabled={state === 'loading'}
              onFiltersChange={handleFiltersChange}
            />
          </>
        )}
      </div>
    </details>
  );

  return (
    <div data-testid="historical-data-browser-runtime">
      {savedQueries && savedQueries.length > 0 && (
        <label>
          {locale === 'pt-BR' ? 'Consulta salva' : locale === 'es' ? 'Consulta guardada' : 'Saved query'}
          <select value={savedQueryKey} onChange={event => {
            setSavedQueryKey(event.target.value);
            setParameterValues({});
            setOverrideRange(false);
          }}>
            <option value="">{locale === 'pt-BR' ? 'Consulta avulsa' : locale === 'es' ? 'Consulta puntual' : 'Ad hoc query'}</option>
            {savedQueries.map(item => <option key={item.key} value={item.key}>{item.name}</option>)}
          </select>
        </label>
      )}
      {savedQuery && <>
        <label>
          <input type="checkbox" checked={overrideRange} onChange={event => setOverrideRange(event.target.checked)} />
          {locale === 'pt-BR' ? 'Usar o período abaixo' : locale === 'es' ? 'Usar el período indicado abajo' : 'Use the range below'}
        </label>
        {savedQuery.parameters?.map(parameter => {
          const value = parameterValues[parameter.key] ?? parameter.defaultValue?.value ?? '';
          const change = (next: string) => setParameterValues(current => ({ ...current, [parameter.key]: next }));
          return <label key={parameter.key}>
            {parameter.name}
            {parameter.allowedValues?.length ? <select value={value} onChange={event => change(event.target.value)}>
              <option value="">—</option>
              {parameter.allowedValues.map(option => <option key={option.value} value={option.value}>{option.value}</option>)}
            </select> : <input value={value} onChange={event => change(event.target.value)} />}
          </label>;
        })}
      </>}
      {!savedQuery && draft.datasetKey === 'alarm.events' && alarmViews && alarmViews.length > 0 && (
        <label>
          {locale === 'pt-BR' ? 'Filtro salvo de alarmes' : locale === 'es' ? 'Filtro de alarmas guardado' : 'Saved alarm filter'}
          <select value={alarmViewKey} onChange={event => onAlarmViewChange?.(event.target.value)}>
            <option value="">{locale === 'pt-BR' ? 'Todos' : locale === 'es' ? 'Todas' : 'All'}</option>
            {alarmViews.map(view => <option key={view.key} value={view.key}>{view.name}</option>)}
          </select>
        </label>
      )}
      <HistoricalDataBrowser
        locale={locale}
        columns={columns}
        rows={rows}
        state={state}
        errorMessage={errorMessage}
        savedQueryName={savedQuery?.name}
        showPeriod={!savedQuery || overrideRange}
        filterSummary={filterSummary}
        advancedControls={savedQuery ? undefined : advancedControls}
        onDraftChange={handleDraftChange}
        onQueryRequested={nextDraft => {
          handleDraftChange(nextDraft);
          runFirstPage(nextDraft);
        }}
      />

      {(state === 'ready' || state === 'empty') && (
        <nav className="historical-browser__paging" aria-label={text.title + ' · ' + text.page}>
          <button type="button" onClick={goPrevious} disabled={pageIndex === 0}>{text.previousPage}</button>
          <span>{text.page} {pageIndex + 1}</span>
          <button type="button" onClick={goNext} disabled={!nextCursor}>{text.nextPage}</button>
        </nav>
      )}
    </div>
  );
}
