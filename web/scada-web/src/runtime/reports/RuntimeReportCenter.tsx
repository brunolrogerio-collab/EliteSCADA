import React, { useEffect, useMemo, useRef, useState } from 'react';
import type {
  HistoricalQueryValue,
  ReportEngineeringDto,
  ReportExecutionResult,
  ReportParameterValue,
  ReportRuntimeTimeRange
} from '../../engineering/reports/reportContracts';
import './runtime-reports.css';

type Locale = 'pt-BR' | 'en' | 'es';
type ListItem = Readonly<{ id: string; key: string; name: string; category?: string | null; description?: string | null }>;
type Generation = Readonly<{
  executionId: string;
  reportKey: string;
  reportName: string;
  generatedAtUtc: string;
  timeRange?: ReportRuntimeTimeRange | null;
  result: ReportExecutionResult;
}>;

const copy = {
  'pt-BR': {
    title: 'Relatórios', search: 'Buscar relatório', empty: 'Nenhum relatório autorizado disponível.',
    period: 'Período', relative: 'Últimas', absolute: 'De / Até', from: 'De', to: 'Até',
    generate: 'Gerar relatório', generating: 'Gerando…', cancel: 'Cancelar', cancelled: 'Geração cancelada.', back: 'Voltar', refresh: 'Gerar novamente',
    pdf: 'Exportar PDF', xlsx: 'Exportar Excel', csv: 'Exportar CSV', print: 'Imprimir',
    previous: 'Página anterior', next: 'Próxima página', fitPage: 'Ajustar página', fitWidth: 'Ajustar largura',
    page: 'Página', generated: 'Gerado em', params: 'Parâmetros usados', error: 'Não foi possível gerar o relatório.',
    resolution: 'Resolução', variables: 'Variáveis'
  },
  en: {
    title: 'Reports', search: 'Search reports', empty: 'No authorized reports are available.',
    period: 'Period', relative: 'Last', absolute: 'From / To', from: 'From', to: 'To',
    generate: 'Generate report', generating: 'Generating…', cancel: 'Cancel', cancelled: 'Generation cancelled.', back: 'Back', refresh: 'Regenerate',
    pdf: 'Export PDF', xlsx: 'Export Excel', csv: 'Export CSV', print: 'Print',
    previous: 'Previous page', next: 'Next page', fitPage: 'Fit page', fitWidth: 'Fit width',
    page: 'Page', generated: 'Generated at', params: 'Parameters used', error: 'Could not generate the report.',
    resolution: 'Resolution', variables: 'Variables'
  },
  es: {
    title: 'Informes', search: 'Buscar informe', empty: 'No hay informes autorizados disponibles.',
    period: 'Período', relative: 'Últimas', absolute: 'Desde / Hasta', from: 'Desde', to: 'Hasta',
    generate: 'Generar informe', generating: 'Generando…', cancel: 'Cancelar', cancelled: 'Generación cancelada.', back: 'Volver', refresh: 'Generar de nuevo',
    pdf: 'Exportar PDF', xlsx: 'Exportar Excel', csv: 'Exportar CSV', print: 'Imprimir',
    previous: 'Página anterior', next: 'Página siguiente', fitPage: 'Ajustar página', fitWidth: 'Ajustar ancho',
    page: 'Página', generated: 'Generado el', params: 'Parámetros usados', error: 'No se pudo generar el informe.',
    resolution: 'Resolución', variables: 'Variables'
  }
} as const;

const relativePresets = [
  { seconds: 15 * 60, label: '15 min' },
  { seconds: 60 * 60, label: '1 h' },
  { seconds: 8 * 60 * 60, label: '8 h' },
  { seconds: 24 * 60 * 60, label: '24 h' },
  { seconds: 7 * 24 * 60 * 60, label: '7 d' }
] as const;

export function RuntimeReportCenter({ locale }: { locale: Locale }) {
  const t = copy[locale];
  const [reports, setReports] = useState<readonly ListItem[]>([]);
  const [filter, setFilter] = useState('');
  const [selected, setSelected] = useState<ReportEngineeringDto | null>(null);
  const [generation, setGeneration] = useState<Generation | null>(null);
  const [rangeKind, setRangeKind] = useState<'relative' | 'absolute'>('relative');
  const [duration, setDuration] = useState(24 * 60 * 60);
  const [fromLocal, setFromLocal] = useState(() => localInput(new Date(Date.now() - 24 * 60 * 60 * 1000)));
  const [toLocal, setToLocal] = useState(() => localInput(new Date()));
  const [parameters, setParameters] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [zoom, setZoom] = useState(100);
  const generationAbort = useRef<AbortController | null>(null);

  useEffect(() => {
    let active = true;
    void api<readonly ListItem[]>('/api/runtime/reports').then(items => {
      if (!active) return;
      setReports(items);
      const requested = new URLSearchParams(window.location.search).get('report');
      const target = requested ? items.find(item => item.key.toLocaleLowerCase() === requested.toLocaleLowerCase()) : undefined;
      if (target) void choose(target);
    }).catch(err => {
      if (active) setError(message(err, t.error));
    });
    return () => {
      active = false;
      generationAbort.current?.abort();
    };
  }, [t.error]);

  const visible = useMemo(() => reports.filter(report => {
    const needle = filter.trim().toLocaleLowerCase();
    return !needle || [report.name, report.category, report.description].some(value => value?.toLocaleLowerCase().includes(needle));
  }), [reports, filter]);

  async function choose(item: ListItem) {
    setError('');
    setGeneration(null);
    try {
      const report = await api<ReportEngineeringDto>(`/api/runtime/reports/${encodeURIComponent(item.key)}`);
      setSelected(report);
      setRangeKind(report.timeRange?.defaultKind ?? 'relative');
      setDuration(report.timeRange?.defaultRelativeDurationSeconds ?? 24 * 60 * 60);
      if (report.timeRange?.defaultFromUtc)
        setFromLocal(localInput(new Date(report.timeRange.defaultFromUtc)));
      if (report.timeRange?.defaultToUtc)
        setToLocal(localInput(new Date(report.timeRange.defaultToUtc)));
      setParameters(Object.fromEntries((report.parameters ?? []).map(p => [p.key, p.defaultValue.value])));
    } catch (err) {
      setError(message(err, t.error));
    }
  }

  async function generate() {
    if (!selected) return;
    generationAbort.current?.abort();
    const controller = new AbortController();
    generationAbort.current = controller;
    setBusy(true);
    setError('');
    try {
      const timeRange: ReportRuntimeTimeRange = rangeKind === 'relative'
        ? { kind: 'relative', durationSeconds: duration }
        : { kind: 'absolute', fromUtc: new Date(fromLocal).toISOString(), toUtc: new Date(toLocal).toISOString() };
      if (timeRange.kind === 'absolute' && Date.parse(timeRange.fromUtc ?? '') >= Date.parse(timeRange.toUtc ?? ''))
        throw new Error(locale === 'pt-BR' ? '“De” deve ser anterior a “Até”.' : locale === 'es' ? '“Desde” debe ser anterior a “Hasta”.' : 'From must be earlier than To.');
      const typed = Object.fromEntries((selected.parameters ?? []).map(def => [
        def.key,
        { type: def.type, value: parameters[def.key] ?? def.defaultValue.value } satisfies ReportParameterValue
      ]));
      const generated = await api<Generation>(`/api/runtime/reports/${encodeURIComponent(selected.key)}/generate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ parameters: typed, timeRange }),
        signal: controller.signal
      });
      setGeneration(generated);
      setPage(1);
      setZoom(100);
    } catch (err) {
      setError(controller.signal.aborted ? t.cancelled : message(err, t.error));
    } finally {
      if (generationAbort.current === controller) generationAbort.current = null;
      if (!controller.signal.aborted) setBusy(false);
      else setBusy(false);
    }
  }

  function cancelGeneration() {
    generationAbort.current?.abort();
    generationAbort.current = null;
    setBusy(false);
    setError(t.cancelled);
  }

  if (generation && selected) {
    return <ReportViewer
      locale={locale}
      t={t}
      report={selected}
      generation={generation}
      page={page}
      setPage={setPage}
      zoom={zoom}
      setZoom={setZoom}
      onBack={() => setGeneration(null)}
      onRegenerate={generate}
    />;
  }

  return <main className="shell report-center">
    <header className="report-center__heading">
      <div><h1>{t.title}</h1></div>
      <input aria-label={t.search} placeholder={t.search} value={filter} onChange={e => setFilter(e.target.value)} />
    </header>
    {error && <p role="alert" className="report-center__error">{error}</p>}
    <div className="report-center__layout">
      <aside className="report-center__catalog" aria-label={t.title}>
        {visible.length === 0 ? <p>{t.empty}</p> : visible.map(item =>
          <button key={item.id || item.key} className={selected?.key === item.key ? 'active' : ''} onClick={() => void choose(item)}>
            <strong>{item.name}</strong><span>{item.category}</span><small>{item.description}</small>
          </button>)}
      </aside>
      <section className="report-center__generator">
        {selected ? <>
          <h2>{selected.name}</h2>
          {selected.description && <p>{selected.description}</p>}
          <dl className="report-center__summary">
            <div><dt>{t.variables}</dt><dd>{selected.variables?.filter(v => v.visible !== false).length ?? '—'}</dd></div>
            <div><dt>{t.resolution}</dt><dd>{resolutionLabel(selected, locale)}</dd></div>
          </dl>
          <fieldset><legend>{t.period}</legend>
            <div className="report-center__range-tabs">
              <button type="button" className={rangeKind === 'relative' ? 'active' : ''} onClick={() => setRangeKind('relative')} disabled={selected.timeRange?.allowRelative === false}>{t.relative}</button>
              <button type="button" className={rangeKind === 'absolute' ? 'active' : ''} onClick={() => setRangeKind('absolute')} disabled={selected.timeRange?.allowAbsolute === false}>{t.absolute}</button>
            </div>
            {rangeKind === 'relative'
              ? <div className="report-center__relative">
                  <select aria-label={t.relative} value={relativePresets.some(p => p.seconds === duration) ? duration : 'custom'} onChange={e => {
                    if (e.target.value !== 'custom') setDuration(Number(e.target.value));
                  }}>
                    {relativePresets.map(p => <option key={p.seconds} value={p.seconds}>{p.label}</option>)}
                    <option value="custom">{locale === 'pt-BR' ? 'Personalizado' : locale === 'es' ? 'Personalizado' : 'Custom'}</option>
                  </select>
                  {!relativePresets.some(p => p.seconds === duration) && <DurationInput seconds={duration} locale={locale} onChange={setDuration} />}
                  {relativePresets.some(p => p.seconds === duration) && <button type="button" onClick={() => setDuration(duration + 1)}>
                    {locale === 'pt-BR' ? 'Personalizar' : locale === 'es' ? 'Personalizar' : 'Custom'}
                  </button>}
                </div>
              : <div className="report-center__absolute">
                  <label>{t.from}<input type="datetime-local" step="1" value={fromLocal} onChange={e => setFromLocal(e.target.value)} /></label>
                  <label>{t.to}<input type="datetime-local" step="1" value={toLocal} onChange={e => setToLocal(e.target.value)} /></label>
                </div>}
          </fieldset>
          {(selected.parameters ?? []).length > 0 && <fieldset><legend>{t.params}</legend>
            <div className="report-center__parameters">{selected.parameters?.map(def =>
              <label key={def.key}>{def.name}
                <input value={parameters[def.key] ?? ''} onChange={e => setParameters(current => ({ ...current, [def.key]: e.target.value }))} />
              </label>)}</div>
          </fieldset>}
          <div className="report-center__generate-actions">
            <button className="report-center__generate" onClick={() => void generate()} disabled={busy}>{busy ? t.generating : t.generate}</button>
            {busy && <button type="button" onClick={cancelGeneration}>{t.cancel}</button>}
          </div>
        </> : <p>{locale === 'pt-BR' ? 'Selecione um relatório.' : locale === 'es' ? 'Seleccione un informe.' : 'Select a report.'}</p>}
      </section>
    </div>
  </main>;
}

function ReportViewer({ locale, t, report, generation, page, setPage, zoom, setZoom, onBack, onRegenerate }: {
  locale: Locale; t: typeof copy[Locale]; report: ReportEngineeringDto; generation: Generation;
  page: number; setPage: (value: number) => void; zoom: number; setZoom: (value: number) => void;
  onBack: () => void; onRegenerate: () => void;
}) {
  const rowsPerPage = rowsPerGeneratedPage(report);
  const query = generation.result.queries[0];
  const rows = query?.rows ?? [];
  const columns = query?.columns ?? [];
  const pages = Math.max(1, Math.ceil(rows.length / rowsPerPage));
  const currentPage = Math.min(page, pages);
  const visibleRows = rows.slice((currentPage - 1) * rowsPerPage, currentPage * rowsPerPage);
  const exportUrl = (format: string) => `/api/runtime/reports/executions/${generation.executionId}/export/${format}`;

  return <main className="shell report-viewer">
    <header className="report-viewer__toolbar">
      <button onClick={onBack} aria-label={t.back}>← {t.back}</button>
      <strong>{report.name}</strong>
      <div className="report-viewer__actions">
        <button title={t.pdf} aria-label={t.pdf} onClick={() => window.location.assign(exportUrl('pdf'))}>PDF</button>
        <button title={t.xlsx} aria-label={t.xlsx} onClick={() => window.location.assign(exportUrl('xlsx'))}>XLSX</button>
        <button title={t.csv} aria-label={t.csv} onClick={() => window.location.assign(exportUrl('csv'))}>CSV</button>
        <button title={t.print} aria-label={t.print} onClick={() => window.open(`/api/runtime/reports/executions/${generation.executionId}/print`, '_blank', 'noopener,noreferrer')}>⌘P</button>
      </div>
    </header>
    <section className="report-viewer__meta">
      <span>{t.generated}: {new Date(generation.generatedAtUtc).toLocaleString(locale)}</span>
      {query && <span>{new Date(query.fromUtc).toLocaleString(locale)} — {new Date(query.toUtc).toLocaleString(locale)}</span>}
      <span>{t.params}: {effectiveParameters(generation.result.parameters)}</span>
      <button onClick={onRegenerate}>{t.refresh}</button>
    </section>
    <nav className="report-viewer__pagination" aria-label={t.page}>
      <button aria-label={t.previous} disabled={currentPage <= 1} onClick={() => setPage(currentPage - 1)}>‹</button>
      <span>{t.page} {currentPage} / {pages}</span>
      <button aria-label={t.next} disabled={currentPage >= pages} onClick={() => setPage(currentPage + 1)}>›</button>
      <button aria-label={t.fitPage} title={t.fitPage} onClick={() => setZoom(80)}>▣</button>
      <button aria-label={t.fitWidth} title={t.fitWidth} onClick={() => setZoom(100)}>↔</button>
      <label>Zoom <input type="range" min="60" max="160" step="10" value={zoom} onChange={e => setZoom(Number(e.target.value))} /> {zoom}%</label>
    </nav>
    <div className="report-viewer__page-wrap" style={{ transform: `scale(${zoom / 100})`, transformOrigin: 'top center' }}>
      <GeneratedReportPage
        report={report}
        generation={generation}
        query={query}
        rows={visibleRows}
        allRows={rows}
        rowOffset={(currentPage - 1) * rowsPerPage}
        locale={locale}
        page={currentPage}
        pages={pages}
      />
      <details className="report-viewer__table">
        <summary>{locale === 'pt-BR' ? 'Visualização tabular' : locale === 'es' ? 'Vista tabular' : 'Tabular view'}</summary>
        <table><thead><tr>{columns.map(c => <th key={c.field}>{columnLabel(report, c.field)}</th>)}</tr></thead>
          <tbody>{visibleRows.map((row, index) => <tr key={index}>{columns.map(c => <td key={c.field}>{displayFieldValue(report, c.field, row.cells[c.field], locale)}</td>)}</tr>)}</tbody>
        </table>
      </details>
    </div>
  </main>;
}

function GeneratedReportPage({ report, generation, query, rows, allRows, rowOffset, locale, page, pages }: {
  report: ReportEngineeringDto;
  generation: Generation;
  query: ReportExecutionResult['queries'][number] | undefined;
  rows: readonly { cells: Readonly<Record<string, HistoricalQueryValue>> }[];
  allRows: readonly { cells: Readonly<Record<string, HistoricalQueryValue>> }[];
  rowOffset: number;
  locale: Locale;
  page: number;
  pages: number;
}) {
  const dimensions = pageDimensions(report);
  const pageStyle: React.CSSProperties = {
    width: `${dimensions.width}mm`,
    minHeight: `${dimensions.height}mm`,
    padding: `${report.page?.marginTopMillimeters ?? 10}mm ${report.page?.marginRightMillimeters ?? 10}mm ${report.page?.marginBottomMillimeters ?? 10}mm ${report.page?.marginLeftMillimeters ?? 10}mm`
  };
  const sections = report.sections ?? [];
  const header = sections.filter(section => section.kind === 'reportHeader' && page === 1);
  const pageHeader = sections.filter(section => section.kind === 'pageHeader');
  const details = sections.filter(section => section.kind === 'detail');
  const pageFooter = sections.filter(section => section.kind === 'pageFooter');
  const footer = sections.filter(section => section.kind === 'reportFooter' && page === pages);

  return <article className="report-generated-page" style={pageStyle} data-testid="generated-report-page">
    {header.map(section => <RuntimeSection key={section.key} report={report} section={section} generation={generation} locale={locale} />)}
    {pageHeader.map(section => <RuntimeSection key={section.key} report={report} section={section} generation={generation} locale={locale} />)}
    {rows.map((row, rowIndex) => {
      const globalIndex = rowOffset + rowIndex;
      const previous = globalIndex > 0 ? allRows[globalIndex - 1] : undefined;
      const next = globalIndex + 1 < allRows.length ? allRows[globalIndex + 1] : undefined;
      return <React.Fragment key={rowIndex}>
        {(report.groups ?? []).flatMap(group =>
          groupValueChanged(previous, row, group.field)
            ? sections
                .filter(section => section.kind === 'groupHeader' && section.groupKey === group.key)
                .map(section => <RuntimeSection key={`${section.key}-header-${rowIndex}`} report={report} section={section} row={row} generation={generation} locale={locale} />)
            : [])}
        {details.map(section =>
          <RuntimeSection key={`${section.key}-${rowIndex}`} report={report} section={section} row={row} generation={generation} locale={locale} />)}
        {(report.groups ?? []).flatMap(group =>
          groupValueChanged(row, next, group.field)
            ? sections
                .filter(section => section.kind === 'groupFooter' && section.groupKey === group.key)
                .map(section => <RuntimeSection key={`${section.key}-footer-${rowIndex}`} report={report} section={section} row={row} generation={generation} locale={locale} />)
            : [])}
      </React.Fragment>;
    })}
    {details.length === 0 && <section className="report-runtime-fallback">
      <h1>{report.name}</h1>
      {query && <p>{new Date(query.fromUtc).toLocaleString(locale)} — {new Date(query.toUtc).toLocaleString(locale)}</p>}
    </section>}
    {pageFooter.map(section => <RuntimeSection key={section.key} report={report} section={section} generation={generation} locale={locale} />)}
    {footer.map(section => <RuntimeSection key={section.key} report={report} section={section} generation={generation} locale={locale} />)}
    {report.page?.showPageNumbers !== false && <div className="report-generated-page__number">{page} / {pages}</div>}
  </article>;
}

function RuntimeSection({ report, section, row, generation, locale }: {
  report: ReportEngineeringDto;
  section: NonNullable<ReportEngineeringDto['sections']>[number];
  row?: { cells: Readonly<Record<string, HistoricalQueryValue>> };
  generation: Generation;
  locale: Locale;
}) {
  return <section className="report-runtime-section" style={{ height: `${section.heightMillimeters}mm` }} data-section-kind={section.kind}>
    {(section.controls ?? []).filter(control => control.kind !== 'pageBreak').map(control =>
      <RuntimeControl key={control.key} report={report} control={control} row={row} generation={generation} locale={locale} />)}
  </section>;
}

function RuntimeControl({ report, control, row, generation, locale }: {
  report: ReportEngineeringDto;
  control: NonNullable<NonNullable<ReportEngineeringDto['sections']>[number]['controls']>[number];
  row?: { cells: Readonly<Record<string, HistoricalQueryValue>> };
  generation: Generation;
  locale: Locale;
}) {
  const style: React.CSSProperties = {
    position: 'absolute',
    left: `${control.xMillimeters}mm`,
    top: `${control.yMillimeters}mm`,
    width: `${control.widthMillimeters}mm`,
    height: `${control.heightMillimeters}mm`,
    fontFamily: control.style?.fontFamily || undefined,
    fontSize: control.style?.fontSizePoints ? `${control.style.fontSizePoints}pt` : undefined,
    fontWeight: control.style?.bold ? 700 : undefined,
    fontStyle: control.style?.italic ? 'italic' : undefined,
    textAlign: control.style?.textAlignment || undefined,
    color: control.style?.foreground || undefined,
    background: control.style?.background || undefined,
    borderWidth: control.style?.borderWidth ?? undefined
  };
  if (control.kind === 'image' && control.assetId)
    return <img className="report-runtime-control image" style={style} src={`/api/visual-assets/${encodeURIComponent(control.assetId)}/content`} alt={control.text ?? control.key} />;
  if (control.kind === 'chart')
    return <RuntimeChart style={style} report={report} generation={generation} queryKey={control.queryKey} field={control.field} />;
  if (control.kind === 'line')
    return <div className="report-runtime-control line" style={{ ...style, height: 0, borderTop: '1px solid currentColor' }} />;
  if (control.kind === 'rectangle' || control.kind === 'roundedRectangle' || control.kind === 'ellipse')
    return <div className={`report-runtime-control ${control.kind}`} style={{ ...style, border: `${Math.max(1, control.style?.borderWidth ?? 1)}px solid currentColor`, borderRadius: control.kind === 'ellipse' ? '50%' : control.kind === 'roundedRectangle' ? '4mm' : 0 }} />;
  const value = control.kind === 'dataField' || control.kind === 'booleanState' || control.kind === 'barcode'
    ? displayFieldValue(report, control.field ?? '', row?.cells[control.field ?? ''], locale)
    : control.text ?? control.key;
  return <div className={`report-runtime-control ${control.kind}`} style={style}>{value}</div>;
}

function RuntimeChart({ style, report, generation, queryKey, field }: {
  style: React.CSSProperties;
  report: ReportEngineeringDto;
  generation: Generation;
  queryKey?: string | null;
  field?: string | null;
}) {
  const query = generation.result.queries.find(item => item.queryKey === queryKey) ?? generation.result.queries[0];
  const valueField = field || (report.tableLayout === 'wide'
    ? query?.columns.find(column => column.field.startsWith('v:'))?.field
    : 'value');
  const values = (query?.rows ?? []).map((row, index) => ({ index, value: Number(row.cells[valueField ?? '']?.value) })).filter(point => Number.isFinite(point.value));
  if (values.length < 2) return <div className="report-runtime-control chart empty" style={style}>—</div>;
  const min = Math.min(...values.map(point => point.value));
  const max = Math.max(...values.map(point => point.value));
  const span = max === min ? 1 : max - min;
  const points = values.map(point => `${values.length === 1 ? 50 : point.index * 100 / (values.length - 1)},${95 - (point.value - min) * 90 / span}`).join(' ');
  return <svg className="report-runtime-control chart" style={style} viewBox="0 0 100 100" preserveAspectRatio="none" role="img" aria-label={valueField ?? 'chart'}>
    <polyline points={points} fill="none" stroke="currentColor" strokeWidth="1.5" vectorEffect="non-scaling-stroke" />
  </svg>;
}

function groupValueChanged(
  previous: { cells: Readonly<Record<string, HistoricalQueryValue>> } | undefined,
  current: { cells: Readonly<Record<string, HistoricalQueryValue>> } | undefined,
  field: string
) {
  const before = previous?.cells[field]?.value ?? null;
  const after = current?.cells[field]?.value ?? null;
  return before !== after;
}

function rowsPerGeneratedPage(report: ReportEngineeringDto) {
  const dimensions = pageDimensions(report);
  const verticalMargins = (report.page?.marginTopMillimeters ?? 10) + (report.page?.marginBottomMillimeters ?? 10);
  const fixed = (report.sections ?? []).filter(section => section.kind === 'pageHeader' || section.kind === 'pageFooter').reduce((sum, section) => sum + section.heightMillimeters, 0);
  const detail = (report.sections ?? []).find(section => section.kind === 'detail')?.heightMillimeters ?? 7;
  return Math.max(1, Math.min(100, Math.floor((dimensions.height - verticalMargins - fixed - 10) / Math.max(1, detail))));
}

function pageDimensions(report: ReportEngineeringDto) {
  const paper = (report.page?.paperSizeKey ?? 'A4').toUpperCase();
  const portrait = paper === 'LETTER' ? { width: 215.9, height: 279.4 } : paper === 'A3' ? { width: 297, height: 420 } : { width: 210, height: 297 };
  return report.page?.orientation === 'landscape' ? { width: portrait.height, height: portrait.width } : portrait;
}

function displayFieldValue(report: ReportEngineeringDto, field: string, value: HistoricalQueryValue | undefined, locale: Locale) {
  if (!value?.value) return '—';
  const variable = field.startsWith('v:')
    ? report.variables?.find(item => item.tagId.toLocaleLowerCase() === field.slice(2).toLocaleLowerCase())
    : undefined;
  if (value.kind === 'boolean' && variable) {
    const truth = value.value.toLocaleLowerCase() === 'true';
    return truth ? variable.booleanTrueLabel || (locale === 'pt-BR' ? 'Verdadeiro' : locale === 'es' ? 'Verdadero' : 'True')
      : variable.booleanFalseLabel || (locale === 'pt-BR' ? 'Falso' : 'False');
  }
  if (value.kind === 'dateTime') {
    const date = new Date(value.value);
    if (variable?.dateTimeFormat === 'date') return date.toLocaleDateString(locale);
    if (variable?.dateTimeFormat === 'time') return date.toLocaleTimeString(locale);
    if (variable?.dateTimeFormat === 'iso') return date.toISOString();
    return date.toLocaleString(locale);
  }
  if (['number','float','double','int16','int32','int64'].includes(value.kind)) {
    const numeric = Number(value.value);
    if (!Number.isFinite(numeric)) return value.value;
    if (variable?.numericFormat === 'scientific') return numeric.toExponential(variable.decimalPlaces ?? 3);
    if (variable?.numericFormat === 'fixed' || variable?.decimalPlaces != null)
      return numeric.toLocaleString(locale, { minimumFractionDigits: variable?.decimalPlaces ?? 2, maximumFractionDigits: variable?.decimalPlaces ?? 2 });
    return numeric.toLocaleString(locale);
  }
  return value.value;
}

function columnLabel(report: ReportEngineeringDto, field: string) {
  if (field.startsWith('v:')) {
    const id = field.slice(2);
    return report.variables?.find(v => v.tagId.toLocaleLowerCase() === id.toLocaleLowerCase())?.displayLabel
      ?? report.variables?.find(v => v.tagId.toLocaleLowerCase() === id.toLocaleLowerCase())?.name
      ?? field;
  }
  return field;
}

function displayValue(value: HistoricalQueryValue | undefined, locale: Locale) {
  return displayFieldValue({ key: '', name: '' } as ReportEngineeringDto, '', value, locale);
}

function resolutionLabel(report: ReportEngineeringDto, locale: Locale) {
  const resolution = report.resolution;
  if (!resolution || resolution.mode === 'raw') return locale === 'pt-BR' ? 'Dados brutos' : locale === 'es' ? 'Datos brutos' : 'Raw';
  if (resolution.mode === 'sampledFixedStep') return `${locale === 'pt-BR' ? 'Intervalo fixo' : locale === 'es' ? 'Intervalo fijo' : 'Fixed interval'} · ${formatMs(resolution.intervalMilliseconds)}`;
  return `${resolution.aggregateFunction ?? 'aggregate'} · ${formatMs(resolution.bucketMilliseconds)}`;
}

function DurationInput({ seconds, locale, onChange }: { seconds: number; locale: Locale; onChange: (seconds: number) => void }) {
  const initialUnit = seconds % 86400 === 0 ? 86400 : seconds % 3600 === 0 ? 3600 : 60;
  const [unit, setUnit] = useState(initialUnit);
  const value = Math.max(1, Math.round(seconds / unit));
  return <span className="report-center__duration-input">
    <input type="number" min="1" value={value} onChange={event => onChange(Math.max(1, Number(event.target.value) || 1) * unit)} />
    <select aria-label={locale === 'pt-BR' ? 'Unidade do período' : locale === 'es' ? 'Unidad del período' : 'Period unit'} value={unit} onChange={event => {
      const next = Number(event.target.value);
      setUnit(next);
      onChange(value * next);
    }}>
      <option value={60}>min</option><option value={3600}>h</option><option value={86400}>{locale === 'en' ? 'days' : locale === 'es' ? 'días' : 'dias'}</option>
    </select>
  </span>;
}

function effectiveParameters(parameters: Readonly<Record<string, ReportParameterValue>>) {
  const entries = Object.entries(parameters);
  if (entries.length === 0) return '—';
  return entries.map(([key, value]) => `${key}=${value.value}`).join(' · ');
}

function formatMs(value?: number | null) {
  if (!value) return '—';
  if (value % 3600000 === 0) return `${value / 3600000} h`;
  if (value % 60000 === 0) return `${value / 60000} min`;
  return `${value} ms`;
}

function localInput(value: Date) {
  const local = new Date(value.getTime() - value.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 19);
}

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { error?: string; code?: string } | null;
    throw new Error(body?.error ?? body?.code ?? `HTTP ${response.status}`);
  }
  return await response.json() as T;
}

function message(error: unknown, fallback: string) {
  return error instanceof Error && error.message ? error.message : fallback;
}
