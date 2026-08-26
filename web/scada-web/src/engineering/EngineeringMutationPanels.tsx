import React, { useEffect, useMemo, useState } from 'react';
import {
  AlarmEditor as SecuredAlarmEditor,
  DataSourceEditor as SecuredDataSourceEditor,
  TagEditor as SecuredTagEditor
} from './SecuredEngineeringEditors';
import {
  applyEngineeringBulk,
  deleteEngineeringEntity,
  loadEngineeringWorkspace,
  previewEngineeringBulk,
  type EngineeringBulkEntityKind,
  type EngineeringBulkPreviewResult,
  type EngineeringBulkRequest,
  type EngineeringDeleteKind
} from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';
import './engineering-mutations.css';

type EditorProps = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
};

type EntityOption = {
  id: string;
  label: string;
  detail: string;
};

type MutationKind = 'tag' | 'alarm' | 'data-source';
type BulkOperation =
  | 'readOnly'
  | 'historianEnabled'
  | 'enabled'
  | 'priority'
  | 'requiresAcknowledgement'
  | 'shelvingAllowed';

export function TagEditor({ model, locale }: EditorProps) {
  return (
    <>
      <SecuredTagEditor model={model} locale={locale} />
      <EngineeringMutationPanel kind="tag" model={model} locale={locale} />
    </>
  );
}

export function DataSourceEditor({ model, locale }: EditorProps) {
  return (
    <>
      <SecuredDataSourceEditor model={model} locale={locale} />
      <EngineeringMutationPanel kind="data-source" model={model} locale={locale} />
    </>
  );
}

export function AlarmEditor({ model, locale }: EditorProps) {
  return (
    <>
      <SecuredAlarmEditor model={model} locale={locale} />
      <EngineeringMutationPanel kind="alarm" model={model} locale={locale} />
    </>
  );
}

function EngineeringMutationPanel({ kind, model, locale }: EditorProps & { kind: MutationKind }) {
  const text = useMemo(() => mutationText(locale), [locale]);
  const entities = useMemo(() => entityOptions(kind, model), [kind, model]);
  const [deleteId, setDeleteId] = useState(entities[0]?.id ?? '');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [operation, setOperation] = useState<BulkOperation>(() => defaultOperation(kind));
  const [value, setValue] = useState(() => defaultValue(defaultOperation(kind)));
  const [preview, setPreview] = useState<EngineeringBulkPreviewResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!entities.some(entity => entity.id === deleteId)) setDeleteId(entities[0]?.id ?? '');
    setSelectedIds(current => new Set([...current].filter(id => entities.some(entity => entity.id === id))));
  }, [entities, deleteId]);

  useEffect(() => {
    const nextOperation = defaultOperation(kind);
    setOperation(nextOperation);
    setValue(defaultValue(nextOperation));
    setSelectedIds(new Set());
    setPreview(null);
    setError(null);
  }, [kind]);

  useEffect(() => {
    setPreview(null);
    setError(null);
  }, [selectedIds, operation, value]);

  const operations = supportedOperations(kind, text);
  const selectedDelete = entities.find(entity => entity.id === deleteId) ?? null;
  const busy = previewing || applying || deleting;

  const toggleSelected = (id: string) => {
    setSelectedIds(current => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const changeOperation = (next: BulkOperation) => {
    setOperation(next);
    setValue(defaultValue(next));
  };

  const runPreview = async () => {
    if (selectedIds.size === 0) return;
    setPreviewing(true);
    setError(null);
    try {
      setPreview(await previewEngineeringBulk(buildBulkRequest(kind, [...selectedIds], operation, value)));
    } catch (reason) {
      setPreview(null);
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  const runApply = async () => {
    if (!preview?.preview.canApply || selectedIds.size === 0) return;
    if (!window.confirm(text.bulkConfirm)) return;
    setApplying(true);
    setError(null);
    try {
      await applyEngineeringBulk(
        buildBulkRequest(kind, [...selectedIds], operation, value),
        preview.changeVersion);
      window.location.reload();
    } catch (reason) {
      setPreview(null);
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setApplying(false);
    }
  };

  const runDelete = async () => {
    if (!selectedDelete) return;
    if (!window.confirm(`${text.deleteConfirm} ${selectedDelete.label}?\n\n${text.draftWarning}`)) return;
    setDeleting(true);
    setError(null);
    try {
      const workspace = await loadEngineeringWorkspace();
      await deleteEngineeringEntity(deleteKind(kind), selectedDelete.id, workspace.changeVersion);
      window.location.reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <section className="eng-mutation-panel">
      <header className="eng-mutation-header">
        <div>
          <span>{text.explicitMutations}</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <div className="eng-mutation-warning">{text.draftWarning}</div>
      </header>

      <div className="eng-mutation-grid">
        <section className="eng-mutation-card">
          <header>
            <strong>{text.deleteTitle}</strong>
            <span>{text.deleteHint}</span>
          </header>
          <label className="eng-mutation-field">
            <span>{text.entity}</span>
            <select value={deleteId} onChange={event => setDeleteId(event.target.value)} disabled={busy || entities.length === 0}>
              {entities.map(entity => (
                <option key={entity.id} value={entity.id}>{entity.label}</option>
              ))}
            </select>
          </label>
          {selectedDelete && <code className="eng-mutation-detail">{selectedDelete.detail}</code>}
          <button
            type="button"
            className="danger"
            disabled={!selectedDelete || busy}
            onClick={() => void runDelete()}
            data-testid="engineering-delete"
          >
            {deleting ? text.deleting : text.deleteAction}
          </button>
        </section>

        <section className="eng-mutation-card eng-bulk-card">
          <header>
            <strong>{text.bulkTitle}</strong>
            <span>{text.bulkHint}</span>
          </header>

          <div className="eng-bulk-select">
            <div className="eng-bulk-select-header">
              <span>{text.selected}: <b data-testid="engineering-bulk-selected">{selectedIds.size}</b></span>
              <button type="button" onClick={() => setSelectedIds(new Set(entities.map(entity => entity.id)))} disabled={busy || entities.length === 0}>{text.selectAll}</button>
              <button type="button" onClick={() => setSelectedIds(new Set())} disabled={busy || selectedIds.size === 0}>{text.clear}</button>
            </div>
            <div className="eng-bulk-entities">
              {entities.map(entity => (
                <label key={entity.id}>
                  <input
                    type="checkbox"
                    checked={selectedIds.has(entity.id)}
                    onChange={() => toggleSelected(entity.id)}
                    disabled={busy}
                  />
                  <span><strong>{entity.label}</strong><code>{entity.detail}</code></span>
                </label>
              ))}
              {entities.length === 0 && <span className="eng-mutation-empty">{text.noEntities}</span>}
            </div>
          </div>

          <div className="eng-bulk-controls">
            <label className="eng-mutation-field">
              <span>{text.property}</span>
              <select value={operation} onChange={event => changeOperation(event.target.value as BulkOperation)} disabled={busy}>
                {operations.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </label>
            <label className="eng-mutation-field">
              <span>{text.value}</span>
              {operation === 'priority' ? (
                <select value={value} onChange={event => setValue(event.target.value)} disabled={busy}>
                  {['low', 'medium', 'high', 'critical'].map(priority => <option key={priority} value={priority}>{priority}</option>)}
                </select>
              ) : (
                <select value={value} onChange={event => setValue(event.target.value)} disabled={busy}>
                  <option value="true">{text.trueValue}</option>
                  <option value="false">{text.falseValue}</option>
                </select>
              )}
            </label>
          </div>

          <div className="eng-mutation-actions">
            <button
              type="button"
              className="secondary"
              disabled={selectedIds.size === 0 || busy}
              onClick={() => void runPreview()}
              data-testid="engineering-bulk-preview"
            >
              {previewing ? text.previewing : text.preview}
            </button>
            <button
              type="button"
              className="primary"
              disabled={!preview?.preview.canApply || busy}
              onClick={() => void runApply()}
              data-testid="engineering-bulk-apply"
            >
              {applying ? text.applying : text.apply}
            </button>
          </div>

          {preview && (
            <div className={preview.preview.canApply ? 'eng-bulk-preview valid' : 'eng-bulk-preview invalid'}>
              <strong>{preview.preview.canApply ? text.validPreview : text.invalidPreview}</strong>
              <span>{text.affected}: <b data-testid="engineering-bulk-affected">{preview.affectedCount}</b></span>
              <span>{text.updates}: <b>{preview.preview.updateCount}</b></span>
              <span>{text.errors}: <b>{preview.preview.errorCount}</b></span>
              <small>Workspace v{preview.changeVersion}</small>
            </div>
          )}
        </section>
      </div>

      {error && <pre className="eng-mutation-error" aria-live="polite">{error}</pre>}
    </section>
  );
}

function entityOptions(kind: MutationKind, model: EngineeringPackageView): EntityOption[] {
  if (kind === 'tag') {
    return model.tags
      .filter(tag => Boolean(tag.id))
      .map(tag => ({ id: tag.id!, label: tag.path, detail: `${tag.name} · ${tag.dataType}` }));
  }
  if (kind === 'alarm') {
    return model.alarms
      .filter(alarm => Boolean(alarm.id))
      .map(alarm => ({ id: alarm.id!, label: alarm.name, detail: `${alarm.tagPath ?? alarm.tagId ?? '—'} · ${alarm.priority}` }));
  }
  return (model.dataSources ?? [])
    .filter(dataSource => Boolean(dataSource.id))
    .map(dataSource => ({ id: dataSource.id!, label: dataSource.key, detail: `${dataSource.name} · ${dataSource.driver}` }));
}

function buildBulkRequest(
  kind: MutationKind,
  entityIds: string[],
  operation: BulkOperation,
  value: string
): EngineeringBulkRequest {
  const booleanValue = value === 'true';
  if (kind === 'tag') {
    return {
      entityKind: 'tag',
      entityIds,
      tags: operation === 'historianEnabled'
        ? { historianEnabled: booleanValue }
        : { readOnly: booleanValue }
    };
  }
  if (kind === 'alarm') {
    const alarms = operation === 'priority'
      ? { priority: value }
      : operation === 'requiresAcknowledgement'
        ? { requiresAcknowledgement: booleanValue }
        : operation === 'shelvingAllowed'
          ? { shelvingAllowed: booleanValue }
          : { enabled: booleanValue };
    return { entityKind: 'alarm', entityIds, alarms };
  }
  return { entityKind: 'data-source', entityIds, dataSources: { enabled: booleanValue } };
}

function supportedOperations(kind: MutationKind, text: ReturnType<typeof mutationText>): Array<{ value: BulkOperation; label: string }> {
  if (kind === 'tag') return [
    { value: 'readOnly', label: text.readOnly },
    { value: 'historianEnabled', label: text.historianEnabled }
  ];
  if (kind === 'alarm') return [
    { value: 'enabled', label: text.enabled },
    { value: 'priority', label: text.priority },
    { value: 'requiresAcknowledgement', label: text.requiresAcknowledgement },
    { value: 'shelvingAllowed', label: text.shelvingAllowed }
  ];
  return [{ value: 'enabled', label: text.enabled }];
}

function defaultOperation(kind: MutationKind): BulkOperation {
  return kind === 'tag' ? 'readOnly' : 'enabled';
}

function defaultValue(operation: BulkOperation): string {
  return operation === 'priority' ? 'medium' : 'true';
}

function deleteKind(kind: MutationKind): EngineeringDeleteKind {
  return kind === 'tag' ? 'tags' : kind === 'alarm' ? 'alarms' : 'data-sources';
}

function mutationText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    explicitMutations: 'Explicit Engineering mutations', title: 'Delete and bulk edit',
    description: 'Destructive and homogeneous mutations remain separate from the individual draft editor.',
    draftWarning: 'Delete or bulk Apply changes the official Workspace and invalidates any individual draft currently open.',
    deleteTitle: 'Explicit Delete', deleteHint: 'The server checks dependencies and fails closed. No cascade delete is performed.',
    deleteAction: 'Delete entity', deleting: 'Deleting...', deleteConfirm: 'Delete explicitly', entity: 'Entity',
    bulkTitle: 'Safe bulk edit', bulkHint: 'Select entities, choose one homogeneous change, preview it, then Apply.',
    selected: 'Selected', selectAll: 'Select all', clear: 'Clear', property: 'Property', value: 'Value',
    preview: 'Preview bulk change', previewing: 'Previewing...', apply: 'Apply bulk change', applying: 'Applying...',
    bulkConfirm: 'Apply this bulk change to the official Engineering Workspace? Any individual draft will become stale.',
    validPreview: 'Valid bulk candidate', invalidPreview: 'Invalid bulk candidate', affected: 'Affected', updates: 'Updates', errors: 'Errors',
    noEntities: 'No persisted entities are available.', trueValue: 'True', falseValue: 'False',
    readOnly: 'Read-only', historianEnabled: 'Historian enabled', enabled: 'Enabled', priority: 'Priority',
    requiresAcknowledgement: 'Requires acknowledgement', shelvingAllowed: 'Shelving allowed'
  };
  if (locale === 'es') return {
    explicitMutations: 'Mutaciones explícitas de Ingeniería', title: 'Eliminar y edición por lote',
    description: 'Las mutaciones destructivas y homogéneas permanecen separadas del borrador individual.',
    draftWarning: 'Eliminar o Aplicar en lote cambia el Workspace oficial e invalida cualquier borrador individual abierto.',
    deleteTitle: 'Eliminación explícita', deleteHint: 'El servidor verifica dependencias y falla cerrado. No existe eliminación en cascada.',
    deleteAction: 'Eliminar entidad', deleting: 'Eliminando...', deleteConfirm: 'Eliminar explícitamente', entity: 'Entidad',
    bulkTitle: 'Edición segura por lote', bulkHint: 'Seleccione entidades, elija un cambio homogéneo, haga preview y luego Aplique.',
    selected: 'Seleccionados', selectAll: 'Seleccionar todos', clear: 'Limpiar', property: 'Propiedad', value: 'Valor',
    preview: 'Preview del lote', previewing: 'Validando...', apply: 'Aplicar lote', applying: 'Aplicando...',
    bulkConfirm: '¿Aplicar este cambio por lote al Engineering Workspace oficial? Cualquier borrador individual quedará obsoleto.',
    validPreview: 'Candidato válido', invalidPreview: 'Candidato inválido', affected: 'Afectados', updates: 'Actualizaciones', errors: 'Errores',
    noEntities: 'No hay entidades persistidas disponibles.', trueValue: 'Verdadero', falseValue: 'Falso',
    readOnly: 'Solo lectura', historianEnabled: 'Historiador habilitado', enabled: 'Habilitado', priority: 'Prioridad',
    requiresAcknowledgement: 'Requiere reconocimiento', shelvingAllowed: 'Permite shelving'
  };
  return {
    explicitMutations: 'Mutações explícitas de Engineering', title: 'Excluir e editar em lote',
    description: 'Mutações destrutivas e homogêneas ficam separadas do rascunho individual.',
    draftWarning: 'Delete ou Apply em lote altera o Workspace oficial e invalida qualquer rascunho individual que esteja aberto.',
    deleteTitle: 'Delete explícito', deleteHint: 'O servidor verifica dependências e falha fechado. Não existe cascade delete.',
    deleteAction: 'Excluir entidade', deleting: 'Excluindo...', deleteConfirm: 'Excluir explicitamente', entity: 'Entidade',
    bulkTitle: 'Edição segura em lote', bulkHint: 'Selecione entidades, escolha uma alteração homogênea, faça Preview e só então Apply.',
    selected: 'Selecionadas', selectAll: 'Selecionar todas', clear: 'Limpar', property: 'Propriedade', value: 'Valor',
    preview: 'Validar lote', previewing: 'Validando...', apply: 'Aplicar lote', applying: 'Aplicando...',
    bulkConfirm: 'Aplicar esta alteração em lote ao Engineering Workspace oficial? Qualquer rascunho individual ficará obsoleto.',
    validPreview: 'Candidato de lote válido', invalidPreview: 'Candidato de lote inválido', affected: 'Afetadas', updates: 'Atualizações', errors: 'Erros',
    noEntities: 'Nenhuma entidade persistida disponível.', trueValue: 'Verdadeiro', falseValue: 'Falso',
    readOnly: 'Somente leitura', historianEnabled: 'Historiador habilitado', enabled: 'Habilitado', priority: 'Prioridade',
    requiresAcknowledgement: 'Exige reconhecimento', shelvingAllowed: 'Permite shelving'
  };
}
