import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView, ImportPreviewView } from './types';
import './structured-editors.css';

export type OperationalEventEngineering = {
  id?: string | null;
  key: string;
  name: string;
  type: string;
  category: string;
  source: string;
  area?: string | null;
  equipmentPath?: string | null;
  tagId?: string | null;
  tagPath?: string | null;
  message?: string | null;
  enabled?: boolean;
  metadata?: Record<string, string> | null;
};

type PackageWithOperationalEvents = EngineeringPackageView & {
  operationalEvents?: OperationalEventEngineering[];
};

type Props = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
  onApplied?: () => Promise<void> | void;
};

const NEW_IDENTITY = 'draft:new-operational-event';

export function operationalEventCount(model: EngineeringPackageView): number {
  return operationalEvents(model).length;
}

export function OperationalEventEditor({ model, locale, onApplied }: Props) {
  const copy = useMemo(() => operationalEventCopy(locale), [locale]);
  const events = operationalEvents(model);
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() =>
    events[0] ? operationalEventIdentity(events[0]) : null);
  const isNew = selectedIdentity === NEW_IDENTITY;
  const selected = !isNew && selectedIdentity
    ? events.find(item => operationalEventIdentity(item) === selectedIdentity) ?? null
    : null;
  const [draft, setDraft] = useState<OperationalEventEngineering | null>(() =>
    selected ? clone(selected) : null);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<EngineeringPackageView | null>(null);
  const [validatedChangeVersion, setValidatedChangeVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);

  useEffect(() => {
    // NEW mode installs its pristine draft synchronously in choose(). Do not replace
    // that draft here: doing so reintroduces the C17-class transition window where
    // new-mode identity and the previous persisted entity draft briefly disagree.
    if (selectedIdentity === NEW_IDENTITY) {
      invalidatePreview();
      return;
    }

    const current = selectedIdentity
      ? events.find(item => operationalEventIdentity(item) === selectedIdentity) ?? null
      : null;
    if (current) {
      setDraft(clone(current));
      invalidatePreview();
      return;
    }

    if (events[0]) {
      const fallback = events[0];
      setDraft(clone(fallback));
      setSelectedIdentity(operationalEventIdentity(fallback));
    } else {
      setDraft(null);
      setSelectedIdentity(null);
    }
    invalidatePreview();
  }, [selectedIdentity, model]);

  const changed = draft
    ? isNew
      ? JSON.stringify(draft) !== JSON.stringify(newOperationalEventDraft(draft.id ?? undefined))
      : selected ? JSON.stringify(selected) !== JSON.stringify(draft) : false
    : false;

  const localIssues = draft ? validateDraft(draft, events, selected) : [];
  const busy = previewing || applying;
  const filtered = events.filter(item => operationalEventSearchText(item).includes(query.trim().toLowerCase()));

  function invalidatePreview() {
    setPreview(null);
    setCandidate(null);
    setValidatedChangeVersion(null);
    setError(null);
  }

  function choose(identity: string) {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(copy.discardConfirm)) return;

    // Selection and draft move together in one React event/batch. In particular,
    // entering NEW mode never renders with the stable ID/metadata of the previously
    // selected persisted Operational Event.
    if (identity === NEW_IDENTITY) {
      setDraft(newOperationalEventDraft());
    } else {
      const next = events.find(item => operationalEventIdentity(item) === identity) ?? null;
      setDraft(next ? clone(next) : null);
    }
    setSelectedIdentity(identity);
    invalidatePreview();
  }

  function patch(patchValue: Partial<OperationalEventEngineering>) {
    setDraft(current => current ? { ...current, ...patchValue } : current);
    invalidatePreview();
  }

  function reset() {
    if (isNew) setDraft(newOperationalEventDraft(draft?.id ?? undefined));
    else if (selected) setDraft(clone(selected));
    invalidatePreview();
  }

  function selectTag(tagId: string) {
    if (!tagId) {
      patch({ tagId: null, tagPath: null });
      return;
    }
    const tag = model.tags.find(candidateTag => candidateTag.id === tagId);
    patch({ tagId, tagPath: tag?.path ?? null });
  }

  async function runPreview() {
    if (!draft || localIssues.length > 0) return;
    setPreviewing(true);
    setError(null);
    try {
      const next = clone(model) as PackageWithOperationalEvents;
      const existing = operationalEvents(next);
      if (isNew) {
        next.operationalEvents = [...existing, clone(draft)];
      } else if (selected) {
        const identity = operationalEventIdentity(selected);
        next.operationalEvents = existing.map(item =>
          operationalEventIdentity(item) === identity ? clone(draft) : item);
      } else {
        return;
      }

      const before = await loadEngineeringWorkspace();
      const nextPreview = await previewEngineeringPackage(next);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion) {
        throw new Error(copy.workspaceChanged);
      }

      setPreview(nextPreview);
      setCandidate(clone(next));
      setValidatedChangeVersion(after.changeVersion);
    } catch (reason) {
      setPreview(null);
      setCandidate(null);
      setValidatedChangeVersion(null);
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  }

  async function runApply() {
    if (!candidate || !preview?.canApply || validatedChangeVersion === null) return;
    setApplying(true);
    setError(null);
    try {
      await applyEngineeringPackage(candidate, validatedChangeVersion);
      await onApplied?.();
      if (!onApplied) window.location.reload();
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : String(reason);
      setPreview(null);
      setCandidate(null);
      setValidatedChangeVersion(null);
      setError(message);
    } finally {
      setApplying(false);
    }
  }

  const selectedTagId = draft?.tagId
    ?? model.tags.find(tag => tag.path === draft?.tagPath)?.id
    ?? '';

  return (
    <div className="eng-section" data-testid="operational-event-engineering">
      <header className="eng-section-header">
        <div>
          <span className="eng-eyebrow">C14 / C19</span>
          <h1>{copy.title}</h1>
          <p>{copy.description}</p>
        </div>
        <div className="eng-section-meta">
          <strong>{events.length} {copy.configured}</strong>
          <span>{copy.protectedFlow}</span>
        </div>
      </header>

      <div className="eng-editor-layout">
        <aside className="eng-panel eng-table-panel">
          <div className="eng-editor-actions">
            <button type="button" onClick={() => choose(NEW_IDENTITY)} disabled={busy} data-testid="operational-event-new">
              {copy.newEvent}
            </button>
          </div>
          <label>
            <span>{copy.search}</span>
            <input value={query} onChange={event => setQuery(event.target.value)} placeholder={copy.searchPlaceholder} />
          </label>
          <div className="eng-entity-grid" aria-label={copy.listLabel}>
            {filtered.map(item => {
              const identity = operationalEventIdentity(item);
              return (
                <button
                  type="button"
                  key={identity}
                  className={identity === selectedIdentity ? 'active' : ''}
                  onClick={() => choose(identity)}
                  disabled={busy}
                >
                  <strong>{item.name}</strong>
                  <span>{item.key}</span>
                  <small>{item.type} · {item.category}</small>
                </button>
              );
            })}
            {filtered.length === 0 && <span className="eng-empty">{copy.noMatches}</span>}
          </div>
        </aside>

        <section className="eng-editor-form-panel">
          {!draft ? <div className="eng-editor-empty">{copy.selectHint}</div> : (
            <>
              <div className="eng-editor-form-grid">
                <TextField label={copy.name} value={draft.name} onChange={value => patch({ name: value })} />
                <TextField label={copy.key} value={draft.key} mono onChange={value => patch({ key: value })} />
                <TextField label={copy.type} value={draft.type} onChange={value => patch({ type: value })} />
                <TextField label={copy.category} value={draft.category} onChange={value => patch({ category: value })} />
                <TextField label={copy.source} value={draft.source} onChange={value => patch({ source: value })} />
                <TextField label={copy.area} value={draft.area ?? ''} onChange={value => patch({ area: emptyToNull(value) })} />
                <TextField label={copy.equipment} value={draft.equipmentPath ?? ''} mono onChange={value => patch({ equipmentPath: emptyToNull(value) })} />
                <label>
                  <span>{copy.tag}</span>
                  <select value={selectedTagId} onChange={event => selectTag(event.target.value)}>
                    <option value="">{copy.noTag}</option>
                    {model.tags.filter(tag => Boolean(tag.id)).map(tag => (
                      <option key={tag.id} value={tag.id}>{tag.path}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>{copy.enabled}</span>
                  <input type="checkbox" checked={draft.enabled !== false} onChange={event => patch({ enabled: event.target.checked })} />
                </label>
                <label>
                  <span>{copy.message}</span>
                  <textarea rows={3} value={draft.message ?? ''} onChange={event => patch({ message: emptyToNull(event.target.value) })} />
                </label>
              </div>

              {localIssues.length > 0 && (
                <div className="eng-preview-error" role="alert">
                  {localIssues.map(issue => <div key={issue}>{copy.validation[issue] ?? issue}</div>)}
                </div>
              )}

              <div className="eng-editor-actions">
                <button type="button" className="secondary" onClick={reset} disabled={!changed || busy}>{copy.reset}</button>
                <button type="button" className="secondary" onClick={() => void runPreview()} disabled={!changed || busy || localIssues.length > 0} data-testid="operational-event-preview">
                  {previewing ? copy.previewing : copy.previewAction}
                </button>
                <button type="button" className="primary" onClick={() => void runApply()} disabled={!changed || busy || !preview?.canApply || !candidate || validatedChangeVersion === null} data-testid="operational-event-apply">
                  {applying ? copy.applying : copy.applyAction}
                </button>
              </div>

              <section className="eng-preview-panel" aria-live="polite">
                <header>
                  <strong>{preview ? (preview.canApply ? copy.valid : copy.invalid) : copy.notValidated}</strong>
                  {preview && <span>{copy.creates}: {preview.createCount} · {copy.updates}: {preview.updateCount} · {copy.errors}: {preview.errorCount}</span>}
                </header>
                {preview?.items.flatMap(item => item.issues ?? []).map((issue, index) => (
                  <div key={`${issue.code}-${index}`} className={issue.isError ? 'error' : 'warning'}>
                    <strong>{issue.code}</strong> {issue.message}
                  </div>
                ))}
                {error && <pre className="eng-preview-error">{error}</pre>}
                <footer>{copy.workspaceUntouched}</footer>
              </section>
            </>
          )}
        </section>
      </div>
    </div>
  );
}

function operationalEvents(model: EngineeringPackageView): OperationalEventEngineering[] {
  const value = (model as PackageWithOperationalEvents).operationalEvents;
  return Array.isArray(value) ? value : [];
}

function operationalEventIdentity(item: OperationalEventEngineering): string {
  return item.id?.trim() || `key:${item.key.toLowerCase()}`;
}

function operationalEventSearchText(item: OperationalEventEngineering): string {
  return [item.name, item.key, item.type, item.category, item.source, item.area ?? '', item.equipmentPath ?? '', item.tagPath ?? '', item.message ?? '']
    .join(' ')
    .toLowerCase();
}

function newOperationalEventDraft(id = createStableId()): OperationalEventEngineering {
  return {
    id,
    key: 'event.new',
    name: 'New Operational Event',
    type: 'process',
    category: 'operation',
    source: 'server-script',
    area: null,
    equipmentPath: null,
    tagId: null,
    tagPath: null,
    message: null,
    enabled: true,
    metadata: {}
  };
}

function validateDraft(
  draft: OperationalEventEngineering,
  existing: OperationalEventEngineering[],
  selected: OperationalEventEngineering | null
): string[] {
  const issues: string[] = [];
  if (!draft.id?.trim()) issues.push('id');
  if (!draft.key.trim()) issues.push('key');
  if (!draft.name.trim()) issues.push('name');
  if (!draft.type.trim()) issues.push('type');
  if (!draft.category.trim()) issues.push('category');
  if (!draft.source.trim()) issues.push('source');

  const selectedIdentity = selected ? operationalEventIdentity(selected) : null;
  if (existing.some(item =>
    operationalEventIdentity(item) !== selectedIdentity &&
    item.key.trim().toLowerCase() === draft.key.trim().toLowerCase())) {
    issues.push('duplicateKey');
  }

  return Array.from(new Set(issues));
}

function TextField({ label, value, mono = false, onChange }: { label: string; value: string; mono?: boolean; onChange: (value: string) => void }) {
  return <label><span>{label}</span><input className={mono ? 'mono' : ''} value={value} onChange={event => onChange(event.target.value)} /></label>;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function createStableId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, character => {
    const random = Math.floor(Math.random() * 16);
    const value = character === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}

function operationalEventCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Operational Events', description: 'Author canonical process-event definitions through the protected Engineering Preview / Apply workflow.',
    configured: 'configured', protectedFlow: 'Working → Preview → Apply', newEvent: 'New Operational Event', search: 'Search',
    searchPlaceholder: 'Name, key, type, category, source, area, equipment or TAG', listLabel: 'Operational Event definitions', noMatches: 'No matching definitions.',
    selectHint: 'Select a definition or create a new Operational Event.', discardConfirm: 'Discard the current un-applied Operational Event draft?',
    name: 'Name', key: 'Key', type: 'Type', category: 'Category', source: 'Source', area: 'Area', equipment: 'Equipment path', tag: 'TAG', noTag: 'No TAG',
    enabled: 'Enabled', message: 'Default message', reset: 'Reset', previewAction: 'Preview', previewing: 'Previewing…', applyAction: 'Apply', applying: 'Applying…',
    valid: 'Valid Engineering candidate', invalid: 'Invalid Engineering candidate', notValidated: 'Not validated', creates: 'Creates', updates: 'Updates', errors: 'Errors',
    workspaceUntouched: 'Preview does not mutate the official Working Engineering Workspace.', workspaceChanged: 'Engineering Workspace changed during Preview. Reload before applying.',
    validation: { id: 'A stable ID is required.', key: 'Key is required.', name: 'Name is required.', type: 'Type is required.', category: 'Category is required.', source: 'Source is required.', duplicateKey: 'Another Operational Event already uses this key.' } as Record<string, string>
  };
  if (locale === 'es') return {
    title: 'Eventos Operacionales', description: 'Configure definiciones canónicas de eventos de proceso mediante el flujo protegido Preview / Apply de Engineering.',
    configured: 'configurados', protectedFlow: 'Working → Preview → Apply', newEvent: 'Nuevo Evento Operacional', search: 'Buscar',
    searchPlaceholder: 'Nombre, clave, tipo, categoría, origen, área, equipo o TAG', listLabel: 'Definiciones de Eventos Operacionales', noMatches: 'No hay definiciones coincidentes.',
    selectHint: 'Seleccione una definición o cree un nuevo Evento Operacional.', discardConfirm: '¿Descartar el borrador no aplicado del Evento Operacional?',
    name: 'Nombre', key: 'Clave', type: 'Tipo', category: 'Categoría', source: 'Origen', area: 'Área', equipment: 'Ruta del equipo', tag: 'TAG', noTag: 'Sin TAG',
    enabled: 'Habilitado', message: 'Mensaje predeterminado', reset: 'Restablecer', previewAction: 'Preview', previewing: 'Validando…', applyAction: 'Aplicar', applying: 'Aplicando…',
    valid: 'Candidato de Engineering válido', invalid: 'Candidato de Engineering inválido', notValidated: 'No validado', creates: 'Crea', updates: 'Actualiza', errors: 'Errores',
    workspaceUntouched: 'Preview no modifica el Working Engineering Workspace oficial.', workspaceChanged: 'El Engineering Workspace cambió durante Preview. Recargue antes de aplicar.',
    validation: { id: 'Se requiere un ID estable.', key: 'La clave es obligatoria.', name: 'El nombre es obligatorio.', type: 'El tipo es obligatorio.', category: 'La categoría es obligatoria.', source: 'El origen es obligatorio.', duplicateKey: 'Otro Evento Operacional ya utiliza esta clave.' } as Record<string, string>
  };
  return {
    title: 'Eventos Operacionais', description: 'Configure definições canônicas de eventos de processo pelo fluxo protegido Preview / Apply do Engineering.',
    configured: 'configurados', protectedFlow: 'Working → Preview → Apply', newEvent: 'Novo Evento Operacional', search: 'Pesquisar',
    searchPlaceholder: 'Nome, chave, tipo, categoria, origem, área, equipamento ou TAG', listLabel: 'Definições de Eventos Operacionais', noMatches: 'Nenhuma definição correspondente.',
    selectHint: 'Selecione uma definição ou crie um novo Evento Operacional.', discardConfirm: 'Descartar o rascunho não aplicado do Evento Operacional?',
    name: 'Nome', key: 'Chave', type: 'Tipo', category: 'Categoria', source: 'Origem', area: 'Área', equipment: 'Caminho do equipamento', tag: 'TAG', noTag: 'Sem TAG',
    enabled: 'Habilitado', message: 'Mensagem padrão', reset: 'Restaurar', previewAction: 'Preview', previewing: 'Validando…', applyAction: 'Aplicar', applying: 'Aplicando…',
    valid: 'Candidato de Engineering válido', invalid: 'Candidato de Engineering inválido', notValidated: 'Não validado', creates: 'Cria', updates: 'Atualiza', errors: 'Erros',
    workspaceUntouched: 'O Preview não altera o Working Engineering Workspace oficial.', workspaceChanged: 'O Engineering Workspace mudou durante o Preview. Recarregue antes de aplicar.',
    validation: { id: 'É necessário um ID estável.', key: 'A chave é obrigatória.', name: 'O nome é obrigatório.', type: 'O tipo é obrigatório.', category: 'A categoria é obrigatória.', source: 'A origem é obrigatória.', duplicateKey: 'Outro Evento Operacional já utiliza esta chave.' } as Record<string, string>
  };
}