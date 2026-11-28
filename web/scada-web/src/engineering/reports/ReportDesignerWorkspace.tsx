import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage,
  visualAssetContentUrl
} from '../api';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringPackageView, EngineeringSnapshot, ImportPreviewView } from '../types';
import { ProjectReferenceBrowser } from '../project-reference/ProjectReferenceBrowser';
import {
  buildProjectReferenceCatalog,
  type ProjectReferenceDescriptor
} from '../project-reference/projectReferenceModel';
import { previewReportExecution, ReportPreviewApiError } from './reportApi';
import type {
  HistoricalQueryRow,
  ReportControlEngineeringDto,
  ReportEngineeringDto,
  ReportExecutionResult,
  ReportParameterValue,
  ReportSectionEngineeringDto,
  ReportVariableEngineeringDto
} from './reportContracts';
import {
  NEW_REPORT_IDENTITY,
  addReportControl,
  cloneReport,
  createReportDraft,
  defaultFieldForDataset,
  formatHistoricalValue,
  matchesReportIdentity,
  queryResult,
  removeReportControl,
  replaceReportInPackage,
  reportCollection,
  reportIdentity,
  updatePrimaryQueryDataset,
  updateRelativeDuration,
  updateReportControl,
  updateReportSection
} from './reportDesignerModel';
import './report-designer.css';

type ReportDesignerWorkspaceProps = {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
};

type ValidatedCandidate = { package: EngineeringPackageView; changeVersion: number };
type DesignerMode = 'design' | 'preview';

const MILLIMETER_SCALE = 3;
const A4_PORTRAIT_WIDTH_MM = 210;
const A4_LANDSCAPE_WIDTH_MM = 297;

export function ReportDesignerWorkspace({ snapshot, locale, onApplied }: ReportDesignerWorkspaceProps) {
  const text = useMemo(() => copy(locale), [locale]);
  const updateMetadata = (key: string, value: string) => updateDraft(current => ({ ...current, metadata: {...current.metadata, [key]: value} }));
  const updatePage = (patch: Partial<NonNullable<ReportEngineeringDto["page"]>>) => updateDraft(current => ({ ...current, page: { ...current.page, ...patch } }));
  const reports = reportCollection(snapshot.package);
  const [selectedIdentity, setSelectedIdentity] = useState<string>(() =>
    reports[0] ? reportIdentity(reports[0]) : NEW_REPORT_IDENTITY);
  const isNew = selectedIdentity === NEW_REPORT_IDENTITY;
  const selected = !isNew
    ? reports.find(report => matchesReportIdentity(report, selectedIdentity)) ?? null
    : null;
  const [draft, setDraft] = useState<ReportEngineeringDto>(() =>
    selected ? cloneReport(selected) : createReportDraft(reports));
  const [selectedSectionKey, setSelectedSectionKey] = useState<string>(() => draft.sections?.[0]?.key ?? '');
  const [selectedControlKey, setSelectedControlKey] = useState<string | null>(null);
  const [mode, setMode] = useState<DesignerMode>('design');
  const [engineeringPreview, setEngineeringPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<ValidatedCandidate | null>(null);
  const [execution, setExecution] = useState<ReportExecutionResult | null>(null);
  const [runtimePeriodSeconds, setRuntimePeriodSeconds] = useState(() => readDefaultPeriod(draft));
  const [error, setError] = useState<string | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [validating, setValidating] = useState(false);
  const [applying, setApplying] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const previewAbort = useRef<AbortController | null>(null);

  const changed = isNew || (selected !== null && JSON.stringify(selected) !== JSON.stringify(draft));
  const selectedSection = draft.sections?.find(section => section.key === selectedSectionKey) ?? null;
  const selectedControl = selectedSection?.controls?.find(control => control.key === selectedControlKey) ?? null;
  const primaryQuery = draft.queries?.[0] ?? null;
  const dataQueries = snapshot.package.dataQueries ?? [];
  const reportReferences = useMemo(
    () => buildProjectReferenceCatalog(snapshot.package)
      .filter(reference => reference.family === 'tag' && Boolean(reference.tagReference?.tagId)),
    [snapshot.package]);
  const pageWidth = (draft.page?.orientation ?? 'portrait') === 'landscape'
    ? A4_LANDSCAPE_WIDTH_MM
    : A4_PORTRAIT_WIDTH_MM;
  const contentWidth = Math.max(
    20,
    pageWidth - (draft.page?.marginLeftMillimeters ?? 10) - (draft.page?.marginRightMillimeters ?? 10));

  const invalidateEngineeringValidation = () => {
    setEngineeringPreview(null);
    setCandidate(null);
    setValidationMessage(null);
  };

  const clearExecution = () => {
    previewAbort.current?.abort();
    previewAbort.current = null;
    setExecution(null);
    setMode('design');
  };

  useEffect(() => () => previewAbort.current?.abort(), []);

  useEffect(() => {
    if (selectedIdentity === NEW_REPORT_IDENTITY) {
      const next = createReportDraft(reports);
      setDraft(next);
      setSelectedSectionKey(next.sections?.[0]?.key ?? '');
      setSelectedControlKey(null);
      setRuntimePeriodSeconds(readDefaultPeriod(next));
      invalidateEngineeringValidation();
      clearExecution();
      return;
    }

    const current = reports.find(report => matchesReportIdentity(report, selectedIdentity)) ?? null;
    if (current) {
      const next = cloneReport(current);
      setDraft(next);
      setSelectedSectionKey(next.sections?.[0]?.key ?? '');
      setSelectedControlKey(null);
      setRuntimePeriodSeconds(readDefaultPeriod(next));
      invalidateEngineeringValidation();
      clearExecution();
      return;
    }

    if (reports[0]) setSelectedIdentity(reportIdentity(reports[0]));
    else setSelectedIdentity(NEW_REPORT_IDENTITY);
  }, [selectedIdentity, snapshot.package]);

  useEffect(() => {
    if (!changed && !applying && !previewing) return undefined;
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [changed, applying, previewing]);

  const chooseReport = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text.discardConfirm)) return;
    setSelectedIdentity(identity);
  };

  const updateDraft = (update: (current: ReportEngineeringDto) => ReportEngineeringDto) => {
    setDraft(current => update(current));
    invalidateEngineeringValidation();
    clearExecution();
    setError(null);
  };

  const resetDraft = () => {
    const next = selected ? cloneReport(selected) : createReportDraft(reports);
    setDraft(next);
    setSelectedSectionKey(next.sections?.[0]?.key ?? '');
    setSelectedControlKey(null);
    setRuntimePeriodSeconds(readDefaultPeriod(next));
    invalidateEngineeringValidation();
    clearExecution();
    setError(null);
  };

  const validateDraft = async () => {
    setValidating(true);
    setError(null);
    setValidationMessage(null);
    setEngineeringPreview(null);
    setCandidate(null);
    try {
      const nextPackage = replaceReportInPackage(snapshot.package, selected, draft);
      const before = await loadEngineeringWorkspace();
      const nextPreview = await previewEngineeringPackage(nextPackage);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion)
        throw new Error(text.workspaceChanged);
      setEngineeringPreview(nextPreview);
      if (nextPreview.canApply) {
        setCandidate({ package: JSON.parse(JSON.stringify(nextPackage)) as EngineeringPackageView, changeVersion: after.changeVersion });
        setValidationMessage(text.validationPassed);
      } else {
        setValidationMessage(text.validationFailed);
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setValidating(false);
    }
  };

  const applyDraft = async () => {
    if (!candidate || !engineeringPreview?.canApply) return;
    if (!window.confirm(text.applyConfirm)) return;
    setApplying(true);
    setError(null);
    try {
      const appliedKey = draft.key;
      await applyEngineeringPackage(candidate.package, candidate.changeVersion);
      await onApplied();
      setSelectedIdentity(`key:${appliedKey}`);
      setEngineeringPreview(null);
      setCandidate(null);
      setValidationMessage(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setEngineeringPreview(null);
      setCandidate(null);
      setValidationMessage(null);
    } finally {
      setApplying(false);
    }
  };

  const executePreview = async () => {
    previewAbort.current?.abort();
    const controller = new AbortController();
    previewAbort.current = controller;
    setPreviewing(true);
    setError(null);
    setExecution(null);
    try {
      const parameters: Record<string, ReportParameterValue> = {};
      if ((draft.parameters ?? []).some(parameter => parameter.key === 'periodSeconds')) {
        const parsed = Number(runtimePeriodSeconds);
        if (!Number.isFinite(parsed) || parsed <= 0)
          throw new Error(text.runtimePeriodInvalid);
        parameters.periodSeconds = { type: 'durationSeconds', value: String(Math.trunc(parsed)) };
      }
      const result = await previewReportExecution({ report: draft, parameters }, controller.signal);
      if (controller.signal.aborted) return;
      setExecution(result);
      setMode('preview');
    } catch (reason) {
      if (controller.signal.aborted) return;
      if (reason instanceof ReportPreviewApiError && reason.status === 401)
        setError(text.unauthorized);
      else if (reason instanceof ReportPreviewApiError && reason.status === 403)
        setError(text.forbidden);
      else
        setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      if (previewAbort.current === controller) previewAbort.current = null;
      if (!controller.signal.aborted) setPreviewing(false);
    }
  };

  const cancelPreview = () => {
    previewAbort.current?.abort();
    previewAbort.current = null;
    setPreviewing(false);
  };

  const issues = engineeringPreview?.items.flatMap(item => item.issues ?? []) ?? [];
  const executionRows = execution?.queries.reduce((sum, query) => sum + query.rows.length, 0) ?? 0;

  return <div className="eng-section report-designer-workspace" data-testid="report-designer-workspace">
    <header className="report-designer-header">
      <h1>{text.title}</h1>
      <details className="report-designer-help">
        <summary>{locale === 'pt-BR' ? 'Ajuda' : locale === 'es' ? 'Ayuda' : 'Help'}</summary>
        <div><p>{text.description}</p><p>{text.authorityHint}</p><a href="/help?topic=reports.data-designer">{locale === 'pt-BR' ? 'Como configurar dados, campos e página' : locale === 'es' ? 'Cómo configurar datos, campos y página' : 'How to configure data, fields and page'}</a></div>
      </details>
    </header>

    <div className="report-designer-shell">
      <aside className="report-designer-list" aria-label={text.reportList}>
        <header>
          <strong>{text.reports}</strong>
          <button type="button" className={isNew ? 'active' : ''} onClick={() => chooseReport(NEW_REPORT_IDENTITY)}>+ {text.newReport}</button>
        </header>
        <div className="report-designer-report-list">
          {reports.map(report => <button
            type="button"
            className={matchesReportIdentity(report, selectedIdentity) ? 'selected' : ''}
            key={reportIdentity(report)}
            onClick={() => chooseReport(reportIdentity(report))}
          >
            <strong>{report.name || report.key}</strong>
            <code>{report.key}</code>
            <span>{report.sections?.length ?? 0} {text.sections}</span>
          </button>)}
          {reports.length === 0 ? <p>{text.noReports}</p> : null}
        </div>
      </aside>

      <section className="report-designer-main">
        <div className="report-designer-form">
          <label><span>{text.name}</span><input value={draft.name} onChange={event => updateDraft(current => ({ ...current, name: event.target.value }))}/></label>
          <label><span>{text.key}</span><input className="mono" value={draft.key} onChange={event => updateDraft(current => ({ ...current, key: event.target.value }))}/></label>
          <label className="wide"><span>{text.descriptionLabel}</span><input value={draft.description ?? ''} onChange={event => updateDraft(current => ({ ...current, description: emptyToNull(event.target.value) }))}/></label>
          <label><span>{text.orientation}</span><select value={draft.page?.orientation ?? 'portrait'} onChange={event => updateDraft(current => ({
            ...current,
            page: { ...(current.page ?? {}), orientation: event.target.value as 'portrait' | 'landscape' }
          }))}><option value="portrait">{text.portrait}</option><option value="landscape">{text.landscape}</option></select></label>
        </div>

        <fieldset className="report-page-settings">
          <legend>{text.pagination}</legend>
          <label><input type="checkbox" checked={draft.page?.showPageNumbers !== false} onChange={e => updatePage({ showPageNumbers: e.target.checked })}/>{text.showPage}</label>
          {draft.page?.showPageNumbers !== false && <>
            <label>{text.position}<select value={draft.page?.pageNumberPosition ?? 'footer'} onChange={e => updatePage({ pageNumberPosition: e.target.value as 'header' | 'footer' })}><option value="header">{text.header}</option><option value="footer">{text.footer}</option></select></label>
            <label>{text.alignment}<select value={draft.page?.pageNumberAlignment ?? 'center'} onChange={e => updatePage({ pageNumberAlignment: e.target.value as 'left' | 'center' | 'right' })}><option value="left">{text.left}</option><option value="center">{text.center}</option><option value="right">{text.right}</option></select></label>
            <label>{text.font}<select value={draft.page?.pageNumberFontFamily ?? 'Arial'} onChange={e => updatePage({ pageNumberFontFamily: e.target.value })}>{reportFonts.map(font => <option key={font}>{font}</option>)}</select></label>
            <label>{text.size}<input type="number" min={6} max={36} value={draft.page?.pageNumberFontSizePoints ?? 8} onChange={e => updatePage({ pageNumberFontSizePoints: Number(e.target.value) })}/></label>
            <label><input type="checkbox" checked={draft.page?.showPageCount !== false} onChange={e => updatePage({ showPageCount: e.target.checked })}/>{text.total}</label>
          </>}
        </fieldset>
        <div className="report-designer-toolbar">
          <div className="report-designer-mode">
            <button type="button" className={mode === 'design' ? 'active' : ''} onClick={() => setMode('design')}>{text.design}</button>
            <button type="button" className={mode === 'preview' ? 'active' : ''} disabled={!execution} onClick={() => setMode('preview')}>{text.previewMode}</button>
          </div>
          <div className="report-designer-actions">
            <button type="button" onClick={resetDraft} disabled={applying || validating || previewing}>{text.reset}</button>
            <button type="button" onClick={() => void validateDraft()} disabled={applying || validating || previewing}>{validating ? text.validating : text.validate}</button>
            <button type="button" className="primary" onClick={() => void applyDraft()} disabled={!candidate || !engineeringPreview?.canApply || applying || previewing}>{applying ? text.applying : text.apply}</button>
          </div>
        </div>

        {error ? <div className="report-designer-state error" role="alert"><strong>{text.error}</strong><span>{error}</span></div> : null}
        {validationMessage ? <div className={`report-designer-state ${engineeringPreview?.canApply ? 'success' : 'warning'}`}><strong>{validationMessage}</strong>{issues.length > 0 ? <ul>{issues.slice(0, 8).map((issue, index) => <li key={`${issue.code}-${index}`}>{issue.code}: {issue.message}</li>)}</ul> : null}</div> : null}

        <div className="report-designer-composition">
          <aside className="report-designer-left">
            <section className="report-designer-panel" data-testid="report-query-editor">
              <h2>{text.query}</h2>
              <label><span>{text.savedDataQuery}</span><select value={primaryQuery?.dataQueryId ?? ''} onChange={event => {
                const saved = dataQueries.find(item => item.id === event.target.value);
                updateDraft(current => ({
                  ...current,
                  queries: (current.queries ?? []).map((query, index) => index === 0
                    ? { ...query, dataQueryId: saved?.id ?? null, dataQueryKey: saved?.key ?? null }
                    : query)
                }));
              }}>
                <option value="">{text.embeddedQuery}</option>
                {dataQueries.filter(item => Boolean(item.id)).map(item => <option key={item.id!} value={item.id!}>{item.name} · {item.key}</option>)}
              </select></label>
              <label><span>{text.dataset}</span><select value={primaryQuery?.query.datasetKey ?? 'historian.samples'} onChange={event => updateDraft(current => updatePrimaryQueryDataset(current, event.target.value as 'historian.samples' | 'alarm.events'))}>
                <option value="historian.samples">historian.samples</option>
                <option value="alarm.events">alarm.events</option>
              </select></label>
              <DurationEditor label={text.defaultPeriod} seconds={readDefaultPeriod(draft)} locale={locale} onChange={value => updateDraft(current => updateRelativeDuration(current, value))}/>
              <DurationEditor label={text.runtimePeriod} seconds={runtimePeriodSeconds} locale={locale} onChange={value => { setRuntimePeriodSeconds(String(value)); setExecution(null); setMode('design'); }}/>
              <label><span>{text.resolution}</span><select value={draft.resolution?.mode ?? 'raw'} onChange={event => updateDraft(current => ({
                ...current,
                resolution: resolutionForMode(event.target.value as 'raw' | 'sampledFixedStep' | 'aggregate', current.resolution)
              }))}>
                <option value="raw">{text.raw}</option>
                <option value="sampledFixedStep">{text.fixedInterval}</option>
                <option value="aggregate">{text.summary}</option>
              </select></label>
              {(draft.resolution?.mode ?? 'raw') === 'sampledFixedStep' ? <label><span>{text.interval}</span><select value={draft.resolution?.intervalMilliseconds ?? 300000} onChange={event => updateDraft(current => ({
                ...current, resolution: { ...(current.resolution ?? {}), mode: 'sampledFixedStep', intervalMilliseconds: Number(event.target.value), maximumGapMilliseconds: current.resolution?.maximumGapMilliseconds ?? 300000, bucketAlignment: 'utcDuration' }
              }))}>{intervalOptions().map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label> : null}
              {(draft.resolution?.mode ?? 'raw') === 'aggregate' ? <>
                <label><span>{text.aggregate}</span><select value={draft.resolution?.aggregateFunction ?? 'average'} onChange={event => updateDraft(current => ({
                  ...current, resolution: { ...(current.resolution ?? {}), mode: 'aggregate', aggregateFunction: event.target.value as NonNullable<ReportEngineeringDto['resolution']>['aggregateFunction'], bucketMilliseconds: current.resolution?.bucketMilliseconds ?? 3600000, bucketAlignment: 'utcDuration' }
                }))}>
                  <option value="average">{text.average}</option><option value="minimum">{text.minimum}</option><option value="maximum">{text.maximum}</option><option value="sum">{text.sum}</option><option value="count">{text.count}</option><option value="first">First</option><option value="last">Last</option>
                </select></label>
                <label><span>{text.bucket}</span><select value={draft.resolution?.bucketMilliseconds ?? 3600000} onChange={event => updateDraft(current => ({
                  ...current, resolution: { ...(current.resolution ?? {}), mode: 'aggregate', aggregateFunction: current.resolution?.aggregateFunction ?? 'average', bucketMilliseconds: Number(event.target.value), bucketAlignment: 'utcDuration' }
                }))}>{bucketOptions().map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
              </> : null}
              <label><span>{text.tableLayout}</span><select disabled={draft.resolution?.latestObserved === true} value={draft.tableLayout ?? 'long'} onChange={event => updateDraft(current => ({ ...current, tableLayout: event.target.value as 'long' | 'wide' }))}>
                <option value="long">{text.longTable}</option>
                <option value="wide" disabled={(draft.resolution?.mode ?? 'raw') === 'raw'}>{text.wideTable}</option>
              </select></label>
              {draft.resolution?.mode === 'sampledFixedStep' && <>
                <label><input type="checkbox" checked={draft.resolution.latestObserved === true} onChange={e => updateDraft(current => ({...current, tableLayout: e.target.checked ? 'wide' : current.tableLayout, resolution: {...current.resolution!, latestObserved: e.target.checked}}))}/>{text.observed}</label>
                {draft.resolution.latestObserved && <>
                  <label>{text.numberFormat}<select value={draft.metadata?.numberCulture ?? ''} onChange={e => updateMetadata('numberCulture', e.target.value)}><option value="">1234.50</option><option value="pt-BR">Português (Brasil)</option><option value="en-US">English (US)</option><option value="es-ES">Español</option></select></label>
                  <label>{text.quality}<input value={draft.metadata?.invalidQualityLabel ?? 'Invalid quality'} onChange={e => updateMetadata('invalidQualityLabel', e.target.value)}/></label>
                </>}
                {draft.resolution.latestObserved && <label>{text.tolerance}<input type="number" min={0.001} step="any" value={(draft.resolution.maximumGapMilliseconds ?? 15000) / 1000} onChange={e => updateDraft(current => ({...current, resolution: {...current.resolution!, maximumGapMilliseconds: Math.round(Number(e.target.value) * 1000)}}))}/></label>}
              </>}
              {primaryQuery?.query.datasetKey === 'alarm.events' && <label><input type="checkbox" checked={draft.metadata?.hideEventIds === 'true'} onChange={e => updateDraft(current => ({...current, metadata: {...current.metadata, hideEventIds: String(e.target.checked)}}))}/>{text.hideIds}</label>}
              <div className="report-preview-actions">
                <button type="button" className="primary" onClick={() => void executePreview()} disabled={previewing || applying}>{previewing ? text.previewing : text.runPreview}</button>
                {previewing ? <button type="button" onClick={cancelPreview}>{text.cancel}</button> : null}
              </div>
              <small>{text.queryHint}</small>
            </section>

            <section className="report-designer-panel report-variable-panel" data-testid="report-variable-editor">
              <h2>{text.variables}</h2>
              <p>{text.variablesHint}</p>
              <ProjectReferenceBrowser
                references={reportReferences}
                locale={locale}
                title={text.addVariables}
                isSelectable={reference => Boolean(reference.tagReference?.tagId)}
                onSelect={reference => updateDraft(current => addReportVariable(current, reference))}
              />
              <div className="report-variable-list">
                {(draft.variables ?? []).slice().sort((a, b) => (a.order ?? 0) - (b.order ?? 0)).map(variable =>
                  <VariableEditor
                    key={variable.tagId}
                    variable={variable}
                    text={text}
                    onChange={update => updateDraft(current => updateReportVariable(current, variable.tagId, update))}
                    onMove={direction => updateDraft(current => moveReportVariable(current, variable.tagId, direction))}
                    onRemove={() => updateDraft(current => ({ ...current, variables: (current.variables ?? []).filter(item => item.tagId !== variable.tagId) }))}
                  />)}
              </div>
            </section>

            <section className="report-designer-panel" data-testid="report-group-summary-editor">
              <h2>{text.groupsAndSummaries}</h2>
              <p>{text.groupsHint}</p>
              <div className="report-group-list">
                {(draft.groups ?? []).map(group => <article key={group.key} className="report-group-card">
                  <strong>{group.key}</strong>
                  <label><span>{text.groupField}</span><select value={group.field} onChange={event => updateDraft(current => ({
                    ...current,
                    groups: (current.groups ?? []).map(item => item.key === group.key ? { ...item, field: event.target.value } : item)
                  }))}>{reportFieldOptions(draft).map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
                  <label><span>{text.order}</span><select value={group.direction ?? 'ascending'} onChange={event => updateDraft(current => ({
                    ...current,
                    groups: (current.groups ?? []).map(item => item.key === group.key ? { ...item, direction: event.target.value as 'ascending' | 'descending' } : item)
                  }))}><option value="ascending">{text.ascending}</option><option value="descending">{text.descending}</option></select></label>
                  <button type="button" className="danger" onClick={() => updateDraft(current => ({ ...current, groups: (current.groups ?? []).filter(item => item.key !== group.key) }))}>{text.remove}</button>
                </article>)}
              </div>
              <button type="button" onClick={() => updateDraft(current => addReportGroup(current))}>+ {text.addGroup}</button>
              <div className="report-summary-list">
                {(draft.aggregates ?? []).map(aggregate => <article key={aggregate.key} className="report-group-card">
                  <strong>{aggregate.key}</strong>
                  <label><span>{text.aggregate}</span><select value={aggregate.function} onChange={event => updateDraft(current => ({
                    ...current,
                    aggregates: (current.aggregates ?? []).map(item => item.key === aggregate.key ? { ...item, function: event.target.value as typeof item.function } : item)
                  }))}>
                    <option value="average">{text.average}</option><option value="minimum">{text.minimum}</option><option value="maximum">{text.maximum}</option><option value="sum">{text.sum}</option><option value="count">{text.count}</option><option value="first">First</option><option value="last">Last</option>
                  </select></label>
                  <label><span>{text.summaryField}</span><select value={aggregate.field ?? ''} onChange={event => updateDraft(current => ({
                    ...current,
                    aggregates: (current.aggregates ?? []).map(item => item.key === aggregate.key ? { ...item, field: event.target.value || null } : item)
                  }))}><option value="">{text.allRows}</option>{reportFieldOptions(draft).map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
                  <label><span>{text.scope}</span><select value={aggregate.groupKey ?? ''} onChange={event => updateDraft(current => ({
                    ...current,
                    aggregates: (current.aggregates ?? []).map(item => item.key === aggregate.key ? { ...item, groupKey: event.target.value || null } : item)
                  }))}><option value="">{text.reportScope}</option>{(draft.groups ?? []).map(group => <option key={group.key} value={group.key}>{group.key}</option>)}</select></label>
                  <button type="button" className="danger" onClick={() => updateDraft(current => ({ ...current, aggregates: (current.aggregates ?? []).filter(item => item.key !== aggregate.key) }))}>{text.remove}</button>
                </article>)}
              </div>
              <button type="button" onClick={() => updateDraft(current => addReportAggregate(current))}>+ {text.addSummary}</button>
            </section>

            <section className="report-designer-panel">
              <h2>{text.sections}</h2>
              <div className="report-section-list">
                {(draft.sections ?? []).map(section => <button type="button" className={section.key === selectedSectionKey ? 'selected' : ''} key={section.key} onClick={() => { setSelectedSectionKey(section.key); setSelectedControlKey(null); }}>
                  <strong>{sectionLabel(section.kind, text)}</strong>
                  <code>{section.key}</code>
                  <span>{section.heightMillimeters} mm · {section.controls?.length ?? 0} {text.controls}</span>
                </button>)}
              </div>
              <div className="report-add-controls">{(['pageHeader', 'pageFooter'] as const).filter(kind => !draft.sections?.some(section => section.kind === kind)).map(kind => <button type="button" key={kind} onClick={() => {
                let key: string = kind;
                while (draft.sections?.some(section => section.key === key)) key += '-1';
                updateDraft(current => ({...current, sections: [...(current.sections ?? []), { key, kind, heightMillimeters: 16, controls: [] }]}));
                setSelectedSectionKey(key); setSelectedControlKey(null);
              }}>+ {kind === 'pageHeader' ? text.header : text.footer}</button>)}</div>
              {selectedSection ? <label><span>{text.sectionHeight}</span><input type="number" min="0.1" step="0.5" value={selectedSection.heightMillimeters} onChange={event => updateDraft(current => updateReportSection(current, selectedSection.key, section => ({ ...section, heightMillimeters: Number(event.target.value) })))}/></label> : null}
              {selectedSection ? <div className="report-add-controls"><button type="button" onClick={() => { updateDraft(current => addReportControl(current, selectedSection.key, 'label')); }}>{text.addLabel}</button><button type="button" onClick={() => { updateDraft(current => addReportControl(current, selectedSection.key, 'dataField')); }}>{text.addField}</button><button type="button" onClick={() => updateDraft(current => addReportControl(current, selectedSection.key, 'image'))}>{text.addImage}</button></div> : null}
            </section>
          </aside>

          <section className="report-designer-canvas-wrap">
            <header>
              <div><strong>{mode === 'preview' ? text.previewMode : text.design}</strong><code>{draft.page?.paperSizeKey ?? 'A4'} · {draft.page?.orientation ?? 'portrait'}</code></div>
              <span>{contentWidth.toFixed(1)} mm {text.contentWidth}</span>
            </header>
            {previewing ? <div className="report-designer-loading" aria-live="polite"><span className="eng-spinner"/><strong>{text.previewing}</strong></div> : null}
            {mode === 'preview' && execution ? <ReportPreviewCanvas report={draft} result={execution} widthMillimeters={contentWidth} text={text}/> : <ReportDesignCanvas
              report={draft}
              widthMillimeters={contentWidth}
              selectedSectionKey={selectedSectionKey}
              selectedControlKey={selectedControlKey}
              onSelect={(sectionKey, controlKey) => { setSelectedSectionKey(sectionKey); setSelectedControlKey(controlKey); }}
              text={text}
            />}
            {mode === 'preview' && execution && executionRows === 0 ? <div className="report-designer-empty"><strong>{text.emptyPreview}</strong><span>{text.emptyPreviewHint}</span></div> : null}
          </section>

          <aside className="report-designer-right">
            <section className="report-designer-panel" data-testid="report-control-inspector">
              <h2>{text.inspector}</h2>
              {!selectedControl || !selectedSection ? <div className="report-designer-empty compact"><span>{text.selectControl}</span></div> : <ControlInspector
                report={draft}
                section={selectedSection}
                assets={snapshot.package.visualAssets ?? []}
                control={selectedControl}
                text={text}
                onChange={update => updateDraft(current => updateReportControl(current, selectedSection.key, selectedControl.key, update))}
                onDelete={() => {
                  updateDraft(current => removeReportControl(current, selectedSection.key, selectedControl.key));
                  setSelectedControlKey(null);
                }}
              />}
            </section>
            <section className="report-designer-panel report-preview-summary">
              <h2>{text.previewSummary}</h2>
              {!execution ? <span>{text.previewNotRun}</span> : <>
                <strong>{executionRows} {text.rows}</strong>
                {execution.queries.map(query => <div key={query.queryKey}><code>{query.queryKey}</code><span>{query.dataset}</span><small>{formatDate(query.fromUtc, locale)} → {formatDate(query.toUtc, locale)}</small></div>)}
              </>}
            </section>
          </aside>
        </div>
      </section>
    </div>
  </div>;
}

function PageNumberSample({ report, position }: { report: ReportEngineeringDto; position: 'header' | 'footer' }) {
  const page = report.page;
  if (page?.showPageNumbers === false || (page?.pageNumberPosition ?? 'footer') !== position) return null;
  return <div style={{ textAlign: page?.pageNumberAlignment ?? 'center', font: (page?.pageNumberFontSizePoints ?? 8) + 'pt "' + (page?.pageNumberFontFamily ?? 'Arial') + '"' }}>1{page?.showPageCount !== false && ' / 1'}</div>;
}

function ReportDesignCanvas({ report, widthMillimeters, selectedSectionKey, selectedControlKey, onSelect, text }: {
  report: ReportEngineeringDto;
  widthMillimeters: number;
  selectedSectionKey: string;
  selectedControlKey: string | null;
  onSelect: (sectionKey: string, controlKey: string | null) => void;
  text: ReturnType<typeof copy>;
}) {
  return <div className="report-page" style={{ width: widthMillimeters * MILLIMETER_SCALE }} data-unit="millimeter">
    <PageNumberSample report={report} position="header"/>
    {(report.sections ?? []).map(section => <div
      className={`report-section ${selectedSectionKey === section.key ? 'selected' : ''}`}
      key={section.key}
      style={{ height: section.heightMillimeters * MILLIMETER_SCALE }}
      onClick={() => onSelect(section.key, null)}
      data-section-kind={section.kind}
      data-height-mm={section.heightMillimeters}
    >
      <span className="report-section-tag">{sectionLabel(section.kind, text)}</span>
      {(section.controls ?? []).map(control => <ReportControlView
        key={control.key}
        control={control}
        selected={selectedSectionKey === section.key && selectedControlKey === control.key}
        onClick={event => { event.stopPropagation(); onSelect(section.key, control.key); }}
      />)}
    </div>)}
    <PageNumberSample report={report} position="footer"/>
  </div>;
}

function ReportPreviewCanvas({ report, result, widthMillimeters, text }: {
  report: ReportEngineeringDto;
  result: ReportExecutionResult;
  widthMillimeters: number;
  text: ReturnType<typeof copy>;
}) {
  return <div className="report-page preview" style={{ width: widthMillimeters * MILLIMETER_SCALE }} data-testid="report-preview-canvas" data-unit="millimeter">
    <PageNumberSample report={report} position="header"/>
    {(report.sections ?? []).flatMap(section => {
      const query = queryResult(result, section.queryKey);
      const repeats = section.kind === 'detail' ? Math.max(1, query?.rows.length ?? 0) : 1;
      return Array.from({ length: repeats }, (_, rowIndex) => {
        const row = section.kind === 'detail' ? query?.rows[rowIndex] ?? null : null;
        return <div className="report-section preview" key={`${section.key}-${rowIndex}`} style={{ height: section.heightMillimeters * MILLIMETER_SCALE }} data-section-kind={section.kind}>
          <span className="report-section-tag">{sectionLabel(section.kind, text)}{section.kind === 'detail' ? ` #${rowIndex + 1}` : ''}</span>
          {(section.controls ?? []).map(control => <ReportControlView key={control.key} control={control} row={row}/>) }
        </div>;
      });
    })}
  </div>;
}

function ReportControlView({ control, row, selected = false, onClick }: {
  control: ReportControlEngineeringDto;
  row?: HistoricalQueryRow | null;
  selected?: boolean;
  onClick?: React.MouseEventHandler<HTMLButtonElement>;
}) {
  const style: React.CSSProperties = {
    left: control.xMillimeters * MILLIMETER_SCALE,
    top: control.yMillimeters * MILLIMETER_SCALE,
    width: Math.max(1, control.widthMillimeters * MILLIMETER_SCALE),
    height: Math.max(1, control.heightMillimeters * MILLIMETER_SCALE),
    fontFamily: control.style?.fontFamily ?? undefined,
    fontSize: control.style?.fontSizePoints ? `${control.style.fontSizePoints}pt` : undefined,
    fontWeight: control.style?.bold ? 700 : undefined,
    fontStyle: control.style?.italic ? 'italic' : undefined,
    textAlign: control.style?.textAlignment ?? undefined,
    color: control.style?.foreground ?? undefined,
    background: control.style?.background ?? undefined,
    borderWidth: control.style?.borderWidth ?? undefined
  };
  const value = control.kind === 'dataField' || control.kind === 'booleanState'
    ? formatHistoricalValue(row?.cells[control.field ?? ''])
    : control.text ?? control.key;
  if (control.kind === 'image' && control.assetId) {
    return <button type="button" className={`report-control image ${selected ? 'selected' : ''}`} style={style} onClick={onClick} data-x-mm={control.xMillimeters} data-y-mm={control.yMillimeters}>
      <img src={visualAssetContentUrl(control.assetId)} alt={control.text ?? control.key}/>
    </button>;
  }
  return <button type="button" className={`report-control ${selected ? 'selected' : ''}`} style={style} onClick={onClick} data-kind={control.kind} data-x-mm={control.xMillimeters} data-y-mm={control.yMillimeters}>{value}</button>;
}

function ControlInspector({ report, section, control, assets, text, onChange, onDelete }: {
  assets: NonNullable<EngineeringPackageView['visualAssets']>;
  report: ReportEngineeringDto;
  section: ReportSectionEngineeringDto;
  control: ReportControlEngineeringDto;
  text: ReturnType<typeof copy>;
  onChange: (update: (control: ReportControlEngineeringDto) => ReportControlEngineeringDto) => void;
  onDelete: () => void;
}) {
  const primaryQuery = report.queries?.[0];
  const dataset = primaryQuery?.query.datasetKey;
  return <div className="report-control-form">
    <label><span>{text.controlKey}</span><input className="mono" value={control.key} onChange={event => onChange(current => ({ ...current, key: event.target.value }))}/></label>
    <div className="report-geometry-grid">
      <NumberField label="X (mm)" value={control.xMillimeters} onChange={value => onChange(current => ({ ...current, xMillimeters: value }))}/>
      <NumberField label="Y (mm)" value={control.yMillimeters} onChange={value => onChange(current => ({ ...current, yMillimeters: value }))}/>
      <NumberField label={text.widthMm} value={control.widthMillimeters} min={0.1} onChange={value => onChange(current => ({ ...current, widthMillimeters: value }))}/>
      <NumberField label={text.heightMm} value={control.heightMillimeters} min={0.1} onChange={value => onChange(current => ({ ...current, heightMillimeters: value }))}/>
    </div>
    {control.kind === 'label' ? <label><span>{text.text}</span><textarea value={control.text ?? ''} onChange={event => onChange(current => ({ ...current, text: event.target.value }))}/></label> : null}
    {control.kind === 'dataField' || control.kind === 'booleanState' ? <>
      <label><span>{text.queryKey}</span><select value={control.queryKey ?? primaryQuery?.key ?? ''} onChange={event => onChange(current => ({ ...current, queryKey: event.target.value }))}>{(report.queries ?? []).map(query => <option key={query.key} value={query.key}>{query.key}</option>)}</select></label>
      <label><span>{text.field}</span><input list="report-control-fields" className="mono" value={control.field ?? defaultFieldForDataset(dataset)} onChange={event => onChange(current => ({ ...current, field: event.target.value }))}/></label>
      <datalist id="report-control-fields">{reportFieldOptions(report).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</datalist>
      <small>{text.fieldHint}</small>
    </> : null}
    {control.kind === 'image' ? <label><span>{text.asset}</span><select value={control.assetId ?? ''} onChange={event => onChange(current => ({ ...current, assetId: emptyToNull(event.target.value) }))}><option value="">{text.noAsset}</option>{assets.filter(asset => asset.id && ['image/png', 'image/jpeg'].includes(asset.mediaType)).map(asset => <option key={asset.id!} value={asset.id!}>{asset.name}</option>)}</select></label> : null}
    {control.kind !== 'image' && <>
      <label>{text.font}<select value={control.style?.fontFamily ?? 'Arial'} onChange={e => onChange(current => ({...current, style: {...current.style, fontFamily: e.target.value}}))}>{reportFonts.map(font => <option key={font}>{font}</option>)}</select></label>
      <NumberField label={text.size} min={5} value={control.style?.fontSizePoints ?? 9} onChange={fontSizePoints => onChange(current => ({...current, style: {...current.style, fontSizePoints}}))}/>
    </>}
    <button type="button" className="danger" onClick={onDelete}>{text.deleteControl}</button>
    <small>{sectionLabel(section.kind, text)} · {section.heightMillimeters} mm</small>
  </div>;
}

function reportFieldOptions(report: ReportEngineeringDto) {
  const dataset = report.queries?.[0]?.query.datasetKey;
  const base = dataset === 'alarm.events'
    ? [
        { value: 'timestamp', label: 'Timestamp' },
        { value: 'priority', label: 'Priority' },
        { value: 'state', label: 'State' },
        { value: 'source', label: 'Source' }, { value: 'message', label: 'Message' }, { value: 'tag.path', label: 'Tag' }
      ]
    : [
        { value: 'timestamp', label: 'Timestamp' },
        { value: 'variable', label: 'Variable' },
        { value: 'value', label: 'Value' },
        { value: 'quality', label: 'Quality' }
      ];
  const variables = (report.variables ?? []).filter(item => item.visible !== false).map(item => ({
    value: report.resolution?.latestObserved ? item.displayLabel?.trim() || item.name : `v:${item.tagId}`,
    label: item.displayLabel || item.name
  }));
  return [...base, ...variables];
}

function addReportGroup(report: ReportEngineeringDto): ReportEngineeringDto {
  const queryKey = report.queries?.[0]?.key ?? 'main';
  const index = (report.groups?.length ?? 0) + 1;
  const field = reportFieldOptions(report)[0]?.value ?? 'timestamp';
  return {
    ...report,
    groups: [...(report.groups ?? []), { key: `group-${index}`, queryKey, field, direction: 'ascending' }]
  };
}

function addReportAggregate(report: ReportEngineeringDto): ReportEngineeringDto {
  const queryKey = report.queries?.[0]?.key ?? 'main';
  const index = (report.aggregates?.length ?? 0) + 1;
  return {
    ...report,
    aggregates: [...(report.aggregates ?? []), { key: `summary-${index}`, queryKey, function: 'average', field: 'value', groupKey: null }]
  };
}

function VariableEditor({ variable, text, onChange, onMove, onRemove }: {
  variable: ReportVariableEngineeringDto;
  text: ReturnType<typeof copy>;
  onChange: (update: (current: ReportVariableEngineeringDto) => ReportVariableEngineeringDto) => void;
  onMove: (direction: -1 | 1) => void;
  onRemove: () => void;
}) {
  return <article className="report-variable-card">
    <header><strong>{variable.name}</strong><code>{variable.path}</code></header>
    <small>{variable.dataType}{variable.engineeringUnit ? ` · ${variable.engineeringUnit}` : ''}{variable.source ? ` · ${variable.source}` : ''}</small>
    <label><input type="checkbox" checked={variable.visible !== false} onChange={event => onChange(current => ({ ...current, visible: event.target.checked }))}/><span>{text.visible}</span></label>
    <label><span>{text.displayLabel}</span><input value={variable.displayLabel ?? ''} onChange={event => onChange(current => ({ ...current, displayLabel: emptyToNull(event.target.value) }))}/></label>
    <label><span>{text.unit}</span><select value={variable.unitMode ?? 'automatic'} onChange={event => onChange(current => ({ ...current, unitMode: event.target.value as ReportVariableEngineeringDto['unitMode'] }))}>
      <option value="automatic">{text.unitAutomatic}</option><option value="hidden">{text.unitHidden}</option><option value="labelOverride">{text.unitOverride}</option>
    </select></label>
    {variable.unitMode === 'labelOverride' ? <label><span>{text.unitLabel}</span><input value={variable.unitLabel ?? ''} onChange={event => onChange(current => ({ ...current, unitLabel: event.target.value }))}/></label> : null}
    <label><span>{text.decimals}</span><input type="number" min="0" max="15" value={variable.decimalPlaces ?? ''} onChange={event => onChange(current => ({ ...current, decimalPlaces: event.target.value === '' ? null : Number(event.target.value) }))}/></label>
    <label><span>{text.numericFormat}</span><select value={variable.numericFormat ?? 'standard'} onChange={event => onChange(current => ({ ...current, numericFormat: event.target.value }))}>
      <option value="standard">{text.formatStandard}</option><option value="fixed">{text.formatFixed}</option><option value="scientific">{text.formatScientific}</option>
    </select></label>
    <label><span>{text.dateTimeFormat}</span><select value={variable.dateTimeFormat ?? 'local'} onChange={event => onChange(current => ({ ...current, dateTimeFormat: event.target.value }))}>
      <option value="local">{text.dateTimeLocal}</option><option value="date">{text.dateOnly}</option><option value="time">{text.timeOnly}</option><option value="iso">ISO 8601</option>
    </select></label>
    <div className="report-variable-boolean-labels">
      <label><span>{text.booleanTrue}</span><input value={variable.booleanTrueLabel ?? ''} onChange={event => onChange(current => ({ ...current, booleanTrueLabel: emptyToNull(event.target.value) }))}/></label>
      <label><span>{text.booleanFalse}</span><input value={variable.booleanFalseLabel ?? ''} onChange={event => onChange(current => ({ ...current, booleanFalseLabel: emptyToNull(event.target.value) }))}/></label>
    </div>
    <div className="report-variable-actions"><button type="button" onClick={() => onMove(-1)} aria-label={text.moveUp}>↑</button><button type="button" onClick={() => onMove(1)} aria-label={text.moveDown}>↓</button><button type="button" className="danger" onClick={onRemove}>{text.remove}</button></div>
  </article>;
}

function addReportVariable(report: ReportEngineeringDto, reference: ProjectReferenceDescriptor): ReportEngineeringDto {
  const tagId = reference.tagReference?.tagId;
  if (!tagId || (report.variables ?? []).some(variable => variable.tagId.toLowerCase() === tagId.toLowerCase())) return report;
  const variables = [...(report.variables ?? []), {
    tagId,
    path: reference.reference,
    name: reference.label,
    dataType: reference.dataType,
    engineeringUnit: reference.engineeringUnit ?? null,
    source: reference.providerIdentity ?? null,
    displayLabel: reference.label,
    visible: true,
    order: report.variables?.length ?? 0,
    unitMode: 'automatic' as const
  }];
  return { ...report, variables };
}

function updateReportVariable(
  report: ReportEngineeringDto,
  tagId: string,
  update: (current: ReportVariableEngineeringDto) => ReportVariableEngineeringDto
): ReportEngineeringDto {
  return { ...report, variables: (report.variables ?? []).map(variable => variable.tagId === tagId ? update(variable) : variable) };
}

function moveReportVariable(report: ReportEngineeringDto, tagId: string, direction: -1 | 1): ReportEngineeringDto {
  const variables = [...(report.variables ?? [])].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  const index = variables.findIndex(variable => variable.tagId === tagId);
  const target = index + direction;
  if (index < 0 || target < 0 || target >= variables.length) return report;
  [variables[index], variables[target]] = [variables[target], variables[index]];
  return { ...report, variables: variables.map((variable, order) => ({ ...variable, order })) };
}

function resolutionForMode(
  mode: 'raw' | 'sampledFixedStep' | 'aggregate',
  current: ReportEngineeringDto['resolution']
): NonNullable<ReportEngineeringDto['resolution']> {
  if (mode === 'raw') return { mode: 'raw', bucketAlignment: 'utcDuration' };
  if (mode === 'sampledFixedStep') return { mode, intervalMilliseconds: current?.intervalMilliseconds ?? 300000, maximumGapMilliseconds: current?.maximumGapMilliseconds ?? 300000, bucketAlignment: 'utcDuration' };
  return { mode, aggregateFunction: current?.aggregateFunction ?? 'average', bucketMilliseconds: current?.bucketMilliseconds ?? 3600000, bucketAlignment: 'utcDuration' };
}

function intervalOptions() {
  return [
    { value: 1000, label: '1 s' }, { value: 5000, label: '5 s' }, { value: 15000, label: '15 s' }, { value: 30000, label: '30 s' },
    { value: 60000, label: '1 min' }, { value: 300000, label: '5 min' }, { value: 900000, label: '15 min' },
    { value: 1800000, label: '30 min' }, { value: 3600000, label: '1 h' }
  ];
}

function bucketOptions() {
  return [
    { value: 60000, label: '1 min' }, { value: 300000, label: '5 min' }, { value: 900000, label: '15 min' },
    { value: 3600000, label: '1 h' }, { value: 86400000, label: '24 h (UTC)' }, { value: 604800000, label: '7 d (UTC)' }
  ];
}

function DurationEditor({ label, seconds, locale, onChange }: {
  label: string;
  seconds: string;
  locale: EngineeringLocale;
  onChange: (seconds: number) => void;
}) {
  const numeric = Math.max(1, Number(seconds) || 1);
  const presets = [
    { seconds: 900, label: '15 min' }, { seconds: 3600, label: '1 h' }, { seconds: 28800, label: '8 h' },
    { seconds: 86400, label: '24 h' }, { seconds: 604800, label: locale === 'en' ? '7 days' : locale === 'es' ? '7 días' : '7 dias' }
  ];
  const known = presets.some(item => item.seconds === numeric);
  return <label><span>{label}</span><select value={known ? numeric : 'custom'} onChange={event => {
    if (event.target.value !== 'custom') onChange(Number(event.target.value));
  }}>
    {presets.map(item => <option key={item.seconds} value={item.seconds}>{item.label}</option>)}
    {!known ? <option value="custom">{formatDuration(numeric, locale)}</option> : null}
  </select>
  {!known ? <input aria-label={label} type="number" min="1" value={numeric} onChange={event => onChange(Math.max(1, Number(event.target.value) || 1))}/> : null}
  </label>;
}

function formatDuration(seconds: number, locale: EngineeringLocale) {
  if (seconds % 86400 === 0) return `${seconds / 86400} ${locale === 'en' ? 'days' : locale === 'es' ? 'días' : 'dias'}`;
  if (seconds % 3600 === 0) return `${seconds / 3600} h`;
  if (seconds % 60 === 0) return `${seconds / 60} min`;
  return `${seconds} s`;
}

function NumberField({ label, value, min = 0, onChange }: { label: string; value: number; min?: number; onChange: (value: number) => void }) {
  return <label><span>{label}</span><input type="number" min={min} step="0.5" value={value} onChange={event => onChange(Number(event.target.value))}/></label>;
}

function readDefaultPeriod(report: ReportEngineeringDto): string {
  const parameter = report.parameters?.find(item => item.key === 'periodSeconds');
  if (parameter?.defaultValue.type === 'durationSeconds') return parameter.defaultValue.value;
  const duration = report.queries?.[0]?.query.timeRange.durationSeconds;
  return String(duration ?? 3600);
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function formatDate(value: string, locale: EngineeringLocale): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}

type ReportCopy = ReturnType<typeof copy>;

function sectionLabel(kind: ReportSectionEngineeringDto['kind'], text: ReportCopy): string {
  const labels: Record<ReportSectionEngineeringDto['kind'], string> = {
    reportHeader: text.reportHeader,
    reportFooter: text.reportFooter,
    pageHeader: text.pageHeader,
    pageFooter: text.pageFooter,
    groupHeader: text.groupHeader,
    detail: text.detail,
    groupFooter: text.groupFooter
  };
  return labels[kind];
}

const reportFonts = ['Arial', 'Times New Roman', 'Courier New'];

function copy(locale: EngineeringLocale) {
  const pt = {
    numberFormat: "Formato dos números", quality: "Texto para qualidade inválida", pagination: "Paginação", showPage: "Mostrar página", position: "Posição", header: "Cabeçalho", footer: "Rodapé", alignment: "Alinhamento", left: "Esquerda", center: "Centro", right: "Direita", font: "Fonte", size: "Tamanho (pt)", total: "Incluir total de páginas", addImage: "+ Imagem", observed: "Última amostra observada", tolerance: "Tolerância (s)", hideIds: "Ocultar identificadores de alarmes e tags",
    eyebrow: 'Engineering · Reporting', title: 'Designer de Relatórios', description: 'Configure o relatório, verifique o layout e confira os dados na pré-visualização.',
    authorityTitle: 'Layout do relatório', authorityHint: 'A pré-visualização mantém o layout salvo.', reportList: 'Relatórios', reports: 'Relatórios', newReport: 'Novo', noReports: 'Nenhum relatório salvo.', sections: 'seções', name: 'Nome de exibição', key: 'Identificador', descriptionLabel: 'Descrição', orientation: 'Orientação', portrait: 'Retrato', landscape: 'Paisagem', design: 'Design', previewMode: 'Preview', reset: 'Reverter', validate: 'Validar', validating: 'Validando…', apply: 'Aplicar', applying: 'Aplicando…', validationPassed: 'Validação aprovada', validationFailed: 'Validação encontrou problemas', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue e valide novamente.', applyConfirm: 'Aplicar este Report Engineering validado ao workspace?', discardConfirm: 'Descartar alterações não aplicadas neste relatório?', error: 'Erro', query: 'Consulta', savedDataQuery: 'Consulta de Dados salva', embeddedQuery: 'Consulta incorporada do relatório', dataset: 'Fonte de dados', defaultPeriod: 'Período padrão', runtimePeriod: 'Período do Preview', resolution: 'Resolução', raw: 'Dados brutos', fixedInterval: 'Intervalo fixo', summary: 'Resumo', interval: 'Intervalo', aggregate: 'Agregação', average: 'Média', minimum: 'Mínimo', maximum: 'Máximo', sum: 'Soma', count: 'Contagem', bucket: 'Agrupar a cada', tableLayout: 'Apresentação', longTable: 'Longa / amostras', wideTable: 'Larga / intervalos', variables: 'Dados do relatório', groupsAndSummaries: 'Grupos e resumos', groupsHint: 'Configure agrupamentos e totais sem script. O agrupamento temporal vem da Resolução acima.', groupField: 'Agrupar por', order: 'Ordem', ascending: 'Crescente', descending: 'Decrescente', addGroup: 'Adicionar grupo', summaryField: 'Campo do resumo', scope: 'Escopo', allRows: 'Todas as linhas', reportScope: 'Relatório inteiro', addSummary: 'Adicionar resumo', variablesHint: 'Selecione as TAGs do projeto. Alterar o rótulo ou a unidade mantém o vínculo com a TAG.', addVariables: 'Adicionar variáveis', visible: 'Mostrar coluna', displayLabel: 'Label de apresentação', unit: 'Unidade', unitAutomatic: 'Automática', unitHidden: 'Ocultar', unitOverride: 'Alterar label', unitLabel: 'Texto da unidade', decimals: 'Casas decimais', numericFormat: 'Formato numérico', formatStandard: 'Padrão', formatFixed: 'Fixo', formatScientific: 'Científico', dateTimeFormat: 'Formato de data/hora', dateTimeLocal: 'Data e hora local', dateOnly: 'Somente data', timeOnly: 'Somente hora', booleanTrue: 'Texto para Verdadeiro', booleanFalse: 'Texto para Falso', moveUp: 'Mover para cima', moveDown: 'Mover para baixo', remove: 'Remover', runtimePeriodInvalid: 'O período do Preview deve ser um número positivo.', pageLimit: 'Linhas por página', runPreview: 'Executar Preview', previewing: 'Executando Preview…', cancel: 'Cancelar', queryHint: 'A consulta permanece Historical Query v1. Este editor não aceita SQL livre.', controls: 'controles', sectionHeight: 'Altura da seção (mm)', addLabel: '+ Label', addField: '+ Campo', contentWidth: 'de largura útil', emptyPreview: 'Consulta sem linhas', emptyPreviewHint: 'Header/footer continuam válidos; Detail não recebeu dados.', inspector: 'Propriedades', selectControl: 'Selecione um controle no layout.', controlKey: 'Identificador do controle', widthMm: 'Largura (mm)', heightMm: 'Altura (mm)', text: 'Texto', queryKey: 'Consulta', field: 'Campo', fieldHint: 'Escolha um campo da fonte de dados selecionada.', asset: 'Visual Asset', noAsset: 'Sem asset', deleteControl: 'Excluir controle', previewSummary: 'Resumo do Preview', previewNotRun: 'O Preview ainda não foi executado.', rows: 'linhas', unauthorized: 'Autenticação necessária para executar o Preview.', forbidden: 'O usuário atual não possui autorização para consultar os dados deste relatório.', reportHeader: 'Report Header', reportFooter: 'Report Footer', pageHeader: 'Page Header', pageFooter: 'Page Footer', groupHeader: 'Group Header', detail: 'Detail', groupFooter: 'Group Footer'
  };
  const en = {
    ...pt,
    numberFormat: "Number format", quality: "Invalid quality label", pagination: "Pagination", showPage: "Show page number", position: "Position", header: "Header", footer: "Footer", alignment: "Alignment", left: "Left", center: "Center", right: "Right", font: "Font", size: "Size (pt)", total: "Include total pages", addImage: "+ Image", observed: "Last observed sample", tolerance: "Tolerance (s)", hideIds: "Hide alarm and tag identifiers", eyebrow: 'Engineering · Reporting', title: 'Report Designer', description: 'Configure the report, check its layout and preview the data.', authorityTitle: 'Report layout', authorityHint: 'Preview preserves the saved layout.', reportList: 'Reports', reports: 'Reports', newReport: 'New', noReports: 'No saved reports.', sections: 'sections', name: 'Display name', key: 'Identifier', descriptionLabel: 'Description', orientation: 'Orientation', portrait: 'Portrait', landscape: 'Landscape', design: 'Design', previewMode: 'Preview', reset: 'Reset', validate: 'Validate', validating: 'Validating…', apply: 'Apply', applying: 'Applying…', validationPassed: 'Validation passed', validationFailed: 'Validation found problems', workspaceChanged: 'Engineering Workspace changed during validation. Reload and validate again.', applyConfirm: 'Apply this validated Report Engineering to the workspace?', discardConfirm: 'Discard unapplied changes to this report?', error: 'Error', query: 'Query', savedDataQuery: 'Saved Data Query', embeddedQuery: 'Embedded report query', dataset: 'Data source', defaultPeriod: 'Default period', runtimePeriod: 'Preview period', resolution: 'Resolution', raw: 'Raw data', fixedInterval: 'Fixed interval', summary: 'Summary', interval: 'Interval', aggregate: 'Aggregation', average: 'Average', minimum: 'Minimum', maximum: 'Maximum', sum: 'Sum', count: 'Count', bucket: 'Bucket', tableLayout: 'Presentation', longTable: 'Long / samples', wideTable: 'Wide / intervals', variables: 'Report data', groupsAndSummaries: 'Groups and summaries', groupsHint: 'Configure grouping and totals without scripts. Time grouping comes from Resolution above.', groupField: 'Group by', order: 'Order', ascending: 'Ascending', descending: 'Descending', addGroup: 'Add group', summaryField: 'Summary field', scope: 'Scope', allRows: 'All rows', reportScope: 'Whole report', addSummary: 'Add summary', variablesHint: 'Select project TAGs. Changing the label or unit keeps the link to the same TAG.', addVariables: 'Add variables', visible: 'Show column', displayLabel: 'Display label', unit: 'Unit', unitAutomatic: 'Automatic', unitHidden: 'Hidden', unitOverride: 'Label override', unitLabel: 'Unit text', decimals: 'Decimal places', numericFormat: 'Numeric format', formatStandard: 'Standard', formatFixed: 'Fixed', formatScientific: 'Scientific', dateTimeFormat: 'Date/time format', dateTimeLocal: 'Local date and time', dateOnly: 'Date only', timeOnly: 'Time only', booleanTrue: 'True label', booleanFalse: 'False label', moveUp: 'Move up', moveDown: 'Move down', remove: 'Remove', runtimePeriodInvalid: 'Preview period must be a positive number.', pageLimit: 'Rows per page', runPreview: 'Run Preview', previewing: 'Running Preview…', cancel: 'Cancel', queryHint: 'The query remains Historical Query v1. This editor never accepts free-form SQL.', controls: 'controls', sectionHeight: 'Section height (mm)', addLabel: '+ Label', addField: '+ Field', contentWidth: 'content width', emptyPreview: 'Query returned no rows', emptyPreviewHint: 'Header/footer remain valid; Detail received no data.', inspector: 'Properties', selectControl: 'Select a control in the layout.', controlKey: 'Control identifier', widthMm: 'Width (mm)', heightMm: 'Height (mm)', text: 'Text', queryKey: 'Query', field: 'Field', fieldHint: 'Choose a field from the selected data source.', asset: 'Visual Asset', noAsset: 'No asset', deleteControl: 'Delete control', previewSummary: 'Preview summary', previewNotRun: 'Preview has not been executed.', rows: 'rows', unauthorized: 'Authentication is required to execute Preview.', forbidden: 'The current user is not authorized to query this report data.'
  };
  const es = {
    ...pt,
    numberFormat: "Formato numérico", quality: "Texto de calidad inválida", pagination: "Paginación", showPage: "Mostrar página", position: "Posición", header: "Encabezado", footer: "Pie de página", alignment: "Alineación", left: "Izquierda", center: "Centro", right: "Derecha", font: "Fuente", size: "Tamaño (pt)", total: "Incluir el total de páginas", addImage: "+ Imagen", observed: "Última muestra observada", tolerance: "Tolerancia (s)", hideIds: "Ocultar identificadores de alarmas y variables", title: 'Diseñador de Informes', description: 'Configure el informe, compruebe el diseño y consulte los datos en la vista previa.', authorityTitle: 'Diseño del informe', authorityHint: 'La vista previa conserva el diseño guardado.', reportList: 'Informes', reports: 'Informes', newReport: 'Nuevo', noReports: 'No hay informes guardados.', sections: 'secciones', name: 'Nombre visible', key: 'Identificador', descriptionLabel: 'Descripción', orientation: 'Orientación', portrait: 'Vertical', landscape: 'Horizontal', design: 'Diseño', reset: 'Revertir', validate: 'Validar', validating: 'Validando…', apply: 'Aplicar', applying: 'Aplicando…', validationPassed: 'Validación aprobada', validationFailed: 'La validación encontró problemas', query: 'Consulta', savedDataQuery: 'Consulta de Datos guardada', embeddedQuery: 'Consulta incorporada del informe', dataset: 'Fuente de datos', defaultPeriod: 'Período predeterminado', runtimePeriod: 'Período del Preview', resolution: 'Resolución', raw: 'Datos brutos', fixedInterval: 'Intervalo fijo', summary: 'Resumen', interval: 'Intervalo', aggregate: 'Agregación', average: 'Promedio', minimum: 'Mínimo', maximum: 'Máximo', sum: 'Suma', count: 'Conteo', bucket: 'Agrupar cada', tableLayout: 'Presentación', longTable: 'Larga / muestras', wideTable: 'Ancha / intervalos', variables: 'Datos del informe', groupsAndSummaries: 'Grupos y resúmenes', groupsHint: 'Configure agrupaciones y totales sin scripts. La agrupación temporal viene de Resolución.', groupField: 'Agrupar por', order: 'Orden', ascending: 'Ascendente', descending: 'Descendente', addGroup: 'Agregar grupo', summaryField: 'Campo del resumen', scope: 'Ámbito', allRows: 'Todas las filas', reportScope: 'Informe completo', addSummary: 'Agregar resumen', variablesHint: 'Seleccione las TAGs del proyecto. Cambiar la etiqueta o la unidad conserva el vínculo con la TAG.', addVariables: 'Agregar variables', visible: 'Mostrar columna', displayLabel: 'Label visible', unit: 'Unidad', unitAutomatic: 'Automática', unitHidden: 'Ocultar', unitOverride: 'Cambiar label', unitLabel: 'Texto de unidad', decimals: 'Decimales', numericFormat: 'Formato numérico', formatStandard: 'Estándar', formatFixed: 'Fijo', formatScientific: 'Científico', dateTimeFormat: 'Formato de fecha/hora', dateTimeLocal: 'Fecha y hora local', dateOnly: 'Solo fecha', timeOnly: 'Solo hora', booleanTrue: 'Texto Verdadero', booleanFalse: 'Texto Falso', moveUp: 'Mover arriba', moveDown: 'Mover abajo', remove: 'Quitar', runtimePeriodInvalid: 'El período del Preview debe ser un número positivo.', pageLimit: 'Filas por página', runPreview: 'Ejecutar Preview', previewing: 'Ejecutando Preview…', cancel: 'Cancelar', queryHint: 'La consulta sigue siendo Historical Query v1. Este editor no acepta SQL libre.', controls: 'controles', sectionHeight: 'Altura de sección (mm)', addLabel: '+ Etiqueta', addField: '+ Campo', contentWidth: 'de ancho útil', emptyPreview: 'La consulta no devolvió filas', emptyPreviewHint: 'Header/footer siguen válidos; Detail no recibió datos.', inspector: 'Propiedades', selectControl: 'Seleccione un control en el layout.', controlKey: 'Identificador del control', widthMm: 'Ancho (mm)', heightMm: 'Alto (mm)', text: 'Texto', queryKey: 'Consulta', field: 'Campo', fieldHint: 'Elija un campo de la fuente de datos seleccionada.', asset: 'Visual Asset', noAsset: 'Sin asset', deleteControl: 'Eliminar control', previewSummary: 'Resumen del Preview', previewNotRun: 'El Preview todavía no fue ejecutado.', rows: 'filas', unauthorized: 'Se requiere autenticación para ejecutar el Preview.', forbidden: 'El usuario actual no está autorizado para consultar los datos de este informe.'
  };
  return locale === 'en' ? en : locale === 'es' ? es : pt;
}
