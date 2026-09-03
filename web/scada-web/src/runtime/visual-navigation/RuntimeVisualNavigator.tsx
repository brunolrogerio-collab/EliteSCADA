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
import {
  createRuntimeVisualCatalog,
  createRuntimeVisualNavigationState,
  executeVisualNavigationAction,
  resolveActiveScreen,
  resolveMountedPopup,
  resolveVisualNavigationAction,
  RuntimeVisualCompositionError,
  type RuntimeVisualCatalog,
  type RuntimeVisualNavigationState
} from './runtimeVisualNavigationModel';
import { RuntimeVisualDefinitionRenderer } from './RuntimeVisualDefinitionRenderer';

export type RuntimeVisualNavigatorProps = Readonly<{
  engineeringPackage: Pick<EngineeringPackageView, 'screens' | 'popups' | 'dynamos'>;
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

  const designSize = resolveRuntimeLogicalSize(activeScreen.properties);

  const dispatch = (event: CanonicalVisualEvent, popupRuntimeInstanceId?: string) => {
    try {
      const action = resolveVisualNavigationAction(event.element, event.eventKey);
      if (!action) return;
      const next = executeVisualNavigationAction(catalog, state, action, {
        popupRuntimeInstanceId,
        popupIdFactory
      });
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
            scriptContext={scriptContext}
            onScriptDispatch={onScriptDispatch}
            onVisualEvent={event => dispatch(event)}
            visualAssetUrl={visualAssetUrl}
          />
        </section>

        <div className="runtime-visual-popup-layer" data-popup-count={state.popups.length}>
          {state.popups.map((mount, index) => {
            try {
              const popup = resolveMountedPopup(catalog, mount);
              return <section
                className="runtime-visual-popup"
                key={mount.runtimeInstanceId}
                data-popup-key={popup.key}
                data-popup-runtime-instance-id={mount.runtimeInstanceId}
                data-popup-stack-index={index}
              >
                <header className="runtime-visual-popup-header">
                  <strong>{popup.name || popup.key}</strong>
                  <code>{popup.key}</code>
                </header>
                <div
                  className="runtime-visual-popup-content"
                  style={resolveVisualDefinitionSurfaceStyle(popup.properties, visualAssetUrl)}
                >
                  <RuntimeVisualDefinitionRenderer
                    visualDefinitionId={popup.id ?? ''}
                    runtimeContextId={`popup:${mount.runtimeInstanceId}`}
                    elements={popup.elements}
                    emptyLabel={emptyLabel}
                    locale={locale}
                    dynamoDefinitions={engineeringPackage.dynamos}
                    scriptContext={scriptContext}
                    onScriptDispatch={onScriptDispatch}
                    onVisualEvent={event => dispatch(event, mount.runtimeInstanceId)}
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
  return new RuntimeVisualCompositionError(
    'VISUAL_RUNTIME_COMPOSITION_FAILED',
    reason instanceof Error ? reason.message : String(reason)
  );
}
