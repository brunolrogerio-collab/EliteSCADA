import React, { useEffect, useMemo, useState } from 'react';
import { applyEngineeringPackage, loadEngineeringWorkspace, previewEngineeringPackage } from '../api';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringPackageView, EngineeringSnapshot, ImportPreviewView, ScreenEngineering } from '../types';
import { CanonicalVisualRenderer } from './CanonicalVisualRenderer';
import {
  NEW_SCREEN_IDENTITY,
  cloneEngineeringValue,
  countVisualElements,
  createScreenDraft,
  replaceScreenInPackage,
  screenIdentity
} from './visualEditorCanonicalModel';
import './VisualEditorWorkspace.css';

type VisualEditorWorkspaceProps = {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
};

type ValidatedCandidate = { package: EngineeringPackageView; changeVersion: number };

export function VisualEditorWorkspace({ snapshot, locale, onApplied }: VisualEditorWorkspaceProps) {
  const text = useMemo(() => visualEditorText(locale), [locale]);
  const screens = snapshot.package.screens ?? [];
  const [selectedIdentity, setSelectedIdentity] = useState<string>(() => screens[0] ? screenIdentity(screens[0]) : NEW_SCREEN_IDENTITY);
  const isNew = selectedIdentity === NEW_SCREEN_IDENTITY;
  const selected = !isNew ? screens.find(screen => screenIdentity(screen) === selectedIdentity) ?? null : null;
  const [draft, setDraft] = useState<ScreenEngineering>(() => selected ? cloneEngineeringValue(selected) : createScreenDraft(screens, locale));
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<ValidatedCandidate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);

  const invalidateValidation = () => {
    setPreview(null);
    setCandidate(null);
    setError(null);
  };

  useEffect(() => {
    if (selectedIdentity === NEW_SCREEN_IDENTITY) {
      setDraft(createScreenDraft(screens, locale));
      invalidateValidation();
      return;
    }
    const current = screens.find(screen => screenIdentity(screen) === selectedIdentity) ?? null;
    if (current) {
      setDraft(cloneEngineeringValue(current));
      invalidateValidation();
      return;
    }
    if (screens[0]) setSelectedIdentity(screenIdentity(screens[0]));
    else setSelectedIdentity(NEW_SCREEN_IDENTITY);
  }, [selectedIdentity, snapshot.package]);

  const changed = isNew ? true : selected !== null && JSON.stringify(selected) !== JSON.stringify(draft);

  useEffect(() => {
    if (!changed && !applying) return undefined;
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [changed, applying]);

  const chooseScreen = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text.discardConfirm)) return;
    setSelectedIdentity(identity);
    invalidateValidation();
  };

  const updateDraft = (update: (current: ScreenEngineering) => ScreenEngineering) => {
    setDraft(current => update(current));
    invalidateValidation();
  };

  const resetDraft = () => {
    setDraft(selected ? cloneEngineeringValue(selected) : createScreenDraft(screens, locale));
    invalidateValidation();
  };

  const validateDraft = async () => {
    setPreviewing(true);
    setError(null);
    setPreview(null);
    setCandidate(null);
    try {
      const nextPackage = replaceScreenInPackage(snapshot.package, selected, draft);
      const before = await loadEngineeringWorkspace();
      const nextPreview = await previewEngineeringPackage(nextPackage);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion) throw new Error(text.workspaceChanged);
      setPreview(nextPreview);
      setCandidate({ package: cloneEngineeringValue(nextPackage), changeVersion: after.changeVersion });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  const applyDraft = async () => {
    if (!candidate || !preview?.canApply) return;
    if (!window.confirm(text.applyConfirm)) return;
    setApplying(true);
    setError(null);
    try {
      const appliedKey = draft.key;
      await applyEngineeringPackage(candidate.package, candidate.changeVersion);
      await onApplied();
      setSelectedIdentity(`key:${appliedKey}`);
      setPreview(null);
      setCandidate(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreview(null);
      setCandidate(null);
    } finally {
      setApplying(false);
    }
  };

  const issues = preview?.items.flatMap(item => item.issues ?? []) ?? [];
  const objectCount = countVisualElements(draft.elements);

  return <div className="eng-section visual-editor-workspace" data-testid="visual-editor-workspace">
    <header className="visual-editor-header">
      <div><span>{text.eyebrow}</span><h1>{text.title}</h1><p>{text.description}</p></div>
      <div className="visual-editor-authority"><strong>{text.authorityTitle}</strong><span>{text.authorityHint}</span></div>
    </header>

    <div className="visual-editor-shell">
      <aside className="visual-editor-screens" aria-label={text.screenList}>
        <header><strong>{text.screens}</strong><button type="button" className={isNew ? 'active' : ''} onClick={() => chooseScreen(NEW_SCREEN_IDENTITY)}>+ {text.newScreen}</button></header>
        <div className="visual-editor-screen-list">
          {screens.map(screen => {
            const identity = screenIdentity(screen);
            return <button type="button" className={identity === selectedIdentity ? 'selected' : ''} key={identity} onClick={() => chooseScreen(identity)}>
              <strong>{screen.name || screen.key}</strong><code>{screen.key}</code><span>{screen.route || text.noRoute} · {countVisualElements(screen.elements)} {text.objects}</span>
            </button>;
          })}
        </div>
      </aside>

      <section className="visual-editor-main">
        <div className="visual-editor-screen-form">
          <label><span>{text.name}</span><input value={draft.name} onChange={event => updateDraft(current => ({ ...current, name: event.target.value }))} /></label>
          <label><span>{text.key}</span><input className="mono" value={draft.key} onChange={event => updateDraft(current => ({ ...current, key: event.target.value }))} /></label>
          <label><span>{text.route}</span><input className="mono" value={draft.route ?? ''} placeholder="/overview" onChange={event => updateDraft(current => ({ ...current, route: emptyToNull(event.target.value) }))} /></label>
          <div className="visual-editor-draft-state"><span>{text.draft}</span><strong>{isNew ? text.newDraft : changed ? text.changed : text.unchanged}</strong><small>{objectCount} {text.objects}</small></div>
        </div>

        <div className="visual-editor-composition">
          <aside className="visual-editor-slot visual-editor-palette-slot"><strong>{text.objectsPanel}</strong><span>{text.objectsPanelHint}</span></aside>
          <section className="visual-editor-canvas-slot">
            <header><div><strong>{draft.name || draft.key || text.untitled}</strong><code>{draft.route || text.noRoute}</code></div><span>{text.canonicalPreview}</span></header>
            <CanonicalVisualRenderer elements={draft.elements} emptyLabel={text.emptyCanvas} />
          </section>
          <aside className="visual-editor-slot visual-editor-inspector-slot"><strong>{text.propertiesPanel}</strong><span>{text.propertiesPanelHint}</span></aside>
        </div>

        <div className="visual-editor-actions">
          <button type="button" className="secondary" disabled={!changed || previewing || applying} onClick={resetDraft}>{text.reset}</button>
          <button type="button" className="secondary" disabled={!changed || previewing || applying} onClick={() => void validateDraft()} data-testid="visual-editor-preview">{previewing ? text.previewing : text.preview}</button>
          <button type="button" className="primary" disabled={!changed || !preview?.canApply || !candidate || previewing || applying} onClick={() => void applyDraft()} data-testid="visual-editor-apply">{applying ? text.applying : text.apply}</button>
        </div>

        <section className="visual-editor-preview-panel" aria-live="polite">
          <header>
            <div><span>{text.validation}</span><strong className={preview ? (preview.canApply ? 'valid' : 'invalid') : ''}>{error ? text.previewFailed : preview ? (preview.canApply ? text.valid : text.invalid) : text.notValidated}</strong></div>
            {preview && <div><span>{preview.createCount} {text.creates}</span><span>{preview.updateCount} {text.updates}</span><span>{preview.errorCount} {text.errors}</span></div>}
          </header>
          {error && <pre>{error}</pre>}
          {issues.length > 0 && <div className="visual-editor-issues">{issues.map((issue, index) => <div className={issue.isError ? 'error' : 'warning'} key={`${issue.code}-${issue.entityKey}-${index}`}><strong>{issue.code}</strong><span>{issue.message}</span><small>{issue.entityKind}: {issue.entityKey}</small></div>)}</div>}
          <footer>{text.previewFooter}</footer>
        </section>
      </section>
    </div>
  </div>;
}

function emptyToNull(value: string): string | null { return value.trim().length === 0 ? null : value; }

function visualEditorText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Canonical graphical Engineering', title: 'Screen editor foundation', description: 'Screens are edited as canonical Engineering. Canvas state remains transient and is never a second project authority.',
    authorityTitle: 'Preview required before Apply', authorityHint: 'The public Engineering Preview/Apply and Workspace CAS protect Screen changes.', screenList: 'Screen list', screens: 'Screens', newScreen: 'New Screen', noRoute: 'no route', objects: 'objects',
    name: 'Name', key: 'Key', route: 'Route', draft: 'Draft', newDraft: 'New', changed: 'Changed', unchanged: 'Unchanged', untitled: 'Untitled Screen', objectsPanel: 'Objects', objectsPanelHint: 'Wave 08 object palette integration slot. Canonical object creation enters through this composition boundary.',
    propertiesPanel: 'Properties', propertiesPanelHint: 'Wave 08 property inspector integration slot. Registered properties remain authoritative.', canonicalPreview: 'Canonical preview', emptyCanvas: 'This Screen has no canonical visual objects yet.', reset: 'Reset draft', preview: 'Preview change', previewing: 'Previewing...', apply: 'Apply to Workspace', applying: 'Applying...',
    validation: 'Engineering validation', previewFailed: 'Preview failed', valid: 'Valid candidate', invalid: 'Invalid candidate', notValidated: 'Not validated', creates: 'creates', updates: 'updates', errors: 'errors', previewFooter: 'Preview does not mutate Working. Apply uses the validated Workspace version and reloads the canonical snapshot.',
    discardConfirm: 'Discard the current Screen draft?', applyConfirm: 'Apply this validated Screen draft to the official Engineering Workspace?', workspaceChanged: 'The Engineering Workspace changed during validation. Reload the canonical snapshot and validate again.'
  };
  if (locale === 'es') return {
    eyebrow: 'Ingeniería gráfica canónica', title: 'Base del editor de Pantallas', description: 'Las Pantallas se editan como Engineering canónico. El estado del Canvas es transitorio y nunca se convierte en una segunda autoridad del proyecto.',
    authorityTitle: 'Preview obligatorio antes de Aplicar', authorityHint: 'El Preview/Apply público y CAS del Workspace protegen los cambios de Pantalla.', screenList: 'Lista de Pantallas', screens: 'Pantallas', newScreen: 'Nueva Pantalla', noRoute: 'sin ruta', objects: 'objetos',
    name: 'Nombre', key: 'Clave', route: 'Ruta', draft: 'Borrador', newDraft: 'Nuevo', changed: 'Modificado', unchanged: 'Sin cambios', untitled: 'Pantalla sin título', objectsPanel: 'Objetos', objectsPanelHint: 'Punto de integración de la paleta de objetos de Wave 08. La creación canónica entra por este límite.',
    propertiesPanel: 'Propiedades', propertiesPanelHint: 'Punto de integración del inspector de Wave 08. Las propiedades registradas siguen siendo autoridad.', canonicalPreview: 'Preview canónico', emptyCanvas: 'Esta Pantalla todavía no contiene objetos visuales canónicos.', reset: 'Restablecer borrador', preview: 'Preview del cambio', previewing: 'Validando...', apply: 'Aplicar al Workspace', applying: 'Aplicando...',
    validation: 'Validación de Engineering', previewFailed: 'Falló el Preview', valid: 'Candidato válido', invalid: 'Candidato inválido', notValidated: 'No validado', creates: 'creaciones', updates: 'actualizaciones', errors: 'errores', previewFooter: 'Preview no modifica Working. Aplicar usa la versión validada del Workspace y recarga el snapshot canónico.',
    discardConfirm: '¿Descartar el borrador actual de la Pantalla?', applyConfirm: '¿Aplicar este borrador validado al Engineering Workspace oficial?', workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue el snapshot canónico y valide nuevamente.'
  };
  return {
    eyebrow: 'Engenharia gráfica canônica', title: 'Fundação do editor de Telas', description: 'Telas são editadas como Engineering canônico. Estado de Canvas permanece transitório e nunca vira uma segunda autoridade do projeto.',
    authorityTitle: 'Preview obrigatório antes do Apply', authorityHint: 'O Preview/Apply público e o CAS do Workspace protegem as mudanças da Tela.', screenList: 'Lista de Telas', screens: 'Telas', newScreen: 'Nova Tela', noRoute: 'sem rota', objects: 'objetos',
    name: 'Nome', key: 'Chave', route: 'Rota', draft: 'Rascunho', newDraft: 'Novo', changed: 'Alterado', unchanged: 'Sem alterações', untitled: 'Tela sem título', objectsPanel: 'Objetos', objectsPanelHint: 'Ponto de integração da paleta da Wave 08. A criação canônica de objetos entra por esta fronteira.',
    propertiesPanel: 'Propriedades', propertiesPanelHint: 'Ponto de integração do inspetor da Wave 08. As propriedades registradas continuam sendo a autoridade.', canonicalPreview: 'Preview canônico', emptyCanvas: 'Esta Tela ainda não possui objetos visuais canônicos.', reset: 'Restaurar rascunho', preview: 'Preview da alteração', previewing: 'Validando...', apply: 'Aplicar ao Workspace', applying: 'Aplicando...',
    validation: 'Validação de Engineering', previewFailed: 'Falha no Preview', valid: 'Candidato válido', invalid: 'Candidato inválido', notValidated: 'Não validado', creates: 'criações', updates: 'atualizações', errors: 'erros', previewFooter: 'Preview não altera o Working. Apply usa a versão validada do Workspace e recarrega o snapshot canônico.',
    discardConfirm: 'Descartar o rascunho atual da Tela?', applyConfirm: 'Aplicar este rascunho validado ao Engineering Workspace oficial?', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue o snapshot canônico e valide novamente.'
  };
}
