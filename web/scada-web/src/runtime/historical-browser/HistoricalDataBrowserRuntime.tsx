import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { HistoricalDataBrowser, type HistoricalBrowserColumn, type HistoricalBrowserRow, type HistoricalBrowserViewState } from './HistoricalDataBrowser';
import { HistoricalFilterBuilder } from './HistoricalFilterBuilder';
import {
  HistoricalQueryApiError,
  executeHistoricalQuery,
  type HistoricalColumn,
  type HistoricalFilter,
  type HistoricalQueryResponse,
  type HistoricalSortDirection
} from './historicalQueryApi';
import {
  buildHistoricalQueryRequest,
  canSearchHistoricalColumns,
  projectHistoricalQueryResponse,
  sortableHistoricalColumns
} from './historicalBrowserQueryAdapter';
import { summarizeHistoricalFilter } from './historicalBrowserFilters';
import { createHistoricalBrowserDraft, type HistoricalBrowserDraft } from './historicalBrowserPresentation';
import {
  historicalBrowserCopy,
  type HistoricalBrowserLocale
} from './historicalBrowserI18n';

export type HistoricalQueryLoader = (
  request: ReturnType<typeof buildHistoricalQueryRequest>,
  signal?: AbortSignal
) => Promise<HistoricalQueryResponse>;

export type HistoricalDataBrowserRuntimeProps = Readonly<{
  locale?: HistoricalBrowserLocale;
  queryLoader?: HistoricalQueryLoader;
}>;

/**
 * Runtime controller for the Historical Data Browser. It consumes only the
 * integrated Historical Query v1 HTTP contract and keeps ad-hoc query choices
 * in browser state; no query settings are persisted to Engineering here.
 */
export function HistoricalDataBrowserRuntime({
  locale = 'en',
  queryLoader = executeHistoricalQuery
}: HistoricalDataBrowserRuntimeProps) {
  const text = historicalBrowserCopy(locale);
  const [draft, setDraft] = useState<HistoricalBrowserDraft>(() => createHistoricalBrowserDraft());
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

  const searchable = canSearchHistoricalColumns(responseColumns);
  const sortableColumns = useMemo(() => sortableHistoricalColumns(responseColumns), [responseColumns]);

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
      const request = buildHistoricalQueryRequest(queryDraft, {
        filters,
        search: searchable ? search : '',
        sort: selectedSort,
        cursor
      });
      const response = await queryLoader(request, controller.signal);
      if (controller.signal.aborted) return;

      const projected = projectHistoricalQueryResponse(response);
      setResponseColumns(response.columns);
      setColumns(projected.columns.map(column => Object.freeze({
        key: column.key,
        label: column.label,
        scalarType: 'String' as const
      })));
      setRows(projected.rows.map(row => Object.freeze({
        id: row.id,
        cells: row.cells,
        detail: row.detail
      })));
      setResolvedRange(Object.freeze({ fromUtc: projected.fromUtc, toUtc: projected.toUtc }));
      setNextCursor(projected.nextCursor);
      setPageIndex(nextPageIndex);
      setPageCursors(current => replaceCursorStack ? Object.freeze([null]) : current);
      setState(projected.rows.length === 0 ? 'empty' : 'ready');
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof HistoricalQueryApiError) {
        if (error.issue === 'unauthenticated' || error.issue === 'forbidden') {
          setState('unauthorized');
          setErrorMessage(error.message);
          return;
        }
        setState('error');
        setErrorMessage(error.message);
        return;
      }
      setState('error');
      setErrorMessage(error instanceof Error ? error.message : text.queryFailed);
    }
  }, [filters, queryLoader, search, searchable, sortDirection, sortField, text.queryFailed]);

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
    const summary: string[] = filters.map(summarizeHistoricalFilter);
    if (searchable && search.trim()) summary.push(`${text.search}: ${search.trim()}`);
    if (sortField) summary.push(`${text.sortField}: ${sortField} ${sortDirection === 'ascending' ? text.ascending : text.descending}`);
    if (resolvedRange) summary.push(`${resolvedRange.fromUtc} → ${resolvedRange.toUtc}`);
    summary.push(`${text.page} ${pageIndex + 1}`);
    return Object.freeze(summary);
  }, [filters, pageIndex, resolvedRange, search, searchable, sortDirection, sortField, text]);

  return (
    <div data-testid="historical-data-browser-runtime">
      <div className="historical-browser__query-tools">
        <label>
          {text.search}
          <input
            aria-label={text.search}
            value={search}
            maxLength={200}
            disabled={!searchable || state === 'loading'}
            placeholder={searchable ? text.searchPlaceholder : text.searchDiscovery}
            onChange={event => setSearch(event.target.value)}
          />
        </label>
        <label>
          {text.sortField}
          <select
            aria-label={text.sortField}
            value={sortField}
            disabled={sortableColumns.length === 0 || state === 'loading'}
            onChange={event => setSortField(event.target.value)}
          >
            <option value="">{text.serverDefault}</option>
            {sortableColumns.map(column => <option key={column.field} value={column.field}>{column.field}</option>)}
          </select>
        </label>
        <label>
          {text.direction}
          <select
            aria-label={text.direction}
            value={sortDirection}
            disabled={!sortField || state === 'loading'}
            onChange={event => setSortDirection(event.target.value as HistoricalSortDirection)}
          >
            <option value="descending">{text.descending}</option>
            <option value="ascending">{text.ascending}</option>
          </select>
        </label>
        <button type="button" disabled={state === 'loading'} onClick={() => runFirstPage()}>{text.applyQuery}</button>
      </div>

      <HistoricalFilterBuilder
        locale={locale}
        columns={responseColumns}
        filters={filters}
        disabled={state === 'loading'}
        onFiltersChange={handleFiltersChange}
      />

      <HistoricalDataBrowser
        locale={locale}
        columns={columns}
        rows={rows}
        state={state}
        errorMessage={errorMessage}
        filterSummary={filterSummary}
        onDraftChange={handleDraftChange}
        onQueryRequested={nextDraft => {
          handleDraftChange(nextDraft);
          runFirstPage(nextDraft);
        }}
        onRefreshRequested={() => runFirstPage()}
      />

      <nav className="historical-browser__paging" aria-label={`${text.title} · ${text.page}`}>
        <button type="button" onClick={goPrevious} disabled={pageIndex === 0 || state === 'loading'}>{text.previousPage}</button>
        <span>{text.page} {pageIndex + 1}</span>
        <button type="button" onClick={goNext} disabled={!nextCursor || state === 'loading'}>{text.nextPage}</button>
      </nav>
    </div>
  );
}
