import React, { useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from '../api';
import type {
  DynamoEngineering,
  EngineeringPackageView,
  EngineeringSnapshot,
  ImportPreviewView,
  PopupEngineering,
  ScreenEngineering,
  VisualElementEngineering
} from '../types';
import type { EngineeringLocale } from '../i18n';
import { cloneEngineeringValue } from './visualEditorCanonicalModel';

type CommandEngineeringView = Readonly<{
  id?: string | null;
  key: string;
  name: string;
  enabled?: boolean;
}>;

type C16EngineeringPackage = EngineeringPackageView & {
  startupScreenId?: string | null;
  commands?: CommandEngineeringView[] | null;
};

type PositionedPopup = PopupEngineering & {
  x?: number | null;
  y?: number | null;
};

type OperationalAction = Readonly<{
  eventKey: string;
  kind: 'ExecuteCommand';
  targetKey?: string | null;
  commandId?: string | null;
  parameters?: Readonly<Record<string, unknown>> | null;
  version?: number;
}>;

type DefinitionKind = 'screen' | 'dynamo' | 'popup';

type Candidate = Readonly<{
  package: C16EngineeringPackage;
  changeVersion: number;
  label: string;
}>;

const LOGICAL_WIDTH = 1920;
const LOGICAL_HEIGHT = 1080;

export function HmiOperationalConfigurationPanel({
  snapshot,
  locale,
  onApplied
}: Readonly<{
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
}>) {
  const model = snapshot.package as C16EngineeringPackage;
  const screens = model.screens ?? [];
  const popups = (model.popups ?? []) as PositionedPopup[];
  const dynamos = model.dynamos ?? [];
  const commands = useMemo(
    () => (model.commands ?? []).filter(command => command.enabled !== false && Boolean(command.id)),
    [model.commands]
  );
  const copy = useMemo(() => text(locale), [locale]);

  const [startupScreenId, setStartupScreenId] = useState(model.startupScreenId ?? '');
  const [popupIdentity, setPopupIdentity] = useState(() => identity(popups[0]));
  const selectedPopup = popups.find(popup => identity(popup) === popupIdentity) ?? null;
  const [popupX, setPopupX] = useState(() => selectedPopup?.x ?? 0);
  const [popupY, setPopupY] = useState(() => selectedPopup?.y ?? 0);

  const [definitionKind, setDefinitionKind] = useState<DefinitionKind>('screen');
  const definitions = definitionChoices(definitionKind, screens, dynamos, popups);
  const [definitionIdentity, setDefinitionIdentity] = useState(() => definitions[0]?.identity ?? '');
  const selectedDefinition = definitions.find(item => item.identity === definitionIdentity) ?? definitions[0] ?? null;
  const elements = selectedDefinition ? flattenElements(selectedDefinition.elements) : [];
  const [visualObjectId, setVisualObjectId] = useState(() => elements[0]?.id ?? '');
  const [eventKey, setEventKey] = useState('click');
  const [commandId, setCommandId] = useState(() => commands[0]?.id ?? '');

  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<Candidate | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const invalidate = () => {
    setPreview(null);
    setCandidate(null);
    setError(null);
  };

  const previewCandidate = async (nextPackage: C16EngineeringPackage, label: string) => {
    setBusy(true);
    setPreview(null);
    setCandidate(null);
    setError(null);
    try {
      const before = await loadEngineeringWorkspace();
      const nextPreview = await previewEngineeringPackage(nextPackage);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion)
        throw new Error(copy.workspaceChanged);
      setPreview(nextPreview);
      setCandidate(Object.freeze({
        package: cloneEngineeringValue(nextPackage),
        changeVersion: after.changeVersion,
        label
      }));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  const applyCandidate = async () => {
    if (!candidate || !preview?.canApply) return;
    setBusy(true);
    setError(null);
    try {
      await applyEngineeringPackage(candidate.package, candidate.changeVersion);
      setPreview(null);
      setCandidate(null);
      await onApplied();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreview(null);
      setCandidate(null);
    } finally {
      setBusy(false);
    }
  };

  const previewStartup = () => {
    if (!startupScreenId) {
      setError(copy.startupRequired);
      return;
    }
    const screen = screens.find(item => item.id === startupScreenId);
    if (!screen?.id) {
      setError(copy.startupUnresolved);
      return;
    }
    const next = cloneEngineeringValue(model);
    next.startupScreenId = screen.id;
    void previewCandidate(next, copy.startupLabel);
  };

  const selectPopup = (nextIdentity: string) => {
    const popup = popups.find(item => identity(item) === nextIdentity) ?? null;
    setPopupIdentity(nextIdentity);
    setPopupX(popup?.x ?? 0);
    setPopupY(popup?.y ?? 0);
    invalidate();
  };

  const previewPopup = () => {
    if (!selectedPopup) {
      setError(copy.popupRequired);
      return;
    }
    if (!Number.isFinite(popupX) || !Number.isFinite(popupY)) {
      setError(copy.popupFinite);
      return;
    }
    const next = cloneEngineeringValue(model);
    next.popups = (next.popups ?? []).map(popup =>
      identity(popup) === popupIdentity ? { ...popup, x: popupX, y: popupY } : popup
    );
    void previewCandidate(next, copy.popupLabel);
  };

  const chooseDefinitionKind = (kind: DefinitionKind) => {
    const next = definitionChoices(kind, screens, dynamos, popups);
    setDefinitionKind(kind);
    setDefinitionIdentity(next[0]?.identity ?? '');
    setVisualObjectId(next[0] ? flattenElements(next[0].elements)[0]?.id ?? '' : '');
    invalidate();
  };

  const chooseDefinition = (nextIdentity: string) => {
    const next = definitionChoices(definitionKind, screens, dynamos, popups)
      .find(item => item.identity === nextIdentity) ?? null;
    setDefinitionIdentity(nextIdentity);
    setVisualObjectId(next ? flattenElements(next.elements)[0]?.id ?? '' : '');
    invalidate();
  };

  const previewCommandAction = () => {
    const normalizedEvent = eventKey.trim();
    if (!selectedDefinition || !visualObjectId) {
      setError(copy.visualObjectRequired);
      return;
    }
    if (!normalizedEvent) {
      setError(copy.eventRequired);
      return;
    }
    if (!commandId || !commands.some(command => command.id === commandId)) {
      setError(copy.commandRequired);
      return;
    }

    const action: OperationalAction = Object.freeze({
      eventKey: normalizedEvent,
      kind: 'ExecuteCommand',
      targetKey: null,
      commandId,
      parameters: null,
      version: 1
    });
    const next = cloneEngineeringValue(model);
    const updated = replaceElementAction(selectedDefinition.elements, visualObjectId, action);
    if (!updated.changed) {
      setError(copy.visualObjectUnresolved);
      return;
    }

    if (definitionKind === 'screen') {
      next.screens = (next.screens ?? []).map(screen =>
        identity(screen) === definitionIdentity ? { ...screen, elements: updated.elements } : screen
      );
    } else if (definitionKind === 'popup') {
      next.popups = (next.popups ?? []).map(popup =>
        identity(popup) === definitionIdentity ? { ...popup, elements: updated.elements } : popup
      );
    } else {
      next.dynamos = (next.dynamos ?? []).map(dynamo =>
        identity(dynamo) === definitionIdentity ? { ...dynamo, elements: updated.elements } : dynamo
      );
    }
    void previewCandidate(next, copy.commandLabel);
  };

  const issues = preview?.items.flatMap(item => item.issues ?? []) ?? [];

  return <section className="eng-panel" data-testid="hmi-operational-configuration">
    <header className="eng-section-header">
      <div>
        <span className="eng-eyebrow">W14-C16</span>
        <h2>{copy.title}</h2>
        <p>{copy.description}</p>
      </div>
    </header>

    <div style={{ display: 'grid', gap: 16, gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))' }}>
      <fieldset>
        <legend>{copy.startupTitle}</legend>
        <label>
          <span>{copy.startupScreen}</span>
          <select
            data-testid="hmi-startup-screen"
            value={startupScreenId}
            onChange={event => { setStartupScreenId(event.currentTarget.value); invalidate(); }}
          >
            <option value="">{copy.select}</option>
            {screens.filter(screen => Boolean(screen.id)).map(screen =>
              <option key={screen.id} value={screen.id}>{screen.name || screen.key} · {screen.key}</option>
            )}
          </select>
        </label>
        <p>{copy.startupHint}</p>
        <button type="button" className="secondary" disabled={busy} onClick={previewStartup} data-testid="hmi-startup-preview">{copy.preview}</button>
      </fieldset>

      <fieldset>
        <legend>{copy.popupTitle}</legend>
        <label><span>{copy.popup}</span><select data-testid="hmi-popup-select" value={popupIdentity} onChange={event => selectPopup(event.currentTarget.value)}>
          {popups.map(popup => <option key={identity(popup)} value={identity(popup)}>{popup.name || popup.key} · {popup.key}</option>)}
        </select></label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <label><span>X</span><input data-testid="hmi-popup-x" type="number" value={popupX} onChange={event => { setPopupX(Number(event.currentTarget.value)); invalidate(); }} /></label>
          <label><span>Y</span><input data-testid="hmi-popup-y" type="number" value={popupY} onChange={event => { setPopupY(Number(event.currentTarget.value)); invalidate(); }} /></label>
        </div>
        <p>{copy.popupHint} {LOGICAL_WIDTH}×{LOGICAL_HEIGHT}.</p>
        <button type="button" className="secondary" disabled={busy || !selectedPopup} onClick={previewPopup} data-testid="hmi-popup-preview">{copy.preview}</button>
      </fieldset>

      <fieldset>
        <legend>{copy.commandTitle}</legend>
        <label><span>{copy.definitionKind}</span><select data-testid="hmi-command-definition-kind" value={definitionKind} onChange={event => chooseDefinitionKind(event.currentTarget.value as DefinitionKind)}>
          <option value="screen">Screen</option><option value="dynamo">Dynamo</option><option value="popup">Popup</option>
        </select></label>
        <label><span>{copy.definition}</span><select data-testid="hmi-command-definition" value={selectedDefinition?.identity ?? ''} onChange={event => chooseDefinition(event.currentTarget.value)}>
          {definitions.map(item => <option key={item.identity} value={item.identity}>{item.label}</option>)}
        </select></label>
        <label><span>{copy.visualObject}</span><select data-testid="hmi-command-object" value={visualObjectId} onChange={event => { setVisualObjectId(event.currentTarget.value); invalidate(); }}>
          <option value="">{copy.select}</option>
          {elements.filter(element => Boolean(element.id)).map(element => <option key={element.id!} value={element.id!}>{element.key} · {element.type}</option>)}
        </select></label>
        <label><span>{copy.event}</span><input data-testid="hmi-command-event" value={eventKey} onChange={event => { setEventKey(event.currentTarget.value); invalidate(); }} /></label>
        <label><span>Command</span><select data-testid="hmi-command-select" value={commandId} onChange={event => { setCommandId(event.currentTarget.value); invalidate(); }}>
          <option value="">{copy.select}</option>
          {commands.map(command => <option key={command.id!} value={command.id!}>{command.name || command.key} · {command.key}</option>)}
        </select></label>
        <p>{copy.commandHint}</p>
        <button type="button" className="secondary" disabled={busy} onClick={previewCommandAction} data-testid="hmi-command-preview">{copy.preview}</button>
      </fieldset>
    </div>

    <div className="visual-editor-actions" style={{ marginTop: 16 }}>
      <button type="button" className="primary" disabled={busy || !preview?.canApply || !candidate} onClick={() => void applyCandidate()} data-testid="hmi-operational-apply">
        {busy ? copy.working : candidate ? `${copy.apply}: ${candidate.label}` : copy.apply}
      </button>
    </div>
    {error ? <pre role="alert" data-testid="hmi-operational-error">{error}</pre> : null}
    {preview ? <div className="visual-editor-preview-panel" data-testid="hmi-operational-preview-result">
      <strong>{preview.canApply ? copy.valid : copy.invalid}</strong>
      <span>{preview.createCount} {copy.creates} · {preview.updateCount} {copy.updates} · {preview.errorCount} {copy.errors}</span>
      {issues.length > 0 ? <div className="visual-editor-issues">{issues.map((issue, index) => <div key={`${issue.code}:${index}`} className={issue.isError ? 'error' : 'warning'}><strong>{issue.code}</strong><span>{issue.message}</span></div>)}</div> : null}
    </div> : null}
  </section>;
}

function identity(value: { id?: string | null; key: string } | null | undefined): string {
  if (!value) return '';
  return value.id ? `id:${value.id}` : `key:${value.key}`;
}

function definitionChoices(
  kind: DefinitionKind,
  screens: readonly ScreenEngineering[],
  dynamos: readonly DynamoEngineering[],
  popups: readonly PositionedPopup[]
): ReadonlyArray<{ identity: string; label: string; elements: readonly VisualElementEngineering[] }> {
  const values: ReadonlyArray<ScreenEngineering | DynamoEngineering | PositionedPopup> =
    kind === 'screen' ? screens : kind === 'popup' ? popups : dynamos;
  return values.map(value => ({
    identity: identity(value),
    label: `${value.name || value.key} · ${value.key}`,
    elements: value.elements ?? []
  }));
}

function flattenElements(elements: readonly VisualElementEngineering[]): VisualElementEngineering[] {
  const result: VisualElementEngineering[] = [];
  for (const element of elements) {
    result.push(element);
    result.push(...flattenElements(element.children ?? []));
  }
  return result;
}

function replaceElementAction(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  action: OperationalAction
): Readonly<{ elements: VisualElementEngineering[]; changed: boolean }> {
  let changed = false;
  const next = elements.map(element => {
    let current = element;
    if (current.id === objectId) {
      const actions = (current.actions ?? []).filter(existing =>
        existing.eventKey.trim().toLocaleLowerCase('en-US') !== action.eventKey.trim().toLocaleLowerCase('en-US')
      );
      current = {
        ...current,
        actions: [
          ...actions,
          action as unknown as NonNullable<VisualElementEngineering['actions']>[number]
        ]
      };
      changed = true;
    }
    if (current.children?.length) {
      const childResult = replaceElementAction(current.children, objectId, action);
      if (childResult.changed) {
        current = { ...current, children: childResult.elements };
        changed = true;
      }
    }
    return current;
  });
  return Object.freeze({ elements: next, changed });
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'HMI operational configuration', description: 'Canonical Startup Screen, Popup logical position and Operational Command actions.', startupTitle: 'Startup / Home', startupScreen: 'Startup Screen', startupHint: 'Runtime resolves this stable Screen identity. Missing or unresolved references are explicit errors, never lexical fallback.', popupTitle: 'Popup position', popup: 'Popup', popupHint: 'X/Y are persisted logical HMI coordinates. Finite off-canvas values are clamped by Runtime to the logical stage', commandTitle: 'Operational Command action', definitionKind: 'Definition type', definition: 'Definition', visualObject: 'Visual object', event: 'Event key', commandHint: 'The visual layer stores only a stable Command reference. Active existence, authorization, scope, execution and audit remain backend authority.', select: 'Select…', preview: 'Preview change', apply: 'Apply to Workspace', working: 'Working…', valid: 'Valid Engineering candidate', invalid: 'Invalid Engineering candidate', creates: 'creates', updates: 'updates', errors: 'errors', startupRequired: 'Select a Startup Screen.', startupUnresolved: 'Selected Startup Screen does not resolve to a stable Engineering identity.', popupRequired: 'Select a Popup.', popupFinite: 'Popup X/Y must be finite numbers.', visualObjectRequired: 'Select a visual definition and object.', visualObjectUnresolved: 'Selected visual object could not be resolved.', eventRequired: 'Event key is required.', commandRequired: 'Select a canonical enabled Command.', workspaceChanged: 'Engineering Workspace changed during validation. Reload and preview again.', startupLabel: 'Startup Screen', popupLabel: 'Popup X/Y', commandLabel: 'ExecuteCommand action'
  };
  if (locale === 'es') return {
    title: 'Configuración operativa HMI', description: 'Pantalla inicial, posición lógica de Popup y acciones de Comando Operativo canónicas.', startupTitle: 'Inicio / Home', startupScreen: 'Pantalla inicial', startupHint: 'Runtime resuelve esta identidad estable. Las referencias ausentes o no resueltas son errores explícitos, nunca fallback léxico.', popupTitle: 'Posición del Popup', popup: 'Popup', popupHint: 'X/Y son coordenadas lógicas HMI persistidas. Runtime limita valores finitos fuera del canvas al escenario lógico', commandTitle: 'Acción de Comando Operativo', definitionKind: 'Tipo de definición', definition: 'Definición', visualObject: 'Objeto visual', event: 'Evento', commandHint: 'La capa visual guarda solamente una referencia estable de Command. Existencia activa, autorización, alcance, ejecución y auditoría siguen bajo autoridad del backend.', select: 'Seleccionar…', preview: 'Preview del cambio', apply: 'Aplicar al Workspace', working: 'Procesando…', valid: 'Candidato Engineering válido', invalid: 'Candidato Engineering inválido', creates: 'creaciones', updates: 'actualizaciones', errors: 'errores', startupRequired: 'Seleccione una Pantalla inicial.', startupUnresolved: 'La Pantalla inicial no resuelve a una identidad estable.', popupRequired: 'Seleccione un Popup.', popupFinite: 'X/Y del Popup deben ser números finitos.', visualObjectRequired: 'Seleccione una definición y un objeto visual.', visualObjectUnresolved: 'No se pudo resolver el objeto visual.', eventRequired: 'El evento es obligatorio.', commandRequired: 'Seleccione una Command canónica habilitada.', workspaceChanged: 'Engineering Workspace cambió durante la validación. Recargue y valide de nuevo.', startupLabel: 'Pantalla inicial', popupLabel: 'Popup X/Y', commandLabel: 'Acción ExecuteCommand'
  };
  return {
    title: 'Configuração operacional da HMI', description: 'Tela inicial, posição lógica de Popup e ações de Comando Operacional como contratos canônicos.', startupTitle: 'Inicial / Home', startupScreen: 'Tela inicial', startupHint: 'O Runtime resolve esta identidade estável. Referência ausente ou não resolvida é erro explícito, nunca fallback lexical.', popupTitle: 'Posição do Popup', popup: 'Popup', popupHint: 'X/Y são coordenadas lógicas HMI persistidas. Valores finitos fora do canvas são limitados pelo Runtime ao stage lógico', commandTitle: 'Ação de Comando Operacional', definitionKind: 'Tipo de definição', definition: 'Definição', visualObject: 'Objeto visual', event: 'Evento', commandHint: 'A camada visual persiste apenas a referência estável da Command. Existência ativa, autorização, escopo, execução e audit continuam autoridade do backend.', select: 'Selecione…', preview: 'Preview da mudança', apply: 'Aplicar ao Workspace', working: 'Processando…', valid: 'Candidato Engineering válido', invalid: 'Candidato Engineering inválido', creates: 'criações', updates: 'atualizações', errors: 'erros', startupRequired: 'Selecione uma Tela inicial.', startupUnresolved: 'A Tela inicial selecionada não resolve para uma identidade estável.', popupRequired: 'Selecione um Popup.', popupFinite: 'X/Y do Popup precisam ser números finitos.', visualObjectRequired: 'Selecione uma definição e um objeto visual.', visualObjectUnresolved: 'O objeto visual selecionado não pôde ser resolvido.', eventRequired: 'O evento é obrigatório.', commandRequired: 'Selecione uma Command canônica habilitada.', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue e faça Preview novamente.', startupLabel: 'Tela inicial', popupLabel: 'Popup X/Y', commandLabel: 'Ação ExecuteCommand'
  };
}
