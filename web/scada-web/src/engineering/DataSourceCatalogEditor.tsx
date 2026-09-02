import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import type { EngineeringLocale } from './i18n';
import type { DataSourceEngineering, EngineeringPackageView } from './types';
import './structured-editors.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const NEW_IDENTITY = 'draft:new-datasource:catalog';

export type DataSourceConfigurationField = {
  key: string;
  valueKind: string;
  required: boolean;
  displayName: string;
  description?: string | null;
  defaultValue?: string | null;
  allowedValues: string[];
  minimum?: number | null;
  maximum?: number | null;
  advanced: boolean;
};

export type DataSourceTypeDefinition = {
  typeKey: string;
  displayName: string;
  kind: string;
  description?: string | null;
  capabilities: {
    supportsConnectionTest: boolean;
    supportsDiscovery: boolean;
    supportsBrowse: boolean;
    supportsFileImport: boolean;
    supportsReconcile: boolean;
    supportsSharedTransportInfrastructure: boolean;
  };
  configurationSchema?: {
    schemaId: string;
    schemaVersion: number;
    dataSourceFields: DataSourceConfigurationField[];
    tagBindingFields: DataSourceConfigurationField[];
  } | null;
};

type CatalogResponse = { dataSourceTypes: DataSourceTypeDefinition[] };
type Props = { model: EngineeringPackageView; locale: EngineeringLocale };

export async function loadDataSourceTypeCatalog(): Promise<DataSourceTypeDefinition[]> {
  const response = await fetch(`${API}/api/engineering/data-source-types`, {
    headers: { accept: 'application/json' }
  });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return ((await response.json()) as CatalogResponse).dataSourceTypes ?? [];
}

export function settingsForType(type: DataSourceTypeDefinition): {
  settings: Record<string, string>;
  secretReferences: Record<string, string>;
} {
  const settings: Record<string, string> = {};
  const secretReferences: Record<string, string> = {};
  for (const field of type.configurationSchema?.dataSourceFields ?? []) {
    if (field.defaultValue == null || field.defaultValue === '') continue;
    if (isProtectedReference(field.valueKind)) secretReferences[field.key] = field.defaultValue;
    else settings[field.key] = field.defaultValue;
  }
  return { settings, secretReferences };
}

export function switchDataSourceType(
  source: DataSourceEngineering,
  type: DataSourceTypeDefinition
): DataSourceEngineering {
  const defaults = settingsForType(type);
  return {
    ...source,
    driver: type.typeKey,
    settings: defaults.settings,
    secretReferences: defaults.secretReferences
  };
}

export function DataSourceCatalogEditor({ model, locale }: Props) {
  const copy = useMemo(() => text(locale), [locale]);
  const sources = model.dataSources ?? [];
  const [catalog, setCatalog] = useState<DataSourceTypeDefinition[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => sources[0] ? identity(sources[0]) : null);
  const [draft, setDraft] = useState<DataSourceEngineering | null>(() => sources[0] ? clone(sources[0]) : null);
  const [preview, setPreview] = useState<Awaited<ReturnType<typeof previewEngineeringPackage>> | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isNew = selectedIdentity === NEW_IDENTITY;
  const selected = !isNew && selectedIdentity
    ? sources.find(source => identity(source) === selectedIdentity) ?? null
    : null;

  useEffect(() => {
    let alive = true;
    void loadDataSourceTypeCatalog()
      .then(types => { if (alive) setCatalog(types); })
      .catch(reason => { if (alive) setCatalogError(reason instanceof Error ? reason.message : String(reason)); });
    return () => { alive = false; };
  }, []);

  useEffect(() => {
    if (selectedIdentity === NEW_IDENTITY) {
      const first = catalog[0];
      const next: DataSourceEngineering = {
        key: '', name: '', driver: first?.typeKey ?? '', enabled: true,
        settings: first ? settingsForType(first).settings : {},
        secretReferences: first ? settingsForType(first).secretReferences : {}
      };
      setDraft(next);
      setPreview(null);
      return;
    }
    const current = selectedIdentity ? sources.find(source => identity(source) === selectedIdentity) ?? null : null;
    if (current) {
      setDraft(clone(current));
      setPreview(null);
    }
  }, [selectedIdentity, sources, catalog]);

  const currentType = draft ? catalog.find(type => type.typeKey === draft.driver) ?? null : null;
  const unsupported = Boolean(draft?.driver && catalog.length > 0 && !currentType);
  const changed = Boolean(draft && (isNew || (selected && JSON.stringify(selected) !== JSON.stringify(draft))));

  const choose = (next: string) => {
    if (next === selectedIdentity) return;
    if (changed && !window.confirm(copy.discard)) return;
    setSelectedIdentity(next);
    setError(null);
  };

  const changeType = (typeKey: string) => {
    const type = catalog.find(candidate => candidate.typeKey === typeKey);
    if (!draft || !type) return;
    setDraft(switchDataSourceType(draft, type));
    setPreview(null);
    setError(null);
  };

  const changeSetting = (field: DataSourceConfigurationField, value: string) => {
    if (!draft) return;
    const protectedReference = isProtectedReference(field.valueKind);
    const target = { ...(protectedReference ? draft.secretReferences ?? {} : draft.settings ?? {}) };
    if (value === '') delete target[field.key];
    else target[field.key] = value;
    setDraft({
      ...draft,
      ...(protectedReference ? { secretReferences: target } : { settings: target })
    });
    setPreview(null);
  };

  const candidate = (): EngineeringPackageView | null => {
    if (!draft) return null;
    const next = clone(model);
    next.dataSources = isNew
      ? [...(next.dataSources ?? []), clone(draft)]
      : (next.dataSources ?? []).map(source => identity(source) === selectedIdentity ? clone(draft) : source);
    return next;
  };

  const runPreview = async () => {
    const next = candidate();
    if (!next) return;
    setBusy(true); setError(null);
    try { setPreview(await previewEngineeringPackage(next)); }
    catch (reason) { setPreview(null); setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };

  const runApply = async () => {
    const next = candidate();
    if (!next || !preview?.canApply) return;
    setBusy(true); setError(null);
    try {
      const workspace = await loadEngineeringWorkspace();
      await applyEngineeringPackage(next, workspace.changeVersion);
      window.location.reload();
    } catch (reason) { setPreview(null); setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };

  return (
    <section className="eng-editor-shell" data-testid="schema-data-source-editor">
      <header className="eng-editor-heading">
        <div><span>Engineering</span><h2>{copy.title}</h2><p>{copy.description}</p></div>
        <button type="button" onClick={() => choose(NEW_IDENTITY)}>{copy.newSource}</button>
      </header>

      {catalogError && <pre className="eng-editor-error">{copy.catalogError}: {catalogError}</pre>}
      <div className="eng-editor-layout">
        <aside className="eng-entity-picker">
          {sources.map(source => (
            <button type="button" key={identity(source)} className={identity(source) === selectedIdentity ? 'selected' : ''} onClick={() => choose(identity(source))}>
              <strong>{source.name || source.key}</strong><code>{source.key}</code><span>{source.driver}</span>
            </button>
          ))}
        </aside>

        <section className="eng-editor-form-panel">
          {!draft ? <div className="eng-editor-empty">{copy.noSelection}</div> : <>
            <div className="eng-editor-form-grid">
              <Field label={copy.name}><input value={draft.name} onChange={event => { setDraft({ ...draft, name: event.target.value }); setPreview(null); }} /></Field>
              <Field label={copy.key}><input className="mono" value={draft.key} onChange={event => { setDraft({ ...draft, key: event.target.value }); setPreview(null); }} /></Field>
              <Field label={copy.type}>
                <select data-testid="data-source-type" value={draft.driver} onChange={event => changeType(event.target.value)} disabled={catalog.length === 0}>
                  {unsupported && <option value={draft.driver}>{copy.unsupported}: {draft.driver}</option>}
                  {!draft.driver && <option value="">{copy.chooseType}</option>}
                  {catalog.map(type => <option key={type.typeKey} value={type.typeKey}>{type.displayName}</option>)}
                </select>
                {currentType && <small><code>{currentType.typeKey}</code>{currentType.description ? ` · ${currentType.description}` : ''}</small>}
                {unsupported && <small className="eng-editor-error">{copy.unsupportedHint}</small>}
              </Field>
              <Field label={copy.enabled}>
                <select value={draft.enabled === false ? 'false' : 'true'} onChange={event => { setDraft({ ...draft, enabled: event.target.value === 'true' }); setPreview(null); }}>
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
                  />
                ))}
                {(currentType.configurationSchema?.dataSourceFields.length ?? 0) === 0 && <span>{copy.noSettings}</span>}
              </div>
            </section>}

            <div className="eng-editor-actions">
              <button type="button" disabled={!changed || busy || unsupported || !currentType} onClick={() => void runPreview()}>{copy.preview}</button>
              <button type="button" className="primary" disabled={!preview?.canApply || busy} onClick={() => void runApply()}>{copy.apply}</button>
            </div>
            {preview && <div className={preview.canApply ? 'eng-editor-preview valid' : 'eng-editor-preview invalid'}>
              <strong>{preview.canApply ? copy.valid : copy.invalid}</strong><span>{copy.errors}: {preview.errorCount}</span>
            </div>}
            {error && <pre className="eng-editor-error">{error}</pre>}
          </>}
        </section>
      </div>
    </section>
  );
}

function ConfigurationField({ field, value, onChange }: {
  field: DataSourceConfigurationField;
  value: string;
  onChange: (value: string) => void;
}) {
  const label = `${field.displayName}${field.required ? ' *' : ''}`;
  const common = { value, onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => onChange(event.target.value) };
  return <Field label={label} hint={field.description ?? undefined}>
    {field.valueKind === 'boolean' ? (
      <select {...common}><option value="">—</option><option value="true">true</option><option value="false">false</option></select>
    ) : field.valueKind === 'enum' ? (
      <select {...common}><option value="">—</option>{field.allowedValues.map(option => <option key={option} value={option}>{option}</option>)}</select>
    ) : ['integer', 'port', 'number', 'duration'].includes(field.valueKind) ? (
      <input type="number" value={value} min={field.minimum ?? undefined} max={field.maximum ?? undefined} step={field.valueKind === 'number' || field.valueKind === 'duration' ? 'any' : '1'} onChange={event => onChange(event.target.value)} />
    ) : (
      <input value={value} onChange={event => onChange(event.target.value)} />
    )}
    <small><code>{field.key}</code>{field.advanced ? ' · advanced' : ''}</small>
  </Field>;
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return <label className="eng-editor-field"><span>{label}</span>{children}{hint && <small>{hint}</small>}</label>;
}

function isProtectedReference(kind: string): boolean {
  return kind === 'secretReference' || kind === 'certificateReference';
}

function identity(source: DataSourceEngineering): string {
  return source.id ?? `key:${source.key}`;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Data Source editor', description: 'Choose a source type from the backend catalog. Configuration fields come from that type schema.',
    newSource: 'New Data Source', catalogError: 'Could not load source type catalog', noSelection: 'Select or create a Data Source.',
    name: 'Name', key: 'Key', type: 'Type', enabled: 'Enabled', yes: 'Yes', no: 'No', chooseType: 'Choose a type',
    unsupported: 'Unavailable type', unsupportedHint: 'This persisted type is not available in this build. Select a supported type explicitly; it will not be remapped silently.',
    settings: 'Type configuration', settingsHint: 'Only fields declared by the selected backend schema are editable.', noSettings: 'This source type has no configuration fields.',
    preview: 'Validate draft', apply: 'Apply', valid: 'Valid candidate', invalid: 'Invalid candidate', errors: 'Errors', discard: 'Discard unsaved Data Source changes?'
  };
  if (locale === 'es') return {
    title: 'Editor de Data Source', description: 'Seleccione un tipo del catálogo backend. Los campos provienen del schema de ese tipo.',
    newSource: 'Nueva Data Source', catalogError: 'No se pudo cargar el catálogo de tipos', noSelection: 'Seleccione o cree una Data Source.',
    name: 'Nombre', key: 'Clave', type: 'Tipo', enabled: 'Habilitado', yes: 'Sí', no: 'No', chooseType: 'Seleccione un tipo',
    unsupported: 'Tipo no disponible', unsupportedHint: 'El tipo persistido no existe en esta build. Seleccione otro explícitamente; no será reinterpretado.',
    settings: 'Configuración del tipo', settingsHint: 'Solo los campos declarados por el schema backend son editables.', noSettings: 'Este tipo no tiene campos de configuración.',
    preview: 'Validar borrador', apply: 'Aplicar', valid: 'Candidato válido', invalid: 'Candidato inválido', errors: 'Errores', discard: '¿Descartar los cambios no guardados?'
  };
  return {
    title: 'Editor de Data Source', description: 'Escolha um tipo no catálogo do backend. Os campos de configuração vêm do schema desse tipo.',
    newSource: 'Nova Data Source', catalogError: 'Não foi possível carregar o catálogo de tipos', noSelection: 'Selecione ou crie uma Data Source.',
    name: 'Nome', key: 'Chave', type: 'Tipo', enabled: 'Habilitado', yes: 'Sim', no: 'Não', chooseType: 'Escolha um tipo',
    unsupported: 'Tipo indisponível', unsupportedHint: 'O tipo persistido não existe nesta build. Selecione outro explicitamente; ele não será reinterpretado silenciosamente.',
    settings: 'Configuração do tipo', settingsHint: 'Somente campos declarados pelo schema do backend podem ser editados.', noSettings: 'Este tipo não possui campos de configuração.',
    preview: 'Validar rascunho', apply: 'Aplicar', valid: 'Candidato válido', invalid: 'Candidato inválido', errors: 'Erros', discard: 'Descartar alterações não salvas da Data Source?'
  };
}
