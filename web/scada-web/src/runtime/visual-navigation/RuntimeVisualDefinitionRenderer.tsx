import React, {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent,
  type PointerEvent
} from 'react';
import { createPortal } from 'react-dom';
import { useRuntimeActionFeedback } from './RuntimeActionFeedback';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import { c07VisualEditorText } from '../../engineering/visual-editor/c07VisualEditorI18n';
import {
  CanonicalVisualRenderer,
  type CanonicalVisualEvent,
  type VisualAssetUrlResolver
} from '../../engineering/visual-editor/CanonicalVisualRenderer';
import { useVisualBindingSamples } from '../../engineering/visual-editor/visualEditorLiveValues';
import type {
  DynamoEngineering,
  EquipmentEngineering,
  TemplateEngineering,
  VisualElementEngineering
} from '../../engineering/types';
import type { EngineeringLocale } from '../../engineering/i18n';
import {
  ClientVisualEventDispatcher,
  type ClientVisualEventDispatchRecord,
  type ClientVisualPythonRuntimeFactory
} from '../../python-runtime/clientVisualEventDispatcher';
import type { VisualTweenFrameClock } from '../../visual-runtime/runtimeVisualTween';
import {
  createRuntimeVisualInstances,
  projectRuntimeVisualElements
} from './runtimeVisualInstanceComposition';
import {
  collectRuntimeDynamoEventOnlyObjectIds,
  collectRuntimeDynamoStateBindingElements,
  expandRuntimeDynamoVisuals,
  resolveRuntimeDynamoStateIndicators,
  type RuntimeDynamoStateIndicator
} from './runtimeDynamoVisualProjection';
import { writeRuntimeTagValue } from '../runtimeTagWriteApi';
import type { SliderTagWrite } from '../../engineering/visual-editor/SliderVisualElement';
import { useOptionalHistoricalPlayback } from '../historical-playback/HistoricalPlaybackContext';

export type RuntimeVisualDefinitionRendererProps = Readonly<{
  visualDefinitionId: string;
  runtimeContextId: string;
  elements: readonly VisualElementEngineering[] | null | undefined;
  emptyLabel: string;
  locale?: EngineeringLocale;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  equipmentDefinitions?: readonly EquipmentEngineering[] | null;
  templateDefinitions?: readonly TemplateEngineering[] | null;
  scriptContext?: ScriptEngineeringContext | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  onScriptDispatch?: (records: readonly ClientVisualEventDispatchRecord[]) => void;
  runtimeFactory?: ClientVisualPythonRuntimeFactory;
  frameClock?: VisualTweenFrameClock;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl?: VisualAssetUrlResolver;
}>;

/**
 * Mounted Wave 10 bridge between canonical visual Engineering and transient
 * Client Visual Python Runtime state.
 *
 * CanonicalVisualRenderer remains the only process-artwork renderer. C07 expands
 * Dynamo instances only in this transient projection so public parameter
 * bindings are resolved before the renderer subscribes to TAGs. Semantic Dynamo
 * state is rendered as a separate read-only overlay anchored to the rendered
 * Dynamo root, keeping the canonical visual element tree stable while live
 * values change. Python receives no DOM, React or browser authority.
 */
export function RuntimeVisualDefinitionRenderer({
  visualDefinitionId,
  runtimeContextId,
  elements,
  emptyLabel,
  locale,
  dynamoDefinitions,
  equipmentDefinitions,
  templateDefinitions,
  scriptContext,
  onVisualEvent,
  onScriptDispatch,
  runtimeFactory,
  frameClock,
  onTagWrite = writeRuntimeTagValue,
  visualAssetUrl
}: RuntimeVisualDefinitionRendererProps) {
  const runtimeLocale = locale ?? 'pt-BR';
  const runtimeText = c07VisualEditorText(runtimeLocale).runtimeState;
  const playback = useOptionalHistoricalPlayback();
  const historical = playback?.mode === 'historicalPlayback';
  const [revision, setRevision] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const [dynamoStateHosts, setDynamoStateHosts] = useState<ReadonlyMap<string, HTMLElement>>(
    () => new Map()
  );
  const instances = useMemo(
    () => createRuntimeVisualInstances(elements, runtimeContextId),
    [elements, runtimeContextId]
  );
  const dynamoEventOnlyObjectIds = useMemo(
    () => collectRuntimeDynamoEventOnlyObjectIds(elements),
    [elements]
  );
  const dispatcher = useMemo(() => new ClientVisualEventDispatcher({
    visualDefinitionId,
    instances,
    eventOnlyObjectIds: dynamoEventOnlyObjectIds,
    onVisualStateChanged: () => setRevision(current => current + 1),
    runtimeFactory,
    frameClock,
    tagWriter: historical ? null : undefined
  }), [visualDefinitionId, instances, dynamoEventOnlyObjectIds, runtimeFactory, frameClock, historical]);
  const interactionEventKeys = useMemo(() => {
    const byObject = new Map<string, Set<string>>();
    for (const reference of scriptContext?.visualEventReferences ?? []) {
      if (reference.visualDefinitionId !== visualDefinitionId ||
          reference.eventKind !== 'objectInteraction' || !reference.visualObjectId) continue;
      const eventKey = (reference.eventKey ?? 'click').trim().toLocaleLowerCase('en-US');
      const keys = byObject.get(reference.visualObjectId) ?? new Set<string>();
      keys.add(eventKey);
      byObject.set(reference.visualObjectId, keys);
    }
    return byObject;
  }, [scriptContext, visualDefinitionId]);
  const lastPointerMoveDispatch = useRef(new Map<string, number>());

  useEffect(() => () => dispatcher.dispose(), [dispatcher]);

  const projectedElements = useMemo(
    () => projectRuntimeVisualElements(elements, instances),
    [elements, instances, revision]
  );
  const expandedDynamoElements = useMemo(
    () => expandRuntimeDynamoVisuals(projectedElements, dynamoDefinitions, runtimeLocale),
    [projectedElements, dynamoDefinitions, runtimeLocale]
  );
  const dynamoStateBindingElements = useMemo(
    () => collectRuntimeDynamoStateBindingElements(expandedDynamoElements),
    [expandedDynamoElements]
  );
  const liveDynamoStateSamples = useVisualBindingSamples(dynamoStateBindingElements, !historical);
  const dynamoStateSamples = historical ? (playback?.samples ?? new Map()) : liveDynamoStateSamples;
  const dynamoStateIndicators = useMemo(
    () => resolveRuntimeDynamoStateIndicators(expandedDynamoElements, dynamoStateSamples, runtimeLocale),
    [expandedDynamoElements, dynamoStateSamples, runtimeLocale]
  );

  useLayoutEffect(() => {
    const root = rootRef.current;
    if (!root) {
      setDynamoStateHosts(new Map());
      return;
    }

    const next = new Map<string, HTMLElement>();
    for (const node of root.querySelectorAll<HTMLElement>('[data-object-id]')) {
      const objectId = node.dataset.objectId?.trim();
      const eventKeys = objectId ? interactionEventKeys.get(objectId) : undefined;
      if (eventKeys?.size) node.dataset.objectEvents = [...eventKeys].join(' ');
      else delete node.dataset.objectEvents;
      if (objectId && !next.has(objectId)) next.set(objectId, node);
    }
    setDynamoStateHosts(next);
  }, [expandedDynamoElements, interactionEventKeys]);

  const captureObjectInteraction = (event: MouseEvent<HTMLDivElement> | PointerEvent<HTMLDivElement>, eventKey: string) => {
    if (historical || !scriptContext || !visualDefinitionId.trim()) return;
    const target = event.target;
    if (!(target instanceof Element)) return;
    if (target.closest('[data-runtime-session-control]')) return;

    let visualElement: HTMLElement | null = target.closest<HTMLElement>('[data-object-id]');
    while (visualElement && event.currentTarget.contains(visualElement)) {
      const objectId = visualElement.dataset.objectId?.trim();
      const configured = objectId ? interactionEventKeys.get(objectId) : undefined;
      const related = 'relatedTarget' in event && event.relatedTarget instanceof Node
        ? event.relatedTarget
        : null;
      const crossingWithinObject = related !== null && visualElement.contains(related);
      const enteringOrLeaving = eventKey === 'pointerenter' || eventKey === 'pointerleave';
      const movingPointer = eventKey === 'pointermove';
      const pointerType = 'pointerType' in event ? event.pointerType : 'mouse';
      const isMousePointer = !('pointerType' in event) || pointerType === 'mouse';
      if (objectId && configured?.has(eventKey) && (instances.has(objectId) || dynamoEventOnlyObjectIds.has(objectId)) &&
          (!enteringOrLeaving || !crossingWithinObject) && (!movingPointer || isMousePointer)) {
        if (movingPointer) {
          const now = globalThis.performance?.now?.() ?? Date.now();
          const lastKey = `${objectId}:${eventKey}`;
          if (now - (lastPointerMoveDispatch.current.get(lastKey) ?? Number.NEGATIVE_INFINITY) < 80) {
            visualElement = visualElement.parentElement?.closest<HTMLElement>('[data-object-id]') ?? null;
            continue;
          }
          lastPointerMoveDispatch.current.set(lastKey, now);
        }
        void dispatcher.dispatchObjectInteraction({
          visualDefinitionId,
          objectId,
          eventKey,
          pointer: {
            x: event.clientX,
            y: event.clientY,
            type: pointerType,
            button: event.button,
            buttons: event.buttons
          },
          context: scriptContext
        }).then(records => onScriptDispatch?.(records));
      }
      const parent = visualElement.parentElement;
      visualElement = parent?.closest<HTMLElement>('[data-object-id]') ?? null;
    }
  };

  return <div
    ref={rootRef}
    className="runtime-visual-definition"
    data-runtime-visual-definition-id={visualDefinitionId || undefined}
    data-runtime-visual-context-id={runtimeContextId}
    onClickCapture={event => captureObjectInteraction(event, 'click')}
    onPointerOverCapture={event => captureObjectInteraction(event, 'pointerenter')}
    onPointerOutCapture={event => captureObjectInteraction(event, 'pointerleave')}
    onPointerMoveCapture={event => captureObjectInteraction(event, 'pointermove')}
  >
    <CanonicalVisualRenderer
      elements={expandedDynamoElements}
      emptyLabel={emptyLabel}
      locale={runtimeLocale}
      dynamoDefinitions={dynamoDefinitions}
      equipmentDefinitions={equipmentDefinitions}
      templateDefinitions={templateDefinitions}
      onVisualEvent={onVisualEvent}
      onTagWrite={historical ? undefined : onTagWrite}
      visualAssetUrl={visualAssetUrl}
      showTechnicalFallbackText={false}
      liveBindings={!historical}
      bindingSamples={historical ? playback?.samples : undefined}
      operatorTimeRangeControls
    />
    <RuntimeDynamoStateLayer
      indicators={dynamoStateIndicators}
      hosts={dynamoStateHosts}
      feedbackMismatchLabel={runtimeText.feedbackMismatch}
    />
  </div>;
}

function RuntimeDynamoStateLayer({
  indicators,
  hosts,
  feedbackMismatchLabel
}: {
  indicators: readonly RuntimeDynamoStateIndicator[];
  hosts: ReadonlyMap<string, HTMLElement>;
  feedbackMismatchLabel: string;
}) {
  const actionFeedback = useRuntimeActionFeedback();
  return <>
    {[...actionFeedback].map(([objectId, feedback]) => {
      let host = hosts.get(objectId);
      if (!host) for (const candidate of hosts.values()) {
        if ([...candidate.querySelectorAll<HTMLElement>('[data-object-id]')].some(child => child.dataset.objectId === objectId)) { host = candidate; break; }
      }
      if (!host) return null;
      const failed = feedback.state === 'failed' || feedback.state === 'Rejected' ||
        feedback.state === 'Failed' || feedback.state === 'TimedOut' || feedback.state === 'Unknown';
      const succeeded = feedback.state === 'confirmed' || feedback.state === 'Completed';
      return createPortal(<span key={objectId} role="status" data-dynamo-command-state={feedback.state}
        data-runtime-action-state={feedback.state}
        style={{ position: 'absolute', bottom: 2, left: 2, zIndex: 1000, pointerEvents: 'none', borderRadius: 3, padding: '2px 4px', fontSize: 10, color: '#fff', background: failed ? '#9a2525' : succeeded ? '#235539' : '#354c67' }}>{feedback.label}</span>, host);
    })}
    {indicators.map(indicator => {
      const host = hosts.get(indicator.objectId);
      if (!host) return null;
      return createPortal(<span
        key={indicator.instanceId}
        role="status"
        data-testid="runtime-dynamo-state-indicator"
        className="runtime-dynamo-state-indicator"
        data-dynamo-instance-id={indicator.instanceId}
        data-dynamo-key={indicator.dynamoKey}
        data-dynamo-state={indicator.state}
        data-dynamo-state-priority={indicator.priority}
        data-dynamo-quality={indicator.quality}
        data-dynamo-feedback-mismatch={indicator.feedbackMismatch || undefined}
        title={`${indicator.label}${indicator.feedbackMismatch ? ` · ${feedbackMismatchLabel}` : ''}`}
        style={{
          position: 'absolute',
          left: 2,
          top: 2,
          zIndex: 2147480000,
          minWidth: 78,
          height: 18,
          boxSizing: 'border-box',
          display: 'grid',
          placeItems: 'center',
          padding: '0 4px',
          border: `1px solid ${indicator.foreground}`,
          borderRadius: 3,
          background: indicator.background,
          color: indicator.foreground,
          fontFamily: 'system-ui, sans-serif',
          fontSize: 9,
          fontWeight: 700,
          lineHeight: 1,
          whiteSpace: 'nowrap',
          pointerEvents: 'none'
        }}
      >{indicator.label}</span>, host);
    })}
  </>;
}
