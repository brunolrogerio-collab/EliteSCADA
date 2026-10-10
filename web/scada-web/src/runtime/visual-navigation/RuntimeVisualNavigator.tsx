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
import { useMobileRuntime } from '../useMobileRuntime';
import { RuntimeActionFeedbackContext, actionFeedbackLabel, type RuntimeActionFeedback } from './RuntimeActionFeedback';
import { resolveRuntimeLogicalSize } from './runtimeLogicalCanvas';
import {
  executeRuntimeCommand,
  executeRuntimeRichCommand,
  loadRuntimeRichCommandDefinition,
  RuntimeCommandExecutionError,
  type RuntimeRichCommandDefinition,
  type RuntimeRichCommandOutcome
} from './runtimeCommandApi';
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
import { useOptionalHistoricalPlayback } from '../historical-playback/HistoricalPlaybackContext';
import { RuntimeHistoricalPlaybackReadOnlyError } from '../historical-playback/runtimeHistoricalPlaybackGuard';

export type RuntimeVisualNavigatorProps = Readonly<{
  engineeringPackage: Pick<EngineeringPackageView, 'screens' | 'popups' | 'dynamos' | 'equipment' | 'templates'>;
  initialScreenKey: string;
  mobileOrientation?: 'landscape' | 'portrait';
  mobileScreens?: Readonly<Record<string, string>>;
  locale?: EngineeringLocale;
  emptyLabel?: string;
  popupIdFactory?: () => string;
  scriptContext?: ScriptEngineeringContext | null;
  onScriptDispatch?: (records: readonly ClientVisualEventDispatchRecord[]) => void;
  visualAssetUrl?: VisualAssetUrlResolver;
  onVisualContextChange?: (context: Readonly<{ screenKey: string; popupKeys: readonly string[] }>) => void;
}>;

type NavigationResolution = Readonly<{
  state: RuntimeVisualNavigationState | null;
  diagnostic: RuntimeVisualCompositionError | null;
}>;

type OperationalVisualAction = Readonly<
  Omit<VisualNavigationActionEngineering, 'kind'> & {
    kind: 'NavigateScreen' | 'OpenPopup' | 'ClosePopup' | 'ExecuteCommand' | 'ExecuteRichCommand' | 'SetTagValue' | 'ToggleTagBoolean';
    commandId?: string | null;
    version?: number;
  }
>;

type RichCommandPrompt = Readonly<{ objectId: string; requestId: string; definition: RuntimeRichCommandDefinition }>;
type RichCommandFieldError = 'required' | 'invalid';

const POPUP_FALLBACK_TITLE: Readonly<Record<EngineeringLocale, string>> = Object.freeze({
  'pt-BR': 'Janela',
  en: 'Popup',
  es: 'Ventana'
});

export function RuntimeVisualNavigator({
  engineeringPackage,
  initialScreenKey,
  mobileOrientation = 'landscape',
  mobileScreens,
  locale = 'pt-BR',
  emptyLabel = 'Sem objetos visuais.',
  popupIdFactory,
  scriptContext,
  onScriptDispatch,
  visualAssetUrl,
  onVisualContextChange
}: RuntimeVisualNavigatorProps) {
  const playback = useOptionalHistoricalPlayback();
  const mobile = useMobileRuntime();
  const [actionFeedback, setActionFeedback] = useState<ReadonlyMap<string, RuntimeActionFeedback>>(new Map());
  const [richCommandPrompt, setRichCommandPrompt] = useState<RichCommandPrompt | null>(null);
  const [richCommandValues, setRichCommandValues] = useState<Readonly<Record<string, string>>>({});
  const [richCommandValidationErrors, setRichCommandValidationErrors] =
    useState<Readonly<Record<string, RichCommandFieldError>>>({});
  const richCommandPromptOwner = React.useRef<Readonly<{ objectId: string; requestId: string }> | null>(null);
  const richCommandSubmitting = React.useRef(false);
  const actionInFlight = React.useRef(new Set<string>());
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
    setActionFeedback(new Map());
  }, [catalog, initialScreenKey]);

  useEffect(() => {
    if (!state || !onVisualContextChange) return;
    try {
      const popupKeys = state.popups.map(mount => resolveMountedPopup(catalog, mount).key);
      onVisualContextChange(Object.freeze({
        screenKey: mobile ? mobileScreens?.[state.activeScreenKey] ?? state.activeScreenKey : state.activeScreenKey,
        popupKeys: Object.freeze(popupKeys)
      }));
    } catch {
      // The normal Runtime diagnostic path reports invalid composition.
    }
  }, [catalog, state, mobile, mobileScreens, onVisualContextChange]);

  if (!state) {
    return <RuntimeDiagnostic diagnostic={diagnostic ?? new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_SCREEN_NOT_FOUND',
      `Runtime initial Screen '${initialScreenKey}' could not be resolved.`
    )} />;
  }

  let activeScreen;
  try {
    activeScreen = resolveActiveScreen(catalog, state);
    const variant = mobile ? mobileScreens?.[activeScreen.key] : undefined;
    if (variant) activeScreen = resolveActiveScreen(catalog, { ...state, activeScreenKey: variant });
  } catch (reason) {
    return <RuntimeDiagnostic diagnostic={asRuntimeDiagnostic(reason)} />;
  }

  const designSize = resolveRuntimeLogicalSize();

  const dispatch = async (event: CanonicalVisualEvent, popupRuntimeInstanceId?: string) => {
    const objectId = event.element.id ?? event.element.key;
    let operational = false;
    let acquired = false;
    let retainInFlight = false;
    const requestId = crypto.randomUUID();
    const started = Date.now();
    const feedback = (status: RuntimeActionFeedback['state']) => setActionFeedback(previous => {
      if (status !== 'pending' && previous.get(objectId)?.requestId !== requestId) return previous;
      return new Map(previous).set(objectId, { state: status, label: actionFeedbackLabel(status, locale), requestId });
    });
    const confirmWrite = async (tagId: string, value: unknown) => {
      feedback('accepted');
      try {
        const sample = (await loadReadableRuntimeTags()).find(tag => tag.id.toLowerCase() === tagId.toLowerCase())?.current;
        if (sample && (sample.quality === 0 || String(sample.quality).toLowerCase() === 'good') &&
            sample.value === value && Date.parse(sample.timestamp) >= started) feedback('confirmed');
      } catch { /* Transport acceptance is not process confirmation; retain the truthful accepted state. */ }
    };
    try {
      const rawAction = resolveVisualNavigationAction(event.element, event.eventKey);
      if (!rawAction) return;
      const action = normalizeVisualActionWireKind(rawAction);
      operational = ['ExecuteCommand', 'ExecuteRichCommand', 'SetTagValue', 'ToggleTagBoolean'].includes(action.kind);
      if (operational) {
        if (actionInFlight.current.has(objectId)) return;
        actionInFlight.current.add(objectId); acquired = true; feedback('pending');
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
        feedback('accepted');
        setDiagnostic(null);
        return;
      }
      if (action.kind === 'ExecuteRichCommand') {
        const commandId = action.commandId?.trim();
        if (!commandId) {
          throw new RuntimeVisualCompositionError(
            'VISUAL_RUNTIME_RICH_COMMAND_REFERENCE_REQUIRED',
            `ExecuteRichCommand action '${action.eventKey}' does not contain a canonical Rich Command identity.`
          );
        }

        const definition = await loadRuntimeRichCommandDefinition(commandId);
        if (definition.parameters.length === 0) {
          const result = await executeRuntimeRichCommand(commandId, {});
          feedback(result.outcome);
          if (isRichCommandErrorOutcome(result.outcome)) {
            setDiagnostic(new RuntimeVisualCompositionError(
              'VISUAL_RUNTIME_RICH_COMMAND_' + result.outcome.toUpperCase(),
              result.message || result.code || 'Rich Command invocation did not complete.'
            ));
          } else {
            setDiagnostic(null);
          }
          return;
        }

        if (richCommandPromptOwner.current) {
          setActionFeedback(previous => {
            if (previous.get(objectId)?.requestId !== requestId) return previous;
            const next = new Map(previous);
            next.delete(objectId);
            return next;
          });
          return;
        }

        richCommandPromptOwner.current = { objectId, requestId };
        setRichCommandValues({});
        setRichCommandValidationErrors({});
        setRichCommandPrompt({ objectId, requestId, definition });
        retainInFlight = true;
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
        await confirmWrite(tagId, value);
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
        await confirmWrite(tag.id, !tag.current.value);
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
      if (operational) feedback('failed');
      setDiagnostic(asRuntimeDiagnostic(reason));
    } finally {
      if (acquired && !retainInFlight) actionInFlight.current.delete(objectId);
    }
  };

  const updateFeedback = (objectId: string, requestId: string, status: RuntimeActionFeedback['state']) => {
    setActionFeedback(previous => {
      if (previous.get(objectId)?.requestId !== requestId) return previous;
      return new Map(previous).set(objectId, { state: status, label: actionFeedbackLabel(status, locale), requestId });
    });
  };

  const cancelRichCommandPrompt = () => {
    if (!richCommandPrompt) return;
    if (richCommandPromptOwner.current?.requestId === richCommandPrompt.requestId) {
      richCommandPromptOwner.current = null;
    }
    actionInFlight.current.delete(richCommandPrompt.objectId);
    setActionFeedback(previous => {
      if (previous.get(richCommandPrompt.objectId)?.requestId !== richCommandPrompt.requestId) return previous;
      const next = new Map(previous);
      next.delete(richCommandPrompt.objectId);
      return next;
    });
    setRichCommandPrompt(null);
    setRichCommandValues({});
    setRichCommandValidationErrors({});
  };

  const submitRichCommandPrompt = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const prompt = richCommandPrompt;
    if (!prompt || richCommandSubmitting.current) return;
    const validationErrors = validateRichCommandPrompt(prompt.definition, richCommandValues);
    setRichCommandValidationErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) return;
    richCommandSubmitting.current = true;
    updateFeedback(prompt.objectId, prompt.requestId, 'pending');

    const parameters: Record<string, unknown> = {};
    for (const parameter of prompt.definition.parameters) {
      const rawValue = richCommandValues[parameter.key] ?? '';
      if (rawValue === '') continue;
      parameters[parameter.key] = parameter.schema.kind === 'Boolean'
        ? rawValue === 'true'
        : rawValue;
    }

    try {
      const result = await executeRuntimeRichCommand(prompt.definition.commandId, parameters);
      updateFeedback(prompt.objectId, prompt.requestId, result.outcome);
      if (isRichCommandErrorOutcome(result.outcome)) {
        setDiagnostic(new RuntimeVisualCompositionError(
          'VISUAL_RUNTIME_RICH_COMMAND_' + result.outcome.toUpperCase(),
          result.message || result.code || 'Rich Command invocation did not complete.'
        ));
      } else {
        setDiagnostic(null);
      }
    } catch (reason) {
      // The invocation may have reached Runtime; report ambiguity and never retry.
      updateFeedback(prompt.objectId, prompt.requestId, 'Unknown');
      setDiagnostic(new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_RICH_COMMAND_UNKNOWN',
        reason instanceof Error ? reason.message : 'The Rich Command outcome is unknown.'
      ));
    } finally {
      actionInFlight.current.delete(prompt.objectId);
      richCommandSubmitting.current = false;
      if (richCommandPromptOwner.current?.requestId === prompt.requestId) {
        richCommandPromptOwner.current = null;
      }
      setRichCommandPrompt(null);
      setRichCommandValues({});
      setRichCommandValidationErrors({});
    }
  };

  const richCommandCopy = locale === 'pt-BR'
    ? { title: 'Executar comando', required: 'obrigatório', value: 'Escolha um valor', cancel: 'Cancelar', submit: 'Executar', close: 'Fechar', validationRequired: 'Este campo é obrigatório.', validationInvalid: 'Valor inválido.' }
    : locale === 'es'
      ? { title: 'Ejecutar comando', required: 'obligatorio', value: 'Elige un valor', cancel: 'Cancelar', submit: 'Ejecutar', close: 'Cerrar', validationRequired: 'Este campo es obligatorio.', validationInvalid: 'Valor no válido.' }
      : { title: 'Execute command', required: 'required', value: 'Choose a value', cancel: 'Cancel', submit: 'Execute', close: 'Close', validationRequired: 'This field is required.', validationInvalid: 'Invalid value.' };

  const updateRichCommandValue = (key: string, value: string) => {
    setRichCommandValues(previous => ({ ...previous, [key]: value }));
    setRichCommandValidationErrors(previous => {
      if (!previous[key]) return previous;
      const next = { ...previous };
      delete next[key];
      return next;
    });
  };

  return <RuntimeActionFeedbackContext.Provider value={actionFeedback}><div
    className="runtime-visual-navigator"
    data-testid="runtime-visual-navigator"
    data-active-screen-key={state.activeScreenKey}
    data-runtime-temporal-mode={playback?.mode === 'historicalPlayback' ? 'historical-playback' : 'live'}
    data-runtime-historical-at={playback?.atUtc ?? undefined}
  >
    <RuntimeLogicalViewport designSize={designSize} mobileOrientation={mobileOrientation}>
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

    {actionFeedback.size > 0 ? <div
      role="status"
      aria-live="polite"
      data-testid="runtime-action-feedback"
      style={{ position: 'absolute', left: 8, bottom: 8, zIndex: 10001 }}
    >
      {Array.from(actionFeedback.entries()).map(([objectId, feedback]) => <div
        key={objectId}
        data-object-id={objectId}
        data-state={feedback.state}
      >{feedback.label}</div>)}
    </div> : null}
    {diagnostic ? <RuntimeDiagnostic diagnostic={diagnostic} /> : null}
    {richCommandPrompt ? <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="runtime-rich-command-title"
      data-testid="runtime-rich-command-parameters"
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        display: 'grid',
        placeItems: 'center',
        padding: 16,
        background: 'rgba(0, 0, 0, 0.48)'
      }}
    >
      <form
        noValidate
        onSubmit={event => { void submitRichCommandPrompt(event); }}
        style={{
          display: 'grid',
          gap: 12,
          width: 'min(440px, 100%)',
          maxHeight: '80vh',
          overflowY: 'auto',
          padding: 20,
          borderRadius: 8,
          background: 'var(--surface, #fff)',
          color: 'var(--text-primary, #222)',
          boxShadow: '0 12px 40px rgba(0,0,0,.3)'
        }}
      >
        <h2 id="runtime-rich-command-title">{richCommandPrompt.definition.semanticKey || richCommandCopy.title}</h2>
        {richCommandPrompt.definition.description ? <p>{richCommandPrompt.definition.description}</p> : null}
        {richCommandPrompt.definition.parameters.map(parameter => {
          const kind = parameter.schema.kind;
          const value = richCommandValues[parameter.key] ?? '';
          const label = parameter.description?.trim() || parameter.key;
          const fieldError = richCommandValidationErrors[parameter.key];
          return <label key={parameter.key} style={{ display: 'grid', gap: 4 }}>
            <span>{label}{parameter.required ? ' (' + richCommandCopy.required + ')' : ''}</span>
            {kind === 'Boolean' ? <select
              value={value}
              required={parameter.required}
              aria-invalid={Boolean(fieldError)}
              aria-describedby={fieldError ? 'runtime-rich-command-error-' + parameter.key : undefined}
              onChange={event => updateRichCommandValue(parameter.key, event.target.value)}
            >
              <option value="">{richCommandCopy.value}</option>
              <option value="true">true</option>
              <option value="false">false</option>
            </select> : kind === 'Enum' ? <select
              value={value}
              required={parameter.required}
              aria-invalid={Boolean(fieldError)}
              aria-describedby={fieldError ? 'runtime-rich-command-error-' + parameter.key : undefined}
              onChange={event => updateRichCommandValue(parameter.key, event.target.value)}
            >
              <option value="">{richCommandCopy.value}</option>
              {(parameter.schema.enumValues ?? []).map(option => <option key={option} value={option}>{option}</option>)}
            </select> : <input
              type="text"
              inputMode={kind === 'Integer' || kind === 'Number' || kind === 'Percentage' ? 'decimal' : undefined}
              value={value}
              required={parameter.required}
              maxLength={parameter.schema.maximumLength ?? undefined}
              aria-invalid={Boolean(fieldError)}
              aria-describedby={fieldError ? 'runtime-rich-command-error-' + parameter.key : undefined}
              onChange={event => updateRichCommandValue(parameter.key, event.target.value)}
            />}
            {fieldError ? <span
              id={'runtime-rich-command-error-' + parameter.key}
              role="alert"
            >{fieldError === 'required' ? richCommandCopy.validationRequired : richCommandCopy.validationInvalid}</span> : null}
          </label>;
        })}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button type="button" onClick={cancelRichCommandPrompt} disabled={richCommandSubmitting.current}>{richCommandCopy.cancel}</button>
          <button type="submit" disabled={richCommandSubmitting.current}>{richCommandCopy.submit}</button>
        </div>
      </form>
    </div> : null}
  </div></RuntimeActionFeedbackContext.Provider>;
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
    case 'ExecuteRichCommand':
    case 'executeRichCommand': kind = 'ExecuteRichCommand'; break;
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
  if (kind === 'ExecuteRichCommand' && action.version !== 2) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_RICH_COMMAND_ACTION_VERSION_UNSUPPORTED',
      `ExecuteRichCommand action '${action.eventKey}' must use action Version 2.`
    );
  }
  return Object.freeze({ ...(action as unknown as OperationalVisualAction), kind });
}

function isRichCommandErrorOutcome(outcome: RuntimeRichCommandOutcome): boolean {
  return outcome === 'Rejected' || outcome === 'Failed' || outcome === 'TimedOut' || outcome === 'Unknown';
}

function isRuntimeWriteValue(value: unknown): value is string | number | boolean {
  return typeof value === 'boolean' || typeof value === 'string' || (typeof value === 'number' && Number.isFinite(value));
}

function validateRichCommandPrompt(
  definition: RuntimeRichCommandDefinition,
  values: Readonly<Record<string, string>>
): Readonly<Record<string, RichCommandFieldError>> {
  const errors: Record<string, RichCommandFieldError> = {};
  for (const parameter of definition.parameters) {
    const rawValue = values[parameter.key] ?? '';
    if (rawValue === '') {
      if (parameter.required) errors[parameter.key] = 'required';
      continue;
    }

    const { kind, minimum, maximum, maximumLength, enumValues } = parameter.schema;
    if (kind === 'Boolean' && rawValue !== 'true' && rawValue !== 'false') {
      errors[parameter.key] = 'invalid';
      continue;
    }
    if (kind === 'Enum' && !(enumValues ?? []).includes(rawValue)) {
      errors[parameter.key] = 'invalid';
      continue;
    }
    if (kind === 'String' && maximumLength != null && rawValue.length > maximumLength) {
      errors[parameter.key] = 'invalid';
      continue;
    }

    if (kind === 'Integer' || kind === 'Number' || kind === 'Percentage') {
      const candidate = rawValue.trim();
      const numericValue = candidate === '' ? Number.NaN : Number(candidate);
      if (!Number.isFinite(numericValue) ||
          (kind === 'Integer' && !Number.isInteger(numericValue)) ||
          (minimum != null && numericValue < minimum) ||
          (maximum != null && numericValue > maximum)) {
        errors[parameter.key] = 'invalid';
      }
    }
  }
  return errors;
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
  if (reason instanceof RuntimeHistoricalPlaybackReadOnlyError) {
    return new RuntimeVisualCompositionError(
      'HISTORICAL_PLAYBACK_READ_ONLY',
      reason.message
    );
  }
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
