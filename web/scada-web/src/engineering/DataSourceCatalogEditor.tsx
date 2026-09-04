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
import { resolveDriverCatalogResource } from './driverCatalogI18n';
import type { EngineeringLocale } from './i18n';
import { OpcUaDataSourceDiscoveryAssistant } from './OpcUaDataSourceDiscoveryAssistant';
import type { DataSourceEngineering, EngineeringPackageView } from './types';
import './structured-editors.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type CatalogResponse = { dataSourceTypes: DataSourceTypeDefinition[] };
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
  const [catalogError, setCatalogError] = useState<string | null>(null);
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
    void loadDataSourceTypeCatalog()
      .then(types => {
        if (!alive) return;
        setCatalog(types);
        setCatalogError(null);
      })
      .catch(reason => {
        if (alive) setCatalogError(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { alive = false; };
  }, []);

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
  const unsupported = Boolean(draft?.driver && catalog.length > 0 && !currentType);
  const pristineNew = newDataSourceDraft();
  const changed = Boolean(draft && (isNew
    ? JSON.stringify(draft) !== JSON.stringify(pristineNew)
    : selected && JSON.stringify(selected) !== JSON.stringify(draft)));
  const incompatible = draft && currentType
    ? incompatibleDataSourceConfiguration(draft, currentType)
    : { settings: [], secretReferences: [] };
  const hasIncompatible = incompatible.settings.length + incompatible.secretReferences.length > 0;
  const clientIssues = draft ? validateDataSourceDraft(draft, currentType) : [];

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

  return (
    <section className="eng-editor-shell" data-testid="schema-data-source-editor">
      <header className="eng-editor-heading">
        <div><span>Engineering</span><h2>{copy.title}</h2><p>{copy.description}</p></div>
        <button type="button" onClick={() => choose(NEW_DATA_SOURCE_IDENTITY)}>{copy.newSource}</button>
      </header>

      {catalogError && <pre className="eng-editor-error">{copy.catalogError}: {catalogError}</pre>}
      <div className="eng-editor-layout">
        <aside className="eng-entity-picker">
          {sources.map(source => (
            <button type="button" key={dataSourceIdentity(source)} className={dataSourceIdentity(source) === selectedIdentity ? 'selected' : ''} onClick={() => choose(dataSourceIdentity(source))}>
              <strong>{source.name || source.key}</strong><code>{source.key}</code><span>{source.driver}</span>
            </button>
          ))}
        </aside>

        <section className="eng-editor-form-panel">
          {!draft ? <div className="eng-editor-empty">{copy.noSelection}</div> : <>
            <div className="eng-editor-form-grid">
              <Field label={copy.name}><input required value={draft.name} onChange={event => updateDraft({ ...draft, name: event.target.value })} /></Field>
              <Field label={copy.key}><input required className="mono" value={draft.key} onChange={event => updateDraft({ ...draft, key: event.target.value })} /></Field>
              <Field label={copy.type}>
                <select
                  data-testid="data-source-type"
                  value={currentType?.typeKey ?? draft.driver}
                  onChange={event => changeType(event.target.value)}
                  disabled={catalog.length === 0}
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

            {currentType && <section className="eng-dictionary-editor">
              <header><strong>{copy.settings}</strong><span>{copy.settingsHint}</span></header>
              <div className="eng-editor-form-grid">
                {(currentType.configurationSchema?.dataSourceFields ?? []).map(field => (
                  <ConfigurationField
                    key={field.key}
                    field={field}
                    value={(isProtectedReference(field.valueKind) ? draft.secretReferences : draft.settings)?.[field.key] ?? field.defaultValue ?? ''}
                    onChange={value => changeSetting(field, value)}
                    locale={locale}
                    copy={copy}
                  />
                ))}
                {(currentType.configurationSchema?.dataSourceFields.length ?? 0) === 0 && <span>{copy.noSettings}</span>}
              </div>
            </section>}

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
    {field.valueKind === 'boolean' ? (
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
    title: 'Data Source editor', description: 'Choose a source type from the backend catalog. Configuration fields come from that type schema.',
    newSource: 'New Data Source', catalogError: 'Could not load source type catalog', noSelection: 'Select or create a Data Source.',
    name: 'Name', key: 'Key', type: 'Data Source type', enabled: 'Enabled', yes: 'Yes', no: 'No', chooseType: 'Choose a type',
    unsupported: 'Unavailable type', unsupportedHint: 'This persisted type is not available in this build. Select a supported type explicitly; it will not be remapped silently.',
    settings: 'Type configuration', settingsHint: 'Only fields declared by the selected backend schema are editable.', noSettings: 'This source type has no configuration fields.',
    incompatibleTitle: 'Incompatible persisted settings', incompatibleHint: 'These keys are not valid for the selected source type. They are not reinterpreted automatically.', removeIncompatible: 'Remove incompatible settings', incompatibleField: 'This persisted setting does not belong to the selected source type.',
    preview: 'Validate draft', apply: 'Apply', valid: 'Valid candidate', invalid: 'Invalid candidate', errors: 'Errors', discard: 'Discard unsaved Data Source changes?',
    workspaceChanged: 'Engineering Workspace changed during validation. Reload and validate the draft again.', fixClientIssues: 'Correct the highlighted Data Source fields before backend validation.',
    clientValidation: 'Fields to correct', expected: 'Expected', required: 'This field is required', integer: 'Enter a whole number', number: 'Enter a valid number', duration: 'Enter a valid duration', enumValue: 'Choose one of the supported values', minimum: 'Value is below the allowed minimum', maximum: 'Value is above the allowed maximum',
    format: 'Format', example: 'Example', advanced: 'advanced'
  };
  if (locale === 'es') return {
    title: 'Editor de Data Source', description: 'Seleccione un tipo del catálogo backend. Los campos provienen del schema de ese tipo.',
    newSource: 'Nueva Data Source', catalogError: 'No se pudo cargar el catálogo de tipos', noSelection: 'Seleccione o cree una Data Source.',
    name: 'Nombre', key: 'Clave', type: 'Tipo de Data Source', enabled: 'Habilitado', yes: 'Sí', no: 'No', chooseType: 'Seleccione un tipo',
    unsupported: 'Tipo no disponible', unsupportedHint: 'El tipo persistido no existe en esta build. Seleccione otro explícitamente; no será reinterpretado.',
    settings: 'Configuración del tipo', settingsHint: 'Solo los campos declarados por el schema backend son editables.', noSettings: 'Este tipo no tiene campos de configuración.',
    incompatibleTitle: 'Configuraciones persistidas incompatibles', incompatibleHint: 'Estas claves no son válidas para el tipo seleccionado. No se reinterpretan automáticamente.', removeIncompatible: 'Eliminar configuraciones incompatibles', incompatibleField: 'Esta configuración persistida no pertenece al tipo seleccionado.',
    preview: 'Validar borrador', apply: 'Aplicar', valid: 'Candidato válido', invalid: 'Candidato inválido', errors: 'Errores', discard: '¿Descartar los cambios no guardados?',
    workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue y valide el borrador nuevamente.', fixClientIssues: 'Corrija los campos indicados antes de la validación backend.',
    clientValidation: 'Campos a corregir', expected: 'Esperado', required: 'Este campo es obligatorio', integer: 'Ingrese un número entero', number: 'Ingrese un número válido', duration: 'Ingrese una duración válida', enumValue: 'Seleccione uno de los valores permitidos', minimum: 'El valor está por debajo del mínimo permitido', maximum: 'El valor supera el máximo permitido',
    format: 'Formato', example: 'Ejemplo', advanced: 'avanzado'
  };
  return {
    title: 'Editor de Data Source', description: 'Escolha um tipo no catálogo do backend. Os campos de configuração vêm do schema desse tipo.',
    newSource: 'Nova Data Source', catalogError: 'Não foi possível carregar o catálogo de tipos', noSelection: 'Selecione ou crie uma Data Source.',
    name: 'Nome', key: 'Chave', type: 'Tipo de Data Source', enabled: 'Habilitado', yes: 'Sim', no: 'Não', chooseType: 'Escolha um tipo',
    unsupported: 'Tipo indisponível', unsupportedHint: 'O tipo persistido não existe nesta build. Selecione outro explicitamente; ele não será reinterpretado silenciosamente.',
    settings: 'Configuração do tipo', settingsHint: 'Somente campos declarados pelo schema do backend podem ser editados.', noSettings: 'Este tipo não possui campos de configuração.',
    incompatibleTitle: 'Configurações persistidas incompatíveis', incompatibleHint: 'Estas chaves não pertencem ao tipo selecionado. Elas não são reinterpretadas automaticamente.', removeIncompatible: 'Remover configurações incompatíveis', incompatibleField: 'Esta configuração persistida não pertence ao tipo selecionado.',
    preview: 'Validar rascunho', apply: 'Aplicar', valid: 'Candidato válido', invalid: 'Candidato inválido', errors: 'Erros', discard: 'Descartar alterações não salvas da Data Source?',
    workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue e valide o rascunho novamente.', fixClientIssues: 'Corrija os campos indicados da Data Source antes da validação no backend.',
    clientValidation: 'Campos a corrigir', expected: 'Esperado', required: 'Este campo é obrigatório', integer: 'Informe um número inteiro', number: 'Informe um número válido', duration: 'Informe uma duração válida', enumValue: 'Escolha um dos valores permitidos', minimum: 'O valor está abaixo do mínimo permitido', maximum: 'O valor está acima do máximo permitido',
    format: 'Formato', example: 'Exemplo', advanced: 'avançado'
  };
}
