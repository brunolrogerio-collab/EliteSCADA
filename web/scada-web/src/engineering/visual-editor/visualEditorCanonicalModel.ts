import type {
  BindingEngineering,
  EngineeringPackageView,
  ScreenEngineering,
  VisualAnalogFillEngineering,
  VisualBooleanConditionEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyValue,
  VisualPropertyExpressionEngineering
} from '../types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema,
  supportsAnalogFill,
  VISUAL_PROPERTY_KEYS,
  type VisualObjectPropertySchema
} from '../../visual-runtime';
import type {
  VisualEditorBounds,
  VisualEditorMutationIntent,
  VisualEditorPoint,
  VisualEditorZOrderOperation
} from './visualEditorContracts';

export const NEW_SCREEN_IDENTITY = 'draft:new-screen';

export type VisualEditorMutationOptions = Readonly<{
  createObjectId?: () => string;
  duplicateOffset?: number;
}>;

export function screenIdentity(screen: ScreenEngineering): string {
  return screen.id ? `id:${screen.id}` : `key:${screen.key}`;
}

export function cloneEngineeringValue<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export function createScreenDraft(existing: readonly ScreenEngineering[], locale: 'pt-BR' | 'en' | 'es'): ScreenEngineering {
  const nextIndex = nextScreenIndex(existing);
  const key = `screen-${nextIndex}`;
  const name = locale === 'en' ? `Screen ${nextIndex}` : locale === 'es' ? `Pantalla ${nextIndex}` : `Tela ${nextIndex}`;
  return {
    key,
    name,
    route: `/${key}`,
    elements: [],
    properties: {},
    context: {},
    metadata: {}
  };
}

export function replaceScreenInPackage(
  model: EngineeringPackageView,
  original: ScreenEngineering | null,
  draft: ScreenEngineering
): EngineeringPackageView {
  const candidate = cloneEngineeringValue(model);
  const screens = candidate.screens ?? [];

  if (original === null) {
    candidate.screens = [...screens, cloneEngineeringValue(draft)];
    return candidate;
  }

  const identity = screenIdentity(original);
  candidate.screens = screens.map(screen =>
    screenIdentity(screen) === identity ? cloneEngineeringValue(draft) : screen);
  return candidate;
}

export function updateScreenElement(
  screen: ScreenEngineering,
  objectId: string,
  update: (element: VisualElementEngineering) => VisualElementEngineering
): ScreenEngineering {
  const [elements, changed] = updateElementTree(screen.elements ?? [], objectId, update);
  return changed ? { ...screen, elements } : screen;
}

export function replaceScreenElements(
  screen: ScreenEngineering,
  elements: readonly VisualElementEngineering[]
): ScreenEngineering {
  return {
    ...screen,
    elements: cloneEngineeringValue([...elements])
  };
}

export function countVisualElements(elements: readonly VisualElementEngineering[] | null | undefined): number {
  let count = 0;
  for (const element of elements ?? []) {
    count += 1 + countVisualElements(element.children);
  }
  return count;
}

/**
 * Canonical visual mutation seam. UI components emit intents only; this reducer
 * owns immutable mutation of the Screen Engineering draft so dynamic authoring
 * cannot become a second persistence authority.
 */
export function applyVisualEditorMutationIntent(
  screen: ScreenEngineering,
  intent: VisualEditorMutationIntent,
  options: VisualEditorMutationOptions = {}
): ScreenEngineering {
  const createObjectId = options.createObjectId ?? createRandomObjectId;

  switch (intent.kind) {
    case 'object.add':
      return addVisualObject(screen, intent.objectType, intent.parentObjectId ?? null, intent.at ?? null, intent.initialProperties, createObjectId);
    case 'dynamo.add':
      return addDynamoInstance(screen, intent, createObjectId);
    case 'object.move':
      return moveVisualObjects(screen, intent.objectIds, intent.delta);
    case 'object.resize':
      return resizeVisualObject(screen, intent.objectId, intent.bounds);
    case 'object.rotate':
      return rotateVisualObjects(screen, intent.objectIds, intent.deltaDegrees);
    case 'object.duplicate':
      return duplicateVisualObjects(screen, intent.objectIds, createObjectId, options.duplicateOffset ?? 12);
    case 'object.delete':
      return deleteVisualObjects(screen, intent.objectIds);
    case 'object.zOrder':
      return changeVisualObjectZOrder(screen, intent.objectIds, intent.operation);
    case 'polygon.create':
    case 'polygon.points.set':
      throw new Error(`Polygon structural intent '${intent.kind}' must be handled by the canonical polygon mutation seam.`);
    case 'property.set':
      return setVisualProperty(screen, intent.objectIds, intent.propertyKey, intent.value);
    case 'property.remove':
      return removeVisualProperty(screen, intent.objectIds, intent.propertyKey);
    case 'binding.set':
      return setVisualBinding(screen, intent.objectId, intent.binding);
    case 'binding.remove':
      return removeVisualBinding(screen, intent.objectId, intent.propertyKey);
    case 'propertyExpression.set':
      return setVisualPropertyExpression(screen, intent.objectId, intent.configuration);
    case 'propertyExpression.remove':
      return removeVisualPropertyExpression(screen, intent.objectId, intent.propertyKey);
    case 'booleanCondition.set':
      return setVisualBooleanCondition(screen, intent.objectId, intent.configuration);
    case 'booleanCondition.remove':
      return removeVisualBooleanCondition(screen, intent.objectId, intent.propertyKey);
    case 'analogFill.set':
      return setVisualAnalogFill(screen, intent.objectId, intent.configuration);
    case 'analogFill.remove':
      return removeVisualAnalogFill(screen, intent.objectId);
  }
}

function addDynamoInstance(
  screen: ScreenEngineering,
  intent: Extract<VisualEditorMutationIntent, { kind: 'dynamo.add' }>,
  createObjectId: () => string
): ScreenEngineering {
  const dynamoKey = intent.dynamoKey.trim();
  if (!dynamoKey || /[\u0000-\u001F\u007F]/.test(dynamoKey)) throw new Error('Dynamo key must be stable and non-empty.');
  const at = intent.at ?? { x: 24, y: 24 };
  assertFinitePoint(at, 'Dynamo placement');
  const width = intent.defaultWidth ?? 120;
  const height = intent.defaultHeight ?? 100;
  if (!Number.isFinite(width) || width <= 0 || !Number.isFinite(height) || height <= 0) {
    throw new Error('Dynamo default dimensions must be positive finite values.');
  }

  const usedKeys = collectVisualElementKeys(screen.elements);
  const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.group);
  let element: VisualElementEngineering = {
    id: requireGeneratedObjectId(createObjectId()),
    key: nextVisualElementKey(dynamoKey.split('.').at(-1) || 'dynamo', usedKeys),
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    dynamoKey,
    equipmentPath: intent.equipmentPath?.trim() || null,
    properties: {}
  };
  element = withValidatedProperties(element, schema, {
    [VISUAL_PROPERTY_KEYS.x]: at.x,
    [VISUAL_PROPERTY_KEYS.y]: at.y,
    [VISUAL_PROPERTY_KEYS.width]: width,
    [VISUAL_PROPERTY_KEYS.height]: height
  });
  return { ...screen, elements: [...(screen.elements ?? []), element] };
}

function addVisualObject(
  screen: ScreenEngineering,
  objectType: string,
  parentObjectId: string | null,
  at: VisualEditorPoint | null,
  initialProperties: Readonly<Record<string, VisualEngineeringPropertyValue>> | undefined,
  createObjectId: () => string
): ScreenEngineering {
  const schema = getBuiltinVisualObjectSchema(objectType);
  const usedKeys = collectVisualElementKeys(screen.elements);
  const key = nextVisualElementKey(objectType.replace(/^core\./, '') || 'object', usedKeys);
  let element: VisualElementEngineering = {
    id: requireGeneratedObjectId(createObjectId()),
    key,
    type: objectType,
    properties: {}
  };

  if (objectType === BUILTIN_VISUAL_OBJECT_TYPES.slider) {
    element = withValidatedProperties(element, schema, {
      [VISUAL_PROPERTY_KEYS.width]: 180,
      [VISUAL_PROPERTY_KEYS.height]: 56
    });
  }

  for (const [propertyKey, value] of Object.entries(initialProperties ?? {})) {
    element = withValidatedProperty(element, schema, propertyKey, value);
  }

  if (at) {
    assertFinitePoint(at, 'Object placement');
    element = withValidatedProperty(element, schema, VISUAL_PROPERTY_KEYS.x, at.x);
    element = withValidatedProperty(element, schema, VISUAL_PROPERTY_KEYS.y, at.y);
  }

  if (!parentObjectId) {
    return {
      ...screen,
      elements: [...(screen.elements ?? []), element]
    };
  }

  const parent = requireVisualElement(screen, parentObjectId);
  if (parent.type !== BUILTIN_VISUAL_OBJECT_TYPES.group) {
    throw new Error(`Visual object '${parentObjectId}' cannot contain child objects because it is not a core.group.`);
  }

  return updateScreenElement(screen, parentObjectId, current => ({
    ...current,
    children: [...(current.children ?? []), element]
  }));
}

function moveVisualObjects(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  delta: VisualEditorPoint
): ScreenEngineering {
  assertFinitePoint(delta, 'Move delta');
  const ids = requireVisualElements(screen, objectIds);
  let next = screen;
  for (const objectId of ids) {
    next = updateScreenElement(next, objectId, element => {
      const schema = getBuiltinVisualObjectSchema(element.type);
      return withValidatedProperties(element, schema, {
        [VISUAL_PROPERTY_KEYS.x]: effectiveNumericProperty(element, schema, VISUAL_PROPERTY_KEYS.x) + delta.x,
        [VISUAL_PROPERTY_KEYS.y]: effectiveNumericProperty(element, schema, VISUAL_PROPERTY_KEYS.y) + delta.y
      });
    });
  }
  return next;
}

function resizeVisualObject(
  screen: ScreenEngineering,
  objectId: string,
  bounds: VisualEditorBounds
): ScreenEngineering {
  assertFiniteBounds(bounds);
  requireVisualElement(screen, objectId);
  return updateScreenElement(screen, objectId, element => {
    const schema = getBuiltinVisualObjectSchema(element.type);
    return withValidatedProperties(element, schema, {
      [VISUAL_PROPERTY_KEYS.x]: bounds.x,
      [VISUAL_PROPERTY_KEYS.y]: bounds.y,
      [VISUAL_PROPERTY_KEYS.width]: bounds.width,
      [VISUAL_PROPERTY_KEYS.height]: bounds.height
    });
  });
}

function rotateVisualObjects(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  deltaDegrees: number
): ScreenEngineering {
  if (!Number.isFinite(deltaDegrees)) throw new Error('Rotation delta must be finite.');
  const ids = requireVisualElements(screen, objectIds);
  let next = screen;
  for (const objectId of ids) {
    next = updateScreenElement(next, objectId, element => {
      const schema = getBuiltinVisualObjectSchema(element.type);
      return withValidatedProperty(
        element,
        schema,
        VISUAL_PROPERTY_KEYS.rotation,
        effectiveNumericProperty(element, schema, VISUAL_PROPERTY_KEYS.rotation) + deltaDegrees
      );
    });
  }
  return next;
}

function duplicateVisualObjects(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  createObjectId: () => string,
  duplicateOffset: number
): ScreenEngineering {
  if (!Number.isFinite(duplicateOffset)) throw new Error('Duplicate offset must be finite.');
  const ids = new Set(requireVisualElements(screen, objectIds));
  const usedKeys = collectVisualElementKeys(screen.elements);
  const nextElements = duplicateSelectedElements(
    screen.elements ?? [],
    ids,
    usedKeys,
    createObjectId,
    duplicateOffset
  );
  return { ...screen, elements: nextElements };
}

function deleteVisualObjects(screen: ScreenEngineering, objectIds: readonly string[]): ScreenEngineering {
  const ids = new Set(requireVisualElements(screen, objectIds));
  return {
    ...screen,
    elements: deleteElementsFromTree(screen.elements ?? [], ids)
  };
}

function changeVisualObjectZOrder(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  operation: VisualEditorZOrderOperation
): ScreenEngineering {
  const ids = requireVisualElements(screen, objectIds);
  let next = screen;
  for (const objectId of ids) {
    const current = requireVisualElement(next, objectId);
    const schema = getBuiltinVisualObjectSchema(current.type);
    const currentZ = effectiveNumericProperty(current, schema, VISUAL_PROPERTY_KEYS.zIndex);
    const siblingElements = findSiblingElements(next.elements ?? [], objectId);
    if (!siblingElements) throw new Error(`Visual object '${objectId}' has no canonical sibling container.`);
    const siblingZ = siblingElements.map(element => effectiveSiblingZIndex(element));
    const minimum = siblingZ.length ? Math.min(...siblingZ) : currentZ;
    const maximum = siblingZ.length ? Math.max(...siblingZ) : currentZ;
    const nextZ = zOrderValue(currentZ, minimum, maximum, operation);
    next = updateScreenElement(next, objectId, element => withValidatedProperty(
      element,
      getBuiltinVisualObjectSchema(element.type),
      VISUAL_PROPERTY_KEYS.zIndex,
      nextZ
    ));
  }
  return next;
}

function setVisualProperty(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  propertyKey: string,
  value: VisualEngineeringPropertyValue
): ScreenEngineering {
  const ids = requireVisualElements(screen, objectIds);
  for (const objectId of ids) {
    const element = requireVisualElement(screen, objectId);
    validateEditableProperty(element, propertyKey, value);
  }

  let next = screen;
  for (const objectId of ids) {
    next = updateScreenElement(next, objectId, element => withValidatedProperty(
      element,
      getBuiltinVisualObjectSchema(element.type),
      propertyKey,
      value
    ));
  }
  return next;
}

function removeVisualProperty(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  propertyKey: string
): ScreenEngineering {
  const ids = requireVisualElements(screen, objectIds);
  for (const objectId of ids) {
    const element = requireVisualElement(screen, objectId);
    const schema = getBuiltinVisualObjectSchema(element.type);
    const definition = schema.getRequired(propertyKey);
    if (!definition.engineeringEditable) {
      throw new Error(`Visual property '${propertyKey}' is not Engineering-editable for '${element.type}'.`);
    }
  }

  let next = screen;
  for (const objectId of ids) {
    next = updateScreenElement(next, objectId, element => {
      const properties = { ...(element.properties ?? {}) };
      delete properties[propertyKey];
      return { ...element, properties };
    });
  }
  return next;
}

function setVisualBinding(
  screen: ScreenEngineering,
  objectId: string,
  binding: BindingEngineering
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  validateBindingDestination(element, binding.key);
  if (!binding.kind.trim()) throw new Error('Visual binding kind cannot be empty.');
  if (!binding.target.trim()) throw new Error('Visual binding target cannot be empty.');

  return updateScreenElement(screen, objectId, current => {
    const bindings = (current.bindings ?? []).filter(existing => existing.key !== binding.key);
    return {
      ...current,
      bindings: [...bindings, cloneEngineeringValue(binding)]
    };
  });
}

function removeVisualBinding(
  screen: ScreenEngineering,
  objectId: string,
  propertyKey: string
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  validateBindingDestination(element, propertyKey);
  return updateScreenElement(screen, objectId, current => ({
    ...current,
    bindings: (current.bindings ?? []).filter(binding => binding.key !== propertyKey)
  }));
}

function setVisualPropertyExpression(
  screen: ScreenEngineering,
  objectId: string,
  configuration: VisualPropertyExpressionEngineering
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  validateDynamicDestination(element, configuration.propertyKey, configuration.expression.resultType);
  return updateScreenElement(screen, objectId, current => ({
    ...current,
    propertyExpressions: [
      ...(current.propertyExpressions ?? []).filter(item => item.propertyKey !== configuration.propertyKey),
      cloneEngineeringValue(configuration)
    ]
  }));
}

function removeVisualPropertyExpression(
  screen: ScreenEngineering,
  objectId: string,
  propertyKey: string
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  validateDynamicDestinationProperty(element, propertyKey);
  return updateScreenElement(screen, objectId, current => ({
    ...current,
    propertyExpressions: (current.propertyExpressions ?? []).filter(item => item.propertyKey !== propertyKey)
  }));
}

function setVisualBooleanCondition(
  screen: ScreenEngineering,
  objectId: string,
  configuration: VisualBooleanConditionEngineering
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  validateDynamicDestination(element, configuration.propertyKey, 'Boolean');
  if (configuration.kind === 'Direct' && configuration.source.valueType !== 'Boolean') {
    throw new Error('Direct Boolean Condition requires a Boolean source.');
  }
  if (configuration.kind === 'NumericInterval' && configuration.source.valueType !== 'Number') {
    throw new Error('Numeric interval condition requires a Number source.');
  }
  return updateScreenElement(screen, objectId, current => ({
    ...current,
    booleanConditions: [
      ...(current.booleanConditions ?? []).filter(item => item.propertyKey !== configuration.propertyKey),
      cloneEngineeringValue(configuration)
    ]
  }));
}

function removeVisualBooleanCondition(
  screen: ScreenEngineering,
  objectId: string,
  propertyKey: string
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  validateDynamicDestination(element, propertyKey, 'Boolean');
  return updateScreenElement(screen, objectId, current => ({
    ...current,
    booleanConditions: (current.booleanConditions ?? []).filter(item => item.propertyKey !== propertyKey)
  }));
}

function setVisualAnalogFill(
  screen: ScreenEngineering,
  objectId: string,
  configuration: VisualAnalogFillEngineering
): ScreenEngineering {
  const element = requireVisualElement(screen, objectId);
  if (!supportsAnalogFill(element.type)) {
    throw new Error(`Visual object type '${element.type}' does not support Analog Fill.`);
  }
  if (configuration.source.valueType !== 'Number') {
    throw new Error('Analog Fill requires a Number source.');
  }
  if (!Number.isFinite(configuration.inputMinimum) || !Number.isFinite(configuration.inputMaximum) || configuration.inputMinimum === configuration.inputMaximum) {
    throw new Error('Analog Fill requires finite and different input limits.');
  }
  return updateScreenElement(screen, objectId, current => ({
    ...current,
    analogFill: cloneEngineeringValue(configuration)
  }));
}

function removeVisualAnalogFill(screen: ScreenEngineering, objectId: string): ScreenEngineering {
  requireVisualElement(screen, objectId);
  return updateScreenElement(screen, objectId, current => ({ ...current, analogFill: null }));
}

function validateDynamicDestinationProperty(element: VisualElementEngineering, propertyKey: string): 'Boolean' | 'Number' {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const definition = schema.getRequired(propertyKey);
  if (!definition.engineeringEditable) {
    throw new Error(`Visual property '${propertyKey}' is not Engineering-editable for '${element.type}'.`);
  }
  if (!definition.supportsBinding) {
    throw new Error(`Visual property '${propertyKey}' does not support Binding/Expression.`);
  }
  if (definition.type === 'boolean') return 'Boolean';
  if (definition.type === 'number') return 'Number';
  throw new Error(`Visual property '${propertyKey}' is not a Boolean/Number dynamic destination.`);
}

function validateDynamicDestination(
  element: VisualElementEngineering,
  propertyKey: string,
  resultType: 'Boolean' | 'Number'
): void {
  const expected = validateDynamicDestinationProperty(element, propertyKey);
  if (expected !== resultType) {
    throw new Error(`Dynamic result type '${resultType}' is incompatible with visual property '${propertyKey}' type '${expected}'.`);
  }
}

function validateEditableProperty(
  element: VisualElementEngineering,
  propertyKey: string,
  value: VisualEngineeringPropertyValue
): void {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const definition = schema.getRequired(propertyKey);
  if (!definition.engineeringEditable) {
    throw new Error(`Visual property '${propertyKey}' is not Engineering-editable for '${element.type}'.`);
  }
  const validation = schema.validate(propertyKey, value);
  if (!validation.ok) {
    throw new Error(`Invalid value for visual property '${propertyKey}': ${validation.code}.`);
  }
}

function validateBindingDestination(element: VisualElementEngineering, propertyKey: string): void {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const definition = schema.getRequired(propertyKey);
  if (!definition.supportsBinding) {
    throw new Error(`Visual property '${propertyKey}' does not support canonical binding.`);
  }
}

function withValidatedProperties(
  element: VisualElementEngineering,
  schema: VisualObjectPropertySchema,
  values: Readonly<Record<string, VisualEngineeringPropertyValue>>
): VisualElementEngineering {
  let next = element;
  for (const [propertyKey, value] of Object.entries(values)) {
    next = withValidatedProperty(next, schema, propertyKey, value);
  }
  return next;
}

function withValidatedProperty(
  element: VisualElementEngineering,
  schema: VisualObjectPropertySchema,
  propertyKey: string,
  value: VisualEngineeringPropertyValue
): VisualElementEngineering {
  const definition = schema.getRequired(propertyKey);
  if (!definition.engineeringEditable) {
    throw new Error(`Visual property '${propertyKey}' is not Engineering-editable for '${element.type}'.`);
  }
  const validation = schema.validate(propertyKey, value);
  if (!validation.ok) {
    throw new Error(`Invalid value for visual property '${propertyKey}': ${validation.code}.`);
  }
  return {
    ...element,
    properties: {
      ...(element.properties ?? {}),
      [propertyKey]: cloneEngineeringValue(validation.value)
    }
  };
}

function effectiveNumericProperty(
  element: VisualElementEngineering,
  schema: VisualObjectPropertySchema,
  propertyKey: string
): number {
  const explicit = element.properties?.[propertyKey];
  const candidate = explicit === undefined ? schema.getRequired(propertyKey).defaultValue : explicit;
  const validation = schema.validate(propertyKey, candidate);
  if (!validation.ok || typeof validation.value !== 'number') {
    throw new Error(`Visual property '${propertyKey}' is not a valid numeric value for '${element.type}'.`);
  }
  return validation.value;
}

function effectiveSiblingZIndex(element: VisualElementEngineering): number {
  if (!element.type.startsWith('core.')) {
    const legacy = element.properties?.[VISUAL_PROPERTY_KEYS.zIndex];
    return typeof legacy === 'number' && Number.isFinite(legacy) ? legacy : 0;
  }
  const schema = getBuiltinVisualObjectSchema(element.type);
  return effectiveNumericProperty(element, schema, VISUAL_PROPERTY_KEYS.zIndex);
}

function zOrderValue(
  current: number,
  minimum: number,
  maximum: number,
  operation: VisualEditorZOrderOperation
): number {
  switch (operation) {
    case 'bringForward': return current + 1;
    case 'sendBackward': return current - 1;
    case 'bringToFront': return maximum + 1;
    case 'sendToBack': return minimum - 1;
  }
}

function duplicateSelectedElements(
  elements: readonly VisualElementEngineering[],
  selectedIds: ReadonlySet<string>,
  usedKeys: Set<string>,
  createObjectId: () => string,
  duplicateOffset: number
): VisualElementEngineering[] {
  const result: VisualElementEngineering[] = [];
  for (const element of elements) {
    const selected = Boolean(element.id && selectedIds.has(element.id));
    if (selected) {
      result.push(element);
      result.push(cloneVisualElementWithNewIdentity(element, usedKeys, createObjectId, duplicateOffset, true));
      continue;
    }

    if (element.children?.length) {
      const children = duplicateSelectedElements(element.children, selectedIds, usedKeys, createObjectId, duplicateOffset);
      result.push(children === element.children ? element : { ...element, children });
      continue;
    }

    result.push(element);
  }
  return result;
}

function cloneVisualElementWithNewIdentity(
  element: VisualElementEngineering,
  usedKeys: Set<string>,
  createObjectId: () => string,
  duplicateOffset: number,
  applyOffset: boolean
): VisualElementEngineering {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const clone: VisualElementEngineering = {
    ...cloneEngineeringValue(element),
    id: requireGeneratedObjectId(createObjectId()),
    key: nextVisualElementKey(`${element.key || element.type.replace(/^core\./, '')}-copy`, usedKeys),
    children: element.children?.map(child => cloneVisualElementWithNewIdentity(child, usedKeys, createObjectId, duplicateOffset, false)) ?? null
  };

  if (!applyOffset) return clone;
  return withValidatedProperties(clone, schema, {
    [VISUAL_PROPERTY_KEYS.x]: effectiveNumericProperty(clone, schema, VISUAL_PROPERTY_KEYS.x) + duplicateOffset,
    [VISUAL_PROPERTY_KEYS.y]: effectiveNumericProperty(clone, schema, VISUAL_PROPERTY_KEYS.y) + duplicateOffset
  });
}

function deleteElementsFromTree(
  elements: readonly VisualElementEngineering[],
  deletedIds: ReadonlySet<string>
): VisualElementEngineering[] {
  const result: VisualElementEngineering[] = [];
  for (const element of elements) {
    if (element.id && deletedIds.has(element.id)) continue;
    if (element.children?.length) {
      result.push({ ...element, children: deleteElementsFromTree(element.children, deletedIds) });
    } else {
      result.push(element);
    }
  }
  return result;
}

function findSiblingElements(
  elements: readonly VisualElementEngineering[],
  objectId: string
): readonly VisualElementEngineering[] | null {
  if (elements.some(element => element.id === objectId)) return elements;
  for (const element of elements) {
    if (!element.children?.length) continue;
    const found = findSiblingElements(element.children, objectId);
    if (found) return found;
  }
  return null;
}

function requireVisualElements(screen: ScreenEngineering, objectIds: readonly string[]): string[] {
  const ids = [...new Set(objectIds.filter(id => id.trim().length > 0))];
  if (ids.length === 0) throw new Error('Visual mutation requires at least one stable object ID.');
  for (const objectId of ids) requireVisualElement(screen, objectId);
  return ids;
}

function requireVisualElement(screen: ScreenEngineering, objectId: string): VisualElementEngineering {
  const element = findVisualElement(screen.elements ?? [], objectId);
  if (!element) throw new Error(`Visual object '${objectId}' was not found in the canonical Screen draft.`);
  if (!element.id) throw new Error(`Visual object '${objectId}' has no stable canonical ID.`);
  return element;
}

function findVisualElement(
  elements: readonly VisualElementEngineering[],
  objectId: string
): VisualElementEngineering | null {
  for (const element of elements) {
    if (element.id === objectId) return element;
    const nested = element.children?.length ? findVisualElement(element.children, objectId) : null;
    if (nested) return nested;
  }
  return null;
}

function collectVisualElementKeys(
  elements: readonly VisualElementEngineering[] | null | undefined,
  target: Set<string> = new Set<string>()
): Set<string> {
  for (const element of elements ?? []) {
    if (element.key) target.add(element.key.toLowerCase());
    collectVisualElementKeys(element.children, target);
  }
  return target;
}

function nextVisualElementKey(base: string, usedKeys: Set<string>): string {
  const normalizedBase = normalizeObjectKeyBase(base);
  let candidate = normalizedBase;
  let index = 2;
  while (usedKeys.has(candidate.toLowerCase())) {
    candidate = `${normalizedBase}-${index}`;
    index += 1;
  }
  usedKeys.add(candidate.toLowerCase());
  return candidate;
}

function normalizeObjectKeyBase(value: string): string {
  const normalized = value
    .trim()
    .replace(/[^A-Za-z0-9._:-]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return normalized || 'object';
}

function requireGeneratedObjectId(value: string): string {
  const id = value.trim();
  if (!id) throw new Error('Visual object identity generator returned an empty ID.');
  return id;
}

function createRandomObjectId(): string {
  const randomUUID = globalThis.crypto?.randomUUID;
  if (typeof randomUUID !== 'function') {
    throw new Error('Stable visual object identity requires crypto.randomUUID().');
  }
  return randomUUID.call(globalThis.crypto);
}

function assertFinitePoint(point: VisualEditorPoint, label: string): void {
  if (!Number.isFinite(point.x) || !Number.isFinite(point.y)) {
    throw new Error(`${label} must contain finite x/y values.`);
  }
}

function assertFiniteBounds(bounds: VisualEditorBounds): void {
  if (![bounds.x, bounds.y, bounds.width, bounds.height].every(Number.isFinite)) {
    throw new Error('Resize bounds must contain finite x/y/width/height values.');
  }
}

function updateElementTree(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  update: (element: VisualElementEngineering) => VisualElementEngineering
): [VisualElementEngineering[], boolean] {
  let changed = false;
  const next = elements.map(element => {
    if (element.id === objectId) {
      changed = true;
      return cloneEngineeringValue(update(cloneEngineeringValue(element)));
    }

    if (!element.children?.length) return element;
    const [children, childChanged] = updateElementTree(element.children, objectId, update);
    if (!childChanged) return element;
    changed = true;
    return { ...element, children };
  });
  return [next, changed];
}

function nextScreenIndex(existing: readonly ScreenEngineering[]): number {
  const used = new Set(existing.map(screen => screen.key.toLowerCase()));
  let index = existing.length + 1;
  while (used.has(`screen-${index}`)) index += 1;
  return index;
}
