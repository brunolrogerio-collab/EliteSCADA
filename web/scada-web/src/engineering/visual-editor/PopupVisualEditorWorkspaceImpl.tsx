import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from '../api';
import type { EngineeringLocale } from '../i18n';
import {
  buildProjectReferenceCatalog,
  type ClientMemoryDefinitionView
} from '../project-reference/projectReferenceModel';
import type {
  EngineeringPackageView,
  EngineeringSnapshot,
  ImportPreviewView,
  ScreenEngineering
} from '../types';
import { initializeClientMemory } from '../../runtime/clientMemory';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../visual-runtime';
import { BindingEditor } from './binding-editor';
import { VisualEditorCanvas } from './canvas';
import { CanonicalVisualRenderer } from './CanonicalVisualRenderer';
import { DynamoAuthoringCatalogProvider } from './DynamoAuthoringCatalogContext';
import { DynamicPropertyEditor } from './dynamic-property-editor';
import { DynamoLibraryPalette } from './DynamoLibraryPalette';
import { ObjectPalette } from './object-palette';
import {
  NEW_POPUP_IDENTITY,
  createPopupDraft,
  popupFrame,
  popupIdentity,
  popupToVisualScreen,
  replacePopupInPackage,
  visualScreenToPopup,
  type PopupVisualFrame
} from './popupVisualAuthoringModel';
import { popupEditorText } from './popupVisualEditorText';
import { createCanonicalPolygon, updateCanonicalPolygonPoints } from './polygonCanonicalMutations';
import { PropertyInspector } from './property-inspector';
import {
  applyVisualEditorMutationIntent,
  cloneEngineeringValue,
  countVisualElements
} from './visualEditorCanonicalModel';
import type {
  VisualEditorBindingSourceCatalogItem,
  VisualEditorMutationIntent,
  VisualEditorUiIntent,
  VisualEditorViewport
} from './visualEditorContracts';
import {
  applyVisualEditorSelectionIntent,
  normalizeVisualEditorMutationIntent,
  normalizeVisualEditorViewport,
  selectedVisualElements
} from './visualEditorIntegrationModel';
import type { VisualEditorKeyboardCommand } from './visualEditorKeyboardModel';
import {
  applyVisualEditorSessionKeyboardCommand,
  canRedoVisualEditorSession,
  canUndoVisualEditorSession,
  commitVisualEditorSessionDraft,
  createVisualEditorSession,
  currentVisualEditorSessionScreen,
  withVisualEditorSessionSelection,
  type VisualEditorSessionState
} from './visualEditorSessionModel';
import './VisualEditorWorkspace.css';

const DEFAULT_VIEWPORT: VisualEditorViewport = Object.freeze({ zoom: 1, panX: 0, panY: 0 });

type ValidatedPopupCandidate = Readonly<{
  package: EngineeringPackageView;
  changeVersion: number;
}>;

export function PopupVisualEditorWorkspace({
  snapshot,
  locale,
  onApplied
}: {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
}) {
  return <DynamoAuthoringCatalogProvider
    definitions={snapshot.package.dynamos ?? []}
    tags={snapshot.package.tags ?? []}
    visualAssets={snapshot.package.visualAssets ?? []}
  >
    <PopupVisualEditorWorkspaceBody snapshot={snapshot} locale={locale} onApplied={onApplied} />
  </DynamoAuthoringCatalogProvider>;
}

function PopupVisualEditorWorkspaceBody({
  snapshot,
  locale,
  onApplied
}: {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
}) {
  const text = useMemo(() => popupEditorText(locale), [locale]);
  const popups = snapshot.package.popups ?? [];
  const [selectedIdentity, setSelectedIdentity] = useState<string>(() =>
    popups[0] ? popupIdentity(popups[0]) : NEW_POPUP_IDENTITY);
  const isNew = selectedIdentity === NEW_POPUP_IDENTITY;
  const selected = !isNew
    ? popups.find(item => popupIdentity(item) === selectedIdentity) ?? null
    : null;
  const initialPopup = selected ? cloneEngineeringValue(selected) : createPopupDraft(popups, locale);
  const [session, setSessionState] = useState<VisualEditorSessionState>(() =>
    createVisualEditorSession(popupToVisualScreen(initialPopup)));
  const sessionRef = useRef(session);
  const [frame, setFrame] = useState<PopupVisualFrame>(() => popupFrame(initialPopup));
  const draftScreen = currentVisualEditorSessionScreen(session);
  const draftPopup = visualScreenToPopup(draftScreen, frame);
  const selectedObjectIds = session.selectedObjectIds;
  const [viewport, setViewport] = useState<VisualEditorViewport>(DEFAULT_VIEWPORT);
  const [polygonToolActive, setPolygonToolActive] = useState(false);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<ValidatedPopupCandidate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [clientMemoryDefinitions, setClientMemoryDefinitions] = useState<readonly ClientMemoryDefinitionView[]>(Object.freeze([]));

  const replaceSession = (next: VisualEditorSessionState) => {
    sessionRef.current = next;
    setSessionState(next);
  };
  const invalidateValidation = () => {
    setPreview(null);
    setCandidate(null);
    setError(null);
  };

  useEffect(() => {
    let cancelled = false;
    void initializeClientMemory()
      .then(definitions => {
        if (cancelled) return;
        setClientMemoryDefinitions(Object.freeze(definitions.map(definition => Object.freeze({
          id: definition.id,
          name: definition.name,
          path: definition.path,
          dataType: definition.dataType,
          initialValue: definition.initialValue,
          readOnly: definition.readOnly
        }))));
      })
      .catch(() => {
        if (!cancelled) setClientMemoryDefinitions(Object.freeze([]));
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    setPolygonToolActive(false);
    const current = selectedIdentity === NEW_POPUP_IDENTITY
      ? createPopupDraft(popups, locale)
      : popups.find(item => popupIdentity(item) === selectedIdentity) ?? null;
    if (!current) {
      if (popups[0]) setSelectedIdentity(popupIdentity(popups[0]));
      else setSelectedIdentity(NEW_POPUP_IDENTITY);
      return;
    }
    replaceSession(createVisualEditorSession(popupToVisualScreen(cloneEngineeringValue(current))));
    setFrame(popupFrame(current));
    setViewport(DEFAULT_VIEWPORT);
    invalidateValidation();
  }, [selectedIdentity, snapshot.package]);

  const changed = isNew
    ? true
    : selected !== null && JSON.stringify(selected) !== JSON.stringify(draftPopup);
  const selectedElements = useMemo(
    () => selectedVisualElements(draftScreen, selectedObjectIds),
    [draftScreen, selectedObjectIds]
  );
  const selectedElement = selectedElements.length === 1 ? selectedElements[0] : null;
  const projectReferences = useMemo(
    () => buildProjectReferenceCatalog(snapshot.package, clientMemoryDefinitions),
    [snapshot.package, clientMemoryDefinitions]
  );
  const bindingSourceCatalog = useMemo<readonly VisualEditorBindingSourceCatalogItem[]>(() => Object.freeze(
    projectReferences
      .filter(reference => reference.bindingKind === 'Tag' || reference.bindingKind === 'ClientMemory')
      .map(reference => Object.freeze({
        kind: reference.bindingKind!,
        target: reference.reference,
        label: reference.label,
        dataType: reference.dataType,
        engineeringUnit: reference.engineeringUnit ?? null,
        writable: reference.writable,
        family: reference.family,
        tagReference: reference.tagReference ?? null,
        selectorCapability: reference.selectorCapability ?? null,
        bindable: true
      }))
  ), [projectReferences]);

  useEffect(() => {
    if (!changed && !applying) return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [changed, applying]);

  const choosePopup = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text.discardConfirm)) return;
    setSelectedIdentity(identity);
  };

  const updateDraftScreen = (update: (current: ScreenEngineering) => ScreenEngineering) => {
    const current = sessionRef.current;
    replaceSession(commitVisualEditorSessionDraft(current, update(current.history.present)));
    invalidateValidation();
  };

  const updateTemplateKey = (raw: string) => {
    setFrame(current => Object.freeze({ ...current, templateKey: raw.trim() || null }));
    invalidateValidation();
  };

  const handleUiIntent = (intent: VisualEditorUiIntent) => {
    if (intent.kind === 'selection.change') {
      const current = sessionRef.current;
      replaceSession(withVisualEditorSessionSelection(
        current,
        applyVisualEditorSelectionIntent(current.selectedObjectIds, intent)
      ));
      return;
    }
    setViewport(normalizeVisualEditorViewport(intent.viewport));
  };

  const handleMutationIntent = (intent: VisualEditorMutationIntent) => {
    try {
      const current = sessionRef.current;
      const currentDraft = current.history.present;
      if (intent.kind === 'polygon.create') {
        const created = createCanonicalPolygon(currentDraft, intent.points);
        replaceSession(commitVisualEditorSessionDraft(current, created.screen, {
          selectedObjectIds: [created.objectId]
        }));
        setPolygonToolActive(false);
        invalidateValidation();
        return;
      }
      if (intent.kind === 'polygon.points.set') {
        replaceSession(commitVisualEditorSessionDraft(
          current,
          updateCanonicalPolygonPoints(currentDraft, intent.objectId, intent.points)
        ));
        invalidateValidation();
        return;
      }
      const normalized = normalizeVisualEditorMutationIntent(intent);
      replaceSession(commitVisualEditorSessionDraft(
        current,
        applyVisualEditorMutationIntent(currentDraft, normalized)
      ));
      invalidateValidation();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreview(null);
      setCandidate(null);
    }
  };

  const handleKeyboardCommand = (command: VisualEditorKeyboardCommand) => {
    try {
      const current = sessionRef.current;
      const next = applyVisualEditorSessionKeyboardCommand(current, command);
      replaceSession(next);
      if (next.history !== current.history) invalidateValidation();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreview(null);
      setCandidate(null);
    }
  };

  const handlePaletteIntent = (intent: VisualEditorMutationIntent) => {
    if (intent.kind === 'object.add' && intent.objectType === BUILTIN_VISUAL_OBJECT_TYPES.polygon) {
      setPolygonToolActive(true);
      replaceSession(withVisualEditorSessionSelection(sessionRef.current, Object.freeze([])));
      setError(null);
      return;
    }
    setPolygonToolActive(false);
    handleMutationIntent(intent);
  };

  const resetDraft = () => {
    const source = selected ? cloneEngineeringValue(selected) : createPopupDraft(popups, locale);
    replaceSession(createVisualEditorSession(popupToVisualScreen(source)));
    setFrame(popupFrame(source));
    setViewport(DEFAULT_VIEWPORT);
    setPolygonToolActive(false);
    invalidateValidation();
  };

  const validateDraft = async () => {
    setPreviewing(true);
    setError(null);
    setPreview(null);
    setCandidate(null);
    try {
      const nextPackage = replacePopupInPackage(snapshot.package, selected, draftPopup);
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
      const appliedKey = draftPopup.key;
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

  return <div className="eng-section visual-editor-workspace" data-testid="popup-visual-editor-workspace">
    <header className="visual-editor-header">
      <div><span>{text.eyebrow}</span><h1>{text.title}</h1><p>{text.description}</p></div>
      <div className="visual-editor-authority"><strong>{text.authorityTitle}</strong><span>{text.authorityHint}</span></div>
    </header>

    <div className="visual-editor-shell">
      <aside className="visual-editor-screens" aria-label={text.popupList}>
        <header><strong>{text.popups}</strong><button type="button" className={isNew ? 'active' : ''} onClick={() => choosePopup(NEW_POPUP_IDENTITY)}>+ {text.newPopup}</button></header>
        <div className="visual-editor-screen-list">
          {popups.map(popup => {
            const identity = popupIdentity(popup);
            return <button type="button" className={identity === selectedIdentity ? 'selected' : ''} key={identity} onClick={() => choosePopup(identity)}>
              <strong>{popup.name || popup.key}</strong>
              <code>{popup.key}</code>
              <span>{popup.templateKey?.trim() ? `${text.template}: ${popup.templateKey}` : text.standalone} · {countVisualElements(popup.elements)} {text.objects}</span>
            </button>;
          })}
        </div>
      </aside>

      <section className="visual-editor-main">
        <div className="visual-editor-screen-form">
          <label><span>{text.name}</span><input value={draftScreen.name} onChange={event => updateDraftScreen(current => ({ ...current, name: event.target.value }))} /></label>
          <label><span>{text.key}</span><input className="mono" value={draftScreen.key} onChange={event => updateDraftScreen(current => ({ ...current, key: event.target.value }))} /></label>
          <label><span>{text.template}</span><input className="mono" value={frame.templateKey ?? ''} placeholder={text.standalone} onChange={event => updateTemplateKey(event.target.value)} /></label>
          <div className="visual-editor-draft-state"><span>{text.draft}</span><strong>{isNew ? text.newDraft : changed ? text.changed : text.unchanged}</strong><small>{countVisualElements(draftScreen.elements)} {text.objects}</small></div>
        </div>

        <div className="visual-editor-composition">
          <aside className="visual-editor-slot visual-editor-palette-slot">
            <ObjectPalette onMutationIntent={handlePaletteIntent} />
            <DynamoLibraryPalette
              definitions={snapshot.package.dynamos ?? []}
              locale={locale}
              onMutationIntent={handlePaletteIntent}
            />
            <section className="visual-editor-asset-library">
              <strong>{text.assets}</strong>
              <span>{text.assetHint}</span>
              <small>{snapshot.package.visualAssets?.length ?? 0} {text.assetsAvailable}</small>
            </section>
          </aside>

          <section className="visual-editor-canvas-slot">
            <header><div><strong>{draftScreen.name || draftScreen.key}</strong><code>{frame.templateKey?.trim() ? `${text.template}: ${frame.templateKey}` : text.standalone}</code></div><span>{polygonToolActive ? text.polygonDrawing : text.interactiveCanvas}</span></header>
            <VisualEditorCanvas
              screen={draftScreen}
              selectedObjectIds={selectedObjectIds}
              viewport={viewport}
              onUiIntent={handleUiIntent}
              onMutationIntent={handleMutationIntent}
              onKeyboardCommand={handleKeyboardCommand}
              canUndo={canUndoVisualEditorSession(session)}
              canRedo={canRedoVisualEditorSession(session)}
              polygonToolActive={polygonToolActive}
              onPolygonToolCancel={() => setPolygonToolActive(false)}
            />
            <div className="visual-editor-canonical-preview-label"><strong>{text.canonicalPreview}</strong><span>{text.canonicalPreviewHint}</span></div>
            <CanonicalVisualRenderer
              elements={draftScreen.elements}
              emptyLabel={text.emptyCanvas}
              locale={locale}
              dynamoDefinitions={snapshot.package.dynamos}
            />
          </section>

          <aside className="visual-editor-slot visual-editor-inspector-slot">
            <PropertyInspector
              selectedElements={selectedElements}
              visualAssets={snapshot.package.visualAssets ?? []}
              onMutationIntent={handleMutationIntent}
            />
            {selectedElement?.id ? <DynamicPropertyEditor
              element={selectedElement}
              sourceCatalog={bindingSourceCatalog}
              onBindingIntent={handleMutationIntent}
              onSetExpression={configuration => handleMutationIntent({ kind: 'propertyExpression.set', objectId: selectedElement.id!, configuration })}
              onRemoveExpression={propertyKey => handleMutationIntent({ kind: 'propertyExpression.remove', objectId: selectedElement.id!, propertyKey })}
              onSetBooleanCondition={configuration => handleMutationIntent({ kind: 'booleanCondition.set', objectId: selectedElement.id!, configuration })}
              onRemoveBooleanCondition={propertyKey => handleMutationIntent({ kind: 'booleanCondition.remove', objectId: selectedElement.id!, propertyKey })}
              onSetAnalogFill={configuration => handleMutationIntent({ kind: 'analogFill.set', objectId: selectedElement.id!, configuration })}
              onRemoveAnalogFill={() => handleMutationIntent({ kind: 'analogFill.remove', objectId: selectedElement.id! })}
            /> : null}
            {selectedElement ? <BindingEditor
              element={selectedElement}
              sourceCatalog={bindingSourceCatalog}
              onMutationIntent={handleMutationIntent}
              locale={locale}
            /> : <p className="visual-editor-selection-hint">{text.selectObject}</p>}
          </aside>
        </div>

        <div className="visual-editor-actions">
          <button type="button" className="secondary" disabled={!changed || previewing || applying} onClick={resetDraft}>{text.reset}</button>
          <button type="button" className="secondary" disabled={!changed || previewing || applying} onClick={() => void validateDraft()} data-testid="popup-visual-editor-preview">{previewing ? text.previewing : text.preview}</button>
          <button type="button" className="primary" disabled={!changed || !preview?.canApply || !candidate || previewing || applying} onClick={() => void applyDraft()} data-testid="popup-visual-editor-apply">{applying ? text.applying : text.apply}</button>
        </div>

        <section className="visual-editor-preview-panel" aria-live="polite">
          <header>
            <div><span>{text.validation}</span><strong className={preview ? (preview.canApply ? 'valid' : 'invalid') : ''}>{error ? text.previewFailed : preview ? (preview.canApply ? text.valid : text.invalid) : text.notValidated}</strong></div>
            {preview ? <div><span>{preview.createCount} {text.creates}</span><span>{preview.updateCount} {text.updates}</span><span>{preview.errorCount} {text.errors}</span></div> : null}
          </header>
          {error ? <pre>{error}</pre> : null}
          {issues.length > 0 ? <div className="visual-editor-issues">{issues.map((issue, index) => <div className={issue.isError ? 'error' : 'warning'} key={`${issue.code}-${issue.entityKey}-${index}`}><strong>{issue.code}</strong><span>{issue.message}</span><small>{issue.entityKind}: {issue.entityKey}</small></div>)}</div> : null}
          <footer>{text.previewFooter}</footer>
        </section>
      </section>
    </div>
  </div>;
}
