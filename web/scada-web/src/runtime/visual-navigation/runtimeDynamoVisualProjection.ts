import type { DynamoEngineering, VisualElementEngineering } from '../../engineering/types';
import {
  resolveDynamoRuntimeState
} from '../../engineering/visual-editor/dynamo/dynamoRuntimeStateModel';
import type { VisualLiveScalarSample } from '../../engineering/visual-editor/visualEditorLiveValues';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../visual-runtime';
import {
  projectDynamoRuntimeElements,
  resolveDynamoRuntimeEquipmentPath
} from './dynamoRuntimeBindingProjection';
import {
  composeDynamoRuntime,
  resolveDynamoDefinition,
  runtimeDynamoElementIdentity
} from './runtimeVisualNavigationModel';

const RUNTIME_DYNAMO_KEY = 'runtime.dynamo.key';
const RUNTIME_DYNAMO_INSTANCE_ID = 'runtime.dynamo.instanceId';
const RUNTIME_DYNAMO_DEFINITION_ID = 'runtime.dynamo.definitionId';
const RUNTIME_DYNAMO_EXPANDED = 'runtime.dynamo.expanded';

/**
 * Expands Dynamo instances only in transient Runtime projection. The persisted
 * Engineering instance stays a single canonical object carrying `dynamoKey` and
 * typed public parameters. Internal IDs are scoped to the instance so repeated
 * uses of the same definition cannot collide in the mounted renderer.
 */
export function expandRuntimeDynamoVisuals(
  elements: readonly VisualElementEngineering[] | null | undefined,
  definitions: readonly DynamoEngineering[] | null | undefined
): readonly VisualElementEngineering[] {
  return Object.freeze((elements ?? []).map((element, index) =>
    expandElement(element, definitions, `root.${index}`)));
}

/**
 * Adds a transient semantic state indicator after live samples have been
 * collected from the exact projected public bindings. The indicator is text +
 * color; color is supportive, never the only state signal.
 */
export function decorateRuntimeDynamoVisualStates(
  elements: readonly VisualElementEngineering[],
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>
): readonly VisualElementEngineering[] {
  return Object.freeze(elements.map(element => decorateElement(element, liveSamples)));
}

export function isExpandedRuntimeDynamo(element: VisualElementEngineering): boolean {
  return element.metadata?.[RUNTIME_DYNAMO_EXPANDED] === 'true';
}

function expandElement(
  element: VisualElementEngineering,
  definitions: readonly DynamoEngineering[] | null | undefined,
  path: string
): VisualElementEngineering {
  if (element.dynamoKey?.trim()) {
    const definition = resolveDynamoDefinition(definitions, element.dynamoKey);
    const composition = composeDynamoRuntime(element, definition);
    const equipmentPath = resolveDynamoRuntimeEquipmentPath(
      element.equipmentPath ?? null,
      composition.parameters
    );
    const projected = projectDynamoRuntimeElements(
      definition.elements ?? [],
      composition.parameters,
      equipmentPath
    );
    const children = projected.map((child, index) =>
      scopeDefinitionElement(child, composition.instanceId, `${path}.${index}`));

    return Object.freeze({
      ...element,
      dynamoKey: null,
      children: Object.freeze(children),
      metadata: Object.freeze({
        ...(element.metadata ?? {}),
        [RUNTIME_DYNAMO_KEY]: composition.definitionKey,
        [RUNTIME_DYNAMO_INSTANCE_ID]: composition.instanceId,
        [RUNTIME_DYNAMO_DEFINITION_ID]: composition.definitionId,
        [RUNTIME_DYNAMO_EXPANDED]: 'true'
      })
    });
  }

  if (!element.children?.length) return element;
  return Object.freeze({
    ...element,
    children: Object.freeze(element.children.map((child, index) =>
      expandElement(child, definitions, `${path}.${index}`)))
  });
}

function scopeDefinitionElement(
  element: VisualElementEngineering,
  instanceId: string,
  path: string
): VisualElementEngineering {
  const scopedId = element.id?.trim()
    ? runtimeDynamoElementIdentity(instanceId, element.id)
    : `${instanceId}/anonymous/${path}/${stableToken(element.key)}`;
  return Object.freeze({
    ...element,
    id: scopedId,
    children: element.children?.length
      ? Object.freeze(element.children.map((child, index) =>
          scopeDefinitionElement(child, instanceId, `${path}.${index}`)))
      : element.children
  });
}

function decorateElement(
  element: VisualElementEngineering,
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>
): VisualElementEngineering {
  const children = (element.children ?? []).map(child => decorateElement(child, liveSamples));
  if (!isExpandedRuntimeDynamo(element)) {
    return children === element.children
      ? element
      : Object.freeze({ ...element, children: Object.freeze(children) });
  }

  const resolution = resolveDynamoRuntimeState(children, liveSamples);
  const instanceId = element.metadata?.[RUNTIME_DYNAMO_INSTANCE_ID] ?? element.id ?? element.key;
  const presentation = statePresentation(resolution.state.kind);
  const tooltip = mergeTooltip(
    element.properties?.tooltip,
    `Dynamo state: ${presentation.label}${resolution.feedbackMismatch ? ' · feedback mismatch' : ''}`
  );
  const badge = createStateBadge(instanceId, presentation.label, presentation.background, presentation.foreground);

  return Object.freeze({
    ...element,
    properties: Object.freeze({
      ...(element.properties ?? {}),
      tooltip
    }),
    children: Object.freeze([...children, badge]),
    metadata: Object.freeze({
      ...(element.metadata ?? {}),
      'runtime.dynamo.state': resolution.state.kind,
      'runtime.dynamo.statePriority': String(resolution.state.priority),
      'runtime.dynamo.quality': resolution.state.quality,
      'runtime.dynamo.feedbackMismatch': resolution.feedbackMismatch ? 'true' : 'false'
    })
  });
}

function createStateBadge(
  instanceId: string,
  label: string,
  background: string,
  foreground: string
): VisualElementEngineering {
  return Object.freeze({
    id: `${instanceId}/__state`,
    key: '__dynamo-state',
    type: BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay,
    properties: Object.freeze({
      x: 2,
      y: 2,
      width: 78,
      height: 18,
      zIndex: 2147480000,
      opacity: 0.94,
      backgroundColor: background,
      strokeColor: foreground,
      strokeWidth: 1,
      strokeStyle: 'solid',
      cornerRadius: 3,
      text: label,
      textColor: foreground,
      fontSize: 9,
      fontWeight: '700',
      horizontalAlignment: 'center',
      verticalAlignment: 'center',
      tooltip: `Dynamo state: ${label}`,
      enabled: false
    }),
    metadata: Object.freeze({
      'runtime.dynamo.stateIndicator': 'true'
    })
  });
}

function statePresentation(kind: string): Readonly<{
  label: string;
  background: string;
  foreground: string;
}> {
  switch (kind) {
    case 'bad-quality': return Object.freeze({ label: 'BAD QUALITY', background: '#334155', foreground: '#FFFFFF' });
    case 'fault': return Object.freeze({ label: 'FAULT', background: '#7F1D1D', foreground: '#FFFFFF' });
    case 'alarm': return Object.freeze({ label: 'ALARM', background: '#92400E', foreground: '#FFFFFF' });
    case 'uncertain-quality': return Object.freeze({ label: 'UNCERTAIN', background: '#854D0E', foreground: '#FFFFFF' });
    case 'command-intent': return Object.freeze({ label: 'COMMAND', background: '#1D4ED8', foreground: '#FFFFFF' });
    case 'transitioning': return Object.freeze({ label: 'TRANSITION', background: '#6D28D9', foreground: '#FFFFFF' });
    case 'active': return Object.freeze({ label: 'ACTIVE', background: '#166534', foreground: '#FFFFFF' });
    case 'inactive': return Object.freeze({ label: 'INACTIVE', background: '#475569', foreground: '#FFFFFF' });
    default: return Object.freeze({ label: 'UNKNOWN', background: '#52525B', foreground: '#FFFFFF' });
  }
}

function mergeTooltip(existing: unknown, state: string): string {
  const current = typeof existing === 'string' ? existing.trim() : '';
  return current ? `${current}\n${state}` : state;
}

function stableToken(value: string): string {
  const token = value.trim().replace(/[^a-zA-Z0-9_.-]+/g, '-');
  return token || 'element';
}
