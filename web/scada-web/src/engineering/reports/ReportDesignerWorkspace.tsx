import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage,
  visualAssetContentUrl
} from '../api';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringPackageView, EngineeringSnapshot, ImportPreviewView } from '../types';
import { previewReportExecution, ReportPreviewApiError } from './reportApi';
import type {
  HistoricalQueryRow,
  ReportControlEngineeringDto,
  ReportEngineeringDto,
  ReportExecutionResult,
  ReportParameterValue,
  ReportSectionEngineeringDto
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
      <div>
        <span>{text.eyebrow}</span>
        <h1>{text.title}</h1>
        <p>{text.description}</p>
      </div>
      <div className="report-designer-authority">
        <strong>{text.authorityTitle}</strong>
        <span>{text.authorityHint}</span>
      </div>
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
              <label><span>{text.dataset}</span><select value={primaryQuery?.query.datasetKey ?? 'historian.samples'} onChange={event => updateDraft(current => updatePrimaryQueryDataset(current, event.target.value as 'historian.samples' | 'alarm.events'))}>
                <option value="historian.samples">historian.samples</option>
                <option value="alarm.events">alarm.events</option>
              </select></label>
              <label><span>{text.defaultPeriod}</span><input type="number" min="1" value={readDefaultPeriod(draft)} onChange={event => updateDraft(current => updateRelativeDuration(current, Number(event.target.value)))}/></label>
              <label><span>{text.runtimePeriod}</span><input type="number" min="1" value={runtimePeriodSeconds} onChange={event => { setRuntimePeriodSeconds(event.target.value); setExecution(null); setMode('design'); }}/></label>
              <label><span>{text.pageLimit}</span><input type="number" min="1" max="200" value={primaryQuery?.query.page?.limit ?? 50} onChange={event => updateDraft(current => updatePrimaryPageLimit(current, Number(event.target.value)))}/></label>
              <div className="report-preview-actions">
                <button type="button" className="primary" onClick={() => void executePreview()} disabled={previewing || applying}>{previewing ? text.previewing : text.runPreview}</button>
                {previewing ? <button type="button" onClick={cancelPreview}>{text.cancel}</button> : null}
              </div>
              <small>{text.queryHint}</small>
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
              {selectedSection ? <label><span>{text.sectionHeight}</span><input type="number" min="0.1" step="0.5" value={selectedSection.heightMillimeters} onChange={event => updateDraft(current => updateReportSection(current, selectedSection.key, section => ({ ...section, heightMillimeters: Number(event.target.value) })))}/></label> : null}
              {selectedSection ? <div className="report-add-controls"><button type="button" onClick={() => { updateDraft(current => addReportControl(current, selectedSection.key, 'label')); }}>{text.addLabel}</button><button type="button" onClick={() => { updateDraft(current => addReportControl(current, selectedSection.key, 'dataField')); }}>{text.addField}</button></div> : null}
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

function ReportDesignCanvas({ report, widthMillimeters, selectedSectionKey, selectedControlKey, onSelect, text }: {
  report: ReportEngineeringDto;
  widthMillimeters: number;
  selectedSectionKey: string;
  selectedControlKey: string | null;
  onSelect: (sectionKey: string, controlKey: string | null) => void;
  text: ReturnType<typeof copy>;
}) {
  return <div className="report-page" style={{ width: widthMillimeters * MILLIMETER_SCALE }} data-unit="millimeter">
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
  </div>;
}

function ReportPreviewCanvas({ report, result, widthMillimeters, text }: {
  report: ReportEngineeringDto;
  result: ReportExecutionResult;
  widthMillimeters: number;
  text: ReturnType<typeof copy>;
}) {
  return <div className="report-page preview" style={{ width: widthMillimeters * MILLIMETER_SCALE }} data-testid="report-preview-canvas" data-unit="millimeter">
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

function ControlInspector({ report, section, control, text, onChange, onDelete }: {
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
      <label><span>{text.field}</span><input className="mono" value={control.field ?? defaultFieldForDataset(dataset)} onChange={event => onChange(current => ({ ...current, field: event.target.value }))}/></label>
      <small>{text.fieldHint}</small>
    </> : null}
    {control.kind === 'image' ? <label><span>{text.asset}</span><select value={control.assetId ?? ''} onChange={event => onChange(current => ({ ...current, assetId: emptyToNull(event.target.value) }))}><option value="">{text.noAsset}</option></select></label> : null}
    <button type="button" className="danger" onClick={onDelete}>{text.deleteControl}</button>
    <small>{sectionLabel(section.kind, text)} · {section.heightMillimeters} mm</small>
  </div>;
}

function NumberField({ label, value, min = 0, onChange }: { label: string; value: number; min?: number; onChange: (value: number) => void }) {
  return <label><span>{label}</span><input type="number" min={min} step="0.5" value={value} onChange={event => onChange(Number(event.target.value))}/></label>;
}

function updatePrimaryPageLimit(report: ReportEngineeringDto, limit: number): ReportEngineeringDto {
  const normalized = Math.max(1, Math.min(200, Math.trunc(limit || 1)));
  return {
    ...report,
    queries: (report.queries ?? []).map((query, index) => index === 0
      ? { ...query, query: { ...query.query, page: { limit: normalized } } }
      : query)
  };
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

function copy(locale: EngineeringLocale) {
  const pt = {
    eyebrow: 'Engineering · Reporting', title: 'Designer de Relatórios', description: 'Edite Report Engineering canônico em milímetros, valide pelo ciclo Engineering e visualize dados pelo Report Execution protegido.',
    authorityTitle: 'Autoridade canônica', authorityHint: 'Layout é ReportEngineeringDto. Preview é derivado e não altera Engineering.', reportList: 'Relatórios', reports: 'Relatórios', newReport: 'Novo', noReports: 'Nenhum relatório salvo.', sections: 'seções', name: 'Nome', key: 'Chave', descriptionLabel: 'Descrição', orientation: 'Orientação', portrait: 'Retrato', landscape: 'Paisagem', design: 'Design', previewMode: 'Preview', reset: 'Reverter', validate: 'Validar', validating: 'Validando…', apply: 'Aplicar', applying: 'Aplicando…', validationPassed: 'Validação aprovada', validationFailed: 'Validação encontrou problemas', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue e valide novamente.', applyConfirm: 'Aplicar este Report Engineering validado ao workspace?', discardConfirm: 'Descartar alterações não aplicadas neste relatório?', error: 'Erro', query: 'Consulta', dataset: 'Dataset', defaultPeriod: 'Período padrão (s)', runtimePeriod: 'Período do Preview (s)', runtimePeriodInvalid: 'O período do Preview deve ser um número positivo.', pageLimit: 'Linhas por página', runPreview: 'Executar Preview', previewing: 'Executando Preview…', cancel: 'Cancelar', queryHint: 'A consulta permanece Historical Query v1. Este editor não aceita SQL livre.', controls: 'controles', sectionHeight: 'Altura da seção (mm)', addLabel: '+ Label', addField: '+ Campo', contentWidth: 'de largura útil', emptyPreview: 'Consulta sem linhas', emptyPreviewHint: 'Header/footer continuam válidos; Detail não recebeu dados.', inspector: 'Propriedades', selectControl: 'Selecione um controle no layout.', controlKey: 'Chave do controle', widthMm: 'Largura (mm)', heightMm: 'Altura (mm)', text: 'Texto', queryKey: 'Consulta', field: 'Campo', fieldHint: 'O campo deve existir no dataset canônico selecionado.', asset: 'Visual Asset', noAsset: 'Sem asset', deleteControl: 'Excluir controle', previewSummary: 'Resumo do Preview', previewNotRun: 'O Preview ainda não foi executado.', rows: 'linhas', unauthorized: 'Autenticação necessária para executar o Preview.', forbidden: 'O usuário atual não possui autorização para consultar os dados deste relatório.', reportHeader: 'Report Header', reportFooter: 'Report Footer', pageHeader: 'Page Header', pageFooter: 'Page Footer', groupHeader: 'Group Header', detail: 'Detail', groupFooter: 'Group Footer'
  };
  const en = {
    ...pt, eyebrow: 'Engineering · Reporting', title: 'Report Designer', description: 'Edit canonical Report Engineering in millimeters, validate through the Engineering lifecycle, and preview data through protected Report Execution.', authorityTitle: 'Canonical authority', authorityHint: 'Layout is ReportEngineeringDto. Preview is derived and never mutates Engineering.', reportList: 'Reports', reports: 'Reports', newReport: 'New', noReports: 'No saved reports.', sections: 'sections', name: 'Name', key: 'Key', descriptionLabel: 'Description', orientation: 'Orientation', portrait: 'Portrait', landscape: 'Landscape', design: 'Design', previewMode: 'Preview', reset: 'Reset', validate: 'Validate', validating: 'Validating…', apply: 'Apply', applying: 'Applying…', validationPassed: 'Validation passed', validationFailed: 'Validation found problems', workspaceChanged: 'Engineering Workspace changed during validation. Reload and validate again.', applyConfirm: 'Apply this validated Report Engineering to the workspace?', discardConfirm: 'Discard unapplied changes to this report?', error: 'Error', query: 'Query', dataset: 'Dataset', defaultPeriod: 'Default period (s)', runtimePeriod: 'Preview period (s)', runtimePeriodInvalid: 'Preview period must be a positive number.', pageLimit: 'Rows per page', runPreview: 'Run Preview', previewing: 'Running Preview…', cancel: 'Cancel', queryHint: 'The query remains Historical Query v1. This editor never accepts free-form SQL.', controls: 'controls', sectionHeight: 'Section height (mm)', addLabel: '+ Label', addField: '+ Field', contentWidth: 'content width', emptyPreview: 'Query returned no rows', emptyPreviewHint: 'Header/footer remain valid; Detail received no data.', inspector: 'Properties', selectControl: 'Select a control in the layout.', controlKey: 'Control key', widthMm: 'Width (mm)', heightMm: 'Height (mm)', text: 'Text', queryKey: 'Query', field: 'Field', fieldHint: 'The field must exist in the selected canonical dataset.', asset: 'Visual Asset', noAsset: 'No asset', deleteControl: 'Delete control', previewSummary: 'Preview summary', previewNotRun: 'Preview has not been executed.', rows: 'rows', unauthorized: 'Authentication is required to execute Preview.', forbidden: 'The current user is not authorized to query this report data.'
  };
  const es = {
    ...pt, title: 'Diseñador de Informes', description: 'Edite Report Engineering canónico en milímetros, valide por el ciclo Engineering y previsualice datos mediante Report Execution protegido.', authorityTitle: 'Autoridad canónica', authorityHint: 'El layout es ReportEngineeringDto. El Preview es derivado y no modifica Engineering.', reportList: 'Informes', reports: 'Informes', newReport: 'Nuevo', noReports: 'No hay informes guardados.', sections: 'secciones', name: 'Nombre', key: 'Clave', descriptionLabel: 'Descripción', orientation: 'Orientación', portrait: 'Vertical', landscape: 'Horizontal', design: 'Diseño', reset: 'Revertir', validate: 'Validar', validating: 'Validando…', apply: 'Aplicar', applying: 'Aplicando…', validationPassed: 'Validación aprobada', validationFailed: 'La validación encontró problemas', query: 'Consulta', defaultPeriod: 'Período predeterminado (s)', runtimePeriod: 'Período del Preview (s)', runtimePeriodInvalid: 'El período del Preview debe ser un número positivo.', pageLimit: 'Filas por página', runPreview: 'Ejecutar Preview', previewing: 'Ejecutando Preview…', cancel: 'Cancelar', queryHint: 'La consulta sigue siendo Historical Query v1. Este editor no acepta SQL libre.', controls: 'controles', sectionHeight: 'Altura de sección (mm)', addLabel: '+ Etiqueta', addField: '+ Campo', contentWidth: 'de ancho útil', emptyPreview: 'La consulta no devolvió filas', emptyPreviewHint: 'Header/footer siguen válidos; Detail no recibió datos.', inspector: 'Propiedades', selectControl: 'Seleccione un control en el layout.', controlKey: 'Clave del control', widthMm: 'Ancho (mm)', heightMm: 'Alto (mm)', text: 'Texto', queryKey: 'Consulta', field: 'Campo', fieldHint: 'El campo debe existir en el dataset canónico seleccionado.', asset: 'Visual Asset', noAsset: 'Sin asset', deleteControl: 'Eliminar control', previewSummary: 'Resumen del Preview', previewNotRun: 'El Preview todavía no fue ejecutado.', rows: 'filas', unauthorized: 'Se requiere autenticación para ejecutar el Preview.', forbidden: 'El usuario actual no está autorizado para consultar los datos de este informe.'
  };
  return locale === 'en' ? en : locale === 'es' ? es : pt;
}
