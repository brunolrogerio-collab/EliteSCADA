import React, {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent
} from 'react';
import { createPortal } from 'react-dom';
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
  collectRuntimeDynamoStateBindingElements,
  expandRuntimeDynamoVisuals,
  resolveRuntimeDynamoStateIndicators,
  type RuntimeDynamoStateIndicator
} from './runtimeDynamoVisualProjection';
import { writeRuntimeTagValue } from '../runtimeTagWriteApi';
import type { SliderTagWrite } from '../../engineering/visual-editor/SliderVisualElement';

export type RuntimeVisualDefinitionRendererProps = Readonly<{
  visualDefinitionId: string;
  runtimeContextId: string;
  elements: readonly VisualElementEngineering[] | null | undefined;
  emptyLabel: string;
  locale?: EngineeringLocale;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
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
  const [revision, setRevision] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const [dynamoStateHosts, setDynamoStateHosts] = useState<ReadonlyMap<string, HTMLElement>>(
    () => new Map()
  );
  const instances = useMemo(
    () => createRuntimeVisualInstances(elements, runtimeContextId),
    [elements, runtimeContextId]
  );
  const dispatcher = useMemo(() => new ClientVisualEventDispatcher({
    instances,
    onVisualStateChanged: () => setRevision(current => current + 1),
    runtimeFactory,
    frameClock
  }), [instances, runtimeFactory, frameClock]);

  useEffect(() => () => dispatcher.dispose(), [dispatcher]);

  const projectedElements = useMemo(
    () => projectRuntimeVisualElements(elements, instances),
    [elements, instances, revision]
  );
  const expandedDynamoElements = useMemo(
    () => expandRuntimeDynamoVisuals(projectedElements, dynamoDefinitions),
    [projectedElements, dynamoDefinitions]
  );
  const dynamoStateBindingElements = useMemo(
    () => collectRuntimeDynamoStateBindingElements(expandedDynamoElements),
    [expandedDynamoElements]
  );
  const dynamoStateSamples = useVisualBindingSamples(dynamoStateBindingElements);
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
      if (objectId && !next.has(objectId)) next.set(objectId, node);
    }
    setDynamoStateHosts(next);
  }, [expandedDynamoElements]);

  const captureObjectInteraction = (event: MouseEvent<HTMLDivElement>) => {
    if (!scriptContext || !visualDefinitionId.trim()) return;
    const target = event.target;
    if (!(target instanceof Element)) return;

    let visualElement: HTMLElement | null = target.closest<HTMLElement>('[data-object-id]');
    while (visualElement && event.currentTarget.contains(visualElement)) {
      const objectId = visualElement.dataset.objectId?.trim();
      if (objectId && instances.has(objectId)) {
        void dispatcher.dispatchObjectInteraction({
          visualDefinitionId,
          objectId,
          eventKey: 'click',
          context: scriptContext
        }).then(records => onScriptDispatch?.(records));
        return;
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
    onClickCapture={captureObjectInteraction}
  >
    <CanonicalVisualRenderer
      elements={expandedDynamoElements}
      emptyLabel={emptyLabel}
      locale={runtimeLocale}
      onVisualEvent={onVisualEvent}
      onTagWrite={onTagWrite}
      visualAssetUrl={visualAssetUrl}
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
  return <>
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
        title={`${indicator.dynamoKey} · ${indicator.label}${indicator.feedbackMismatch ? ` · ${feedbackMismatchLabel}` : ''}`}
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
