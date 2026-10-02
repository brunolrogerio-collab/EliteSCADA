import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  applyEngineeringPackage,
  importVisualAsset,
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
import { resolveRuntimeLogicalSize } from '../../runtime/visual-navigation/runtimeLogicalCanvas';
import {
  resolvePopupLogicalBounds,
  resolvePopupLogicalPosition
} from '../../runtime/visual-navigation/runtimePopupPosition';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../visual-runtime';
import { VisualEditorCanvas } from './canvas';
import { DynamoAuthoringCatalogProvider } from './DynamoAuthoringCatalogContext';
import { VisualEditorAuthoringSidebar, type VisualEditorAuthoringTab } from './VisualEditorAuthoringSidebar';
import { VisualEditorSelectionInspector, type VisualEditorInspectorTab } from './VisualEditorSelectionInspector';
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
import { VisualEditorRegionToggle } from './VisualEditorRegionToggle';
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
  canPasteVisualEditorSession,
  canRedoVisualEditorSession,
  canUndoVisualEditorSession,
  commitVisualEditorSessionDraft,
  createVisualEditorSession,
  currentVisualEditorSessionScreen,
  withVisualEditorSessionSelection,
  type VisualEditorSessionState
} from './visualEditorSessionModel';
import './VisualEditorWorkspace.css';
import './PopupVisualEditorWorkspace.css';

const DEFAULT_VIEWPORT: VisualEditorViewport = Object.freeze({ zoom: 1, panX: 0, panY: 0 });
const RUNTIME_DESIGN_SIZE = resolveRuntimeLogicalSize();

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
  const popupBounds = resolvePopupLogicalBounds(draftPopup);
  const popupPosition = resolvePopupLogicalPosition(draftPopup, RUNTIME_DESIGN_SIZE, popupBounds);
  const selectedObjectIds = session.selectedObjectIds;
  const [viewport, setViewport] = useState<VisualEditorViewport>(DEFAULT_VIEWPORT);
  const [polygonToolActive, setPolygonToolActive] = useState(false);
  const [screensCollapsed, setScreensCollapsed] = useState(false);
  const [paletteCollapsed, setPaletteCollapsed] = useState(false);
  const [propertiesCollapsed, setPropertiesCollapsed] = useState(false);
  const [authoringTab, setAuthoringTab] = useState<VisualEditorAuthoringTab>('structure');
  const [inspectorTab, setInspectorTab] = useState<VisualEditorInspectorTab>('properties');
  const workspaceRef = useRef<HTMLDivElement | null>(null);
  const selectedIdentityRef = useRef(selectedIdentity);
  const preserveDraftAfterAssetImportRef = useRef(false);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<ValidatedPopupCandidate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [importingAsset, setImportingAsset] = useState(false);
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
    const sameSelection = selectedIdentityRef.current === selectedIdentity;
    selectedIdentityRef.current = selectedIdentity;
    if (preserveDraftAfterAssetImportRef.current) {
      preserveDraftAfterAssetImportRef.current = false;
      if (sameSelection) return;
    }
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
    if (!changed && !applying && !importingAsset) return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [changed, applying, importingAsset]);

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

  const showInspector = (tab: VisualEditorInspectorTab, focusRename = false) => {
    setPropertiesCollapsed(false);
    setInspectorTab(tab);
    if (focusRename) {
      requestAnimationFrame(() => requestAnimationFrame(() => {
        workspaceRef.current?.querySelector<HTMLInputElement>('[data-testid="visual-property-identity-key"]')?.focus();
      }));
    }
  };

  const showStructure = () => {
    setPaletteCollapsed(false);
    setAuthoringTab('structure');
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

  const importAsset = async (file: File): Promise<string | null> => {
    setImportingAsset(true);
    setError(null);
    try {
      const currentWorkspace = await loadEngineeringWorkspace();
      if (currentWorkspace.changeVersion !== snapshot.workspace.changeVersion) throw new Error(text.workspaceChanged);
      const imported = await importVisualAsset(file, currentWorkspace.changeVersion, { fileName: file.name });
      preserveDraftAfterAssetImportRef.current = true;
      await onApplied();
      return imported.asset.id ?? null;
    } catch (reason) {
      preserveDraftAfterAssetImportRef.current = false;
      setError(reason instanceof Error ? reason.message : String(reason));
      return null;
    } finally {
      setImportingAsset(false);
    }
  };

  const issues = preview?.items.flatMap(item => item.issues ?? []) ?? [];

  const layoutClassName = [
    'eng-section visual-editor-workspace',
    screensCollapsed ? 'visual-editor-workspace--screens-collapsed' : '',
    paletteCollapsed ? 'visual-editor-workspace--palette-collapsed' : '',
    propertiesCollapsed ? 'visual-editor-workspace--properties-collapsed' : ''
  ].filter(Boolean).join(' ');

  return <div ref={workspaceRef} className={layoutClassName} data-testid="popup-visual-editor-workspace">
    <header className="visual-editor-header">
      <div className="visual-editor-header-title"><h1>{text.title}</h1><details className="visual-editor-help"><summary>{text.help}</summary><p>{text.description}</p><div className="visual-editor-authority"><strong>{text.authorityTitle}</strong><span>{text.authorityHint}</span></div></details></div>
    </header>

    <div className="visual-editor-shell">
      <aside className="visual-editor-screens" aria-label={text.popupList}>
        <header>
          <strong>{text.popups}</strong>
          <div className="visual-editor-region-actions">
            <button type="button" className={isNew ? 'active' : ''} onClick={() => choosePopup(NEW_POPUP_IDENTITY)}>+ {text.newPopup}</button>
            <VisualEditorRegionToggle region="screens" collapsed={screensCollapsed} locale={locale} onToggle={() => setScreensCollapsed(value => !value)} />
          </div>
        </header>
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
          <label><span>{text.name}</span><input aria-description={text.nameHint} value={draftScreen.name} onChange={event => updateDraftScreen(current => ({ ...current, name: event.target.value }))} /><small aria-hidden="true">{text.nameHint}</small></label>
          <label><span>{text.key}</span><input aria-description={text.keyHint} className="mono" value={draftScreen.key} onChange={event => updateDraftScreen(current => ({ ...current, key: event.target.value }))} /><small aria-hidden="true">{text.keyHint}</small></label>
          <label><span>{text.template}</span><input className="mono" value={frame.templateKey ?? ''} placeholder={text.standalone} onChange={event => updateTemplateKey(event.target.value)} /></label>
          <div className="visual-editor-draft-state"><span>{text.draft}</span><strong>{isNew ? text.newDraft : changed ? text.changed : text.unchanged}</strong><small>{countVisualElements(draftScreen.elements)} {text.objects}</small></div>
        </div>

        <div className="visual-editor-composition">
          <aside className="visual-editor-slot visual-editor-palette-slot">
            <VisualEditorRegionToggle region="palette" collapsed={paletteCollapsed} locale={locale} onToggle={() => setPaletteCollapsed(value => !value)} />
            <VisualEditorAuthoringSidebar
              screen={draftScreen}
              selectedObjectIds={selectedObjectIds}
              definitions={snapshot.package.dynamos ?? []}
              equipment={snapshot.package.equipment ?? []}
              templates={snapshot.package.templates ?? []}
              visualAssets={snapshot.package.visualAssets ?? []}
              locale={locale}
              activeTab={authoringTab}
              onActiveTabChange={setAuthoringTab}
              onUiIntent={handleUiIntent}
              onMutationIntent={handlePaletteIntent}
              onCommand={handleKeyboardCommand}
              assetImport={{
                busy: importingAsset,
                disabled: applying || previewing,
                onFile: importAsset
              }}
            />
          </aside>

          <section className="visual-editor-canvas-slot" aria-describedby={`popup-authoring-bounds-${draftScreen.id}`}>
            <span id={`popup-authoring-bounds-${draftScreen.id}`} className="visual-editor-canvas-meta-sr-only" data-testid="popup-authoring-bounds">
              {`${text.logicalBounds} ${popupBounds.width} × ${popupBounds.height} · ${text.runtimePosition} X ${popupPosition.x}, Y ${popupPosition.y}`}
            </span>
            <VisualEditorCanvas
              screen={draftScreen}
              selectedObjectIds={selectedObjectIds}
              viewport={viewport}
              onUiIntent={handleUiIntent}
              onMutationIntent={handleMutationIntent}
              onKeyboardCommand={handleKeyboardCommand}
              canUndo={canUndoVisualEditorSession(session)}
              canRedo={canRedoVisualEditorSession(session)}
              canPaste={canPasteVisualEditorSession(session)}
              onInsertObject={objectType => handlePaletteIntent({ kind: 'object.add', objectType })}
              onInspectorTabRequest={showInspector}
              onStructureRequest={showStructure}
              locale={locale}
              dynamoDefinitions={snapshot.package.dynamos}
              emptyLabel={text.emptyCanvas}
              logicalBoundary={{
                width: popupBounds.width,
                height: popupBounds.height,
                label: `${text.logicalBounds}: ${popupBounds.width} × ${popupBounds.height}`
              }}
              polygonToolActive={polygonToolActive}
              onPolygonToolCancel={() => setPolygonToolActive(false)}
            />
          </section>

          <aside className="visual-editor-slot visual-editor-inspector-slot">
            <VisualEditorRegionToggle region="properties" collapsed={propertiesCollapsed} locale={locale} onToggle={() => setPropertiesCollapsed(value => !value)} />
            <VisualEditorSelectionInspector
              screen={draftScreen}
              selectedElements={selectedElements}
              selectedObjectIds={selectedObjectIds}
              sourceCatalog={bindingSourceCatalog}
              visualAssets={snapshot.package.visualAssets ?? []}
              locale={locale}
              activeTab={inspectorTab}
              onActiveTabChange={setInspectorTab}
              onMutationIntent={handleMutationIntent}
              onCommand={handleKeyboardCommand}
              onImportImage={importAsset}
              imageImportDisabled={applying || previewing}
              imageImportBusy={importingAsset}
            />
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
