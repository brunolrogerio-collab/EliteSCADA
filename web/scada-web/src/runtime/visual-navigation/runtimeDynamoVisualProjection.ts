import type { EngineeringLocale } from '../../engineering/i18n';
import type { DynamoEngineering, VisualElementEngineering } from '../../engineering/types';
import { c07VisualEditorText } from '../../engineering/visual-editor/c07VisualEditorI18n';
import { resolveDynamoRuntimeState } from '../../engineering/visual-editor/dynamo/dynamoRuntimeStateModel';
import type { VisualLiveScalarSample } from '../../engineering/visual-editor/visualEditorLiveValues';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../visual-runtime';
import {
  normalizeDynamoDefinitionParameterContract,
  normalizeDynamoInstanceParameterContract
} from './dynamoParameterWireContract';
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
}>;

/**
 * Expands Dynamo instances only in transient Runtime projection. The persisted
 * Engineering instance stays a single canonical object carrying `dynamoKey` and
 * typed public parameters. Internal IDs are scoped to the instance so repeated
 * uses of the same definition cannot collide in the mounted renderer.
 */
export function expandRuntimeDynamoVisuals(
  elements: readonly VisualElementEngineering[] | null | undefined,
  definitions: readonly DynamoEngineering[] | null | undefined,
  locale: EngineeringLocale = 'pt-BR'
): readonly VisualElementEngineering[] {
  return Object.freeze((elements ?? []).map((element, index) =>
    expandElementFailClosed(element, definitions, `root.${index}`, locale)));
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
 * Placement is intentionally absent from this model. Runtime anchors each
 * indicator to the rendered Dynamo root so the canonical renderer remains the
 * only authority for transform, nesting and scroll geometry.
 */
export function resolveRuntimeDynamoStateIndicators(
  elements: readonly VisualElementEngineering[],
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>,
  locale: EngineeringLocale = 'pt-BR'
): readonly RuntimeDynamoStateIndicator[] {
  const result: RuntimeDynamoStateIndicator[] = [];
  for (const element of elements) collectIndicators(element, liveSamples, result, locale);
  return Object.freeze(result);
}

export function isExpandedRuntimeDynamo(element: VisualElementEngineering): boolean {
  return element.metadata?.[RUNTIME_DYNAMO_EXPANDED] === 'true';
}

function expandElementFailClosed(
  element: VisualElementEngineering,
  definitions: readonly DynamoEngineering[] | null | undefined,
  path: string,
  locale: EngineeringLocale
): VisualElementEngineering {
  try {
    return expandElement(element, definitions, path, locale);
  } catch (reason) {
    return runtimeDynamoDiagnosticElement(element, reason, locale);
  }
}

function expandElement(
  element: VisualElementEngineering,
  definitions: readonly DynamoEngineering[] | null | undefined,
  path: string,
  locale: EngineeringLocale
): VisualElementEngineering {
  if (element.dynamoKey?.trim()) {
    const definition = normalizeDynamoDefinitionParameterContract(
      resolveDynamoDefinition(definitions, element.dynamoKey)
    );
    const normalizedInstance = normalizeDynamoInstanceParameterContract(element);
    const composition = composeDynamoRuntime(normalizedInstance, definition);
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
      // The persisted instance type remains `dynamo`. Once the definition has
      // been expanded into transient Runtime children, however, the canonical
      // renderer must receive a real container type so those children are
      // reachable for rendering and interaction. Keeping `type: dynamo` here
      // makes the renderer take its legacy-placeholder path and discards the
      // expanded child tree, including authored C16 actions.
      type: BUILTIN_VISUAL_OBJECT_TYPES.group,
      dynamoKey: null,
      children: [...children],
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
    children: element.children.map((child, index) =>
      expandElementFailClosed(child, definitions, `${path}.${index}`, locale))
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
      ? element.children.map((child, index) =>
          scopeDefinitionElement(child, instanceId, `${path}.${index}`))
      : element.children
  });
}

function collectIndicators(
  element: VisualElementEngineering,
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>,
  result: RuntimeDynamoStateIndicator[],
  locale: EngineeringLocale
): void {
  if (isExpandedRuntimeDynamo(element)) {
    const resolution = resolveDynamoRuntimeState(element.children ?? [], liveSamples);
    const instanceId = element.metadata?.[RUNTIME_DYNAMO_INSTANCE_ID] ?? element.id ?? element.key;
    const dynamoKey = element.metadata?.[RUNTIME_DYNAMO_KEY] ?? 'unknown';
    const presentation = statePresentation(resolution.state.kind, locale);
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
      foreground: presentation.foreground
    }));
    return;
  }

  for (const child of element.children ?? []) {
    collectIndicators(child, liveSamples, result, locale);
  }
}

function runtimeDynamoDiagnosticElement(
  source: VisualElementEngineering,
  reason: unknown,
  locale: EngineeringLocale
): VisualElementEngineering {
  const message = reason instanceof Error ? reason.message : String(reason);
  const code = reason && typeof reason === 'object' && 'code' in reason
    ? String((reason as { code?: unknown }).code ?? 'VISUAL_RUNTIME_DYNAMO_FAILED')
    : 'VISUAL_RUNTIME_DYNAMO_FAILED';
  const properties = source.properties ?? {};
  const text = c07VisualEditorText(locale).runtimeState;
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
      text: text.dynamoError,
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

function statePresentation(kind: string, locale: EngineeringLocale): Readonly<{
  label: string;
  background: string;
  foreground: string;
}> {
  const text = c07VisualEditorText(locale).runtimeState;
  switch (kind) {
    case 'bad-quality': return Object.freeze({ label: text.badQuality, background: '#334155', foreground: '#FFFFFF' });
    case 'fault': return Object.freeze({ label: text.fault, background: '#7F1D1D', foreground: '#FFFFFF' });
    case 'alarm': return Object.freeze({ label: text.alarm, background: '#92400E', foreground: '#FFFFFF' });
    case 'uncertain-quality': return Object.freeze({ label: text.uncertain, background: '#854D0E', foreground: '#FFFFFF' });
    case 'command-intent': return Object.freeze({ label: text.command, background: '#1D4ED8', foreground: '#FFFFFF' });
    case 'transitioning': return Object.freeze({ label: text.transition, background: '#6D28D9', foreground: '#FFFFFF' });
    case 'active': return Object.freeze({ label: text.active, background: '#166534', foreground: '#FFFFFF' });
    case 'inactive': return Object.freeze({ label: text.inactive, background: '#475569', foreground: '#FFFFFF' });
    default: return Object.freeze({ label: text.unknown, background: '#52525B', foreground: '#FFFFFF' });
  }
}

function finiteOr(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function stableToken(value: string): string {
  const token = value.trim().replace(/[^a-zA-Z0-9_.-]+/g, '-');
  return token || 'element';
}
