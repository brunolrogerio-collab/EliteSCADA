import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import {
  NEW_DATA_SOURCE_IDENTITY,
  buildDataSourceCandidate,
  cloneDataSourceValue,
  dataSourceIdentity,
  draftForDataSourceSelection,
  incompatibleDataSourceConfiguration,
  isProtectedReference,
  newDataSourceDraft,
  removeIncompatibleDataSourceConfiguration,
  switchDataSourceType,
  validateDataSourceDraft,
  type DataSourceConfigurationField,
  type DataSourceDraftIssue,
  type DataSourceTypeDefinition
} from './DataSourceCatalogEditor.logic';
import { backendReferenceFromName } from './backendReferenceFromName';
import { resolveDriverCatalogResource } from './driverCatalogI18n';
import type { EngineeringLocale } from './i18n';
import { OpcUaDataSourceDiscoveryAssistant } from './OpcUaDataSourceDiscoveryAssistant';
import { DataSourceConnectionTest } from './DataSourceConnectionTest';
import { EngineeringEntityActions } from './EngineeringEntityActions';
import { WorkflowFormDisclosure, WorkflowFormSection } from './StructuredFormPrimitives';
import type { DataSourceEngineering, EngineeringPackageView } from './types';
import './structured-editors.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type CatalogResponse = { dataSourceTypes: DataSourceTypeDefinition[] };
type SerialPortCatalogResponse = { authority: string; ports: Array<{ deviceName: string }> };
type CatalogStatus = 'loading' | 'ready' | 'error';
type Props = { model: EngineeringPackageView; locale: EngineeringLocale };

type EditorText = ReturnType<typeof text>;

export async function loadDataSourceTypeCatalog(): Promise<DataSourceTypeDefinition[]> {
  const response = await fetch(`${API}/api/engineering/data-source-types`, {
    headers: { accept: 'application/json' }
  });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return ((await response.json()) as CatalogResponse).dataSourceTypes ?? [];
}

export function DataSourceCatalogEditor({ model, locale }: Props) {
  const copy = useMemo(() => text(locale), [locale]);
  const sources = useMemo(() => model.dataSources ?? [], [model.dataSources]);
  const [catalog, setCatalog] = useState<DataSourceTypeDefinition[]>([]);
  const [catalogStatus, setCatalogStatus] = useState<CatalogStatus>('loading');
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogLoadVersion, setCatalogLoadVersion] = useState(0);
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => sources[0] ? dataSourceIdentity(sources[0]) : null);
  const [draft, setDraft] = useState<DataSourceEngineering | null>(() => sources[0] ? cloneDataSourceValue(sources[0]) : null);
  const [preview, setPreview] = useState<Awaited<ReturnType<typeof previewEngineeringPackage>> | null>(null);
  const [validatedCandidate, setValidatedCandidate] = useState<EngineeringPackageView | null>(null);
  const [validatedChangeVersion, setValidatedChangeVersion] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isNew = selectedIdentity === NEW_DATA_SOURCE_IDENTITY;
  const selected = !isNew && selectedIdentity
    ? sources.find(source => dataSourceIdentity(source) === selectedIdentity) ?? null
    : null;

  const invalidateValidation = () => {
    setPreview(null);
    setValidatedCandidate(null);
    setValidatedChangeVersion(null);
  };

  useEffect(() => {
    let alive = true;
    setCatalogStatus('loading');
    setCatalogError(null);
    void loadDataSourceTypeCatalog()
      .then(types => {
        if (!alive) return;
        setCatalog(types);
        setCatalogStatus('ready');
      })
      .catch(reason => {
        if (!alive) return;
        setCatalog([]);
        setCatalogError(reason instanceof Error ? reason.message : String(reason));
        setCatalogStatus('error');
      });
    return () => { alive = false; };
  }, [catalogLoadVersion]);

  useEffect(() => {
    if (selectedIdentity === NEW_DATA_SOURCE_IDENTITY) return;

    const current = selectedIdentity
      ? sources.find(source => dataSourceIdentity(source) === selectedIdentity) ?? null
      : null;
    if (current) {
      setDraft(cloneDataSourceValue(current));
      setPreview(null);
      setValidatedCandidate(null);
      setValidatedChangeVersion(null);
      return;
    }

    if (sources[0]) setSelectedIdentity(dataSourceIdentity(sources[0]));
    else setDraft(null);
  }, [selectedIdentity, sources]);

  const currentType = draft
    ? catalog.find(type => type.typeKey.toLowerCase() === draft.driver.toLowerCase()) ?? null
    : null;
  const unsupported = catalogStatus === 'ready' && Boolean(draft?.driver && catalog.length > 0 && !currentType);
  const pristineNew = newDataSourceDraft();
  const changed = Boolean(draft && (isNew
    ? JSON.stringify(draft) !== JSON.stringify(pristineNew)
    : selected && JSON.stringify(selected) !== JSON.stringify(draft)));
  const incompatible = draft && currentType
    ? incompatibleDataSourceConfiguration(draft, currentType)
    : { settings: [], secretReferences: [] };
  const hasIncompatible = incompatible.settings.length + incompatible.secretReferences.length > 0;
  const clientIssues = draft && catalogStatus === 'ready' ? validateDataSourceDraft(draft, currentType) : [];

  const updateDraft = (next: DataSourceEngineering) => {
    setDraft(next);
    invalidateValidation();
    setError(null);
  };

  const choose = (next: string) => {
    if (next === selectedIdentity) return;
    if (changed && !window.confirm(copy.discard)) return;
    setDraft(draftForDataSourceSelection(next, sources));
    setSelectedIdentity(next);
    invalidateValidation();
    setError(null);
  };

  const changeType = (typeKey: string) => {
    const type = catalog.find(candidate => candidate.typeKey === typeKey);
    if (!draft || !type) return;
    updateDraft(switchDataSourceType(draft, type));
  };

  const changeSetting = (field: DataSourceConfigurationField, value: string) => {
    if (!draft) return;
    const protectedReference = isProtectedReference(field.valueKind);
    const target = { ...(protectedReference ? draft.secretReferences ?? {} : draft.settings ?? {}) };
    if (value === '') delete target[field.key];
    else target[field.key] = value;
    updateDraft({
      ...draft,
      ...(protectedReference ? { secretReferences: target } : { settings: target })
    });
  };

  const removeIncompatible = () => {
    if (!draft || !currentType) return;
    updateDraft(removeIncompatibleDataSourceConfiguration(draft, currentType));
  };

  const candidate = (): EngineeringPackageView | null => {
    if (!draft) return null;
    return buildDataSourceCandidate(model, draft, selectedIdentity, isNew);
  };

  const runPreview = async () => {
    const next = candidate();
    if (!next) return;
    if (clientIssues.length > 0) {
      setError(copy.fixClientIssues);
      invalidateValidation();
      return;
    }

    setBusy(true);
    setError(null);
    invalidateValidation();
    try {
      const before = await loadEngineeringWorkspace();
      const nextPreview = await previewEngineeringPackage(next);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion)
        throw new Error(copy.workspaceChanged);

      setPreview(nextPreview);
      setValidatedCandidate(cloneDataSourceValue(next));
      setValidatedChangeVersion(after.changeVersion);
    } catch (reason) {
      invalidateValidation();
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  const runApply = async () => {
    if (!validatedCandidate || !preview?.canApply || validatedChangeVersion === null) return;
    setBusy(true);
    setError(null);
    try {
      await applyEngineeringPackage(validatedCandidate, validatedChangeVersion);
      window.location.reload();
    } catch (reason) {
      invalidateValidation();
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  const previewIssues = preview?.items.flatMap(item => item.issues ?? []) ?? [];
  const configurationFields = currentType?.configurationSchema?.dataSourceFields ?? [];
  const primaryFields = configurationFields.filter(field => !field.advanced);
  const advancedFields = configurationFields.filter(field => field.advanced);

  return (
    <section className="eng-editor-shell" data-testid="schema-data-source-editor">
      <header className="eng-editor-heading">
        <div className="eng-editor-heading-title"><h2>{copy.title}</h2><details className="eng-editor-help"><summary>{locale === 'en' ? 'Help' : locale === 'es' ? 'Ayuda' : 'Ajuda'}</summary><p>{copy.description}</p></details></div>
        <button type="button" onClick={() => choose(NEW_DATA_SOURCE_IDENTITY)}>{copy.newSource}</button>
      </header>

      {catalogStatus === 'loading' && (
        <div className="eng-editor-empty" role="status" aria-live="polite" data-testid="data-source-catalog-loading">
          {copy.catalogLoading}
        </div>
      )}
      {catalogStatus === 'error' && (
        <section className="eng-preview-panel" role="alert" data-testid="data-source-catalog-error">
          <header>
            <strong className="invalid">{copy.catalogError}</strong>
            <button
              type="button"
              onClick={() => setCatalogLoadVersion(version => version + 1)}
              data-testid="data-source-catalog-reload"
            >
              {copy.catalogReload}
            </button>
          </header>
          <span>{catalogError}</span>
        </section>
      )}
      {catalogStatus === 'ready' && catalog.length === 0 && (
        <div className="eng-editor-empty" role="status" data-testid="data-source-catalog-empty">
          {copy.catalogEmpty}
        </div>
      )}
      <div className="eng-editor-layout">
        <aside className="eng-entity-picker">
          {sources.map(source => (
            <button type="button" key={dataSourceIdentity(source)} className={dataSourceIdentity(source) === selectedIdentity ? 'selected' : ''} aria-label={`${source.name || source.key} (${source.key})`} aria-current={dataSourceIdentity(source) === selectedIdentity ? 'true' : undefined} onClick={() => choose(dataSourceIdentity(source))}>
              <strong>{source.name || source.key}</strong><span>{source.driver}</span>
            </button>
          ))}
        </aside>

        <section className="eng-editor-form-panel">
          {!draft ? <div className="eng-editor-empty">{copy.noSelection}</div> : <>
            <WorkflowFormSection title={copy.identitySection} description={copy.identityHint}>
            <div className="eng-editor-form-grid">
              <Field label={copy.name}><input aria-description={copy.nameHint} required value={draft.name} onChange={event => { const name = event.target.value; updateDraft({ ...draft, name, ...(isNew ? { key: backendReferenceFromName(name) } : {}) }); }} /></Field>
              <Field label={copy.type}>
                <select
                  data-testid="data-source-type"
                  value={currentType?.typeKey ?? draft.driver}
                  onChange={event => changeType(event.target.value)}
                  disabled={catalogStatus !== 'ready' || catalog.length === 0}
                  aria-busy={catalogStatus === 'loading'}
                >
                  {unsupported && <option value={draft.driver}>{copy.unsupported}: {draft.driver}</option>}
                  {!draft.driver && <option value="">{copy.chooseType}</option>}
                  {catalog.map(type => (
                    <option key={type.typeKey} value={type.typeKey}>
                      {resolveDriverCatalogResource(locale, type.displayNameResourceKey, type.displayName)}
                    </option>
                  ))}
                </select>
                {currentType && <small>
                  <code>{currentType.typeKey}</code>
                  {currentType.description ? ` · ${resolveDriverCatalogResource(locale, currentType.descriptionResourceKey, currentType.description)}` : ''}
                </small>}
                {unsupported && <small className="eng-editor-error">{copy.unsupportedHint}</small>}
              </Field>
              <Field label={copy.enabled}>
                <select value={draft.enabled === false ? 'false' : 'true'} onChange={event => updateDraft({ ...draft, enabled: event.target.value === 'true' })}>
                  <option value="true">{copy.yes}</option><option value="false">{copy.no}</option>
                </select>
              </Field>
            </div>
            </WorkflowFormSection>

            {currentType && <WorkflowFormSection title={copy.settings} description={copy.settingsHint}>
              <div className="eng-editor-form-grid">
                {primaryFields.map(field => (
                  <ConfigurationField
                    key={field.key}
                    field={field}
                    value={(isProtectedReference(field.valueKind) ? draft.secretReferences : draft.settings)?.[field.key] ?? field.defaultValue ?? ''}
                    onChange={value => changeSetting(field, value)}
                    locale={locale}
                    copy={copy}
                  />
                ))}
                {configurationFields.length === 0 && <span>{copy.noSettings}</span>}
              </div>
            </WorkflowFormSection>}

            {currentType && advancedFields.length > 0 && (
              <WorkflowFormDisclosure
                title={copy.advancedSettings}
                description={copy.advancedSettingsHint}
                testId="data-source-advanced-disclosure"
              >
                <div className="eng-editor-form-grid">
                  {advancedFields.map(field => (
                    <ConfigurationField
                      key={field.key}
                      field={field}
                      value={(isProtectedReference(field.valueKind) ? draft.secretReferences : draft.settings)?.[field.key] ?? field.defaultValue ?? ''}
                      onChange={value => changeSetting(field, value)}
                      locale={locale}
                      copy={copy}
                    />
                  ))}
                </div>
              </WorkflowFormDisclosure>
            )}

            {currentType?.capabilities.supportsConnectionTest && <DataSourceConnectionTest
              draft={draft}
              persistedId={selected?.id}
              unchanged={!changed}
              enabled={!busy && !unsupported && clientIssues.length === 0 && Boolean(draft.key.trim())}
              locale={locale}
            />}

            {currentType && <OpcUaDataSourceDiscoveryAssistant
              draft={draft}
              definition={currentType}
              locale={locale}
              onChange={updateDraft}
            />}

            {hasIncompatible && currentType && (
              <section className="eng-preview-panel" aria-live="polite" data-testid="data-source-incompatible-settings">
                <header>
                  <strong className="invalid">{copy.incompatibleTitle}</strong>
                  <button type="button" onClick={removeIncompatible} data-testid="data-source-remove-incompatible">{copy.removeIncompatible}</button>
                </header>
                <span>{copy.incompatibleHint}</span>
                <div className="eng-preview-issues">
                  {[...incompatible.settings, ...incompatible.secretReferences].map(key => (
                    <div className="warning" key={key}><code>{key}</code></div>
                  ))}
                </div>
              </section>
            )}

            {clientIssues.length > 0 && (
              <section className="eng-preview-panel" aria-live="polite" data-testid="data-source-client-validation">
                <header><strong className="invalid">{copy.clientValidation}</strong></header>
                <div className="eng-preview-issues">
                  {clientIssues.map((issue, index) => (
                    <div className="error" key={`${issue.fieldKey}-${issue.code}-${index}`}>
                      <strong>{fieldLabel(issue, currentType, copy, locale)}</strong>
                      <span>{clientIssueMessage(issue, copy)}</span>
                    </div>
                  ))}
                </div>
              </section>
            )}

            <div className="eng-editor-actions">
              <button type="button" disabled={!changed || busy || unsupported || !currentType || clientIssues.length > 0} onClick={() => void runPreview()} data-testid="data-source-preview">{copy.preview}</button>
              <button type="button" className="primary" disabled={!changed || !preview?.canApply || busy || validatedChangeVersion === null} onClick={() => void runApply()} data-testid="data-source-apply">{copy.apply}</button>
            </div>
            <p className="eng-workflow-note">{copy.saveHint}</p>
            {preview && <section className="eng-preview-panel" aria-live="polite">
              <header><strong className={preview.canApply ? 'valid' : 'invalid'}>{preview.canApply ? copy.valid : copy.invalid}</strong><span>{copy.errors}: {preview.errorCount}</span></header>
              {previewIssues.length > 0 && <div className="eng-preview-issues">
                {previewIssues.map((issue, index) => (
                  <div className={issue.isError ? 'error' : 'warning'} key={`${issue.code}-${issue.entityKey}-${index}`}>
                    <strong>{issue.code}</strong><span>{issue.message}</span><small>{issue.entityKey}</small>
                  </div>
                ))}
              </div>}
            </section>}
            {error && <pre className="eng-editor-error">{error}</pre>}
            <EngineeringEntityActions
              kind="data-source"
              model={model}
              locale={locale}
              selectedEntity={selected?.id ? { id: selected.id, label: selected.key, detail: `${selected.name} · ${selected.driver}` } : null}
            />
          </>}
        </section>
      </div>
    </section>
  );
}

function ConfigurationField({ field, value, onChange, locale, copy }: {
  field: DataSourceConfigurationField;
  value: string;
  onChange: (value: string) => void;
  locale: EngineeringLocale;
  copy: EditorText;
}) {
  const displayName = resolveDriverCatalogResource(locale, field.displayNameResourceKey, field.displayName);
  const description = resolveDriverCatalogResource(locale, field.descriptionResourceKey, field.description);
  const label = `${displayName}${field.required ? ' *' : ''}`;
  const common = {
    value,
    onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => onChange(event.target.value)
  };
  const detail = [
    description || null,
    field.expectedFormat ? `${copy.format}: ${field.expectedFormat}` : null,
    field.exampleValue ? `${copy.example}: ${field.exampleValue}` : null
  ].filter(Boolean).join(' · ');

  return <Field label={label} hint={detail || undefined}>
    {field.key === 'holdingRanges' ? (
      <HoldingRegisterRangesInput value={value} onChange={onChange} copy={copy} />
    ) : field.valueKind === 'serialPort' ? (
      <ServerSerialPortInput value={value} onChange={onChange} copy={copy} fieldKey={field.key} />
    ) : field.valueKind === 'boolean' ? (
      <select {...common} data-testid={`data-source-setting-${field.key}`}><option value="">—</option><option value="true">{copy.yes}</option><option value="false">{copy.no}</option></select>
    ) : field.valueKind === 'enum' ? (
      <select {...common} data-testid={`data-source-setting-${field.key}`}><option value="">—</option>{field.allowedValues.map(option => <option key={option} value={option}>{option}</option>)}</select>
    ) : ['integer', 'port', 'number'].includes(field.valueKind) ? (
      <input
        data-testid={`data-source-setting-${field.key}`}
        type="number"
        value={value}
        min={field.minimum ?? undefined}
        max={field.maximum ?? undefined}
        step={field.valueKind === 'number' ? 'any' : '1'}
        placeholder={field.exampleValue ?? undefined}
        onChange={event => onChange(event.target.value)}
      />
    ) : (
      <input
        data-testid={`data-source-setting-${field.key}`}
        value={value}
        placeholder={field.exampleValue ?? undefined}
        onChange={event => onChange(event.target.value)}
      />
    )}
    <small><code>{field.key}</code>{field.advanced ? ` · ${copy.advanced}` : ''}</small>
  </Field>;
}

function ServerSerialPortInput({ value, onChange, copy, fieldKey }: {
  value: string;
  onChange: (value: string) => void;
  copy: EditorText;
  fieldKey: string;
}) {
  const [ports, setPorts] = useState<string[]>([]);
  const [status, setStatus] = useState<'loading' | 'ready' | 'unavailable'>('loading');
  const listId = `server-serial-ports-${fieldKey.replace(/[^a-z0-9_-]/gi, '-')}`;

  useEffect(() => {
    let alive = true;
    setStatus('loading');
    void fetch(`${API}/api/engineering/host/serial-ports`, { headers: { accept: 'application/json' } })
      .then(async response => {
        if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
        return await response.json() as SerialPortCatalogResponse;
      })
      .then(result => {
        if (!alive) return;
        setPorts((result.ports ?? []).map(port => port.deviceName).filter(Boolean));
        setStatus('ready');
      })
      .catch(() => {
        if (!alive) return;
        setPorts([]);
        setStatus('unavailable');
      });
    return () => { alive = false; };
  }, [fieldKey]);

  return <>
    <input
      list={listId}
      value={value}
      onChange={event => onChange(event.target.value)}
      placeholder={copy.serialPortExample}
      data-testid={`data-source-setting-${fieldKey}`}
      autoComplete="off"
    />
    <datalist id={listId}>
      {ports.map(port => <option value={port} key={port} />)}
    </datalist>
    <small data-testid="server-serial-port-authority">
      {copy.serialPortAuthority}
      {status === 'loading' ? ` · ${copy.serialPortLoading}` : ''}
      {status === 'unavailable' ? ` · ${copy.serialPortUnavailable}` : ''}
      {status === 'ready' && ports.length === 0 ? ` · ${copy.serialPortNoneVisible}` : ''}
    </small>
  </>;
}

type RangeRow = { start: string; end: string };

function HoldingRegisterRangesInput({ value, onChange, copy }: {
  value: string;
  onChange: (value: string) => void;
  copy: EditorText;
}) {
  const [rows, setRows] = useState<RangeRow[]>(() => parseHoldingRanges(value));

  useEffect(() => {
    const formatted = formatHoldingRanges(rows);
    if (formatted !== value) setRows(parseHoldingRanges(value));
  }, [value]);

  const commit = (next: RangeRow[]) => {
    setRows(next);
    onChange(formatHoldingRanges(next));
  };

  return <div className="eng-dictionary-editor" data-testid="holding-register-ranges-editor">
    {rows.map((row, index) => (
      <div className="eng-editor-form-grid" key={index}>
        <label className="eng-editor-field">
          <span>{copy.rangeStart}</span>
          <input
            type="number"
            min={0}
            max={65535}
            step={1}
            value={row.start}
            onChange={event => commit(rows.map((candidate, rowIndex) => rowIndex === index ? { ...candidate, start: event.target.value } : candidate))}
            data-testid={`holding-range-start-${index}`}
          />
        </label>
        <label className="eng-editor-field">
          <span>{copy.rangeEnd}</span>
          <input
            type="number"
            min={0}
            max={65535}
            step={1}
            value={row.end}
            onChange={event => commit(rows.map((candidate, rowIndex) => rowIndex === index ? { ...candidate, end: event.target.value } : candidate))}
            data-testid={`holding-range-end-${index}`}
          />
        </label>
        <button
          type="button"
          className="secondary"
          disabled={rows.length <= 1}
          onClick={() => commit(rows.filter((_, rowIndex) => rowIndex !== index))}
          data-testid={`holding-range-remove-${index}`}
        >
          {copy.removeRange}
        </button>
      </div>
    ))}
    <button
      type="button"
      className="secondary"
      onClick={() => commit([...rows, { start: '', end: '' }])}
      data-testid="holding-range-add"
    >
      {copy.addRange}
    </button>
    <small>{copy.rangeHint}</small>
  </div>;
}

function parseHoldingRanges(value: string): RangeRow[] {
  const body = value.trim().replace(/^v1:/i, '');
  const rows = body.split(';')
    .map(entry => entry.trim())
    .filter(Boolean)
    .map(entry => {
      const [start = '', end = ''] = entry.split('-', 2);
      return { start, end };
    });
  return rows.length > 0 ? rows : [{ start: '0', end: '999' }];
}

function formatHoldingRanges(rows: readonly RangeRow[]): string {
  return `v1:${rows.map(row => `${row.start}-${row.end}`).join(';')}`;
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return <label className="eng-editor-field"><span>{label}</span>{children}{hint && <small>{hint}</small>}</label>;
}

function fieldLabel(
  issue: DataSourceDraftIssue,
  type: DataSourceTypeDefinition | null,
  copy: EditorText,
  locale: EngineeringLocale
): string {
  if (issue.fieldKey === '$name') return copy.name;
  if (issue.fieldKey === '$key') return copy.key;
  if (issue.fieldKey === '$type') return copy.type;
  const field = type?.configurationSchema?.dataSourceFields.find(candidate => candidate.key === issue.fieldKey);
  return field
    ? resolveDriverCatalogResource(locale, field.displayNameResourceKey, field.displayName)
    : issue.fieldKey;
}

function clientIssueMessage(issue: DataSourceDraftIssue, copy: EditorText): string {
  const expectation = issue.expected ? ` ${copy.expected}: ${issue.expected}.` : '';
  if (issue.code === 'required') return `${copy.required}.${expectation}`;
  if (issue.code === 'integer') return `${copy.integer}.${expectation}`;
  if (issue.code === 'number') return `${copy.number}.${expectation}`;
  if (issue.code === 'duration') return `${copy.duration}.${expectation}`;
  if (issue.code === 'enum') return `${copy.enumValue}.${expectation}`;
  if (issue.code === 'minimum') return `${copy.minimum}.${expectation}`;
  if (issue.code === 'maximum') return `${copy.maximum}.${expectation}`;
  return copy.incompatibleField;
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Data Source editor', description: 'Choose the source first, then configure only the protocol fields needed for this connection.',
    newSource: 'New Data Source', catalogLoading: 'Loading Data Source types…', catalogError: 'Could not load source type catalog', catalogEmpty: 'No Data Source types are available in this installation.', catalogReload: 'Reload catalog', noSelection: 'Select or create a Data Source.',
    name: 'Data Source name', nameHint: 'For new sources, the internal identifier is generated from this name; existing identifiers are preserved.', key: 'Identifier', keyHint: 'Stable internal reference; it is not the displayed name.', type: 'Data Source type', enabled: 'Enabled', yes: 'Yes', no: 'No', chooseType: 'Choose a type',
    unsupported: 'Unavailable type', unsupportedHint: 'This persisted type is not available in this installation. Select a supported type explicitly; it will not be remapped silently.',
    identitySection: 'Source identity', identityHint: 'Enter one source name. New sources get an internal identifier from it; existing identifiers are kept unchanged.', settings: 'Connection settings', settingsHint: 'Common settings for the selected source type.', advancedSettings: 'Advanced protocol settings', advancedSettingsHint: 'Rare or tuning-specific fields are available only when needed.', noSettings: 'This source type has no configuration fields.',
    incompatibleTitle: 'Incompatible persisted settings', incompatibleHint: 'These keys are not valid for the selected source type. They are not reinterpreted automatically.', removeIncompatible: 'Remove incompatible settings', incompatibleField: 'This persisted setting does not belong to the selected source type.',
    preview: 'Check changes', apply: 'Apply to Workspace', valid: 'Ready to apply', invalid: 'Invalid candidate', errors: 'Errors', saveHint: 'Checking changes does not alter the Workspace. Apply updates it; save or publish from Overview to update Runtime.', discard: 'Discard unsaved Data Source changes?',
    workspaceChanged: 'The Workspace changed during validation. Reload and validate the draft again.', fixClientIssues: 'Correct the highlighted Data Source fields before validation.',
    clientValidation: 'Fields to correct', expected: 'Expected', required: 'This field is required', integer: 'Enter a whole number', number: 'Enter a valid number', duration: 'Enter a valid duration', enumValue: 'Choose one of the supported values', minimum: 'Value is below the allowed minimum', maximum: 'Value is above the allowed maximum',
    format: 'Format', example: 'Example', advanced: 'advanced',
    serialPortAuthority: 'Ports are enumerated on the EliteSCADA server, not in this browser.', serialPortLoading: 'loading server ports', serialPortUnavailable: 'enumeration unavailable; manual device names are still allowed', serialPortNoneVisible: 'no server ports are visible right now', serialPortExample: 'COM3 or /dev/ttyUSB0',
    rangeStart: 'Start', rangeEnd: 'End', addRange: 'Add range', removeRange: 'Remove', rangeHint: 'Ranges are 0-based, may not overlap, and are stored in the canonical versioned format.'
  };
  if (locale === 'es') return {
    title: 'Editor de Fuente de datos', description: 'Seleccione primero la fuente y configure solo los campos de protocolo necesarios para esta conexión.',
    newSource: 'Nueva Fuente de datos', catalogLoading: 'Cargando tipos de Fuente de datos…', catalogError: 'No se pudo cargar el catálogo de tipos', catalogEmpty: 'No hay tipos de Fuente de datos disponibles en esta instalación.', catalogReload: 'Recargar catálogo', noSelection: 'Seleccione o cree una Fuente de datos.',
    name: 'Nombre de la Fuente de datos', nameHint: 'En fuentes nuevas, el identificador interno se genera a partir de este nombre; se conservan los identificadores existentes.', key: 'Identificador', keyHint: 'Referencia interna estable; no es el nombre mostrado.', type: 'Tipo de Fuente de datos', enabled: 'Habilitado', yes: 'Sí', no: 'No', chooseType: 'Seleccione un tipo',
    unsupported: 'Tipo no disponible', unsupportedHint: 'El tipo persistido no está disponible en esta instalación. Seleccione otro explícitamente; no será reinterpretado.',
    identitySection: 'Identidad de la fuente', identityHint: 'Ingrese un solo nombre. Las fuentes nuevas reciben un identificador interno generado a partir de él; los identificadores existentes se conservan.', settings: 'Configuración de conexión', settingsHint: 'Opciones comunes del tipo de fuente seleccionado.', advancedSettings: 'Opciones avanzadas del protocolo', advancedSettingsHint: 'Los campos raros o de ajuste aparecen solo cuando son necesarios.', noSettings: 'Este tipo no tiene campos de configuración.',
    incompatibleTitle: 'Configuraciones persistidas incompatibles', incompatibleHint: 'Estas claves no son válidas para el tipo seleccionado. No se reinterpretan automáticamente.', removeIncompatible: 'Eliminar configuraciones incompatibles', incompatibleField: 'Esta configuración persistida no pertenece al tipo seleccionado.',
    preview: 'Verificar cambios', apply: 'Aplicar al Workspace', valid: 'Listo para aplicar', invalid: 'Candidato inválido', errors: 'Errores', saveHint: 'Verificar no cambia el Workspace. Aplicar lo actualiza; guarde o publique desde Overview para actualizar Runtime.', discard: '¿Descartar los cambios no guardados?',
    workspaceChanged: 'El Área de trabajo de Ingeniería cambió durante la validación. Recargue y valide el borrador nuevamente.', fixClientIssues: 'Corrija los campos indicados antes de la validación.',
    clientValidation: 'Campos a corregir', expected: 'Esperado', required: 'Este campo es obligatorio', integer: 'Ingrese un número entero', number: 'Ingrese un número válido', duration: 'Ingrese una duración válida', enumValue: 'Seleccione uno de los valores permitidos', minimum: 'El valor está por debajo del mínimo permitido', maximum: 'El valor supera el máximo permitido',
    format: 'Formato', example: 'Ejemplo', advanced: 'avanzado',
    serialPortAuthority: 'Los puertos se enumeran en el servidor EliteSCADA, no en este navegador.', serialPortLoading: 'cargando puertos del servidor', serialPortUnavailable: 'enumeración no disponible; se permite escribir el dispositivo manualmente', serialPortNoneVisible: 'no hay puertos del servidor visibles ahora', serialPortExample: 'COM3 o /dev/ttyUSB0',
    rangeStart: 'Inicio', rangeEnd: 'Fin', addRange: 'Agregar rango', removeRange: 'Quitar', rangeHint: 'Los rangos son base 0, no pueden superponerse y se guardan en el formato canónico versionado.'
  };
  return {
    title: 'Editor de Fonte de dados', description: 'Escolha primeiro a fonte e configure somente os campos de protocolo necessários para esta conexão.',
    newSource: 'Nova Fonte de dados', catalogLoading: 'Carregando tipos de Fonte de dados…', catalogError: 'Não foi possível carregar o catálogo de tipos', catalogEmpty: 'Nenhum tipo de Fonte de dados está disponível nesta instalação.', catalogReload: 'Recarregar catálogo', noSelection: 'Selecione ou crie uma Fonte de dados.',
    name: 'Nome da fonte de dados', nameHint: 'Em fontes novas, o identificador interno é gerado a partir deste nome; identificadores existentes são preservados.', key: 'Identificador', keyHint: 'Referência estável usada internamente; não é o nome exibido.', type: 'Tipo de Fonte de dados', enabled: 'Habilitado', yes: 'Sim', no: 'Não', chooseType: 'Escolha um tipo',
    unsupported: 'Tipo indisponível', unsupportedHint: 'O tipo persistido não está disponível nesta instalação. Selecione outro explicitamente; ele não será reinterpretado silenciosamente.',
    identitySection: 'Identidade da fonte', identityHint: 'Informe um único nome. Em fontes novas, o identificador interno é gerado a partir dele; identificadores existentes são preservados.', settings: 'Configuração da conexão', settingsHint: 'Campos comuns do tipo de fonte selecionado.', advancedSettings: 'Configurações avançadas do protocolo', advancedSettingsHint: 'Campos raros ou de ajuste aparecem somente quando necessários.', noSettings: 'Este tipo não possui campos de configuração.',
    incompatibleTitle: 'Configurações persistidas incompatíveis', incompatibleHint: 'Estas chaves não pertencem ao tipo selecionado. Elas não são reinterpretadas automaticamente.', removeIncompatible: 'Remover configurações incompatíveis', incompatibleField: 'Esta configuração persistida não pertence ao tipo selecionado.',
    preview: 'Verificar alterações', apply: 'Aplicar ao Workspace', valid: 'Pronto para aplicar', invalid: 'Candidato inválido', errors: 'Erros', saveHint: 'Verificar não altera o Workspace. Aplicar atualiza o Workspace; salve ou publique em Visão geral para atualizar o Runtime.', discard: 'Descartar alterações não salvas da Fonte de dados?',
    workspaceChanged: 'A Área de trabalho de Engenharia mudou durante a validação. Recarregue e valide o rascunho novamente.', fixClientIssues: 'Corrija os campos indicados da Fonte de dados antes da validação.',
    clientValidation: 'Campos a corrigir', expected: 'Esperado', required: 'Este campo é obrigatório', integer: 'Informe um número inteiro', number: 'Informe um número válido', duration: 'Informe uma duração válida', enumValue: 'Escolha um dos valores permitidos', minimum: 'O valor está abaixo do mínimo permitido', maximum: 'O valor está acima do máximo permitido',
    format: 'Formato', example: 'Exemplo', advanced: 'avançado',
    serialPortAuthority: 'As portas são enumeradas no servidor EliteSCADA, não neste navegador.', serialPortLoading: 'carregando portas do servidor', serialPortUnavailable: 'enumeração indisponível; ainda é possível informar o dispositivo manualmente', serialPortNoneVisible: 'nenhuma porta do servidor está visível agora', serialPortExample: 'COM3 ou /dev/ttyUSB0',
    rangeStart: 'Início', rangeEnd: 'Fim', addRange: 'Adicionar range', removeRange: 'Remover', rangeHint: 'Os ranges são base 0, não podem se sobrepor e são salvos no formato canônico versionado.'
  };
}
