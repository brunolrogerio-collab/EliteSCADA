import type { DynamoEngineering, VisualElementEngineering } from '../../engineering/types';
import { resolveDynamoRuntimeState } from '../../engineering/visual-editor/dynamo/dynamoRuntimeStateModel';
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

export type RuntimeDynamoStateIndicator = Readonly<{
  objectId: string;
  instanceId: string;
  dynamoKey: string;
  state: string;
  priority: number;
  quality: string;
  feedbackMismatch: boolean;
  label: string;
  background: string;
  foreground: string;
  x: number;
  y: number;
}>;

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
    expandElementFailClosed(element, definitions, `root.${index}`)));
}

/** Only projected Dynamo internals are sampled by the C07 state overlay. */
export function collectRuntimeDynamoStateBindingElements(
  elements: readonly VisualElementEngineering[]
): readonly VisualElementEngineering[] {
  const result: VisualElementEngineering[] = [];
  const visit = (element: VisualElementEngineering) => {
    if (isExpandedRuntimeDynamo(element)) {
      result.push(...(element.children ?? []));
      return;
    }
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements) visit(element);
  return Object.freeze(result);
}

/**
 * Resolves semantic state indicators without changing the visual element tree.
 * This keeps CanonicalVisualRenderer input stable while live samples change.
 */
export function resolveRuntimeDynamoStateIndicators(
  elements: readonly VisualElementEngineering[],
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>
): readonly RuntimeDynamoStateIndicator[] {
  const result: RuntimeDynamoStateIndicator[] = [];
  for (const element of elements) collectIndicators(element, liveSamples, 0, 0, 1, 1, result);
  return Object.freeze(result);
}

export function isExpandedRuntimeDynamo(element: VisualElementEngineering): boolean {
  return element.metadata?.[RUNTIME_DYNAMO_EXPANDED] === 'true';
}

function expandElementFailClosed(
  element: VisualElementEngineering,
  definitions: readonly DynamoEngineering[] | null | undefined,
  path: string
): VisualElementEngineering {
  try {
    return expandElement(element, definitions, path);
  } catch (reason) {
    return runtimeDynamoDiagnosticElement(element, reason);
  }
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
      expandElementFailClosed(child, definitions, `${path}.${index}`)))
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

function collectIndicators(
  element: VisualElementEngineering,
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>,
  parentX: number,
  parentY: number,
  parentScaleX: number,
  parentScaleY: number,
  result: RuntimeDynamoStateIndicator[]
): void {
  const x = parentX + finiteOr(element.properties?.x, 0) * parentScaleX;
  const y = parentY + finiteOr(element.properties?.y, 0) * parentScaleY;
  const scaleX = parentScaleX * finiteOr(element.properties?.scaleX, 1);
  const scaleY = parentScaleY * finiteOr(element.properties?.scaleY, 1);

  if (isExpandedRuntimeDynamo(element)) {
    const resolution = resolveDynamoRuntimeState(element.children ?? [], liveSamples);
    const instanceId = element.metadata?.[RUNTIME_DYNAMO_INSTANCE_ID] ?? element.id ?? element.key;
    const dynamoKey = element.metadata?.[RUNTIME_DYNAMO_KEY] ?? 'unknown';
    const presentation = statePresentation(resolution.state.kind);
    result.push(Object.freeze({
      objectId: element.id ?? instanceId,
      instanceId,
      dynamoKey,
      state: resolution.state.kind,
      priority: resolution.state.priority,
      quality: resolution.state.quality,
      feedbackMismatch: resolution.feedbackMismatch,
      label: presentation.label,
      background: presentation.background,
      foreground: presentation.foreground,
      x: x + 2 * scaleX,
      y: y + 2 * scaleY
    }));
    return;
  }

  for (const child of element.children ?? []) {
    collectIndicators(child, liveSamples, x, y, scaleX, scaleY, result);
  }
}

function runtimeDynamoDiagnosticElement(
  source: VisualElementEngineering,
  reason: unknown
): VisualElementEngineering {
  const message = reason instanceof Error ? reason.message : String(reason);
  const code = reason && typeof reason === 'object' && 'code' in reason
    ? String((reason as { code?: unknown }).code ?? 'VISUAL_RUNTIME_DYNAMO_FAILED')
    : 'VISUAL_RUNTIME_DYNAMO_FAILED';
  const properties = source.properties ?? {};
  return Object.freeze({
    id: source.id ?? undefined,
    key: source.key || source.dynamoKey || 'invalid-dynamo',
    type: BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay,
    properties: Object.freeze({
      x: finiteOr(properties.x, 0),
      y: finiteOr(properties.y, 0),
      width: Math.max(finiteOr(properties.width, 132), 48),
      height: Math.max(finiteOr(properties.height, 40), 24),
      zIndex: finiteOr(properties.zIndex, 0),
      backgroundColor: '#7F1D1D',
      strokeColor: '#FFFFFF',
      strokeWidth: 1,
      strokeStyle: 'solid',
      cornerRadius: 3,
      text: 'DYNAMO ERROR',
      textColor: '#FFFFFF',
      fontSize: 10,
      fontWeight: 700,
      horizontalAlignment: 'center',
      verticalAlignment: 'middle',
      tooltip: `${code}: ${message}`,
      enabled: false
    }),
    metadata: Object.freeze({
      ...(source.metadata ?? {}),
      'runtime.dynamo.diagnosticCode': code,
      'runtime.dynamo.diagnostic': message
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

function finiteOr(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function stableToken(value: string): string {
  const token = value.trim().replace(/[^a-zA-Z0-9_.-]+/g, '-');
  return token || 'element';
}
