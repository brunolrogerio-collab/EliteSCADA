import React, { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../../engineering/i18n';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import type { EngineeringPackageView } from '../../engineering/types';
import type {
  CanonicalVisualEvent,
  VisualAssetUrlResolver
} from '../../engineering/visual-editor/CanonicalVisualRenderer';
import { resolveVisualDefinitionSurfaceStyle } from '../../engineering/visual-editor/visualDefinitionSurfaceModel';
import type { ClientVisualEventDispatchRecord } from '../../python-runtime/clientVisualEventDispatcher';
import { RuntimeLogicalViewport } from './RuntimeLogicalViewport';
import { resolveRuntimeLogicalSize } from './runtimeLogicalCanvas';
import { executeRuntimeCommand, RuntimeCommandExecutionError } from './runtimeCommandApi';
import { useOptionalHistoricalPlayback } from '../historical-playback/HistoricalPlaybackContext';
import { writeRuntimeTagValue } from '../runtimeTagWriteApi';
import { loadReadableRuntimeTags } from '../liveTagTransport';
import { resolvePopupLogicalBounds, resolvePopupLogicalPosition } from './runtimePopupPosition';
import {
  createRuntimeVisualCatalog,
  createRuntimeVisualNavigationState,
  executeVisualNavigationAction,
  resolveActiveScreen,
  resolveMountedPopup,
  resolveVisualNavigationAction,
  RuntimeVisualCompositionError,
  type RuntimeVisualCatalog,
  type RuntimeVisualNavigationState,
  type VisualNavigationActionEngineering
} from './runtimeVisualNavigationModel';
import { RuntimeVisualDefinitionRenderer } from './RuntimeVisualDefinitionRenderer';

export type RuntimeVisualNavigatorProps = Readonly<{
  engineeringPackage: Pick<EngineeringPackageView, 'screens' | 'popups' | 'dynamos' | 'equipment' | 'templates'>;
  initialScreenKey: string;
  locale?: EngineeringLocale;
  emptyLabel?: string;
  popupIdFactory?: () => string;
  scriptContext?: ScriptEngineeringContext | null;
  onScriptDispatch?: (records: readonly ClientVisualEventDispatchRecord[]) => void;
  visualAssetUrl?: VisualAssetUrlResolver;
}>;

type NavigationResolution = Readonly<{
  state: RuntimeVisualNavigationState | null;
  diagnostic: RuntimeVisualCompositionError | null;
}>;

type OperationalVisualAction = Readonly<
  Omit<VisualNavigationActionEngineering, 'kind'> & {
    kind: 'NavigateScreen' | 'OpenPopup' | 'ClosePopup' | 'ExecuteCommand' | 'SetTagValue' | 'ToggleTagBoolean';
    commandId?: string | null;
  }
>;

const POPUP_FALLBACK_TITLE: Readonly<Record<EngineeringLocale, string>> = Object.freeze({
  'pt-BR': 'Janela',
  en: 'Popup',
  es: 'Ventana'
});

export function RuntimeVisualNavigator({
  engineeringPackage,
  initialScreenKey,
  locale = 'pt-BR',
  emptyLabel = 'Sem objetos visuais.',
  popupIdFactory,
  scriptContext,
  onScriptDispatch,
  visualAssetUrl
}: RuntimeVisualNavigatorProps) {
  const catalog = useMemo(() => createRuntimeVisualCatalog(engineeringPackage), [engineeringPackage]);
  const initialResolution = useMemo(
    () => resolveInitialNavigation(catalog, initialScreenKey),
    [catalog, initialScreenKey]
  );
  const [state, setState] = useState<RuntimeVisualNavigationState | null>(initialResolution.state);
  const [diagnostic, setDiagnostic] = useState<RuntimeVisualCompositionError | null>(initialResolution.diagnostic);
  const playback = useOptionalHistoricalPlayback();
  const playbackActive = playback?.mode === 'historicalPlayback';

  useEffect(() => {
    const next = resolveInitialNavigation(catalog, initialScreenKey);
    setState(next.state);
    setDiagnostic(next.diagnostic);
  }, [catalog, initialScreenKey]);

  if (!state) {
    return <RuntimeDiagnostic diagnostic={diagnostic ?? new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_SCREEN_NOT_FOUND',
      `Runtime initial Screen '${initialScreenKey}' could not be resolved.`
    )} />;
  }

  let activeScreen;
  try {
    activeScreen = resolveActiveScreen(catalog, state);
  } catch (reason) {
    return <RuntimeDiagnostic diagnostic={asRuntimeDiagnostic(reason)} />;
  }

  const designSize = resolveRuntimeLogicalSize();

  const dispatch = async (event: CanonicalVisualEvent, popupRuntimeInstanceId?: string) => {
    try {
      const rawAction = resolveVisualNavigationAction(event.element, event.eventKey);
      if (!rawAction) return;
      const action = normalizeVisualActionWireKind(rawAction);
      if (playbackActive &&
          (action.kind === 'ExecuteCommand' || action.kind === 'SetTagValue' || action.kind === 'ToggleTagBoolean')) {
        throw new RuntimeVisualCompositionError(
          'HISTORICAL_PLAYBACK_READ_ONLY',
          `Runtime action '${action.kind}' is blocked while Historical Playback is active.`
        );
      }
      if (action.kind === 'ExecuteCommand') {
        const commandId = action.commandId?.trim();
        if (!commandId) {
          throw new RuntimeVisualCompositionError(
            'VISUAL_RUNTIME_COMMAND_REFERENCE_REQUIRED',
            `ExecuteCommand action '${action.eventKey}' does not contain a canonical Command identity.`
          );
        }
        await executeRuntimeCommand(commandId);
        setDiagnostic(null);
        return;
      }
      if (action.kind === 'SetTagValue') {
        const tagId = action.targetKey?.trim();
        const value = action.parameters?.value;
        if (!tagId || !isRuntimeWriteValue(value)) {
          throw new RuntimeVisualCompositionError(
            'VISUAL_RUNTIME_TAG_WRITE_ACTION_INVALID',
            `SetTagValue action '${action.eventKey}' requires a stable TAG ID and a primitive value.`
          );
        }
        await writeRuntimeTagValue(tagId, value);
        setDiagnostic(null);
        return;
      }
      if (action.kind === 'ToggleTagBoolean') {
        const tagId = action.targetKey?.trim();
        if (!tagId) {
          throw new RuntimeVisualCompositionError(
            'VISUAL_RUNTIME_TAG_TOGGLE_ACTION_INVALID',
            `ToggleTagBoolean action '${action.eventKey}' requires a stable TAG ID.`
          );
        }
        const tag = (await loadReadableRuntimeTags()).find(item => item.id.toLocaleLowerCase() === tagId.toLocaleLowerCase());
        if (!tag || typeof tag.current?.value !== 'boolean') {
          throw new RuntimeVisualCompositionError(
            'VISUAL_RUNTIME_TAG_BOOLEAN_REQUIRED',
            `ToggleTagBoolean action '${action.eventKey}' requires a readable Boolean TAG with a current value.`
          );
        }
        await writeRuntimeTagValue(tag.id, !tag.current.value);
        setDiagnostic(null);
        return;
      }

      const next = executeVisualNavigationAction(
        catalog,
        state,
        action as VisualNavigationActionEngineering,
        {
          popupRuntimeInstanceId,
          popupIdFactory
        }
      );
      setState(next);
      setDiagnostic(null);
    } catch (reason) {
      setDiagnostic(asRuntimeDiagnostic(reason));
    }
  };

  return <div
    className="runtime-visual-navigator"
    data-testid="runtime-visual-navigator"
    data-active-screen-key={state.activeScreenKey}
    data-runtime-temporal-mode={playbackActive ? 'historical-playback' : 'live'}
    data-runtime-historical-at={playbackActive ? playback?.atUtc ?? undefined : undefined}
  >
    <RuntimeLogicalViewport designSize={designSize}>
      <div className="runtime-logical-composition">
        <section
          className="runtime-visual-screen"
          data-screen-key={activeScreen.key}
          style={resolveVisualDefinitionSurfaceStyle(activeScreen.properties, visualAssetUrl)}
        >
          <RuntimeVisualDefinitionRenderer
            visualDefinitionId={activeScreen.id ?? ''}
            runtimeContextId={`screen:${activeScreen.id ?? activeScreen.key}`}
            elements={activeScreen.elements}
            emptyLabel={emptyLabel}
            locale={locale}
            dynamoDefinitions={engineeringPackage.dynamos}
            equipmentDefinitions={engineeringPackage.equipment}
            templateDefinitions={engineeringPackage.templates}
            scriptContext={scriptContext}
            onScriptDispatch={onScriptDispatch}
            onVisualEvent={event => { void dispatch(event); }}
            visualAssetUrl={visualAssetUrl}
          />
        </section>

        <div
          className="runtime-visual-popup-layer"
          data-popup-count={state.popups.length}
          style={{ position: 'absolute', inset: 0, pointerEvents: 'none' }}
        >
          {state.popups.map((mount, index) => {
            try {
              const popup = resolveMountedPopup(catalog, mount);
              const bounds = resolvePopupLogicalBounds(popup);
              const position = resolvePopupLogicalPosition(popup, designSize, bounds);
              return <section
                className="runtime-visual-popup"
                key={mount.runtimeInstanceId}
                data-popup-key={popup.key}
                data-popup-runtime-instance-id={mount.runtimeInstanceId}
                data-popup-stack-index={index}
                data-popup-logical-x={position.x}
                data-popup-logical-y={position.y}
                data-popup-logical-width={bounds.width}
                data-popup-logical-height={bounds.height}
                style={{
                  position: 'absolute',
                  left: position.x,
                  top: position.y,
                  width: bounds.width,
                  zIndex: index + 1,
                  pointerEvents: 'auto'
                }}
              >
                <header className="runtime-visual-popup-header">
                  <strong>{popup.name?.trim() || POPUP_FALLBACK_TITLE[locale]}</strong>
                </header>
                <div
                  className="runtime-visual-popup-content"
                  style={{
                    ...resolveVisualDefinitionSurfaceStyle(popup.properties, visualAssetUrl),
                    width: bounds.width,
                    height: bounds.height
                  }}
                >
                  <RuntimeVisualDefinitionRenderer
                    visualDefinitionId={popup.id ?? ''}
                    runtimeContextId={`popup:${mount.runtimeInstanceId}`}
                    elements={popup.elements}
                    emptyLabel={emptyLabel}
                    locale={locale}
                    dynamoDefinitions={engineeringPackage.dynamos}
                    equipmentDefinitions={engineeringPackage.equipment}
                    templateDefinitions={engineeringPackage.templates}
                    scriptContext={scriptContext}
                    onScriptDispatch={onScriptDispatch}
                    onVisualEvent={event => { void dispatch(event, mount.runtimeInstanceId); }}
                    visualAssetUrl={visualAssetUrl}
                  />
                </div>
              </section>;
            } catch (reason) {
              return <RuntimeDiagnostic
                key={mount.runtimeInstanceId}
                diagnostic={asRuntimeDiagnostic(reason)}
              />;
            }
          })}
        </div>
      </div>
    </RuntimeLogicalViewport>

    {diagnostic ? <RuntimeDiagnostic diagnostic={diagnostic} /> : null}
  </div>;
}

function normalizeVisualActionWireKind(action: VisualNavigationActionEngineering): OperationalVisualAction {
  const wireKind = String((action as VisualNavigationActionEngineering & Readonly<{ kind: unknown }>).kind).trim();
  let kind: OperationalVisualAction['kind'];
  switch (wireKind) {
    case 'NavigateScreen':
    case 'navigateScreen': kind = 'NavigateScreen'; break;
    case 'OpenPopup':
    case 'openPopup': kind = 'OpenPopup'; break;
    case 'ClosePopup':
    case 'closePopup': kind = 'ClosePopup'; break;
    case 'ExecuteCommand':
    case 'executeCommand': kind = 'ExecuteCommand'; break;
    case 'SetTagValue':
    case 'setTagValue': kind = 'SetTagValue'; break;
    case 'ToggleTagBoolean':
    case 'toggleTagBoolean': kind = 'ToggleTagBoolean'; break;
    default:
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_ACTION_KIND_UNSUPPORTED',
        `Visual action '${action.eventKey}' has unsupported wire kind '${wireKind}'.`
      );
  }
  return Object.freeze({ ...(action as unknown as OperationalVisualAction), kind });
}

function isRuntimeWriteValue(value: unknown): value is string | number | boolean {
  return typeof value === 'boolean' || typeof value === 'string' || (typeof value === 'number' && Number.isFinite(value));
}

function resolveInitialNavigation(
  catalog: RuntimeVisualCatalog,
  initialScreenKey: string
): NavigationResolution {
  try {
    return Object.freeze({
      state: createRuntimeVisualNavigationState(catalog, initialScreenKey),
      diagnostic: null
    });
  } catch (reason) {
    return Object.freeze({ state: null, diagnostic: asRuntimeDiagnostic(reason) });
  }
}

function RuntimeDiagnostic({ diagnostic }: { diagnostic: RuntimeVisualCompositionError }) {
  return <div
    className="runtime-visual-diagnostic"
    role="alert"
    data-testid="runtime-visual-diagnostic"
    data-diagnostic-code={diagnostic.code}
  >
    <strong>{diagnostic.code}</strong>
    <span>{diagnostic.message}</span>
  </div>;
}

function asRuntimeDiagnostic(reason: unknown): RuntimeVisualCompositionError {
  if (reason instanceof RuntimeVisualCompositionError) return reason;
  if (reason instanceof RuntimeCommandExecutionError) {
    return new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_COMMAND_EXECUTION_FAILED',
      `Operational Command request failed (${reason.status}): ${reason.message}`
    );
  }
  return new RuntimeVisualCompositionError(
    'VISUAL_RUNTIME_COMPOSITION_FAILED',
    reason instanceof Error ? reason.message : String(reason)
  );
}
