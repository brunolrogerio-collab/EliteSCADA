import React, { useEffect, useMemo, useState, type MouseEvent } from 'react';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
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
  decorateRuntimeDynamoVisualStates,
  expandRuntimeDynamoVisuals
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
 * CanonicalVisualRenderer remains the only visual renderer. C07 expands Dynamo
 * instances only in this transient projection so public parameter bindings and
 * semantic state are resolved before rendering; persisted Engineering remains
 * one encapsulated Dynamo instance. Python receives no DOM, React or browser
 * authority.
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
  const [revision, setRevision] = useState(0);
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
  const dynamoStateSamples = useVisualBindingSamples(expandedDynamoElements);
  const renderedElements = useMemo(
    () => decorateRuntimeDynamoVisualStates(expandedDynamoElements, dynamoStateSamples),
    [expandedDynamoElements, dynamoStateSamples]
  );

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
    className="runtime-visual-definition"
    data-runtime-visual-definition-id={visualDefinitionId || undefined}
    data-runtime-visual-context-id={runtimeContextId}
    onClickCapture={captureObjectInteraction}
  >
    <CanonicalVisualRenderer
      elements={renderedElements}
      emptyLabel={emptyLabel}
      locale={locale}
      onVisualEvent={onVisualEvent}
      onTagWrite={onTagWrite}
      visualAssetUrl={visualAssetUrl}
    />
  </div>;
}
