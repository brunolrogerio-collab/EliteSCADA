import React, { useEffect, useMemo, useState } from 'react';
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
  ScreenEngineering,
  VisualElementEngineering
} from '../types';
import { initializeClientMemory } from '../../runtime/clientMemory';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../visual-runtime';
import { BindingEditor } from './binding-editor';
import { VisualEditorCanvas } from './canvas';
import { CanonicalVisualRenderer } from './CanonicalVisualRenderer';
import { DynamicPropertyEditor } from './dynamic-property-editor';
import { ObjectPalette } from './object-palette';
import { createCanonicalPolygon, updateCanonicalPolygonPoints } from './polygonCanonicalMutations';
import { PropertyInspector } from './property-inspector';
import {
  NEW_SCREEN_IDENTITY,
  applyVisualEditorMutationIntent,
  cloneEngineeringValue,
  countVisualElements,
  createScreenDraft,
  replaceScreenInPackage,
  screenIdentity
} from './visualEditorCanonicalModel';
import type {
  VisualEditorBindingSourceCatalogItem,
  VisualEditorMutationIntent,
  VisualEditorUiIntent,
  VisualEditorViewport
} from './visualEditorContracts';
import {
  applyVisualEditorSelectionIntent,
  existingVisualObjectIds,
  normalizeVisualEditorMutationIntent,
  normalizeVisualEditorViewport,
  selectedVisualElements
} from './visualEditorIntegrationModel';
import './VisualEditorWorkspace.css';

type VisualEditorWorkspaceProps = {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
};

type ValidatedCandidate = { package: EngineeringPackageView; changeVersion: number };

const DEFAULT_VIEWPORT: VisualEditorViewport = Object.freeze({ zoom: 1, panX: 0, panY: 0 });

export function VisualEditorWorkspace({ snapshot, locale, onApplied }: VisualEditorWorkspaceProps) {
  const text = useMemo(() => visualEditorText(locale), [locale]);
  const screens = snapshot.package.screens ?? [];
  const [selectedIdentity, setSelectedIdentity] = useState<string>(() => screens[0] ? screenIdentity(screens[0]) : NEW_SCREEN_IDENTITY);
  const isNew = selectedIdentity === NEW_SCREEN_IDENTITY;
  const selected = !isNew ? screens.find(screen => matchesScreenIdentity(screen, selectedIdentity)) ?? null : null;
  const [draft, setDraft] = useState<ScreenEngineering>(() => selected ? cloneEngineeringValue(selected) : createScreenDraft(screens, locale));
  const [selectedObjectIds, setSelectedObjectIds] = useState<readonly string[]>(Object.freeze([]));
  const [viewport, setViewport] = useState<VisualEditorViewport>(DEFAULT_VIEWPORT);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<ValidatedCandidate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [importingAsset, setImportingAsset] = useState(false);
  const [polygonToolActive, setPolygonToolActive] = useState(false);
  const [clientMemoryDefinitions, setClientMemoryDefinitions] = useState<readonly ClientMemoryDefinitionView[]>(Object.freeze([]));

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
    if (selectedIdentity === NEW_SCREEN_IDENTITY) {
      setDraft(createScreenDraft(screens, locale));
      setSelectedObjectIds(Object.freeze([]));
      setViewport(DEFAULT_VIEWPORT);
      invalidateValidation();
      return;
    }
    const current = screens.find(screen => matchesScreenIdentity(screen, selectedIdentity)) ?? null;
    if (current) {
      setDraft(cloneEngineeringValue(current));
      setSelectedObjectIds(Object.freeze([]));
      setViewport(DEFAULT_VIEWPORT);
      invalidateValidation();
      return;
    }
    if (screens[0]) setSelectedIdentity(screenIdentity(screens[0]));
    else setSelectedIdentity(NEW_SCREEN_IDENTITY);
  }, [selectedIdentity, snapshot.package]);

  const changed = isNew ? true : selected !== null && JSON.stringify(selected) !== JSON.stringify(draft);
  const selectedElements = useMemo(
    () => selectedVisualElements(draft, selectedObjectIds),
    [draft, selectedObjectIds]
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
  const visualAssets = snapshot.package.visualAssets ?? [];

  useEffect(() => {
    const existingIds = existingVisualObjectIds(draft);
    setSelectedObjectIds(current => {
      const next = current.filter(objectId => existingIds.has(objectId));
      return next.length === current.length ? current : Object.freeze(next);
    });
  }, [draft]);

  useEffect(() => {
    if (!changed && !applying && !importingAsset) return undefined;
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [changed, applying, importingAsset]);

  const chooseScreen = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text.discardConfirm)) return;
    setSelectedIdentity(identity);
    setSelectedObjectIds(Object.freeze([]));
    setViewport(DEFAULT_VIEWPORT);
    setPolygonToolActive(false);
    invalidateValidation();
  };

  const updateDraft = (update: (current: ScreenEngineering) => ScreenEngineering) => {
    setDraft(current => update(current));
    invalidateValidation();
  };

  const resetDraft = () => {
    setDraft(selected ? cloneEngineeringValue(selected) : createScreenDraft(screens, locale));
    setSelectedObjectIds(Object.freeze([]));
    setViewport(DEFAULT_VIEWPORT);
    setPolygonToolActive(false);
    invalidateValidation();
  };

  const handleUiIntent = (intent: VisualEditorUiIntent) => {
    if (intent.kind === 'selection.change') {
      setSelectedObjectIds(current => applyVisualEditorSelectionIntent(current, intent));
      return;
    }
    setViewport(normalizeVisualEditorViewport(intent.viewport));
  };

  const handleMutationIntent = (intent: VisualEditorMutationIntent) => {
    try {
      if (intent.kind === 'polygon.create') {
        const created = createCanonicalPolygon(draft, intent.points);
        setDraft(created.screen);
        setSelectedObjectIds(Object.freeze([created.objectId]));
        setPolygonToolActive(false);
        invalidateValidation();
        return;
      }
      if (intent.kind === 'polygon.points.set') {
        setDraft(current => updateCanonicalPolygonPoints(current, intent.objectId, intent.points));
        invalidateValidation();
        return;
      }

      const normalizedIntent = normalizeVisualEditorMutationIntent(intent);
      if (normalizedIntent.kind === 'object.delete') {
        const nextDraft = applyVisualEditorMutationIntent(draft, normalizedIntent);
        setDraft(nextDraft);
        const remaining = existingVisualObjectIds(nextDraft);
        setSelectedObjectIds(current => Object.freeze(current.filter(objectId => remaining.has(objectId))));
      } else {
        // Dynamic authoring may emit remove-old-source + set-new-source synchronously.
        // Functional updates ensure those canonical mutations compose instead of
        // each callback applying against a stale render-time draft snapshot.
        setDraft(current => applyVisualEditorMutationIntent(current, normalizedIntent));
      }
      invalidateValidation();
    } catch (reason) {
      setPreview(null);
      setCandidate(null);
      setError(reason instanceof Error ? reason.message : String(reason));
    }
  };

  const handlePaletteIntent = (intent: VisualEditorMutationIntent) => {
    if (intent.kind === 'object.add' && intent.objectType === BUILTIN_VISUAL_OBJECT_TYPES.polygon) {
      setPolygonToolActive(true);
      setSelectedObjectIds(Object.freeze([]));
      setError(null);
      return;
    }
    setPolygonToolActive(false);
    handleMutationIntent(intent);
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
      setSelectedObjectIds(Object.freeze([]));
      setPolygonToolActive(false);
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

  const importAsset = async (file: File) => {
    if (changed) {
      setError(text.assetImportRequiresCleanDraft);
      return;
    }
    setImportingAsset(true);
    setError(null);
    try {
      await importVisualAsset(file, snapshot.workspace.changeVersion, { fileName: file.name });
      await onApplied();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setImportingAsset(false);
    }
  };

  const issues = preview?.items.flatMap(item => item.issues ?? []) ?? [];
  const objectCount = countVisualElements(draft.elements);
  const selectedAssetId = selectedElement ? readAssetId(selectedElement) : '';
  const canChooseImageAsset = selectedElement?.type === BUILTIN_VISUAL_OBJECT_TYPES.image && Boolean(selectedElement.id);

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
            return <button type="button" className={matchesScreenIdentity(screen, selectedIdentity) ? 'selected' : ''} key={identity} onClick={() => chooseScreen(identity)}>
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
          <aside className="visual-editor-slot visual-editor-palette-slot">
            <ObjectPalette
              onMutationIntent={handlePaletteIntent}
              copy={{ title: text.objectsPanel, hint: text.objectsPanelHint, addLabel: text.addObject, labels: text.objectLabels }}
            />
            <section className="visual-editor-asset-library">
              <strong>{text.assets}</strong>
              <span>{text.assetHint}</span>
              <label className="visual-editor-file-import">
                <span>{importingAsset ? text.importingAsset : text.importAsset}</span>
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/bmp"
                  disabled={changed || importingAsset || applying || previewing}
                  onChange={event => {
                    const file = event.currentTarget.files?.[0];
                    event.currentTarget.value = '';
                    if (file) void importAsset(file);
                  }}
                />
              </label>
              {changed ? <small>{text.assetImportRequiresCleanDraft}</small> : null}
            </section>
          </aside>

          <section className="visual-editor-canvas-slot">
            <header><div><strong>{draft.name || draft.key || text.untitled}</strong><code>{draft.route || text.noRoute}</code></div><span>{polygonToolActive ? text.polygonDrawing : text.interactiveCanvas}</span></header>
            <VisualEditorCanvas
              screen={draft}
              selectedObjectIds={selectedObjectIds}
              viewport={viewport}
              onUiIntent={handleUiIntent}
              onMutationIntent={handleMutationIntent}
              polygonToolActive={polygonToolActive}
              onPolygonToolCancel={() => setPolygonToolActive(false)}
            />
            <div className="visual-editor-canonical-preview-label"><strong>{text.canonicalPreview}</strong><span>{text.canonicalPreviewHint}</span></div>
            <CanonicalVisualRenderer elements={draft.elements} emptyLabel={text.emptyCanvas} locale={locale} />
          </section>

          <aside className="visual-editor-slot visual-editor-inspector-slot">
            <PropertyInspector selectedElements={selectedElements} onMutationIntent={handleMutationIntent} />

            {canChooseImageAsset && selectedElement?.id ? (
              <section className="visual-editor-image-asset-picker" data-testid="visual-editor-image-asset-picker">
                <strong>{text.imageAsset}</strong>
                <label>
                  <span>{text.asset}</span>
                  <select
                    value={selectedAssetId}
                    onChange={event => handleMutationIntent({
                      kind: 'property.set',
                      objectIds: [selectedElement.id!],
                      propertyKey: VISUAL_PROPERTY_KEYS.assetRef,
                      value: event.currentTarget.value ? { assetId: event.currentTarget.value } : null
                    })}
                  >
                    <option value="">{text.noAsset}</option>
                    {visualAssets.filter(asset => asset.id).map(asset => (
                      <option key={asset.id!} value={asset.id!}>{asset.name || asset.key} · {asset.originalFileName}</option>
                    ))}
                  </select>
                </label>
              </section>
            ) : null}

            {selectedElement?.id ? (
              <DynamicPropertyEditor
                element={selectedElement}
                sourceCatalog={bindingSourceCatalog}
                onBindingIntent={handleMutationIntent}
                onSetExpression={configuration => handleMutationIntent({
                  kind: 'propertyExpression.set', objectId: selectedElement.id!, configuration
                })}
                onRemoveExpression={propertyKey => handleMutationIntent({
                  kind: 'propertyExpression.remove', objectId: selectedElement.id!, propertyKey
                })}
                onSetBooleanCondition={configuration => handleMutationIntent({
                  kind: 'booleanCondition.set', objectId: selectedElement.id!, configuration
                })}
                onRemoveBooleanCondition={propertyKey => handleMutationIntent({
                  kind: 'booleanCondition.remove', objectId: selectedElement.id!, propertyKey
                })}
                onSetAnalogFill={configuration => handleMutationIntent({
                  kind: 'analogFill.set', objectId: selectedElement.id!, configuration
                })}
                onRemoveAnalogFill={() => handleMutationIntent({
                  kind: 'analogFill.remove', objectId: selectedElement.id!
                })}
              />
            ) : null}

            {selectedElement ? (
              <BindingEditor
                element={selectedElement}
                sourceCatalog={bindingSourceCatalog}
                onMutationIntent={handleMutationIntent}
                locale={locale}
                copy={{
                  title: text.binding,
                  destination: text.bindingDestination,
                  source: text.bindingSource,
                  apply: text.applyBinding,
                  remove: text.removeBinding,
                  noDestinations: text.noBindingDestinations,
                  noSources: text.noBindingSources,
                  current: text.currentBinding,
                  browse: text.browseReferences,
                  exactReference: text.exactReference,
                  exactReferencePlaceholder: text.exactReferencePlaceholder,
                  exactNotFound: text.exactNotFound
                }}
              />
            ) : <p className="visual-editor-selection-hint">{text.selectBindingObject}</p>}
          </aside>
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

function readAssetId(element: VisualElementEngineering): string {
  const value = element.properties?.[VISUAL_PROPERTY_KEYS.assetRef];
  return value !== null && typeof value === 'object' && !Array.isArray(value) && 'assetId' in value ? String(value.assetId ?? '') : '';
}

function matchesScreenIdentity(screen: ScreenEngineering, identity: string): boolean {
  return screenIdentity(screen) === identity || `key:${screen.key}` === identity;
}
function emptyToNull(value: string): string | null { return value.trim().length === 0 ? null : value; }

function visualEditorText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Canonical graphical Engineering', title: 'Screen editor foundation', description: 'Screens are edited as canonical Engineering. Canvas state remains transient and is never a second project authority.',
    authorityTitle: 'Preview required before Apply', authorityHint: 'The public Engineering Preview/Apply and Workspace CAS protect Screen changes.', screenList: 'Screen list', screens: 'Screens', newScreen: 'New Screen', noRoute: 'no route', objects: 'objects',
    name: 'Name', key: 'Key', route: 'Route', draft: 'Draft', newDraft: 'New', changed: 'Changed', unchanged: 'Unchanged', untitled: 'Untitled Screen', objectsPanel: 'Objects', objectsPanelHint: 'Add registered visual objects. Identity and defaults remain canonical authority.', addObject: 'Add', objectLabels: { group: 'Group', rectangle: 'Rectangle', ellipse: 'Ellipse', line: 'Line', polygon: 'Polygon', text: 'Text', image: 'Image', valueDisplay: 'Value display', button: 'Button' },
    propertiesPanel: 'Properties', canonicalPreview: 'Canonical rendered preview', canonicalPreviewHint: 'Renderer projection of the same Engineering draft, including live scalar text bindings.', interactiveCanvas: 'Interactive Canvas', polygonDrawing: 'Polygon drawing mode: click vertices, Enter to finish, Escape to cancel.', emptyCanvas: 'This Screen has no canonical visual objects yet.', reset: 'Reset draft', preview: 'Preview change', previewing: 'Previewing...', apply: 'Apply to Workspace', applying: 'Applying...',
    assets: 'Image assets', assetHint: 'Import project-owned raster assets while the Screen draft is clean, then assign them by stable asset ID.', importAsset: 'Import image', importingAsset: 'Importing image...', assetImportRequiresCleanDraft: 'Apply or reset Screen changes before importing an image asset.', imageAsset: 'Image asset', asset: 'Asset', noAsset: 'No asset',
    binding: 'Binding', bindingDestination: 'Visual property', bindingSource: 'Project source', applyBinding: 'Apply binding', removeBinding: 'Remove binding', noBindingDestinations: 'This object has no bindable visual properties.', noBindingSources: 'No compatible canonical project sources are available.', currentBinding: 'Current binding', browseReferences: 'Browse project references', exactReference: 'Exact reference', exactReferencePlaceholder: 'Type the canonical TAG or variable reference', exactNotFound: 'No compatible source matches this exact reference.', selectBindingObject: 'Select one visual object to edit its canonical binding.',
    validation: 'Engineering validation', previewFailed: 'Preview failed', valid: 'Valid candidate', invalid: 'Invalid candidate', notValidated: 'Not validated', creates: 'creates', updates: 'updates', errors: 'errors', previewFooter: 'Preview does not mutate Working. Apply uses the validated Workspace version and reloads the canonical snapshot.',
    discardConfirm: 'Discard the current Screen draft?', applyConfirm: 'Apply this validated Screen draft to the official Engineering Workspace?', workspaceChanged: 'The Engineering Workspace changed during validation. Reload the canonical snapshot and validate again.'
  };
  if (locale === 'es') return {
    eyebrow: 'Ingeniería gráfica canónica', title: 'Base del editor de Pantallas', description: 'Las Pantallas se editan como Engineering canónico. El estado del Canvas es transitorio y nunca se convierte en una segunda autoridad del proyecto.',
    authorityTitle: 'Preview obligatorio antes de Aplicar', authorityHint: 'El Preview/Apply público y CAS del Workspace protegen los cambios de Pantalla.', screenList: 'Lista de Pantallas', screens: 'Pantallas', newScreen: 'Nueva Pantalla', noRoute: 'sin ruta', objects: 'objetos',
    name: 'Nombre', key: 'Clave', route: 'Ruta', draft: 'Borrador', newDraft: 'Nuevo', changed: 'Modificado', unchanged: 'Sin cambios', untitled: 'Pantalla sin título', objectsPanel: 'Objetos', objectsPanelHint: 'Agregue objetos visuales registrados. La identidad y los valores predeterminados siguen bajo autoridad canónica.', addObject: 'Agregar', objectLabels: { group: 'Grupo', rectangle: 'Rectángulo', ellipse: 'Elipse', line: 'Línea', polygon: 'Polígono', text: 'Texto', image: 'Imagen', valueDisplay: 'Valor', button: 'Botón' },
    propertiesPanel: 'Propiedades', canonicalPreview: 'Preview renderizado canónico', canonicalPreviewHint: 'Proyección del renderer sobre el mismo borrador, incluyendo valores de texto dinámicos.', interactiveCanvas: 'Canvas interactivo', polygonDrawing: 'Modo polígono: haga clic en vértices, Enter para finalizar, Escape para cancelar.', emptyCanvas: 'Esta Pantalla todavía no contiene objetos visuales canónicos.', reset: 'Restablecer borrador', preview: 'Preview del cambio', previewing: 'Validando...', apply: 'Aplicar al Workspace', applying: 'Aplicando...',
    assets: 'Recursos de imagen', assetHint: 'Importe imágenes raster del proyecto con el borrador limpio y asígnelas por ID estable.', importAsset: 'Importar imagen', importingAsset: 'Importando imagen...', assetImportRequiresCleanDraft: 'Aplique o restablezca los cambios de Pantalla antes de importar una imagen.', imageAsset: 'Recurso de imagen', asset: 'Recurso', noAsset: 'Sin recurso',
    binding: 'Binding', bindingDestination: 'Propiedad visual', bindingSource: 'Fuente del proyecto', applyBinding: 'Aplicar binding', removeBinding: 'Eliminar binding', noBindingDestinations: 'Este objeto no tiene propiedades visuales enlazables.', noBindingSources: 'No hay fuentes canónicas compatibles disponibles.', currentBinding: 'Binding actual', browseReferences: 'Explorar referencias del proyecto', exactReference: 'Referencia exacta', exactReferencePlaceholder: 'Escriba la referencia canónica del TAG o variable', exactNotFound: 'Ninguna fuente compatible coincide con esta referencia.', selectBindingObject: 'Seleccione un objeto visual para editar su binding canónico.',
    validation: 'Validación de Engineering', previewFailed: 'Falló el Preview', valid: 'Candidato válido', invalid: 'Candidato inválido', notValidated: 'No validado', creates: 'creaciones', updates: 'actualizaciones', errors: 'errores', previewFooter: 'Preview no modifica Working. Aplicar usa la versión validada del Workspace y recarga el snapshot canónico.',
    discardConfirm: '¿Descartar el borrador actual de la Pantalla?', applyConfirm: '¿Aplicar este borrador validado al Engineering Workspace oficial?', workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue el snapshot canónico y valide nuevamente.'
  };
  return {
    eyebrow: 'Engenharia gráfica canônica', title: 'Fundação do editor de Telas', description: 'Telas são editadas como Engineering canônico. Estado de Canvas permanece transitório e nunca vira uma segunda autoridade do projeto.',
    authorityTitle: 'Preview obrigatório antes do Apply', authorityHint: 'O Preview/Apply público e o CAS do Workspace protegem as mudanças da Tela.', screenList: 'Lista de Telas', screens: 'Telas', newScreen: 'Nova Tela', noRoute: 'sem rota', objects: 'objetos',
    name: 'Nome', key: 'Chave', route: 'Rota', draft: 'Rascunho', newDraft: 'Novo', changed: 'Alterado', unchanged: 'Sem alterações', untitled: 'Tela sem título', objectsPanel: 'Objetos', objectsPanelHint: 'Adicione objetos visuais registrados. Identidade e defaults continuam sob autoridade canônica.', addObject: 'Adicionar', objectLabels: { group: 'Grupo', rectangle: 'Retângulo', ellipse: 'Elipse', line: 'Linha', polygon: 'Polígono', text: 'Texto', image: 'Imagem', valueDisplay: 'Valor', button: 'Botão' },
    propertiesPanel: 'Propriedades', canonicalPreview: 'Preview renderizado canônico', canonicalPreviewHint: 'Projeção do renderer sobre o mesmo rascunho, incluindo valores vivos de texto.', interactiveCanvas: 'Canvas interativo', polygonDrawing: 'Modo polígono: clique nos vértices, Enter para finalizar, Escape para cancelar.', emptyCanvas: 'Esta Tela ainda não possui objetos visuais canônicos.', reset: 'Restaurar rascunho', preview: 'Preview da alteração', previewing: 'Validando...', apply: 'Aplicar ao Workspace', applying: 'Aplicando...',
    assets: 'Assets de imagem', assetHint: 'Importe imagens raster do projeto com o rascunho da Tela limpo e depois associe pelo ID estável.', importAsset: 'Importar imagem', importingAsset: 'Importando imagem...', assetImportRequiresCleanDraft: 'Aplique ou restaure as alterações da Tela antes de importar um asset de imagem.', imageAsset: 'Asset da imagem', asset: 'Asset', noAsset: 'Sem asset',
    binding: 'Binding', bindingDestination: 'Propriedade visual', bindingSource: 'Fonte do projeto', applyBinding: 'Aplicar binding', removeBinding: 'Remover binding', noBindingDestinations: 'Este objeto não possui propriedades visuais com binding.', noBindingSources: 'Não há fontes canônicas compatíveis disponíveis.', currentBinding: 'Binding atual', browseReferences: 'Procurar referências do projeto', exactReference: 'Referência exata', exactReferencePlaceholder: 'Digite a referência canônica do TAG ou variável', exactNotFound: 'Nenhuma fonte compatível corresponde a esta referência.', selectBindingObject: 'Selecione um objeto visual para editar seu binding canônico.',
    validation: 'Validação de Engineering', previewFailed: 'Falha no Preview', valid: 'Candidato válido', invalid: 'Candidato inválido', notValidated: 'Não validado', creates: 'criações', updates: 'atualizações', errors: 'erros', previewFooter: 'Preview não altera o Working. Apply usa a versão validada do Workspace e recarrega o snapshot canônico.',
    discardConfirm: 'Descartar o rascunho atual da Tela?', applyConfirm: 'Aplicar este rascunho validado ao Engineering Workspace oficial?', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue o snapshot canônico e valide novamente.'
  };
}