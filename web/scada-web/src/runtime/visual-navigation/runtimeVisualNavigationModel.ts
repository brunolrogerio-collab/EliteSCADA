import type {
  DynamoEngineering,
  EngineeringPackageView,
  PopupEngineering,
  ScreenEngineering,
  TagValueReferenceEngineering,
  VisualElementEngineering
} from '../../engineering/types';

export const VISUAL_COMPOSITION_RUNTIME_VERSION = 1 as const;

export type DynamoParameterKindEngineering =
  | 'Boolean'
  | 'Number'
  | 'String'
  | 'EquipmentPath'
  | 'TagReference';

export type DynamoParameterDefinitionEngineering = Readonly<{
  key: string;
  kind: DynamoParameterKindEngineering;
  required?: boolean;
  defaultValue?: unknown;
  defaultTagReference?: TagValueReferenceEngineering | null;
  version?: number;
}>;

export type DynamoParameterValueEngineering = Readonly<{
  key: string;
  kind: DynamoParameterKindEngineering;
  value?: unknown;
  tagReference?: TagValueReferenceEngineering | null;
  version?: number;
}>;

export type VisualNavigationActionKindEngineering =
  | 'NavigateScreen'
  | 'OpenPopup'
  | 'ClosePopup';

export type VisualNavigationActionEngineering = Readonly<{
  eventKey: string;
  kind: VisualNavigationActionKindEngineering;
  targetKey?: string | null;
  parameters?: Readonly<Record<string, unknown>> | null;
  version?: number;
}>;

export type CanonicalVisualElementEngineering = VisualElementEngineering & Readonly<{
  dynamoParameters?: readonly DynamoParameterValueEngineering[] | null;
  actions?: readonly VisualNavigationActionEngineering[] | null;
  children?: readonly CanonicalVisualElementEngineering[] | null;
}>;

export type CanonicalDynamoEngineering = DynamoEngineering & Readonly<{
  properties?: Readonly<Record<string, string>> | null;
  context?: Readonly<Record<string, string>> | null;
  metadata?: Readonly<Record<string, string>> | null;
  parameters?: readonly DynamoParameterDefinitionEngineering[] | null;
  elements?: readonly CanonicalVisualElementEngineering[] | null;
}>;

export type RuntimeVisualCatalog = Readonly<{
  screens: ReadonlyMap<string, ScreenEngineering>;
  popups: ReadonlyMap<string, PopupEngineering>;
  dynamos: ReadonlyMap<string, CanonicalDynamoEngineering>;
}>;

export type RuntimePopupMount = Readonly<{
  runtimeInstanceId: string;
  definitionKey: string;
  parameters: Readonly<Record<string, unknown>>;
}>;

export type RuntimeVisualNavigationState = Readonly<{
  activeScreenKey: string;
  popups: readonly RuntimePopupMount[];
}>;

export type RuntimeVisualActionContext = Readonly<{
  popupRuntimeInstanceId?: string | null;
  popupIdFactory?: () => string;
}>;

export type DynamoRuntimeCompositionView = Readonly<{
  instanceId: string;
  definitionId: string;
  definitionKey: string;
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>;
  elements: readonly CanonicalVisualElementEngineering[];
}>;

export class RuntimeVisualCompositionError extends Error {
  constructor(
    public readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'RuntimeVisualCompositionError';
  }
}

export function asCanonicalVisualElement(
  element: VisualElementEngineering
): CanonicalVisualElementEngineering {
  return element as CanonicalVisualElementEngineering;
}

export function asCanonicalDynamo(
  definition: DynamoEngineering
): CanonicalDynamoEngineering {
  return definition as CanonicalDynamoEngineering;
}

export function createRuntimeVisualCatalog(
  engineeringPackage: Pick<EngineeringPackageView, 'screens' | 'popups' | 'dynamos'>
): RuntimeVisualCatalog {
  return Object.freeze({
    screens: indexByKey(engineeringPackage.screens ?? [], 'Screen'),
    popups: indexByKey(engineeringPackage.popups ?? [], 'Popup'),
    dynamos: indexByKey(
      (engineeringPackage.dynamos ?? []).map(asCanonicalDynamo),
      'Dynamo'
    )
  });
}

export function createRuntimeVisualNavigationState(
  catalog: RuntimeVisualCatalog,
  initialScreenKey: string
): RuntimeVisualNavigationState {
  const screen = resolveCatalogEntity(catalog.screens, initialScreenKey, 'screen', 'VISUAL_RUNTIME_SCREEN_NOT_FOUND');
  return Object.freeze({
    activeScreenKey: screen.key,
    popups: Object.freeze([])
  });
}

export function resolveVisualNavigationAction(
  element: VisualElementEngineering,
  eventKey: string
): VisualNavigationActionEngineering | null {
  requireStableText(eventKey, 'event key', 'VISUAL_RUNTIME_EVENT_KEY_INVALID');
  const actions = asCanonicalVisualElement(element).actions ?? [];
  const matches = actions.filter(action => equalsKey(action.eventKey, eventKey));
  if (matches.length > 1) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_ACTION_AMBIGUOUS',
      `Visual element '${element.key}' has multiple navigation actions for event '${eventKey}'.`
    );
  }
  return matches[0] ?? null;
}

export function executeVisualNavigationAction(
  catalog: RuntimeVisualCatalog,
  current: RuntimeVisualNavigationState,
  action: VisualNavigationActionEngineering,
  context: RuntimeVisualActionContext = {}
): RuntimeVisualNavigationState {
  assertCompositionVersion(action.version, `Navigation action '${action.eventKey}'`);

  switch (action.kind) {
    case 'NavigateScreen': {
      const target = requireTarget(action, 'screen');
      const screen = resolveCatalogEntity(catalog.screens, target, 'screen', 'VISUAL_RUNTIME_SCREEN_NOT_FOUND');
      return Object.freeze({
        activeScreenKey: screen.key,
        popups: current.popups
      });
    }
    case 'OpenPopup': {
      const target = requireTarget(action, 'popup');
      const popup = resolveCatalogEntity(catalog.popups, target, 'popup', 'VISUAL_RUNTIME_POPUP_NOT_FOUND');
      const runtimeInstanceId = (context.popupIdFactory ?? createRuntimeInstanceId)();
      requireStableText(runtimeInstanceId, 'Popup runtime instance ID', 'VISUAL_RUNTIME_POPUP_INSTANCE_ID_INVALID');
      const mounted = Object.freeze({
        runtimeInstanceId,
        definitionKey: popup.key,
        parameters: cloneParameterObject(action.parameters ?? {})
      });
      return Object.freeze({
        activeScreenKey: current.activeScreenKey,
        popups: Object.freeze([...current.popups, mounted])
      });
    }
    case 'ClosePopup': {
      if (action.targetKey !== undefined && action.targetKey !== null && action.targetKey !== '') {
        throw new RuntimeVisualCompositionError(
          'VISUAL_RUNTIME_CLOSE_POPUP_TARGET_NOT_ALLOWED',
          `ClosePopup action '${action.eventKey}' cannot declare a target key.`
        );
      }
      const runtimeInstanceId = context.popupRuntimeInstanceId;
      if (!runtimeInstanceId) {
        throw new RuntimeVisualCompositionError(
          'VISUAL_RUNTIME_CLOSE_POPUP_CONTEXT_REQUIRED',
          `ClosePopup action '${action.eventKey}' requires a mounted Popup runtime context.`
        );
      }
      if (!current.popups.some(popup => popup.runtimeInstanceId === runtimeInstanceId)) {
        throw new RuntimeVisualCompositionError(
          'VISUAL_RUNTIME_POPUP_INSTANCE_NOT_FOUND',
          `Mounted Popup runtime instance '${runtimeInstanceId}' was not found.`
        );
      }
      return Object.freeze({
        activeScreenKey: current.activeScreenKey,
        popups: Object.freeze(current.popups.filter(popup => popup.runtimeInstanceId !== runtimeInstanceId))
      });
    }
    default:
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_ACTION_KIND_UNSUPPORTED',
        `Navigation action '${action.eventKey}' has unsupported kind '${String(action.kind)}'.`
      );
  }
}

export function resolveActiveScreen(
  catalog: RuntimeVisualCatalog,
  state: RuntimeVisualNavigationState
): ScreenEngineering {
  return resolveCatalogEntity(catalog.screens, state.activeScreenKey, 'screen', 'VISUAL_RUNTIME_SCREEN_NOT_FOUND');
}

export function resolveMountedPopup(
  catalog: RuntimeVisualCatalog,
  mount: RuntimePopupMount
): PopupEngineering {
  return resolveCatalogEntity(catalog.popups, mount.definitionKey, 'popup', 'VISUAL_RUNTIME_POPUP_NOT_FOUND');
}

export function resolveDynamoDefinition(
  definitions: readonly DynamoEngineering[] | null | undefined,
  dynamoKey: string
): CanonicalDynamoEngineering {
  const catalog = indexByKey((definitions ?? []).map(asCanonicalDynamo), 'Dynamo');
  return resolveCatalogEntity(catalog, dynamoKey, 'Dynamo', 'VISUAL_RUNTIME_DYNAMO_NOT_FOUND');
}

export function composeDynamoRuntime(
  instanceInput: VisualElementEngineering,
  definitionInput: DynamoEngineering
): DynamoRuntimeCompositionView {
  const instance = asCanonicalVisualElement(instanceInput);
  const definition = asCanonicalDynamo(definitionInput);
  if (!instance.dynamoKey || !equalsKey(instance.dynamoKey, definition.key)) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_DYNAMO_REFERENCE_MISMATCH',
      `Visual element '${instance.key}' does not reference Dynamo '${definition.key}'.`
    );
  }
  requireStableText(instance.id ?? '', 'Dynamo instance ID', 'VISUAL_RUNTIME_DYNAMO_INSTANCE_ID_REQUIRED');
  requireStableText(definition.id ?? '', 'Dynamo definition ID', 'VISUAL_RUNTIME_DYNAMO_DEFINITION_ID_REQUIRED');
  assertNoNestedDynamo(definition.elements ?? []);

  const supplied = uniqueParameterValues(instance.dynamoParameters ?? [], instance.key);
  const definitions = uniqueParameterDefinitions(definition.parameters ?? [], definition.key);
  const resolved = new Map<string, DynamoParameterValueEngineering>();

  for (const parameter of definitions.values()) {
    assertCompositionVersion(parameter.version, `Dynamo parameter definition '${parameter.key}'`);
    const suppliedValue = supplied.get(normalizeKey(parameter.key));
    if (suppliedValue) {
      assertCompositionVersion(suppliedValue.version, `Dynamo parameter value '${suppliedValue.key}'`);
      if (suppliedValue.kind !== parameter.kind) {
        throw new RuntimeVisualCompositionError(
          'VISUAL_RUNTIME_DYNAMO_PARAMETER_KIND_MISMATCH',
          `Dynamo parameter '${parameter.key}' expects ${parameter.kind} but instance '${instance.key}' supplies ${suppliedValue.kind}.`
        );
      }
      validateParameterPayload(suppliedValue, false);
      resolved.set(parameter.key, freezeParameterValue(suppliedValue));
      continue;
    }

    if (parameter.kind === 'TagReference' && parameter.defaultTagReference) {
      const value: DynamoParameterValueEngineering = {
        key: parameter.key,
        kind: parameter.kind,
        tagReference: cloneTagReference(parameter.defaultTagReference),
        version: VISUAL_COMPOSITION_RUNTIME_VERSION
      };
      validateParameterPayload(value, false);
      resolved.set(parameter.key, freezeParameterValue(value));
      continue;
    }

    if (parameter.defaultValue !== undefined) {
      const value: DynamoParameterValueEngineering = {
        key: parameter.key,
        kind: parameter.kind,
        value: cloneJsonValue(parameter.defaultValue),
        version: VISUAL_COMPOSITION_RUNTIME_VERSION
      };
      validateParameterPayload(value, false);
      resolved.set(parameter.key, freezeParameterValue(value));
      continue;
    }

    if (parameter.required) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_REQUIRED',
        `Required Dynamo parameter '${parameter.key}' was not supplied for instance '${instance.key}'.`
      );
    }
  }

  for (const suppliedValue of supplied.values()) {
    if (!definitions.has(normalizeKey(suppliedValue.key))) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_UNKNOWN',
        `Dynamo instance '${instance.key}' supplies unknown parameter '${suppliedValue.key}'.`
      );
    }
  }

  return Object.freeze({
    instanceId: instance.id!,
    definitionId: definition.id!,
    definitionKey: definition.key,
    parameters: resolved,
    elements: definition.elements ?? Object.freeze([])
  });
}

export function runtimeDynamoElementIdentity(instanceId: string, definitionElementId: string): string {
  requireStableText(instanceId, 'Dynamo instance ID', 'VISUAL_RUNTIME_DYNAMO_INSTANCE_ID_REQUIRED');
  requireStableText(definitionElementId, 'Dynamo definition element ID', 'VISUAL_RUNTIME_DYNAMO_ELEMENT_ID_REQUIRED');
  return `${instanceId}/${definitionElementId}`;
}

function indexByKey<T extends { key: string }>(values: readonly T[], entityName: string): ReadonlyMap<string, T> {
  const result = new Map<string, T>();
  for (const value of values) {
    requireStableText(value.key, `${entityName} key`, 'VISUAL_RUNTIME_ENTITY_KEY_INVALID');
    const normalized = normalizeKey(value.key);
    if (result.has(normalized)) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_ENTITY_KEY_DUPLICATE',
        `${entityName} key '${value.key}' is duplicated in the runtime Engineering projection.`
      );
    }
    result.set(normalized, value);
  }
  return result;
}

function resolveCatalogEntity<T>(
  catalog: ReadonlyMap<string, T>,
  key: string,
  label: string,
  code: string
): T {
  requireStableText(key, `${label} target key`, 'VISUAL_RUNTIME_TARGET_KEY_INVALID');
  const found = catalog.get(normalizeKey(key));
  if (!found) {
    throw new RuntimeVisualCompositionError(code, `Runtime ${label} target '${key}' was not found.`);
  }
  return found;
}

function requireTarget(action: VisualNavigationActionEngineering, label: string): string {
  const target = action.targetKey;
  if (!target || !target.trim()) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_ACTION_TARGET_REQUIRED',
      `${action.kind} action '${action.eventKey}' requires a ${label} target key.`
    );
  }
  return target;
}

function uniqueParameterDefinitions(
  values: readonly DynamoParameterDefinitionEngineering[],
  definitionKey: string
): Map<string, DynamoParameterDefinitionEngineering> {
  const result = new Map<string, DynamoParameterDefinitionEngineering>();
  for (const value of values) {
    requireStableText(value.key, 'Dynamo parameter definition key', 'VISUAL_RUNTIME_DYNAMO_PARAMETER_KEY_INVALID');
    const key = normalizeKey(value.key);
    if (result.has(key)) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_DUPLICATE',
        `Dynamo '${definitionKey}' defines parameter '${value.key}' more than once.`
      );
    }
    result.set(key, value);
  }
  return result;
}

function uniqueParameterValues(
  values: readonly DynamoParameterValueEngineering[],
  instanceKey: string
): Map<string, DynamoParameterValueEngineering> {
  const result = new Map<string, DynamoParameterValueEngineering>();
  for (const value of values) {
    requireStableText(value.key, 'Dynamo parameter value key', 'VISUAL_RUNTIME_DYNAMO_PARAMETER_KEY_INVALID');
    const key = normalizeKey(value.key);
    if (result.has(key)) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_DUPLICATE',
        `Dynamo instance '${instanceKey}' supplies parameter '${value.key}' more than once.`
      );
    }
    result.set(key, value);
  }
  return result;
}

function validateParameterPayload(value: DynamoParameterValueEngineering, allowMissing: boolean): void {
  if (value.kind === 'TagReference') {
    if (value.value !== undefined) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_SHAPE_INVALID',
        `Dynamo parameter '${value.key}' of kind TagReference cannot carry a scalar value.`
      );
    }
    if (!value.tagReference) {
      if (!allowMissing) {
        throw new RuntimeVisualCompositionError(
          'VISUAL_RUNTIME_DYNAMO_PARAMETER_TAG_REQUIRED',
          `Dynamo parameter '${value.key}' requires a stable TAG reference.`
        );
      }
      return;
    }
    requireStableText(value.tagReference.tagId, 'TAG identity', 'VISUAL_RUNTIME_DYNAMO_PARAMETER_TAG_ID_INVALID');
    return;
  }

  if (value.tagReference) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_DYNAMO_PARAMETER_SHAPE_INVALID',
      `Dynamo parameter '${value.key}' of kind ${value.kind} cannot carry a TAG reference.`
    );
  }
  if (value.value === undefined) {
    if (!allowMissing) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_PARAMETER_VALUE_REQUIRED',
        `Dynamo parameter '${value.key}' requires a value.`
      );
    }
    return;
  }

  const valid = value.kind === 'Boolean'
    ? typeof value.value === 'boolean'
    : value.kind === 'Number'
      ? typeof value.value === 'number' && Number.isFinite(value.value)
      : value.kind === 'String' || value.kind === 'EquipmentPath'
        ? typeof value.value === 'string'
        : false;
  if (!valid) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_DYNAMO_PARAMETER_VALUE_TYPE_INVALID',
      `Dynamo parameter '${value.key}' does not match declared kind ${value.kind}.`
    );
  }
}

function freezeParameterValue(value: DynamoParameterValueEngineering): DynamoParameterValueEngineering {
  return Object.freeze({
    ...value,
    value: value.value === undefined ? undefined : cloneJsonValue(value.value),
    tagReference: value.tagReference ? cloneTagReference(value.tagReference) : value.tagReference
  });
}

function assertNoNestedDynamo(elements: readonly CanonicalVisualElementEngineering[]): void {
  for (const element of elements) {
    if (element.dynamoKey) {
      throw new RuntimeVisualCompositionError(
        'VISUAL_RUNTIME_DYNAMO_NESTING_NOT_SUPPORTED',
        `Dynamo definition element '${element.key}' cannot nest Dynamo '${element.dynamoKey}' in composition version 1.`
      );
    }
    assertNoNestedDynamo(element.children ?? []);
  }
}

function assertCompositionVersion(version: number | undefined, label: string): void {
  const effective = version ?? VISUAL_COMPOSITION_RUNTIME_VERSION;
  if (effective !== VISUAL_COMPOSITION_RUNTIME_VERSION) {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_COMPOSITION_VERSION_UNSUPPORTED',
      `${label} uses unsupported visual composition version ${effective}.`
    );
  }
}

function cloneTagReference(reference: TagValueReferenceEngineering): TagValueReferenceEngineering {
  return Object.freeze({
    tagId: reference.tagId,
    selector: reference.selector ? Object.freeze({ ...reference.selector }) : reference.selector
  });
}

function cloneParameterObject(value: Readonly<Record<string, unknown>>): Readonly<Record<string, unknown>> {
  const result: Record<string, unknown> = Object.create(null) as Record<string, unknown>;
  for (const [key, item] of Object.entries(value)) {
    requireStableText(key, 'Popup parameter key', 'VISUAL_RUNTIME_POPUP_PARAMETER_KEY_INVALID');
    result[key] = cloneJsonValue(item);
  }
  return Object.freeze(result);
}

function cloneJsonValue<T>(value: T): T {
  if (value === null || value === undefined || typeof value !== 'object') return value;
  return structuredClone(value);
}

function createRuntimeInstanceId(): string {
  const factory = globalThis.crypto?.randomUUID;
  if (typeof factory !== 'function') {
    throw new RuntimeVisualCompositionError(
      'VISUAL_RUNTIME_INSTANCE_ID_FACTORY_UNAVAILABLE',
      'Browser UUID generation is required to mount a Popup runtime instance.'
    );
  }
  return factory.call(globalThis.crypto);
}

function normalizeKey(value: string): string {
  return value.toLocaleLowerCase('en-US');
}

function equalsKey(left: string, right: string): boolean {
  return normalizeKey(left) === normalizeKey(right);
}

function requireStableText(value: string, field: string, code: string): void {
  if (!value || !value.trim()) {
    throw new RuntimeVisualCompositionError(code, `${field} must be a non-empty stable value.`);
  }
}
