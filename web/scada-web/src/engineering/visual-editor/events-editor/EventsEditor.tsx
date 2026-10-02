import React, { useEffect, useMemo, useState } from 'react';
import { loadEngineeringSnapshot } from '../../api';
import {
  buildProjectReferenceCatalog,
  type ClientMemoryDefinitionView
} from '../../project-reference/projectReferenceModel';
import {
  MINIMUM_SCRIPT_TIMER_INTERVAL_MS,
  validateVisualEventReference
} from '../../scripts/ScriptEngineeringWorkspace.logic';
import {
  applyScriptMutation,
  loadScriptEngineeringContext,
  previewScriptMutation
} from '../../scripts/scriptEngineeringApi';
import type {
  ScriptEngineeringDefinition,
  ScriptEngineeringEntryPoint,
  ScriptEngineeringEventKind,
  ScriptMutationPreviewToken,
  ScriptVisualEventReference
} from '../../scripts/scriptEngineeringTypes';
import type { VisualElementEngineering } from '../../types';
import { initializeClientMemory } from '../../../runtime/clientMemory';
import type { VisualEditorBindingSourceCatalogItem, VisualEditorMutationIntent } from '../visualEditorContracts';
import type { VisualNavigationActionEngineering } from '../../../runtime/visual-navigation/runtimeVisualNavigationModel';

type EventsEditorProps = {
  visualDefinitionId?: string | null;
  visualObjectId?: string | null;
  sourceCatalog?: readonly VisualEditorBindingSourceCatalogItem[];
  element?: VisualElementEngineering;
  onMutationIntent?: (intent: VisualEditorMutationIntent) => void;
  disabled?: boolean;
  onApplied?: () => Promise<void> | void;
};

type EventChoice = 'click' | 'pointerEnter' | 'pointerMove' | 'pointerLeave' | 'initialize' | 'dispose' | 'tagChanged' | 'clientMemoryChanged' | 'timer';

const EVENT_CHOICES: ReadonlyArray<{ value: EventChoice; label: string; eventKind: ScriptEngineeringEventKind; eventKey?: string }> = [
  { value: 'click', label: 'Click', eventKind: 'objectInteraction', eventKey: 'click' },
  { value: 'pointerEnter', label: 'Pointer enters object', eventKind: 'objectInteraction', eventKey: 'pointerenter' },
  { value: 'pointerMove', label: 'Pointer moves over object', eventKind: 'objectInteraction', eventKey: 'pointermove' },
  { value: 'pointerLeave', label: 'Pointer leaves object', eventKind: 'objectInteraction', eventKey: 'pointerleave' },
  { value: 'initialize', label: 'Initialize', eventKind: 'initialize' },
  { value: 'dispose', label: 'Dispose', eventKind: 'dispose' },
  { value: 'tagChanged', label: 'TAG value change', eventKind: 'tagChanged' },
  { value: 'clientMemoryChanged', label: 'Client Memory change', eventKind: 'clientMemoryChanged' },
  { value: 'timer', label: 'Timer', eventKind: 'timer' }
];

export function EventsEditor({
  visualDefinitionId,
  visualObjectId,
  sourceCatalog,
  element,
  onMutationIntent,
  disabled = false,
  onApplied
}: EventsEditorProps) {
  const [scripts, setScripts] = useState<readonly ScriptEngineeringDefinition[]>([]);
  const [references, setReferences] = useState<readonly ScriptVisualEventReference[]>([]);
  const [resolvedVisualDefinitionId, setResolvedVisualDefinitionId] = useState<string | null>(visualDefinitionId ?? null);
  const [resolvedSourceCatalog, setResolvedSourceCatalog] = useState<readonly VisualEditorBindingSourceCatalogItem[]>(sourceCatalog ?? Object.freeze([]));
  const [visualTargets, setVisualTargets] = useState<Readonly<{ screens: readonly { key: string; name: string }[]; popups: readonly { key: string; name: string }[] }>>({ screens: [], popups: [] });
  const [quickActionKind, setQuickActionKind] = useState<'setValue' | 'toggleBoolean' | 'setTrue' | 'setFalse' | 'openPopup' | 'openScreen'>('setValue');
  const [quickTargetId, setQuickTargetId] = useState('');
  const [quickValue, setQuickValue] = useState('');
  const [choice, setChoice] = useState<EventChoice>('click');
  const [scriptId, setScriptId] = useState('');
  const [entryPoint, setEntryPoint] = useState('');
  const [targetId, setTargetId] = useState('');
  const [tagBitIndex, setTagBitIndex] = useState('');
  const [timerIntervalMs, setTimerIntervalMs] = useState(1000);
  const [previewToken, setPreviewToken] = useState<ScriptMutationPreviewToken | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const eventKind = EVENT_CHOICES.find(item => item.value === choice)!.eventKind;
  const eventKey = EVENT_CHOICES.find(item => item.value === choice)!.eventKey ?? 'click';
  const selectedScript = scripts.find(script => script.id === scriptId) ?? null;
  const matchingEntryPoints = useMemo(
    () => (selectedScript?.entryPoints ?? []).filter(item => item.eventKind === eventKind),
    [selectedScript, eventKind]
  );
  const selectedEntryPoint = matchingEntryPoints.find(item => item.handlerName === entryPoint) ?? null;
  const tagTargets = useMemo(
    () => resolvedSourceCatalog.filter(item => item.kind === 'Tag' && item.tagReference?.tagId && item.writable !== false),
    [resolvedSourceCatalog]
  );
  const quickTagTargets = useMemo(() => tagTargets.filter(item =>
    quickActionKind === 'toggleBoolean' || quickActionKind === 'setTrue' || quickActionKind === 'setFalse'
      ? isBooleanDataType(item.dataType)
      : true), [tagTargets, quickActionKind]);
  const quickNavigationTargets = quickActionKind === 'openPopup' ? visualTargets.popups : visualTargets.screens;
  const selectedQuickTag = quickTagTargets.find(item => item.tagReference?.tagId === quickTargetId) ?? null;
  const configuredActions = element?.actions ?? [];
  const selectedTagTarget = tagTargets.find(item => item.tagReference?.tagId === targetId) ?? null;
  const memoryTargets = useMemo(
    () => resolvedSourceCatalog.filter(item => item.kind === 'ClientMemory' && item.tagReference?.tagId),
    [resolvedSourceCatalog]
  );
  const applicableReferences = useMemo(
    () => references.filter(reference =>
      reference.visualDefinitionId === resolvedVisualDefinitionId &&
      ((reference.visualObjectId ?? null) === (visualObjectId ?? null) || reference.visualObjectId == null)),
    [references, resolvedVisualDefinitionId, visualObjectId]
  );

  const reload = async () => {
    const context = await loadScriptEngineeringContext();
    setScripts(context.scripts.filter(script => script.scope === 'clientVisual' && script.enabled));
    setReferences(context.visualEventReferences);
  };

  useEffect(() => {
    let cancelled = false;
    void loadScriptEngineeringContext()
      .then(context => {
        if (cancelled) return;
        setScripts(context.scripts.filter(script => script.scope === 'clientVisual' && script.enabled));
        setReferences(context.visualEventReferences);
      })
      .catch(reason => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (visualDefinitionId) setResolvedVisualDefinitionId(visualDefinitionId);
    if (sourceCatalog) setResolvedSourceCatalog(sourceCatalog);
    if (visualDefinitionId && sourceCatalog) return;

    let cancelled = false;
    void Promise.all([loadEngineeringSnapshot(), initializeClientMemory()])
      .then(([snapshot, memoryDefinitions]) => {
        if (cancelled) return;
        setVisualTargets({
          screens: (snapshot.package.screens ?? []).map(item => ({ key: item.key, name: item.name ?? item.key })),
          popups: (snapshot.package.popups ?? []).map(item => ({ key: item.key, name: item.name ?? item.key }))
        });
        if (!visualDefinitionId) {
          const screen = (snapshot.package.screens ?? []).find(candidate =>
            Boolean(visualObjectId) && containsVisualObject(candidate.elements ?? [], visualObjectId!));
          setResolvedVisualDefinitionId(screen?.id ?? null);
        }
        if (!sourceCatalog) {
          const memoryViews: readonly ClientMemoryDefinitionView[] = memoryDefinitions.map(definition => ({
            id: definition.id,
            name: definition.name,
            path: definition.path,
            dataType: definition.dataType,
            initialValue: definition.initialValue,
            readOnly: definition.readOnly
          }));
          const catalog = buildProjectReferenceCatalog(snapshot.package, memoryViews)
            .filter(reference => reference.bindingKind === 'Tag' || reference.bindingKind === 'ClientMemory')
            .map(reference => ({
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
            } satisfies VisualEditorBindingSourceCatalogItem));
          setResolvedSourceCatalog(Object.freeze(catalog));
        }
      })
      .catch(reason => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { cancelled = true; };
  }, [sourceCatalog, visualDefinitionId, visualObjectId]);

  useEffect(() => {
    setPreviewToken(null);
    setError(null);
    setEntryPoint('');
    setTargetId('');
    setTagBitIndex('');
  }, [choice, scriptId, resolvedVisualDefinitionId, visualObjectId]);

  useEffect(() => {
    setTagBitIndex('');
    setPreviewToken(null);
  }, [targetId]);

  const buildReference = (): ScriptVisualEventReference => {
    if (!resolvedVisualDefinitionId) throw new Error('Apply the visual definition before authoring events.');
    if (!selectedScript || !selectedEntryPoint) throw new Error('Select a valid Script entry point.');
    if (eventKind === 'objectInteraction' && !visualObjectId) throw new Error('Select one visual object for an object interaction event.');

    const reference: ScriptVisualEventReference = {
      visualDefinitionId: resolvedVisualDefinitionId,
      visualObjectId: choice === 'initialize' || choice === 'dispose' || choice === 'timer' ? null : visualObjectId ?? null,
      eventKind,
      ...(eventKind === 'objectInteraction' ? { eventKey } : {}),
      scriptId: selectedScript.id,
      entryPoint: selectedEntryPoint.handlerName,
      targetReference: null,
      tagReference: null,
      timerIntervalMs: null
    };

    if (choice === 'timer') {
      reference.timerIntervalMs = timerIntervalMs;
    } else if (choice === 'tagChanged') {
      const target = selectedTagTarget;
      if (!target?.tagReference?.tagId) throw new Error('Select a canonical TAG target.');

      let selector = target.tagReference.selector ? { ...target.tagReference.selector } : null;
      if (tagBitIndex.trim()) {
        const capability = target.selectorCapability;
        const bitIndex = Number(tagBitIndex);
        if (!capability || capability.kind !== 'bit')
          throw new Error('This TAG does not support bit selection.');
        if (!Number.isInteger(bitIndex) || bitIndex < capability.minIndex || bitIndex > capability.maxIndex)
          throw new Error(`Bit index must be between ${capability.minIndex} and ${capability.maxIndex}.`);
        selector = { kind: 'bit', index: bitIndex };
      }

      reference.tagReference = {
        tagId: target.tagReference.tagId,
        selector
      };
    } else if (choice === 'clientMemoryChanged') {
      const target = memoryTargets.find(item => item.tagReference?.tagId === targetId || item.target === targetId);
      if (!target?.tagReference?.tagId) throw new Error('Select a Client Memory definition.');
      reference.targetReference = target.tagReference.tagId;
    }

    const issues = validateVisualEventReference(reference);
    if (issues.length > 0) throw new Error(`Invalid event association: ${issues.join(', ')}.`);
    return reference;
  };

  const buildEntryPoint = (reference: ScriptVisualEventReference): ScriptEngineeringEntryPoint => ({
    eventKind: reference.eventKind,
    handlerName: reference.entryPoint,
    targetReference: reference.targetReference ?? null,
    tagReference: reference.tagReference ? {
      tagId: reference.tagReference.tagId,
      selector: reference.tagReference.selector ? { ...reference.tagReference.selector } : null
    } : null,
    timerIntervalMs: reference.timerIntervalMs ?? null
  });

  const candidateScript = (reference: ScriptVisualEventReference): ScriptEngineeringDefinition => {
    if (!selectedScript) throw new Error('Select a Script.');
    const configured = buildEntryPoint(reference);
    return {
      ...selectedScript,
      entryPoints: selectedScript.entryPoints.map(existing =>
        existing.eventKind === configured.eventKind && existing.handlerName === configured.handlerName
          ? configured
          : { ...existing })
    };
  };

  const preview = async () => {
    setBusy(true);
    setError(null);
    setPreviewToken(null);
    try {
      const reference = buildReference();
      const script = candidateScript(reference);
      const nextReferences = [
        ...references.filter(item => !(
          item.visualDefinitionId === reference.visualDefinitionId &&
          (item.visualObjectId ?? null) === (reference.visualObjectId ?? null) &&
          item.eventKind === reference.eventKind &&
          (item.eventKey ?? 'click').toLocaleLowerCase('en-US') === eventKey.toLocaleLowerCase('en-US') &&
          item.scriptId === reference.scriptId &&
          item.entryPoint === reference.entryPoint
        )),
        reference
      ];
      const token = await previewScriptMutation(script, nextReferences, 'UpdateExisting');
      setPreviewToken(token);
      if (!token.preview.canApply) setError('Engineering Preview rejected this event association.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  const apply = async () => {
    if (!previewToken?.preview.canApply) return;
    setBusy(true);
    setError(null);
    try {
      await applyScriptMutation(previewToken);
      await reload();
      setPreviewToken(null);
      await onApplied?.();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreviewToken(null);
    } finally {
      setBusy(false);
    }
  };

  const unavailable = disabled || !resolvedVisualDefinitionId;

  const addQuickAction = () => {
    if (!element?.id || !onMutationIntent) return;
    let action: VisualNavigationActionEngineering;
    switch (quickActionKind) {
      case 'setValue': {
        if (!selectedQuickTag?.tagReference?.tagId) return;
        const value = parseTagValue(quickValue, selectedQuickTag.dataType);
        if (value === undefined) return;
        action = { eventKey: 'click', kind: 'SetTagValue', targetKey: selectedQuickTag.tagReference.tagId, parameters: { value }, version: 1 };
        break;
      }
      case 'toggleBoolean':
      case 'setTrue':
      case 'setFalse':
        if (!selectedQuickTag?.tagReference?.tagId) return;
        action = quickActionKind === 'toggleBoolean'
          ? { eventKey: 'click', kind: 'ToggleTagBoolean', targetKey: selectedQuickTag.tagReference.tagId, version: 1 }
          : { eventKey: 'click', kind: 'SetTagValue', targetKey: selectedQuickTag.tagReference.tagId, parameters: { value: quickActionKind === 'setTrue' }, version: 1 };
        break;
      case 'openPopup':
        if (!quickTargetId) return;
        action = { eventKey: 'click', kind: 'OpenPopup', targetKey: quickTargetId, version: 1 };
        break;
      case 'openScreen':
        if (!quickTargetId) return;
        action = { eventKey: 'click', kind: 'NavigateScreen', targetKey: quickTargetId, version: 1 };
        break;
    }
    onMutationIntent({ kind: 'visualAction.set', objectId: element.id, action });
    setQuickTargetId('');
    setQuickValue('');
  };

  return <section className="visual-editor-events" data-testid="visual-events-editor">
    <header><strong>Events</strong><span>Actions run when the object is clicked</span></header>
    {unavailable ? <p>Apply this visual object before editing canonical event associations.</p> : <>
      <section className="visual-editor-events__quick" aria-label="Create automatic event">
        <strong>Quick event</strong>
        <label><span>When</span><select value={quickActionKind} onChange={event => { setQuickActionKind(event.currentTarget.value as typeof quickActionKind); setQuickTargetId(''); setQuickValue(''); }} data-testid="visual-events-quick-kind">
          <option value="setValue">Set variable value</option>
          <option value="toggleBoolean">Toggle Boolean</option>
          <option value="setTrue">Set Boolean true</option>
          <option value="setFalse">Set Boolean false</option>
          <option value="openPopup">Open popup</option>
          <option value="openScreen">Open screen</option>
        </select></label>
        {quickActionKind === 'openPopup' || quickActionKind === 'openScreen' ? <label><span>{quickActionKind === 'openPopup' ? 'Popup' : 'Screen'}</span><select value={quickTargetId} onChange={event => setQuickTargetId(event.currentTarget.value)} data-testid="visual-events-quick-target">
          <option value="">Select destination</option>
          {quickNavigationTargets.map(target => <option key={target.key} value={target.key}>{target.name}</option>)}
        </select></label> : <>
          <label><span>Variable (TAG)</span><select value={quickTargetId} onChange={event => { setQuickTargetId(event.currentTarget.value); setQuickValue(''); }} data-testid="visual-events-quick-target">
            <option value="">Select variable</option>
            {quickTagTargets.map(target => <option key={target.tagReference!.tagId} value={target.tagReference!.tagId}>{target.label}</option>)}
          </select></label>
          {quickActionKind === 'setValue' && selectedQuickTag ? <label><span>Value</span>{isBooleanDataType(selectedQuickTag.dataType)
            ? <select value={quickValue} onChange={event => setQuickValue(event.currentTarget.value)} data-testid="visual-events-quick-value"><option value="">Select value</option><option value="true">True</option><option value="false">False</option></select>
            : <input value={quickValue} onChange={event => setQuickValue(event.currentTarget.value)} inputMode={isNumericDataType(selectedQuickTag.dataType) ? 'decimal' : 'text'} data-testid="visual-events-quick-value" />}</label> : null}
        </>}
        <small>One automatic action per trigger. This wizard uses Click. After adding, use the editor&apos;s Preview and Apply actions below to save it.</small>
        <button type="button" className="secondary" disabled={!onMutationIntent || !element?.id || (quickActionKind === 'openPopup' || quickActionKind === 'openScreen' ? !quickTargetId : !selectedQuickTag?.tagReference?.tagId || (quickActionKind === 'setValue' && parseTagValue(quickValue, selectedQuickTag?.dataType) === undefined))} onClick={addQuickAction} data-testid="visual-events-quick-add">{configuredActions.some(action => action.eventKey.toLocaleLowerCase('en-US') === 'click') ? 'Replace click event' : 'Add event'}</button>
        {configuredActions.map((action, index) => <div className="visual-editor-events__configured" key={`${action.eventKey}:${index}`}>
          <code>{formatQuickAction(action, resolvedSourceCatalog, visualTargets.screens, visualTargets.popups)}</code>
          <button type="button" aria-label={`Remove ${action.eventKey} event`} disabled={!onMutationIntent || !element?.id} onClick={() => onMutationIntent?.({ kind: 'visualAction.remove', objectId: element!.id!, eventKey: action.eventKey })}>Remove</button>
        </div>)}
      </section>
      <details className="visual-editor-events__scripts">
        <summary>Python script associations</summary>
      <p>Select an enabled client-visual Script with a matching event handler to preview and apply a Python association. Automatic actions above do not require a Script.</p>
      <label><span>Event</span><select data-testid="visual-events-event" value={choice} onChange={event => setChoice(event.currentTarget.value as EventChoice)}>
        {EVENT_CHOICES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select></label>
      <label><span>Script</span><select data-testid="visual-events-script" value={scriptId} onChange={event => setScriptId(event.currentTarget.value)}>
        <option value="">Select Script</option>
        {scripts.map(script => <option key={script.id} value={script.id}>{script.name} · {script.path}</option>)}
      </select></label>
      <label><span>Entry point</span><select data-testid="visual-events-entry-point" value={entryPoint} onChange={event => setEntryPoint(event.currentTarget.value)} disabled={!selectedScript}>
        <option value="">Select handler</option>
        {matchingEntryPoints.map(item => <option key={`${item.eventKind}:${item.handlerName}`} value={item.handlerName}>{item.handlerName}</option>)}
      </select></label>

      {choice === 'tagChanged' ? <>
        <label><span>TAG target</span><select value={targetId} onChange={event => setTargetId(event.currentTarget.value)}>
          <option value="">Select TAG</option>
          {tagTargets.map(target => <option key={`${target.tagReference!.tagId}:${target.target}`} value={target.tagReference!.tagId}>{target.label}</option>)}
        </select></label>
        {selectedTagTarget?.selectorCapability?.kind === 'bit' ? <label>
          <span>Bit selector (optional)</span>
          <input
            type="number"
            min={selectedTagTarget.selectorCapability.minIndex}
            max={selectedTagTarget.selectorCapability.maxIndex}
            step="1"
            value={tagBitIndex}
            placeholder="Whole TAG"
            onChange={event => setTagBitIndex(event.currentTarget.value)}
            data-testid="visual-events-tag-bit"
          />
        </label> : null}
      </> : null}

      {choice === 'clientMemoryChanged' ? <label><span>Client Memory</span><select value={targetId} onChange={event => setTargetId(event.currentTarget.value)}>
        <option value="">Select definition</option>
        {memoryTargets.map(target => <option key={target.tagReference!.tagId} value={target.tagReference!.tagId}>{target.label}</option>)}
      </select></label> : null}

      {choice === 'timer' ? <label><span>Interval (ms)</span><input type="number" min={MINIMUM_SCRIPT_TIMER_INTERVAL_MS} step="1" value={timerIntervalMs} onChange={event => setTimerIntervalMs(Number(event.currentTarget.value))} /></label> : null}

      <div className="visual-editor-events-actions">
        <button type="button" className="secondary" disabled={busy || !selectedEntryPoint} onClick={() => void preview()} data-testid="visual-events-preview">{busy ? 'Working…' : 'Preview event'}</button>
        <button type="button" className="primary" disabled={busy || !previewToken?.preview.canApply} onClick={() => void apply()} data-testid="visual-events-apply">Apply event</button>
      </div>
      {error ? <pre>{error}</pre> : null}
      {previewToken ? <small>{previewToken.preview.canApply ? 'Validated Engineering candidate.' : 'Invalid Engineering candidate.'}</small> : null}
      <div className="visual-editor-events-list">
        {applicableReferences.map((reference, index) => <code key={`${reference.scriptId}:${reference.entryPoint}:${index}`}>
          {reference.eventKind === 'objectInteraction' ? (reference.eventKey ?? 'click') : reference.eventKind} → {reference.entryPoint}{formatTagSelector(reference)}
        </code>)}
      </div>
      </details>
    </>}
  </section>;
}

function isBooleanDataType(value?: string | null): boolean {
  return /^(bool|boolean)$/i.test(value?.trim() ?? '');
}

function isNumericDataType(value?: string | null): boolean {
  return /(int|uint|float|double|decimal|number|single)/i.test(value?.trim() ?? '');
}

function parseTagValue(raw: string, dataType?: string | null): string | number | boolean | undefined {
  if (isBooleanDataType(dataType)) return raw === 'true' ? true : raw === 'false' ? false : undefined;
  if (isNumericDataType(dataType)) {
    if (!raw.trim()) return undefined;
    const value = Number(raw.trim().replace(',', '.'));
    return Number.isFinite(value) ? value : undefined;
  }
  return raw;
}

function formatQuickAction(
  action: VisualNavigationActionEngineering,
  catalog: readonly VisualEditorBindingSourceCatalogItem[],
  screens: readonly { key: string; name: string }[],
  popups: readonly { key: string; name: string }[]
): string {
  const kind = String(action.kind).toLocaleLowerCase('en-US');
  const name = kind === 'openpopup'
    ? popups.find(item => item.key === action.targetKey)?.name ?? action.targetKey ?? ''
    : kind === 'navigatescreen'
      ? screens.find(item => item.key === action.targetKey)?.name ?? action.targetKey ?? ''
      : catalog.find(item => item.tagReference?.tagId === action.targetKey)?.label ?? action.targetKey ?? '';
  if (kind === 'toggletagboolean') return `Click → Toggle ${name}`;
  if (kind === 'settagvalue') return `Click → Set ${name} = ${String(action.parameters?.value ?? '')}`;
  return `Click → ${kind === 'openpopup' ? 'Open popup' : kind === 'navigatescreen' ? 'Open screen' : action.kind} ${name}`;
}

function formatTagSelector(reference: ScriptVisualEventReference): string {
  const selector = reference.tagReference?.selector;
  return selector?.kind === 'bit'
    ? ` · bit ${selector.index.toString().padStart(2, '0')}`
    : '';
}

function containsVisualObject(elements: readonly VisualElementEngineering[], objectId: string): boolean {
  for (const element of elements) {
    if (element.id === objectId) return true;
    if (element.children && containsVisualObject(element.children, objectId)) return true;
  }
  return false;
}
