import React, { useEffect, useMemo, useState, type MouseEvent } from 'react';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import {
  CanonicalVisualRenderer,
  type CanonicalVisualEvent
} from '../../engineering/visual-editor/CanonicalVisualRenderer';
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
}>;

/**
 * Mounted Wave 10 bridge between canonical visual Engineering and transient
 * Client Visual Python Runtime state.
 *
 * CanonicalVisualRenderer remains the only visual renderer. This component
 * supplies a transient Script/Animation projection and turns React click
 * identity into the canonical Script event dispatcher. Python receives no DOM,
 * React or browser authority.
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
  frameClock
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

  const captureObjectInteraction = (event: MouseEvent<HTMLDivElement>) => {
    if (!scriptContext || !visualDefinitionId.trim()) return;
    const target = event.target;
    if (!(target instanceof Element)) return;
    const visualElement = target.closest<HTMLElement>('[data-object-id]');
    if (!visualElement || !event.currentTarget.contains(visualElement)) return;
    const objectId = visualElement.dataset.objectId?.trim();
    if (!objectId || !instances.has(objectId)) return;

    void dispatcher.dispatchObjectInteraction({
      visualDefinitionId,
      objectId,
      eventKey: 'click',
      context: scriptContext
    }).then(records => onScriptDispatch?.(records));
  };

  return <div
    className="runtime-visual-definition"
    data-runtime-visual-definition-id={visualDefinitionId || undefined}
    data-runtime-visual-context-id={runtimeContextId}
    onClickCapture={captureObjectInteraction}
  >
    <CanonicalVisualRenderer
      elements={projectedElements}
      emptyLabel={emptyLabel}
      locale={locale}
      dynamoDefinitions={dynamoDefinitions}
      onVisualEvent={onVisualEvent}
    />
  </div>;
}
