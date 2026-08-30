import React, { useEffect, useMemo, useState } from 'react';
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
import type { VisualEditorBindingSourceCatalogItem } from '../visualEditorContracts';

const MINIMUM_TIMER_INTERVAL_MS = 50;

type EventsEditorProps = {
  visualDefinitionId?: string | null;
  visualObjectId?: string | null;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  disabled?: boolean;
  onApplied?: () => Promise<void> | void;
};

type EventChoice = 'click' | 'initialize' | 'dispose' | 'tagChanged' | 'clientMemoryChanged' | 'timer';

const EVENT_CHOICES: ReadonlyArray<{ value: EventChoice; label: string; eventKind: ScriptEngineeringEventKind }> = [
  { value: 'click', label: 'Click', eventKind: 'objectInteraction' },
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
  disabled = false,
  onApplied
}: EventsEditorProps) {
  const [scripts, setScripts] = useState<readonly ScriptEngineeringDefinition[]>([]);
  const [references, setReferences] = useState<readonly ScriptVisualEventReference[]>([]);
  const [choice, setChoice] = useState<EventChoice>('click');
  const [scriptId, setScriptId] = useState('');
  const [entryPoint, setEntryPoint] = useState('');
  const [targetId, setTargetId] = useState('');
  const [timerIntervalMs, setTimerIntervalMs] = useState(1000);
  const [previewToken, setPreviewToken] = useState<ScriptMutationPreviewToken | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const eventKind = EVENT_CHOICES.find(item => item.value === choice)!.eventKind;
  const selectedScript = scripts.find(script => script.id === scriptId) ?? null;
  const matchingEntryPoints = useMemo(
    () => (selectedScript?.entryPoints ?? []).filter(item => item.eventKind === eventKind),
    [selectedScript, eventKind]
  );
  const selectedEntryPoint = matchingEntryPoints.find(item => item.handlerName === entryPoint) ?? null;
  const tagTargets = useMemo(
    () => sourceCatalog.filter(item => item.kind === 'Tag' && item.tagReference?.tagId),
    [sourceCatalog]
  );
  const memoryTargets = useMemo(
    () => sourceCatalog.filter(item => item.kind === 'ClientMemory' && item.tagReference?.tagId),
    [sourceCatalog]
  );
  const applicableReferences = useMemo(
    () => references.filter(reference =>
      reference.visualDefinitionId === visualDefinitionId &&
      (reference.visualObjectId ?? null) === (visualObjectId ?? null)),
    [references, visualDefinitionId, visualObjectId]
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
    setPreviewToken(null);
    setError(null);
    setEntryPoint('');
    setTargetId('');
  }, [choice, scriptId, visualDefinitionId, visualObjectId]);

  const buildReference = (): ScriptVisualEventReference => {
    if (!visualDefinitionId) throw new Error('Apply the visual definition before authoring events.');
    if (!selectedScript || !selectedEntryPoint) throw new Error('Select a valid Script entry point.');
    if (choice === 'click' && !visualObjectId) throw new Error('Select one visual object for a Click event.');

    const reference: ScriptVisualEventReference = {
      visualDefinitionId,
      visualObjectId: choice === 'initialize' || choice === 'dispose' || choice === 'timer' ? null : visualObjectId ?? null,
      eventKind,
      scriptId: selectedScript.id,
      entryPoint: selectedEntryPoint.handlerName,
      targetReference: null,
      tagReference: null,
      timerIntervalMs: null
    };

    if (choice === 'timer') {
      if (!Number.isInteger(timerIntervalMs) || timerIntervalMs < MINIMUM_TIMER_INTERVAL_MS)
        throw new Error(`Timer interval must be an integer of at least ${MINIMUM_TIMER_INTERVAL_MS} ms.`);
      reference.timerIntervalMs = timerIntervalMs;
    } else if (choice === 'tagChanged') {
      const target = tagTargets.find(item => item.tagReference?.tagId === targetId || item.target === targetId);
      if (!target?.tagReference?.tagId) throw new Error('Select a canonical TAG target.');
      reference.tagReference = {
        tagId: target.tagReference.tagId,
        selector: target.tagReference.selector ? { ...target.tagReference.selector } : null
      };
    } else if (choice === 'clientMemoryChanged') {
      const target = memoryTargets.find(item => item.tagReference?.tagId === targetId || item.target === targetId);
      if (!target?.tagReference?.tagId) throw new Error('Select a Client Memory definition.');
      reference.targetReference = target.tagReference.tagId;
    }

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

  const unavailable = disabled || !visualDefinitionId;

  return <section className="visual-editor-events" data-testid="visual-events-editor">
    <header><strong>Events</strong><span>Canonical Python event associations</span></header>
    {unavailable ? <p>Apply this Screen before editing canonical event associations.</p> : <>
      <label><span>Event</span><select value={choice} onChange={event => setChoice(event.currentTarget.value as EventChoice)}>
        {EVENT_CHOICES.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select></label>
      <label><span>Script</span><select value={scriptId} onChange={event => setScriptId(event.currentTarget.value)}>
        <option value="">Select Script</option>
        {scripts.map(script => <option key={script.id} value={script.id}>{script.name} · {script.path}</option>)}
      </select></label>
      <label><span>Entry point</span><select value={entryPoint} onChange={event => setEntryPoint(event.currentTarget.value)} disabled={!selectedScript}>
        <option value="">Select handler</option>
        {matchingEntryPoints.map(item => <option key={`${item.eventKind}:${item.handlerName}`} value={item.handlerName}>{item.handlerName}</option>)}
      </select></label>

      {choice === 'tagChanged' ? <label><span>TAG target</span><select value={targetId} onChange={event => setTargetId(event.currentTarget.value)}>
        <option value="">Select TAG</option>
        {tagTargets.map(target => <option key={`${target.tagReference!.tagId}:${target.target}`} value={target.tagReference!.tagId}>{target.label}</option>)}
      </select></label> : null}

      {choice === 'clientMemoryChanged' ? <label><span>Client Memory</span><select value={targetId} onChange={event => setTargetId(event.currentTarget.value)}>
        <option value="">Select definition</option>
        {memoryTargets.map(target => <option key={target.tagReference!.tagId} value={target.tagReference!.tagId}>{target.label}</option>)}
      </select></label> : null}

      {choice === 'timer' ? <label><span>Interval (ms)</span><input type="number" min={MINIMUM_TIMER_INTERVAL_MS} step="1" value={timerIntervalMs} onChange={event => setTimerIntervalMs(Number(event.currentTarget.value))} /></label> : null}

      <div className="visual-editor-events-actions">
        <button type="button" className="secondary" disabled={busy || !selectedEntryPoint} onClick={() => void preview()} data-testid="visual-events-preview">{busy ? 'Working…' : 'Preview event'}</button>
        <button type="button" className="primary" disabled={busy || !previewToken?.preview.canApply} onClick={() => void apply()} data-testid="visual-events-apply">Apply event</button>
      </div>
      {error ? <pre>{error}</pre> : null}
      {previewToken ? <small>{previewToken.preview.canApply ? 'Validated Engineering candidate.' : 'Invalid Engineering candidate.'}</small> : null}
      <div className="visual-editor-events-list">
        {applicableReferences.map((reference, index) => <code key={`${reference.scriptId}:${reference.entryPoint}:${index}`}>
          {reference.eventKind} → {reference.entryPoint}
        </code>)}
      </div>
    </>}
  </section>;
}
