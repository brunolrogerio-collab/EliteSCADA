import React, { useCallback, useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import { PythonMonacoEditor } from '../python-editor/PythonMonacoEditor';
import {
  hasBlockingPythonDiagnostics,
  resolvePythonDiagnosticSnapshot,
  type PythonEditorDiagnosticSnapshot
} from '../python-editor/pythonEditorDiagnostics';
import {
  applyScriptMutation,
  deleteScriptDefinition,
  extractDeleteDependencies,
  loadScriptEngineeringContext,
  previewScriptMutation,
  ScriptEngineeringApiError
} from './scriptEngineeringApi';
import {
  buildCanonicalScriptPackage,
  cloneScriptDefinition,
  createNewScriptDefinition,
  previewTokenMatches,
  SCRIPT_DEPENDENCY_KINDS,
  SCRIPT_EVENT_KINDS,
  SCRIPT_SCOPES,
  scriptMutationMode,
  scriptSearchText,
  validateScriptDraft
} from './ScriptEngineeringWorkspace.logic';
import {
  dependencyKindLabel,
  eventKindLabel,
  scopeLabel,
  scriptWorkspaceCopy
} from './ScriptEngineeringWorkspace.copy';
import type {
  ScriptDeleteDependency,
  ScriptEngineeringContext,
  ScriptEngineeringDefinition,
  ScriptMutationPreviewToken
} from './scriptEngineeringTypes';
import './script-engineering-workspace.css';

type ScriptEngineeringWorkspaceProps = {
  locale: EngineeringLocale;
  pythonDiagnosticsByScriptId?: Readonly<Record<string, PythonEditorDiagnosticSnapshot | undefined>>;
};

export function ScriptEngineeringWorkspace({
  locale,
  pythonDiagnosticsByScriptId
}: ScriptEngineeringWorkspaceProps) {
  const copy = useMemo(() => scriptWorkspaceCopy(locale), [locale]);
  const [context, setContext] = useState<ScriptEngineeringContext | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState<ScriptEngineeringDefinition | null>(null);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [previewToken, setPreviewToken] = useState<ScriptMutationPreviewToken | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [deleteDependencies, setDeleteDependencies] = useState<ScriptDeleteDependency[]>([]);

  const refresh = useCallback(async (preferId?: string) => {
    setLoading(true);
    setError(null);
    try {
      const next = await loadScriptEngineeringContext();
      next.scripts.sort((a, b) => a.path.localeCompare(b.path));
      setContext(next);
      const wantedId = preferId ?? selectedId;
      const selected = wantedId ? next.scripts.find(script => script.id === wantedId) : next.scripts[0];
      setSelectedId(selected?.id ?? null);
      setDraft(selected ? cloneScriptDefinition(selected) : null);
      setPreviewToken(null);
      setDeleteConfirm(false);
      setDeleteDependencies([]);
    } catch (cause) {
      setError(errorText(cause, copy.errors));
    } finally {
      setLoading(false);
    }
  }, [copy.errors, selectedId]);

  useEffect(() => { void refresh(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const filteredScripts = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!context) return [];
    return query ? context.scripts.filter(script => scriptSearchText(script).includes(query)) : context.scripts;
  }, [context, search]);

  const ownedVisualReferences = useMemo(
    () => context && draft ? context.visualEventReferences.filter(reference => reference.scriptId === draft.id) : [],
    [context, draft]
  );
  const mode = context && draft ? scriptMutationMode(draft, context.scripts) : 'CreateOnly';
  const currentPackage = draft ? buildCanonicalScriptPackage(draft, context?.visualEventReferences ?? []) : null;
  const previewCurrent = Boolean(currentPackage && previewToken && previewTokenMatches(previewToken, currentPackage, mode));
  const localIssues = draft ? validateScriptDraft(draft) : [];
  const pythonDiagnostics = useMemo(
    () => resolvePythonDiagnosticSnapshot(
      draft?.source ?? '',
      draft ? pythonDiagnosticsByScriptId?.[draft.id] : undefined
    ),
    [draft?.id, draft?.source, pythonDiagnosticsByScriptId]
  );
  const blockingPythonDiagnostics = pythonDiagnostics.status === 'ready' &&
    hasBlockingPythonDiagnostics(pythonDiagnostics.diagnostics);

  function selectScript(script: ScriptEngineeringDefinition) {
    setSelectedId(script.id);
    setDraft(cloneScriptDefinition(script));
    setPreviewToken(null);
    setNotice(null);
    setError(null);
    setDeleteConfirm(false);
    setDeleteDependencies([]);
  }

  function startNewScript() {
    const next = createNewScriptDefinition();
    setSelectedId(next.id);
    setDraft(next);
    setPreviewToken(null);
    setNotice(null);
    setError(null);
    setDeleteConfirm(false);
    setDeleteDependencies([]);
  }

  function patchDraft(patch: Partial<ScriptEngineeringDefinition>) {
    setDraft(current => current ? { ...current, ...patch } : current);
    setNotice(null);
  }

  async function runPreview() {
    if (!context || !draft || localIssues.length > 0 || blockingPythonDiagnostics) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    setDeleteDependencies([]);
    try {
      const token = await previewScriptMutation(draft, context.visualEventReferences, mode);
      setPreviewToken(token);
      setNotice(token.preview.canApply ? copy.previewReady : copy.previewInvalid);
    } catch (cause) {
      setPreviewToken(null);
      setError(errorText(cause, copy.errors));
    } finally {
      setBusy(false);
    }
  }

  async function applyPreview() {
    if (!previewToken || !previewCurrent || !previewToken.preview.canApply) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await applyScriptMutation(previewToken);
      const wasCreate = previewToken.mode === 'CreateOnly';
      const id = previewToken.package.scripts[0]?.id;
      setNotice(wasCreate ? copy.created : copy.updated);
      await refresh(id);
    } catch (cause) {
      setError(errorText(cause, copy.errors));
      if (cause instanceof ScriptEngineeringApiError && cause.status === 409) setPreviewToken(null);
    } finally {
      setBusy(false);
    }
  }

  async function confirmDelete() {
    if (!context || !draft || !context.scripts.some(script => script.id === draft.id)) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    setDeleteDependencies([]);
    try {
      await deleteScriptDefinition(draft.id, context.workspace.changeVersion);
      setNotice(copy.deleted);
      setSelectedId(null);
      await refresh();
    } catch (cause) {
      const dependencies = extractDeleteDependencies(cause);
      setDeleteDependencies(dependencies);
      setError(errorText(cause, copy.errors, dependencies.length > 0));
      setDeleteConfirm(false);
    } finally {
      setBusy(false);
    }
  }

  if (loading && !context) {
    return <section className="script-workspace"><div className="script-state">{copy.refresh}…</div></section>;
  }

  return (
    <section className="script-workspace" aria-label={copy.title}>
      <header className="script-workspace__header">
        <div>
          <div className="script-workspace__eyebrow">PYTHON-WAVE-06</div>
          <h2>{copy.title}</h2>
          <p>{copy.subtitle}</p>
        </div>
        <div className="script-workspace__status">
          <span className="script-badge">{copy.working} v{context?.workspace.changeVersion ?? '—'}</span>
          <span className={`script-badge ${context?.workspace.isDirty ? 'script-badge--attention' : ''}`}>
            {context?.workspace.isDirty ? copy.dirty : copy.clean}
          </span>
          <span className="script-badge script-badge--quiet">{copy.noExecution}</span>
        </div>
      </header>

      {(error || notice) && (
        <div className={`script-message ${error ? 'script-message--error' : 'script-message--ok'}`} role={error ? 'alert' : 'status'}>
          {error ?? notice}
        </div>
      )}

      <div className="script-workspace__toolbar">
        <input
          aria-label={copy.search}
          placeholder={copy.search}
          value={search}
          onChange={event => setSearch(event.target.value)}
        />
        <button type="button" onClick={startNewScript} disabled={busy}>{copy.newScript}</button>
        <button type="button" className="secondary" onClick={() => void refresh()} disabled={busy}>{copy.refresh}</button>
      </div>

      <div className="script-workspace__layout">
        <aside className="script-list" aria-label={copy.title}>
          {filteredScripts.length === 0 ? <p className="script-muted">{copy.empty}</p> : filteredScripts.map(script => (
            <button
              type="button"
              key={script.id}
              className={`script-list__item ${selectedId === script.id ? 'is-selected' : ''}`}
              onClick={() => selectScript(script)}
            >
              <strong>{script.name}</strong>
              <span>{script.path}</span>
              <small>{scopeLabel(script.scope, locale)} · {script.enabled ? copy.enabled : 'Disabled'}</small>
            </button>
          ))}
        </aside>

        <main className="script-editor">
          {!draft ? <div className="script-state">{copy.selectHint}</div> : (
            <>
              <div className="script-editor__titlebar">
                <div>
                  <h3>{draft.name || copy.newScript}</h3>
                  <span className="script-muted">{mode === 'CreateOnly' ? copy.createMode : copy.updateMode} · {draft.id}</span>
                </div>
                <label className="script-check">
                  <input type="checkbox" checked={draft.enabled} onChange={event => patchDraft({ enabled: event.target.checked })} />
                  {copy.enabled}
                </label>
              </div>

              <div className="script-grid script-grid--two">
                <label>{copy.name}<input value={draft.name} onChange={event => patchDraft({ name: event.target.value })} /></label>
                <label>{copy.path}<input value={draft.path} onChange={event => patchDraft({ path: event.target.value })} /></label>
                <label>{copy.scope}
                  <select value={draft.scope} onChange={event => patchDraft({ scope: event.target.value as ScriptEngineeringDefinition['scope'] })}>
                    {SCRIPT_SCOPES.map(scope => <option key={scope} value={scope}>{scopeLabel(scope, locale)}</option>)}
                  </select>
                </label>
                <label>{copy.language}<input value={`${draft.language} ${draft.languageVersion}`} readOnly /></label>
              </div>

              <label>{copy.description}
                <textarea rows={2} value={draft.description ?? ''} onChange={event => patchDraft({ description: event.target.value })} />
              </label>

              <div className="script-source-field">
                <span className="script-source-field__label">{copy.source}</span>
                <PythonMonacoEditor
                  scriptId={draft.id}
                  path={draft.path}
                  source={draft.source}
                  scope={draft.scope}
                  entryPoints={draft.entryPoints}
                  locale={locale}
                  diagnostics={pythonDiagnostics}
                  onSourceChange={source => patchDraft({ source })}
                />
                <small className="script-muted">{copy.sourceHint}</small>
              </div>

              <EditorCollectionHeader title={copy.entryPoints} hint={copy.entryPointsHint} action={copy.addEntryPoint} onAdd={() => patchDraft({ entryPoints: [...draft.entryPoints, { eventKind: 'initialize', handlerName: 'initialize', targetReference: null }] })} />
              <div className="script-rows">
                {draft.entryPoints.map((entry, index) => (
                  <div className="script-row script-row--entry" key={`entry-${index}`}>
                    <label>{copy.event}<select value={entry.eventKind} onChange={event => patchDraft({ entryPoints: draft.entryPoints.map((item, itemIndex) => itemIndex === index ? { ...item, eventKind: event.target.value as typeof entry.eventKind } : item) })}>{SCRIPT_EVENT_KINDS.map(kind => <option key={kind} value={kind}>{eventKindLabel(kind)}</option>)}</select></label>
                    <label>{copy.handler}<input value={entry.handlerName} onChange={event => patchDraft({ entryPoints: draft.entryPoints.map((item, itemIndex) => itemIndex === index ? { ...item, handlerName: event.target.value } : item) })} /></label>
                    <label>{copy.target}<input value={entry.targetReference ?? ''} onChange={event => patchDraft({ entryPoints: draft.entryPoints.map((item, itemIndex) => itemIndex === index ? { ...item, targetReference: event.target.value || null } : item) })} /></label>
                    <button type="button" className="danger ghost" onClick={() => patchDraft({ entryPoints: draft.entryPoints.filter((_, itemIndex) => itemIndex !== index) })}>{copy.remove}</button>
                  </div>
                ))}
              </div>

              <EditorCollectionHeader title={copy.dependencies} hint={copy.dependenciesHint} action={copy.addDependency} onAdd={() => patchDraft({ dependencies: [...draft.dependencies, { kind: 'script', stableReference: '' }] })} />
              <div className="script-rows">
                {draft.dependencies.map((dependency, index) => (
                  <div className="script-row script-row--dependency" key={`dependency-${index}`}>
                    <label>{copy.kind}<select value={dependency.kind} onChange={event => patchDraft({ dependencies: draft.dependencies.map((item, itemIndex) => itemIndex === index ? { ...item, kind: event.target.value as typeof dependency.kind } : item) })}>{SCRIPT_DEPENDENCY_KINDS.map(kind => <option key={kind} value={kind}>{dependencyKindLabel(kind)}</option>)}</select></label>
                    <label>{copy.stableReference}<input value={dependency.stableReference} onChange={event => patchDraft({ dependencies: draft.dependencies.map((item, itemIndex) => itemIndex === index ? { ...item, stableReference: event.target.value } : item) })} /></label>
                    <button type="button" className="danger ghost" onClick={() => patchDraft({ dependencies: draft.dependencies.filter((_, itemIndex) => itemIndex !== index) })}>{copy.remove}</button>
                  </div>
                ))}
              </div>

              <div className="script-preserved">
                <strong>{copy.visualReferences}: {ownedVisualReferences.length}</strong>
                <span>{copy.visualReferencesHint}</span>
              </div>

              {localIssues.length > 0 && (
                <div className="script-validation" role="alert">
                  <strong>{copy.validationTitle}</strong>
                  <ul>{localIssues.map(issue => <li key={issue}>{validationText(issue, copy.validation)}</li>)}</ul>
                </div>
              )}

              {previewToken && (
                <div className={`script-preview ${previewCurrent && previewToken.preview.canApply ? 'script-preview--ok' : 'script-preview--stale'}`}>
                  <strong>{previewCurrent ? (previewToken.preview.canApply ? copy.previewReady : copy.previewInvalid) : copy.previewExpired}</strong>
                  <span>Create {previewToken.preview.createCount} · Update {previewToken.preview.updateCount} · Skip {previewToken.preview.skipCount} · Errors {previewToken.preview.errorCount}</span>
                  {previewToken.preview.items.flatMap(item => item.issues).length > 0 && (
                    <ul>{previewToken.preview.items.flatMap(item => item.issues).map((issue, index) => <li key={`${issue.code}-${index}`}>{issue.code}: {issue.message}</li>)}</ul>
                  )}
                </div>
              )}

              {deleteDependencies.length > 0 && (
                <div className="script-validation">
                  <strong>{copy.errors.deleteConflict}</strong>
                  <ul>{deleteDependencies.map(dependency => <li key={`${dependency.entityKind}-${dependency.entityId}-${dependency.relation}`}>{dependency.entityKind}: {dependency.entityKey} ({dependency.relation})</li>)}</ul>
                </div>
              )}

              <div className="script-actions">
                <button type="button" onClick={() => void runPreview()} disabled={busy || localIssues.length > 0 || blockingPythonDiagnostics}>{copy.preview}</button>
                <button type="button" onClick={() => void applyPreview()} disabled={busy || !previewCurrent || !previewToken?.preview.canApply}>{copy.apply}</button>
                {context?.scripts.some(script => script.id === draft.id) && !deleteConfirm && <button type="button" className="danger" onClick={() => setDeleteConfirm(true)} disabled={busy}>{copy.delete}</button>}
                {deleteConfirm && (
                  <div className="script-delete-confirm">
                    <span>{copy.deleteWarning}</span>
                    <button type="button" className="danger" onClick={() => void confirmDelete()} disabled={busy}>{copy.confirmDelete}</button>
                    <button type="button" className="secondary" onClick={() => setDeleteConfirm(false)} disabled={busy}>{copy.cancel}</button>
                  </div>
                )}
              </div>
            </>
          )}
        </main>
      </div>
    </section>
  );
}

function EditorCollectionHeader({ title, hint, action, onAdd }: { title: string; hint: string; action: string; onAdd: () => void }) {
  return <div className="script-collection-header"><div><h4>{title}</h4><p>{hint}</p></div><button type="button" className="secondary" onClick={onAdd}>{action}</button></div>;
}

function validationText(code: string, validation: Record<string, string>): string {
  return validation[code] ?? code;
}

function errorText(
  cause: unknown,
  errors: { unauthorized: string; forbidden: string; conflict: string; deleteConflict: string; badRequest: string; unavailable: string; generic: string },
  deleteConflict = false
): string {
  if (!(cause instanceof ScriptEngineeringApiError)) return cause instanceof Error ? cause.message : errors.generic;
  if (cause.status === 401) return errors.unauthorized;
  if (cause.status === 403) return errors.forbidden;
  if (cause.status === 409) return deleteConflict ? errors.deleteConflict : errors.conflict;
  if (cause.status === 400 || cause.status === 422) return cause.message || errors.badRequest;
  if (cause.status === 503) return errors.unavailable;
  return cause.message || errors.generic;
}
