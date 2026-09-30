import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringBulk,
  deleteEngineeringEntity,
  loadEngineeringWorkspace,
  previewEngineeringBulk,
  type EngineeringBulkPreviewResult,
  type EngineeringBulkRequest,
  type EngineeringDeleteKind
} from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';
import './engineering-mutations.css';

type MutationKind = 'tag' | 'alarm' | 'data-source';
type BulkOperation =
  | 'readOnly'
  | 'historianEnabled'
  | 'enabled'
  | 'priority'
  | 'requiresAcknowledgement'
  | 'shelvingAllowed';

export type EngineeringEntityContext = {
  id: string;
  label: string;
  detail: string;
};

type EntityOption = EngineeringEntityContext;

export function EngineeringEntityActions({
  kind,
  model,
  locale,
  selectedEntity
}: {
  kind: MutationKind;
  model: EngineeringPackageView;
  locale: EngineeringLocale;
  selectedEntity: EngineeringEntityContext | null;
}) {
  const text = useMemo(() => mutationText(locale), [locale]);
  const entities = useMemo(() => entityOptions(kind, model), [kind, model]);
  const currentEntity = selectedEntity
    ? entities.find(entity => entity.id === selectedEntity.id) ?? selectedEntity
    : null;
  const [bulkOpen, setBulkOpen] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [operation, setOperation] = useState<BulkOperation>(() => defaultOperation(kind));
  const [value, setValue] = useState(() => defaultValue(defaultOperation(kind)));
  const [preview, setPreview] = useState<EngineeringBulkPreviewResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    setSelectedIds(current => new Set([...current].filter(id => entities.some(entity => entity.id === id))));
    setPreview(null);
    setError(null);
  }, [entities]);

  useEffect(() => {
    const nextOperation = defaultOperation(kind);
    setOperation(nextOperation);
    setValue(defaultValue(nextOperation));
    setSelectedIds(new Set());
    setPreview(null);
    setError(null);
    setBulkOpen(false);
  }, [kind]);

  useEffect(() => {
    setPreview(null);
    setError(null);
  }, [selectedIds, operation, value]);

  const operations = supportedOperations(kind, text);
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

  const toggleBulkMode = () => {
    setBulkOpen(current => !current);
    setSelectedIds(new Set());
    setPreview(null);
    setError(null);
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
    if (!currentEntity) return;
    if (!window.confirm(`${text.deleteConfirm} ${currentEntity.label}?\n\n${text.draftWarning}`)) return;
    setDeleting(true);
    setError(null);
    try {
      const workspace = await loadEngineeringWorkspace();
      await deleteEngineeringEntity(deleteKind(kind), currentEntity.id, workspace.changeVersion);
      window.location.reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <section className="eng-mutation-panel eng-entity-actions" data-testid="engineering-entity-actions">
      <header className="eng-entity-actions__header">
        <div className="eng-entity-actions__context" data-testid="engineering-current-entity">
          <span>{text.currentEntity}</span>
          <strong>{currentEntity?.label ?? text.newDraft}</strong>
          <code>{currentEntity?.detail ?? text.newDraftHint}</code>
        </div>
        <button
          type="button"
          className={bulkOpen ? 'secondary active' : 'secondary'}
          aria-expanded={bulkOpen}
          onClick={toggleBulkMode}
          disabled={busy || entities.length === 0}
          data-testid="engineering-bulk-toggle"
        >
          {bulkOpen ? text.closeBulk : text.openBulk}
        </button>
      </header>

      <div className="eng-entity-actions__single">
        <div>
          <strong>{text.entityActions}</strong>
          <span>{currentEntity ? text.deleteHint : text.newDraftActionHint}</span>
        </div>
        <button
          type="button"
          className="danger"
          disabled={!currentEntity || busy}
          onClick={() => void runDelete()}
          data-testid="engineering-delete"
        >
          {deleting ? text.deleting : currentEntity ? `${text.deleteAction}: ${currentEntity.label}` : text.deleteAction}
        </button>
      </div>

      {bulkOpen && (
        <section className="eng-entity-actions__bulk" data-testid="engineering-bulk-panel">
          <header>
            <div>
              <strong>{text.bulkTitle}</strong>
              <span>{text.bulkHint}</span>
            </div>
            <span className="eng-entity-actions__mode">{text.intentionalMode}</span>
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
      )}

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
    currentEntity: 'Current entity', newDraft: 'New draft', newDraftHint: 'Create and validate this entity before destructive actions are available.',
    entityActions: 'Actions for this entity', newDraftActionHint: 'Destructive actions require a persisted current entity.',
    openBulk: 'Edit multiple…', closeBulk: 'Exit bulk mode', intentionalMode: 'Bulk mode',
    draftWarning: 'This changes the official Workspace and invalidates any open draft.',
    deleteHint: 'Delete is bound to the current entity. The server checks dependencies and never cascades silently.',
    deleteAction: 'Delete', deleting: 'Deleting…', deleteConfirm: 'Delete',
    bulkTitle: 'Bulk edit', bulkHint: 'Select entities only in this explicit mode, choose one homogeneous change, Preview, then Apply.',
    selected: 'Selected', selectAll: 'Select all', clear: 'Clear', property: 'Property', value: 'Value',
    preview: 'Preview bulk change', previewing: 'Previewing…', apply: 'Apply bulk change', applying: 'Applying…',
    bulkConfirm: 'Apply this bulk change to the official Engineering Workspace? Any individual draft will become stale.',
    validPreview: 'Valid bulk candidate', invalidPreview: 'Invalid bulk candidate', affected: 'Affected', updates: 'Updates', errors: 'Errors',
    noEntities: 'No persisted entities are available.', trueValue: 'True', falseValue: 'False',
    readOnly: 'Read-only', historianEnabled: 'Historian enabled', enabled: 'Enabled', priority: 'Priority',
    requiresAcknowledgement: 'Requires acknowledgement', shelvingAllowed: 'Shelving allowed'
  };
  if (locale === 'es') return {
    currentEntity: 'Entidad actual', newDraft: 'Nuevo borrador', newDraftHint: 'Cree y valide la entidad antes de usar acciones destructivas.',
    entityActions: 'Acciones de esta entidad', newDraftActionHint: 'Las acciones destructivas requieren una entidad actual persistida.',
    openBulk: 'Editar varias…', closeBulk: 'Salir del modo lote', intentionalMode: 'Modo lote',
    draftWarning: 'Esto cambia el Workspace oficial e invalida cualquier borrador abierto.',
    deleteHint: 'Eliminar está asociado a la entidad actual. El servidor valida dependencias y nunca elimina en cascada silenciosamente.',
    deleteAction: 'Eliminar', deleting: 'Eliminando…', deleteConfirm: 'Eliminar',
    bulkTitle: 'Edición por lote', bulkHint: 'Seleccione entidades solo en este modo explícito, elija un cambio homogéneo, haga Preview y luego Aplique.',
    selected: 'Seleccionadas', selectAll: 'Seleccionar todas', clear: 'Limpiar', property: 'Propiedad', value: 'Valor',
    preview: 'Preview del lote', previewing: 'Validando…', apply: 'Aplicar lote', applying: 'Aplicando…',
    bulkConfirm: '¿Aplicar este cambio por lote al Engineering Workspace oficial? Cualquier borrador individual quedará obsoleto.',
    validPreview: 'Candidato válido', invalidPreview: 'Candidato inválido', affected: 'Afectadas', updates: 'Actualizaciones', errors: 'Errores',
    noEntities: 'No hay entidades persistidas disponibles.', trueValue: 'Verdadero', falseValue: 'Falso',
    readOnly: 'Solo lectura', historianEnabled: 'Historiador habilitado', enabled: 'Habilitado', priority: 'Prioridad',
    requiresAcknowledgement: 'Requiere reconocimiento', shelvingAllowed: 'Permite shelving'
  };
  return {
    currentEntity: 'Entidade atual', newDraft: 'Novo rascunho', newDraftHint: 'Crie e valide a entidade antes de usar ações destrutivas.',
    entityActions: 'Ações desta entidade', newDraftActionHint: 'Ações destrutivas exigem uma entidade atual já persistida.',
    openBulk: 'Editar várias…', closeBulk: 'Sair do modo em lote', intentionalMode: 'Modo em lote',
    draftWarning: 'Isto altera o Workspace oficial e invalida qualquer rascunho aberto.',
    deleteHint: 'Excluir está associado à entidade atual. O servidor verifica dependências e nunca executa cascade delete silencioso.',
    deleteAction: 'Excluir', deleting: 'Excluindo…', deleteConfirm: 'Excluir',
    bulkTitle: 'Edição em lote', bulkHint: 'Selecione entidades somente neste modo explícito, escolha uma alteração homogênea, faça Preview e só então Apply.',
    selected: 'Selecionadas', selectAll: 'Selecionar todas', clear: 'Limpar', property: 'Propriedade', value: 'Valor',
    preview: 'Validar lote', previewing: 'Validando…', apply: 'Aplicar lote', applying: 'Aplicando…',
    bulkConfirm: 'Aplicar esta alteração em lote ao Engineering Workspace oficial? Qualquer rascunho individual ficará obsoleto.',
    validPreview: 'Candidato de lote válido', invalidPreview: 'Candidato de lote inválido', affected: 'Afetadas', updates: 'Atualizações', errors: 'Erros',
    noEntities: 'Nenhuma entidade persistida disponível.', trueValue: 'Verdadeiro', falseValue: 'Falso',
    readOnly: 'Somente leitura', historianEnabled: 'Historiador habilitado', enabled: 'Habilitado', priority: 'Prioridade',
    requiresAcknowledgement: 'Exige reconhecimento', shelvingAllowed: 'Permite shelving'
  };
}
