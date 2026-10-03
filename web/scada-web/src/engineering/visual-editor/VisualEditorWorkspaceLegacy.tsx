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
  DynamoEngineering,
  ImportPreviewView,
  ScreenEngineering,
  TemplateEngineering
} from '../types';
import { initializeClientMemory } from '../../runtime/clientMemory';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../visual-runtime';
import { VisualEditorCanvas } from './canvas';
import { VisualEditorAuthoringSidebar, type VisualEditorAuthoringTab } from './VisualEditorAuthoringSidebar';
import { VisualEditorSelectionInspector, type VisualEditorInspectorTab } from './VisualEditorSelectionInspector';
import { createCanonicalPolygon, updateCanonicalPolygonPoints } from './polygonCanonicalMutations';
import { VisualEditorRegionToggle } from './VisualEditorRegionToggle';
import {
  NEW_SCREEN_IDENTITY,
  applyVisualEditorMutationIntent,
  cloneEngineeringValue,
  countVisualElements,
  createScreenDraft,
  replaceScreenInPackage,
  replaceTemplateInPackage,
  replaceDynamoInPackage,
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
  normalizeVisualEditorMutationIntent,
  normalizeVisualEditorViewport,
  selectedVisualElements
} from './visualEditorIntegrationModel';
import type { VisualEditorKeyboardCommand } from './visualEditorKeyboardModel';
import { DynamoDefinitionParametersEditor } from './dynamo/DynamoDefinitionParametersEditor';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterKindEngineering
} from '../../runtime/visual-navigation/runtimeVisualNavigationModel';
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

type VisualEditorWorkspaceProps = {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
  onAssetImported?: () => Promise<void>;
  definitionKind?: 'screen' | 'template' | 'dynamo';
  initialDefinitionKey?: string | null;
  onRequestClose?: () => void;
};

type ValidatedCandidate = { package: EngineeringPackageView; changeVersion: number };

const DEFAULT_VIEWPORT: VisualEditorViewport = Object.freeze({ zoom: 1, panX: 0, panY: 0 });

export function VisualEditorWorkspace({ snapshot, locale, onApplied, onAssetImported, definitionKind = 'screen', initialDefinitionKey, onRequestClose }: VisualEditorWorkspaceProps) {
  const text = useMemo(() => {
    const copy = visualEditorText(locale);
    if (definitionKind === 'dynamo') {
      const labels = locale === 'en'
        ? { title: 'Dynamo drawing editor', description: 'Edit this shared Dynamo visually. Linked screen instances keep their reference and receive the updated drawing.', authorityTitle: 'Preview required before Apply', authorityHint: 'The visual draft is validated before the shared definition changes.', screenList: 'Dynamo library', screens: 'Dynamos', newScreen: 'New Dynamo', noRoute: 'shared definition', name: 'Display name', nameHint: 'Label shown in the object library.', key: 'Identifier', keyHint: 'Stable identifier; instances depend on this value.', route: 'Shared definition', routeHint: 'Instances remain linked to this Dynamo definition.', untitled: 'Untitled Dynamo', emptyCanvas: 'This Dynamo has no visual objects yet.', discardConfirm: 'Discard the current Dynamo draft?', applyConfirm: 'Apply this validated Dynamo drawing to the Engineering Workspace?' }
        : locale === 'es'
          ? { title: 'Editor de dibujo de Dínamos', description: 'Edite visualmente este Dínamo compartido. Las instancias vinculadas conservan la referencia y reciben el nuevo dibujo.', authorityTitle: 'Preview obligatorio antes de Aplicar', authorityHint: 'El borrador visual se valida antes de cambiar la definición compartida.', screenList: 'Biblioteca de Dínamos', screens: 'Dínamos', newScreen: 'Nuevo Dínamo', noRoute: 'definición compartida', name: 'Nombre visible', nameHint: 'Etiqueta que aparece en la biblioteca.', key: 'Identificador', keyHint: 'Identificador estable usado por las instancias.', route: 'Definición compartida', routeHint: 'Las instancias permanecen vinculadas a este Dínamo.', untitled: 'Dínamo sin nombre', emptyCanvas: 'Este Dínamo todavía no tiene objetos visuales.', discardConfirm: '¿Descartar el borrador del Dínamo?', applyConfirm: '¿Aplicar este dibujo validado al Engineering Workspace?' }
          : { title: 'Editor visual de Dínamos', description: 'Edite este dínamo compartilhado pela interface. As instâncias vinculadas mantêm a referência e recebem o desenho atualizado.', authorityTitle: 'Preview obrigatório antes do Apply', authorityHint: 'O rascunho visual é validado antes de alterar a definição compartilhada.', screenList: 'Biblioteca de dínamos', screens: 'Dínamos', newScreen: 'Novo dínamo', noRoute: 'definição compartilhada', name: 'Nome de exibição', nameHint: 'Rótulo mostrado na biblioteca de objetos.', key: 'Identificador', keyHint: 'Identificador estável utilizado pelas instâncias.', route: 'Definição compartilhada', routeHint: 'As instâncias continuam vinculadas a este dínamo.', untitled: 'Dínamo sem nome', emptyCanvas: 'Este dínamo ainda não possui objetos visuais.', discardConfirm: 'Descartar o rascunho atual do dínamo?', applyConfirm: 'Aplicar este desenho validado à Área de Engenharia?' };
      return { ...copy, ...labels };
    }
    if (definitionKind !== 'template') return copy;
    const labels = locale === 'en'
      ? { title: 'Faceplate Template editor', description: 'Edit the canonical graphic once; every Equipment instance linked to this Template reflects the update.', authorityTitle: 'Preview required before Apply', authorityHint: 'Validation protects the shared Template; Apply updates its canonical drawing without replacing Equipment links.', screenList: 'Template list', screens: 'Templates', newScreen: 'New Template', noRoute: 'linked graphic', name: 'Template name', nameHint: 'Name shown in the reusable library.', key: 'Template identifier', keyHint: 'Stable internal reference.', route: 'Instance link', routeHint: 'Equipment instances keep a stable link to this Template.', untitled: 'Untitled Template', emptyCanvas: 'This Template has no graphical objects yet.', discardConfirm: 'Discard the current Template draft?', applyConfirm: 'Apply this validated Template to the Engineering Workspace?' }
      : locale === 'es'
        ? { title: 'Editor de plantillas Faceplate', description: 'Edite el gráfico canónico una vez; cada instancia de Equipo vinculada refleja el cambio.', authorityTitle: 'Preview obligatorio antes de Aplicar', authorityHint: 'La validación protege la plantilla compartida; Aplicar actualiza el gráfico sin romper los vínculos de Equipo.', screenList: 'Lista de plantillas', screens: 'Plantillas', newScreen: 'Nueva plantilla', noRoute: 'gráfico vinculado', name: 'Nombre de plantilla', nameHint: 'Nombre visible en la biblioteca reutilizable.', key: 'Identificador de plantilla', keyHint: 'Referencia interna estable.', route: 'Vínculo de instancia', routeHint: 'Las instancias de Equipo conservan un vínculo estable con esta plantilla.', untitled: 'Plantilla sin título', emptyCanvas: 'Esta plantilla aún no tiene objetos gráficos.', discardConfirm: '¿Descartar el borrador de plantilla?', applyConfirm: '¿Aplicar esta plantilla validada al espacio de Ingeniería?' }
        : { title: 'Editor de Templates Faceplate', description: 'Edite o desenho canônico uma vez; cada instância de Equipamento vinculada reflete a atualização.', authorityTitle: 'Preview obrigatório antes do Apply', authorityHint: 'A validação protege o Template compartilhado; aplicar atualiza o desenho sem quebrar vínculos dos Equipamentos.', screenList: 'Lista de Templates', screens: 'Templates', newScreen: 'Novo Template', noRoute: 'composição vinculada', name: 'Nome do Template', nameHint: 'Nome mostrado na biblioteca reutilizável.', key: 'Identificador do Template', keyHint: 'Referência interna estável.', route: 'Vínculo das instâncias', routeHint: 'As instâncias de Equipamento mantêm vínculo estável com este Template.', untitled: 'Template sem nome', emptyCanvas: 'Este Template ainda não tem objetos gráficos.', discardConfirm: 'Descartar o rascunho atual do Template?', applyConfirm: 'Aplicar este Template validado à Área de Engenharia?' };
    return { ...copy, ...labels };
  }, [locale, definitionKind]);
  const templates = snapshot.package.templates ?? [];
  const dynamos = snapshot.package.dynamos ?? [];
  const screens = definitionKind === 'template'
    ? templates.map(templateAsScreen)
    : definitionKind === 'dynamo'
      ? dynamos.map(dynamoAsScreen)
      : snapshot.package.screens ?? [];
  const [selectedIdentity, setSelectedIdentity] = useState<string>(() => {
    const initial = initialDefinitionKey ? screens.find(screen => screen.key === initialDefinitionKey) : null;
    const first = initial ?? screens[0];
    return first ? screenIdentity(first) : NEW_SCREEN_IDENTITY;
  });
  const isNew = selectedIdentity === NEW_SCREEN_IDENTITY;
  const selected = !isNew ? screens.find(screen => matchesScreenIdentity(screen, selectedIdentity)) ?? null : null;
  const selectedTemplate = definitionKind === 'template' && selected ? templates.find(item => screenIdentity(item) === screenIdentity(selected)) ?? null : null;
  const selectedDynamo = definitionKind === 'dynamo' && selected ? dynamos.find(item => (item.id ? `id:${item.id}` : `key:${item.key}`) === screenIdentity(selected)) ?? null : null;
  const initialDraft = selected ? cloneEngineeringValue(selected) : createDraft(screens, locale, definitionKind);
  const [session, setSessionState] = useState<VisualEditorSessionState>(() => createVisualEditorSession(initialDraft));
  const [dynamoParameters, setDynamoParameters] = useState<readonly DynamoParameterDefinitionEngineering[]>(
    () => Object.freeze([...(selectedDynamo?.parameters ?? [])])
  );
  const sessionRef = useRef(session);
  const draft = currentVisualEditorSessionScreen(session);
  const selectedObjectIds = session.selectedObjectIds;
  const [viewport, setViewport] = useState<VisualEditorViewport>(DEFAULT_VIEWPORT);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<ValidatedCandidate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [importingAsset, setImportingAsset] = useState(false);
  const [polygonToolActive, setPolygonToolActive] = useState(false);
  const [screensCollapsed, setScreensCollapsed] = useState(false);
  const [paletteCollapsed, setPaletteCollapsed] = useState(false);
  const [propertiesCollapsed, setPropertiesCollapsed] = useState(false);
  const [paletteWidth, setPaletteWidth] = useState(250);
  const [propertiesWidth, setPropertiesWidth] = useState(280);
  const [authoringTab, setAuthoringTab] = useState<VisualEditorAuthoringTab>('structure');
  const [inspectorTab, setInspectorTab] = useState<VisualEditorInspectorTab>('properties');
  const workspaceRef = useRef<HTMLDivElement | null>(null);
  const selectedIdentityRef = useRef(selectedIdentity);
  const preserveDraftAfterAssetImportRef = useRef(false);
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
    if (selectedIdentity === NEW_SCREEN_IDENTITY) {
      replaceSession(createVisualEditorSession(createDraft(screens, locale, definitionKind)));
      setDynamoParameters(Object.freeze([]));
      setViewport(DEFAULT_VIEWPORT);
      invalidateValidation();
      return;
    }
    const current = screens.find(screen => matchesScreenIdentity(screen, selectedIdentity)) ?? null;
    if (current) {
      replaceSession(createVisualEditorSession(cloneEngineeringValue(current)));
      if (definitionKind === 'dynamo') {
        const currentDynamo = dynamos.find(item =>
          (item.id ? `id:${item.id}` : `key:${item.key}`) === screenIdentity(current)) ?? null;
        setDynamoParameters(Object.freeze([...(currentDynamo?.parameters ?? [])]));
      }
      setViewport(DEFAULT_VIEWPORT);
      invalidateValidation();
      return;
    }
    if (screens[0]) setSelectedIdentity(screenIdentity(screens[0]));
    else setSelectedIdentity(NEW_SCREEN_IDENTITY);
  }, [selectedIdentity, snapshot.package, definitionKind]);

  const visualDefinitionChanged = selected !== null && JSON.stringify(selected) !== JSON.stringify(draft);
  const publicInterfaceChanged = definitionKind === 'dynamo' &&
    JSON.stringify(selectedDynamo?.parameters ?? []) !== JSON.stringify(dynamoParameters);
  const changed = isNew ? true : visualDefinitionChanged || publicInterfaceChanged;
  const selectedElements = useMemo(
    () => selectedVisualElements(draft, selectedObjectIds),
    [draft, selectedObjectIds]
  );
  const projectReferences = useMemo(
    () => buildProjectReferenceCatalog(snapshot.package, clientMemoryDefinitions),
    [snapshot.package, clientMemoryDefinitions]
  );
  const bindingSourceCatalog = useMemo<readonly VisualEditorBindingSourceCatalogItem[]>(() => {
    const projectSources = projectReferences
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
      }));

    if (definitionKind !== 'dynamo') return Object.freeze(projectSources);

    const parameterSources = dynamoParameters
      .filter(parameter => parameter.kind !== 'Command')
      .map(parameter => Object.freeze({
      kind: parameter.kind === 'TagReference' ? 'Tag' as const : 'Property' as const,
      target: `{dynamoParameter:${parameter.key}}`,
      label: `Dynamo · ${parameter.key}`,
      dataType: parameterDataType(parameter.kind),
      writable: false,
      bindable: true,
      dynamoParameterKey: parameter.key
    }));
    return Object.freeze([...parameterSources, ...projectSources]);
  }, [projectReferences, definitionKind, dynamoParameters]);
  const visualAssets = snapshot.package.visualAssets ?? [];

  const resizeDock = (region: 'palette' | 'properties', event: React.PointerEvent<HTMLButtonElement>) => {
    if (event.type === 'pointerdown') {
      event.currentTarget.setPointerCapture(event.pointerId);
      return;
    }
    if (event.type !== 'pointermove' || !(event.currentTarget as HTMLButtonElement).hasPointerCapture(event.pointerId)) return;
    const bounds = event.currentTarget.parentElement?.getBoundingClientRect();
    if (!bounds) return;
    if (region === 'palette') setPaletteWidth(Math.max(200, Math.min(380, Math.round(event.clientX - bounds.left))));
    else setPropertiesWidth(Math.max(220, Math.min(440, Math.round(bounds.right - event.clientX))));
  };

  const resizeDockByKeyboard = (region: 'palette' | 'properties', event: React.KeyboardEvent<HTMLButtonElement>) => {
    const direction = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
    if (!direction) return;
    event.preventDefault();
    const delta = direction * (event.shiftKey ? 32 : 12) * (region === 'palette' ? 1 : -1);
    if (region === 'palette') setPaletteWidth(current => Math.max(200, Math.min(380, current + delta)));
    else setPropertiesWidth(current => Math.max(220, Math.min(440, current + delta)));
  };

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
    setViewport(DEFAULT_VIEWPORT);
    setPolygonToolActive(false);
    invalidateValidation();
  };

  const updateDraft = (update: (current: ScreenEngineering) => ScreenEngineering) => {
    const current = sessionRef.current;
    replaceSession(commitVisualEditorSessionDraft(current, update(current.history.present)));
    invalidateValidation();
  };

  const resetDraft = () => {
    replaceSession(createVisualEditorSession(selected ? cloneEngineeringValue(selected) : createDraft(screens, locale, definitionKind)));
    if (definitionKind === 'dynamo') {
      setDynamoParameters(Object.freeze([...(selectedDynamo?.parameters ?? [])]));
    }
    setViewport(DEFAULT_VIEWPORT);
    setPolygonToolActive(false);
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

      const normalizedIntent = normalizeVisualEditorMutationIntent(intent);
      const nextDraft = applyVisualEditorMutationIntent(currentDraft, normalizedIntent);
      replaceSession(commitVisualEditorSessionDraft(current, nextDraft));
      invalidateValidation();
    } catch (reason) {
      setPreview(null);
      setCandidate(null);
      setError(reason instanceof Error ? reason.message : String(reason));
    }
  };

  const handleKeyboardCommand = (command: VisualEditorKeyboardCommand) => {
    try {
      const current = sessionRef.current;
      const next = applyVisualEditorSessionKeyboardCommand(current, command);
      replaceSession(next);
      if (next.history !== current.history) invalidateValidation();
    } catch (reason) {
      setPreview(null);
      setCandidate(null);
      setError(reason instanceof Error ? reason.message : String(reason));
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
      const nextPackage = definitionKind === 'template'
        ? replaceTemplateInPackage(snapshot.package, selectedTemplate, draft)
        : definitionKind === 'dynamo'
          ? replaceDynamoInPackage(snapshot.package, selectedDynamo, draft, dynamoParameters)
          : replaceScreenInPackage(snapshot.package, selected, draft);
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
      replaceSession(createVisualEditorSession(draft));
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

  const importAsset = async (file: File): Promise<string | null> => {
    setImportingAsset(true);
    setError(null);
    try {
      const currentWorkspace = await loadEngineeringWorkspace();
      if (currentWorkspace.changeVersion !== snapshot.workspace.changeVersion) throw new Error(text.workspaceChanged);
      const imported = await importVisualAsset(file, currentWorkspace.changeVersion, { fileName: file.name });
      preserveDraftAfterAssetImportRef.current = true;
      await (onAssetImported ?? onApplied)();
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
  const objectCount = countVisualElements(draft.elements);

  const layoutClassName = [
    'eng-section visual-editor-workspace',
    screensCollapsed ? 'visual-editor-workspace--screens-collapsed' : '',
    paletteCollapsed ? 'visual-editor-workspace--palette-collapsed' : '',
    propertiesCollapsed ? 'visual-editor-workspace--properties-collapsed' : ''
  ].filter(Boolean).join(' ');

  return <div ref={workspaceRef} className={layoutClassName} data-testid="visual-editor-workspace">
    <header className="visual-editor-header">
      <div><h1>{text.title}</h1><details className="visual-editor-help"><summary>{locale === 'en' ? 'Editor guidance' : locale === 'es' ? 'Ayuda del editor' : 'Ajuda do editor'}</summary><p>{text.description}</p><div className="visual-editor-authority"><strong>{text.authorityTitle}</strong><span>{text.authorityHint}</span></div></details></div>
      {onRequestClose ? <button type="button" className="secondary" onClick={() => { if (!changed || window.confirm(text.discardConfirm)) onRequestClose(); }}>{locale === 'en' ? '← Dynamo library' : locale === 'es' ? '← Biblioteca de Dínamos' : '← Biblioteca de dínamos'}</button> : null}
      <span className="visual-editor-header-status">{objectCount} {text.objects}</span>
    </header>

    <div className="visual-editor-shell">
      <aside className="visual-editor-screens" aria-label={text.screenList}>
        <header>
          <strong>{text.screens}</strong>
          <div className="visual-editor-region-actions">
          <button type="button" className={isNew ? 'active' : ''} onClick={() => chooseScreen(NEW_SCREEN_IDENTITY)}>+ {text.newScreen}</button>
            <VisualEditorRegionToggle region="screens" collapsed={screensCollapsed} locale={locale} onToggle={() => setScreensCollapsed(value => !value)} />
          </div>
        </header>
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
          <label><span>{text.name}</span><input aria-description={text.nameHint} value={draft.name} onChange={event => updateDraft(current => ({ ...current, name: event.target.value }))} /><small aria-hidden="true">{text.nameHint}</small></label>
          <label><span>{text.key}</span><input aria-description={text.keyHint} className="mono" value={draft.key} readOnly={definitionKind === 'dynamo' && !isNew} onChange={event => updateDraft(current => ({ ...current, key: event.target.value }))} /><small aria-hidden="true">{text.keyHint}</small></label>
          {definitionKind === 'screen' ? <label><span>{text.route}</span><input aria-description={text.routeHint} className="mono" value={draft.route ?? ''} placeholder="/overview" onChange={event => updateDraft(current => ({ ...current, route: emptyToNull(event.target.value) }))} /><small aria-hidden="true">{text.routeHint}</small></label> : <div className="visual-editor-draft-state"><span>{text.route}</span><strong>{draft.key}</strong><small>{text.routeHint}</small></div>}
          <div className="visual-editor-draft-state"><span>{text.draft}</span><strong>{isNew ? text.newDraft : changed ? text.changed : text.unchanged}</strong><small>{objectCount} {text.objects}</small></div>
        </div>

        {definitionKind === 'dynamo' ? <DynamoDefinitionParametersEditor
          parameters={dynamoParameters}
          locale={locale}
          disabled={previewing || applying}
          onChange={parameters => {
            setDynamoParameters(Object.freeze([...parameters]));
            invalidateValidation();
          }}
        /> : null}

        <div className="visual-editor-composition" style={{ '--visual-editor-palette-width': paletteCollapsed ? '40px' : `${paletteWidth}px`, '--visual-editor-properties-width': propertiesCollapsed ? '40px' : `${propertiesWidth}px` } as React.CSSProperties}>
          <aside className="visual-editor-slot visual-editor-palette-slot">
            <VisualEditorRegionToggle region="palette" collapsed={paletteCollapsed} locale={locale} onToggle={() => setPaletteCollapsed(value => !value)} />
            <VisualEditorAuthoringSidebar
              screen={draft}
              selectedObjectIds={selectedObjectIds}
              definitions={definitionKind === 'dynamo' ? [] : snapshot.package.dynamos ?? []}
              equipment={snapshot.package.equipment ?? []}
              templates={snapshot.package.templates ?? []}
              allowEquipmentInstances={definitionKind === 'screen'}
              visualAssets={visualAssets}
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

          <button type="button" className="visual-editor-dock-resizer" role="separator" aria-orientation="vertical" aria-label={locale === 'en' ? 'Resize object and Dynamo panel' : locale === 'es' ? 'Cambiar ancho de estructura y biblioteca' : 'Redimensionar coluna de estrutura e dínamos'} aria-valuemin={200} aria-valuemax={380} aria-valuenow={paletteWidth} tabIndex={0} onPointerDown={event => resizeDock('palette', event)} onPointerMove={event => resizeDock('palette', event)} onKeyDown={event => resizeDockByKeyboard('palette', event)} />

          <section className="visual-editor-canvas-slot">
            <VisualEditorCanvas
              screen={draft}
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
              equipmentDefinitions={snapshot.package.equipment}
              templateDefinitions={snapshot.package.templates}
              emptyLabel={text.emptyCanvas}
              polygonToolActive={polygonToolActive}
              onPolygonToolCancel={() => setPolygonToolActive(false)}
            />
          </section>

          <button type="button" className="visual-editor-dock-resizer" role="separator" aria-orientation="vertical" aria-label={locale === 'en' ? 'Resize properties panel' : locale === 'es' ? 'Cambiar ancho de propiedades' : 'Redimensionar coluna de propriedades'} aria-valuemin={220} aria-valuemax={440} aria-valuenow={propertiesWidth} tabIndex={0} onPointerDown={event => resizeDock('properties', event)} onPointerMove={event => resizeDock('properties', event)} onKeyDown={event => resizeDockByKeyboard('properties', event)} />

          <aside className="visual-editor-slot visual-editor-inspector-slot">
            <VisualEditorRegionToggle region="properties" collapsed={propertiesCollapsed} locale={locale} onToggle={() => setPropertiesCollapsed(value => !value)} />
            <VisualEditorSelectionInspector
              screen={draft}
              selectedElements={selectedElements}
              selectedObjectIds={selectedObjectIds}
              sourceCatalog={bindingSourceCatalog}
              visualAssets={visualAssets}
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

function matchesScreenIdentity(screen: ScreenEngineering, identity: string): boolean {
  return screenIdentity(screen) === identity || `key:${screen.key}` === identity;
}
function templateAsScreen(template: TemplateEngineering): ScreenEngineering {
  return {
    id: template.id,
    key: template.key,
    name: template.name,
    elements: template.elements ?? [],
    properties: template.properties ?? {},
    context: template.context ?? {},
    metadata: template.metadata ?? {}
  };
}
function dynamoAsScreen(dynamo: DynamoEngineering): ScreenEngineering {
  return {
    id: dynamo.id,
    key: dynamo.key,
    name: dynamo.name,
    elements: [...(dynamo.elements ?? [])],
    properties: dynamo.properties ?? {},
    context: dynamo.context ?? {},
    metadata: dynamo.metadata ?? {}
  };
}
function createDraft(existing: readonly ScreenEngineering[], locale: EngineeringLocale, kind: 'screen' | 'template' | 'dynamo'): ScreenEngineering {
  const draft = createScreenDraft(existing, locale);
  if (kind === 'screen') return draft;
  const prefix = kind === 'dynamo' ? 'dynamo-' : 'template-';
  const namePrefix = kind === 'dynamo' ? (locale === 'en' ? 'Dynamo ' : locale === 'es' ? 'Dínamo ' : 'Dínamo ') : (locale === 'en' ? 'Template ' : locale === 'es' ? 'Plantilla ' : 'Template ');
  return { ...draft, key: draft.key.replace(/^screen-/, prefix), name: draft.name.replace(/^(Screen|Pantalla|Tela) /, namePrefix), route: null };
}
function emptyToNull(value: string): string | null { return value.trim().length === 0 ? null : value; }

function parameterDataType(kind: DynamoParameterKindEngineering): string | null {
  switch (kind) {
    case 'Boolean': return 'Boolean';
    case 'Number': return 'Double';
    case 'String': return 'String';
    case 'EquipmentPath': return 'String';
    case 'TagReference': return null;
    case 'Command': return null;
  }
}

function visualEditorText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Canonical graphical Engineering', title: 'Screen editor foundation', description: 'Screens are edited as canonical Engineering. Canvas state remains transient and is never a second project authority.',
    authorityTitle: 'Preview required before Apply', authorityHint: 'The public Engineering Preview/Apply and Workspace CAS protect Screen changes.', screenList: 'Screen list', screens: 'Screens', newScreen: 'New Screen', noRoute: 'no route', objects: 'objects',
    name: 'Display name', nameHint: 'Label shown to people.', key: 'Identifier', keyHint: 'Stable internal reference for this screen.', route: 'Screen address', routeHint: 'Navigation address inside the application; it is not the identifier.', draft: 'Draft', newDraft: 'New', changed: 'Changed', unchanged: 'Unchanged', untitled: 'Untitled Screen', objectsPanel: 'Objects', objectsPanelHint: 'Add registered visual objects. Identity and defaults remain canonical authority.', addObject: 'Add', objectLabels: { group: 'Group', rectangle: 'Rectangle', ellipse: 'Ellipse', line: 'Line', polygon: 'Polygon', text: 'Text', image: 'Image', valueDisplay: 'Value display', button: 'Button' },
    propertiesPanel: 'Properties', canonicalPreview: 'Canonical rendered preview', canonicalPreviewHint: 'Renderer projection of the same Engineering draft, including live scalar text bindings.', interactiveCanvas: 'Interactive Canvas', polygonDrawing: 'Polygon drawing mode: click vertices, Enter to finish, Escape to cancel.', emptyCanvas: 'This Screen has no canonical visual objects yet.', reset: 'Reset draft', preview: 'Preview change', previewing: 'Previewing...', apply: 'Apply to Workspace', applying: 'Applying...',
    assets: 'Image assets', assetHint: 'Import images to the project and assign them to objects by asset.', importAsset: 'Import image', importingAsset: 'Importing image...', imageAsset: 'Image asset', asset: 'Asset', noAsset: 'No asset',
    binding: 'Binding', bindingDestination: 'Visual property', bindingSource: 'Project source', applyBinding: 'Apply binding', removeBinding: 'Remove binding', noBindingDestinations: 'This object has no bindable visual properties.', noBindingSources: 'No compatible canonical project sources are available.', currentBinding: 'Current binding', browseReferences: 'Browse project references', exactReference: 'Exact reference', exactReferencePlaceholder: 'Type the canonical TAG or variable reference', exactNotFound: 'No compatible source matches this exact reference.', selectBindingObject: 'Select one visual object to edit its canonical binding.',
    validation: 'Engineering validation', previewFailed: 'Preview failed', valid: 'Valid candidate', invalid: 'Invalid candidate', notValidated: 'Not validated', creates: 'creates', updates: 'updates', errors: 'errors', previewFooter: 'Preview does not mutate Working. Apply uses the validated Workspace version and reloads the canonical snapshot.',
    discardConfirm: 'Discard the current Screen draft?', applyConfirm: 'Apply this validated Screen draft to the official Engineering Workspace?', workspaceChanged: 'The Engineering Workspace changed during validation. Reload the canonical snapshot and validate again.'
  };
  if (locale === 'es') return {
    eyebrow: 'Ingeniería gráfica canónica', title: 'Base del editor de Pantallas', description: 'Las Pantallas se editan como Engineering canónico. El estado del Canvas es transitorio y nunca se convierte en una segunda autoridad del proyecto.',
    authorityTitle: 'Preview obligatorio antes de Aplicar', authorityHint: 'El Preview/Apply público y CAS del Workspace protegen los cambios de Pantalla.', screenList: 'Lista de Pantallas', screens: 'Pantallas', newScreen: 'Nueva Pantalla', noRoute: 'sin ruta', objects: 'objetos',
    name: 'Nombre visible', nameHint: 'Etiqueta que se muestra a las personas.', key: 'Identificador', keyHint: 'Referencia interna estable de esta pantalla.', route: 'Dirección de pantalla', routeHint: 'Dirección de navegación en la aplicación; no es el identificador.', draft: 'Borrador', newDraft: 'Nuevo', changed: 'Modificado', unchanged: 'Sin cambios', untitled: 'Pantalla sin título', objectsPanel: 'Objetos', objectsPanelHint: 'Agregue objetos visuales registrados. La identidad y los valores predeterminados siguen bajo autoridad canónica.', addObject: 'Agregar', objectLabels: { group: 'Grupo', rectangle: 'Rectángulo', ellipse: 'Elipse', line: 'Línea', polygon: 'Polígono', text: 'Texto', image: 'Imagen', valueDisplay: 'Valor', button: 'Botón' },
    propertiesPanel: 'Propiedades', canonicalPreview: 'Preview renderizado canónico', canonicalPreviewHint: 'Proyección del renderer sobre el mismo borrador, incluyendo valores de texto dinámicos.', interactiveCanvas: 'Canvas interactivo', polygonDrawing: 'Modo polígono: haga clic en vértices, Enter para finalizar, Escape para cancelar.', emptyCanvas: 'Esta Pantalla todavía no contiene objetos visuales canónicos.', reset: 'Restablecer borrador', preview: 'Preview del cambio', previewing: 'Validando...', apply: 'Aplicar al Workspace', applying: 'Aplicando...',
    assets: 'Recursos de imagen', assetHint: 'Importe imágenes al proyecto y asígnelas a los objetos por recurso.', importAsset: 'Importar imagen', importingAsset: 'Importando imagen...', imageAsset: 'Recurso de imagen', asset: 'Recurso', noAsset: 'Sin recurso',
    binding: 'Binding', bindingDestination: 'Propiedad visual', bindingSource: 'Fuente del proyecto', applyBinding: 'Aplicar binding', removeBinding: 'Eliminar binding', noBindingDestinations: 'Este objeto no tiene propiedades visuales enlazables.', noBindingSources: 'No hay fuentes canónicas compatibles disponibles.', currentBinding: 'Binding actual', browseReferences: 'Explorar referencias del proyecto', exactReference: 'Referencia exacta', exactReferencePlaceholder: 'Escriba la referencia canónica del TAG o variable', exactNotFound: 'Ninguna fuente compatible coincide con esta referencia.', selectBindingObject: 'Seleccione un objeto visual para editar su binding canónico.',
    validation: 'Validación de Engineering', previewFailed: 'Falló el Preview', valid: 'Candidato válido', invalid: 'Candidato inválido', notValidated: 'No validado', creates: 'creaciones', updates: 'actualizaciones', errors: 'errores', previewFooter: 'Preview no modifica Working. Aplicar usa la versión validada del Workspace y recarga el snapshot canónico.',
    discardConfirm: '¿Descartar el borrador actual de la Pantalla?', applyConfirm: '¿Aplicar este borrador validado al Engineering Workspace oficial?', workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue el snapshot canónico y valide nuevamente.'
  };
  return {
    eyebrow: 'Engenharia gráfica canônica', title: 'Fundação do editor de Telas', description: 'Telas são editadas como Engineering canônico. Estado de Canvas permanece transitório e nunca vira uma segunda autoridade do projeto.',
    authorityTitle: 'Preview obrigatório antes do Apply', authorityHint: 'O Preview/Apply público e o CAS do Workspace protegem as mudanças da Tela.', screenList: 'Lista de Telas', screens: 'Telas', newScreen: 'Nova Tela', noRoute: 'sem rota', objects: 'objetos',
    name: 'Nome de exibição', nameHint: 'Rótulo mostrado para as pessoas.', key: 'Identificador', keyHint: 'Referência interna estável desta tela.', route: 'Endereço da tela', routeHint: 'Endereço de navegação dentro do aplicativo; não é o identificador.', draft: 'Rascunho', newDraft: 'Novo', changed: 'Alterado', unchanged: 'Sem alterações', untitled: 'Tela sem título', objectsPanel: 'Objetos', objectsPanelHint: 'Adicione objetos visuais registrados. Identidade e defaults continuam sob autoridade canônica.', addObject: 'Adicionar', objectLabels: { group: 'Grupo', rectangle: 'Retângulo', ellipse: 'Elipse', line: 'Linha', polygon: 'Polígono', text: 'Texto', image: 'Imagem', valueDisplay: 'Valor', button: 'Botão' },
    propertiesPanel: 'Propriedades', canonicalPreview: 'Preview renderizado canônico', canonicalPreviewHint: 'Projeção do renderer sobre o mesmo rascunho, incluindo valores vivos de texto.', interactiveCanvas: 'Canvas interativo', polygonDrawing: 'Modo polígono: clique nos vértices, Enter para finalizar, Escape para cancelar.', emptyCanvas: 'Esta Tela ainda não possui objetos visuais canônicos.', reset: 'Restaurar rascunho', preview: 'Preview da alteração', previewing: 'Validando...', apply: 'Aplicar ao Workspace', applying: 'Aplicando...',
    assets: 'Assets de imagem', assetHint: 'Envie imagens ao projeto e associe-as aos objetos pelo asset.', importAsset: 'Importar imagem', importingAsset: 'Importando imagem...', imageAsset: 'Asset da imagem', asset: 'Asset', noAsset: 'Sem asset',
    binding: 'Binding', bindingDestination: 'Propriedade visual', bindingSource: 'Fonte do projeto', applyBinding: 'Aplicar binding', removeBinding: 'Remover binding', noBindingDestinations: 'Este objeto não possui propriedades visuais com binding.', noBindingSources: 'Não há fontes canônicas compatíveis disponíveis.', currentBinding: 'Binding atual', browseReferences: 'Procurar referências do projeto', exactReference: 'Referência exata', exactReferencePlaceholder: 'Digite a referência canônica do TAG ou variável', exactNotFound: 'Nenhuma fonte compatível corresponde a esta referência.', selectBindingObject: 'Selecione um objeto visual para editar seu binding canônico.',
    validation: 'Validação de Engineering', previewFailed: 'Falha no Preview', valid: 'Candidato válido', invalid: 'Candidato inválido', notValidated: 'Não validado', creates: 'criações', updates: 'atualizações', errors: 'erros', previewFooter: 'Preview não altera o Working. Apply usa a versão validada do Workspace e recarrega o snapshot canônico.',
    discardConfirm: 'Descartar o rascunho atual da Tela?', applyConfirm: 'Aplicar este rascunho validado ao Engineering Workspace oficial?', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue o snapshot canônico e valide novamente.'
  };
}
