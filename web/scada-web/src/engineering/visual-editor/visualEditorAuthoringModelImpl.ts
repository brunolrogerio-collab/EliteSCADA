import type {
  ScreenEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyValue
} from '../types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema,
  VISUAL_PROPERTY_KEYS,
  type VisualObjectPropertySchema
} from '../../visual-runtime';

export const VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY = 'engineering.authoring.locked';

export type VisualEditorAlignmentOperation =
  | 'left'
  | 'horizontalCenter'
  | 'right'
  | 'top'
  | 'verticalMiddle'
  | 'bottom';

export type VisualEditorDistributionOperation =
  | 'horizontalCenters'
  | 'verticalCenters'
  | 'horizontalSpacing'
  | 'verticalSpacing';

export type VisualEditorSizeOperation = 'sameWidth' | 'sameHeight' | 'sameSize';

export type VisualEditorAuthoringOperation =
  | Readonly<{
      kind: 'align';
      objectIds: readonly string[];
      operation: VisualEditorAlignmentOperation;
    }>
  | Readonly<{
      kind: 'distribute';
      objectIds: readonly string[];
      operation: VisualEditorDistributionOperation;
    }>
  | Readonly<{
      kind: 'size';
      objectIds: readonly string[];
      referenceObjectId: string;
      operation: VisualEditorSizeOperation;
    }>
  | Readonly<{
      kind: 'group';
      objectIds: readonly string[];
    }>
  | Readonly<{
      kind: 'ungroup';
      objectIds: readonly string[];
    }>
  | Readonly<{
      kind: 'lock';
      objectIds: readonly string[];
      locked: boolean;
    }>;

export type VisualEditorAuthoringOptions = Readonly<{
  createObjectId?: () => string;
}>;

type ElementBounds = Readonly<{
  x: number;
  y: number;
  width: number;
  height: number;
  right: number;
  bottom: number;
  centerX: number;
  centerY: number;
}>;

/**
 * Wave 14 C07 canonical authoring seam for operations that act on multiple
 * visual objects or authoring-only hierarchy state. It deliberately writes
 * only persisted Engineering data and never Runtime interaction properties.
 */
export function applyVisualEditorAuthoringOperation(
  screen: ScreenEngineering,
  operation: VisualEditorAuthoringOperation,
  options: VisualEditorAuthoringOptions = {}
): ScreenEngineering {
  switch (operation.kind) {
    case 'align':
      return alignElements(screen, operation.objectIds, operation.operation);
    case 'distribute':
      return distributeElements(screen, operation.objectIds, operation.operation);
    case 'size':
      return sizeElements(screen, operation.objectIds, operation.referenceObjectId, operation.operation);
    case 'group':
      return groupElements(screen, operation.objectIds, options.createObjectId ?? createRandomObjectId);
    case 'ungroup':
      return ungroupElements(screen, operation.objectIds);
    case 'lock':
      return setElementsLocked(screen, operation.objectIds, operation.locked);
  }
}

export function isVisualElementAuthoringLocked(element: VisualElementEngineering): boolean {
  return element.metadata?.[VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY]?.trim().toLowerCase() === 'true';
}

export function isVisualElementEffectivelyAuthoringLocked(
  screen: ScreenEngineering,
  objectId: string
): boolean {
  const current = requireElement(screen, objectId);
  if (isVisualElementAuthoringLocked(current)) return true;

  let parent = findParent(screen.elements ?? [], objectId);
  while (parent) {
    if (isVisualElementAuthoringLocked(parent)) return true;
    const parentId = parent.id?.trim();
    if (!parentId) break;
    parent = findParent(screen.elements ?? [], parentId);
  }
  return false;
}

export function assertVisualElementsAuthoringEditable(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): readonly string[] {
  const ids = requireSelection(screen, objectIds, 1);
  for (const objectId of ids) {
    if (isVisualElementEffectivelyAuthoringLocked(screen, objectId)) {
      throw new Error(`Visual object '${objectId}' is locked for Engineering authoring.`);
    }
  }
  return ids;
}

function alignElements(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  operation: VisualEditorAlignmentOperation
): ScreenEngineering {
  const { parentId, siblings, ids } = requireSiblingSelection(screen, objectIds, 2, true);
  const selected = siblings.filter(element => element.id && ids.has(element.id));
  const bounds = selected.map(element => elementBounds(element));
  const left = Math.min(...bounds.map(item => item.x));
  const top = Math.min(...bounds.map(item => item.y));
  const right = Math.max(...bounds.map(item => item.right));
  const bottom = Math.max(...bounds.map(item => item.bottom));
  const horizontalCenter = (left + right) / 2;
  const verticalMiddle = (top + bottom) / 2;

  const replacements = new Map<string, VisualElementEngineering>();
  selected.forEach((element, index) => {
    const currentBounds = bounds[index];
    let x = currentBounds.x;
    let y = currentBounds.y;
    switch (operation) {
      case 'left': x = left; break;
      case 'horizontalCenter': x = horizontalCenter - currentBounds.width / 2; break;
      case 'right': x = right - currentBounds.width; break;
      case 'top': y = top; break;
      case 'verticalMiddle': y = verticalMiddle - currentBounds.height / 2; break;
      case 'bottom': y = bottom - currentBounds.height; break;
    }
    replacements.set(element.id!, withGeometry(element, { x, y }));
  });

  return replaceSiblingContainer(screen, parentId, siblings.map(element =>
    element.id && replacements.has(element.id) ? replacements.get(element.id)! : element));
}

function distributeElements(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  operation: VisualEditorDistributionOperation
): ScreenEngineering {
  const { parentId, siblings, ids } = requireSiblingSelection(screen, objectIds, 3, true);
  const selected = siblings.filter(element => element.id && ids.has(element.id));
  const placements = selected.map((element, stableIndex) => ({
    element,
    bounds: elementBounds(element),
    stableIndex
  }));
  const replacements = new Map<string, VisualElementEngineering>();

  if (operation === 'horizontalCenters' || operation === 'horizontalSpacing') {
    placements.sort((a, b) => a.bounds.centerX - b.bounds.centerX || a.stableIndex - b.stableIndex);
  } else {
    placements.sort((a, b) => a.bounds.centerY - b.bounds.centerY || a.stableIndex - b.stableIndex);
  }

  if (operation === 'horizontalCenters') {
    const first = placements[0].bounds.centerX;
    const last = placements.at(-1)!.bounds.centerX;
    const step = (last - first) / (placements.length - 1);
    placements.forEach((item, index) => replacements.set(
      item.element.id!,
      withGeometry(item.element, { x: first + step * index - item.bounds.width / 2 })
    ));
  } else if (operation === 'verticalCenters') {
    const first = placements[0].bounds.centerY;
    const last = placements.at(-1)!.bounds.centerY;
    const step = (last - first) / (placements.length - 1);
    placements.forEach((item, index) => replacements.set(
      item.element.id!,
      withGeometry(item.element, { y: first + step * index - item.bounds.height / 2 })
    ));
  } else if (operation === 'horizontalSpacing') {
    const firstX = placements[0].bounds.x;
    const lastRight = placements.at(-1)!.bounds.right;
    const totalWidth = placements.reduce((sum, item) => sum + item.bounds.width, 0);
    const gap = (lastRight - firstX - totalWidth) / (placements.length - 1);
    let cursor = firstX;
    placements.forEach(item => {
      replacements.set(item.element.id!, withGeometry(item.element, { x: cursor }));
      cursor += item.bounds.width + gap;
    });
  } else {
    const firstY = placements[0].bounds.y;
    const lastBottom = placements.at(-1)!.bounds.bottom;
    const totalHeight = placements.reduce((sum, item) => sum + item.bounds.height, 0);
    const gap = (lastBottom - firstY - totalHeight) / (placements.length - 1);
    let cursor = firstY;
    placements.forEach(item => {
      replacements.set(item.element.id!, withGeometry(item.element, { y: cursor }));
      cursor += item.bounds.height + gap;
    });
  }

  return replaceSiblingContainer(screen, parentId, siblings.map(element =>
    element.id && replacements.has(element.id) ? replacements.get(element.id)! : element));
}

function sizeElements(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  referenceObjectId: string,
  operation: VisualEditorSizeOperation
): ScreenEngineering {
  const { parentId, siblings, ids } = requireSiblingSelection(screen, objectIds, 2, true);
  if (!ids.has(referenceObjectId)) {
    throw new Error('Same-size operation requires the reference object to be part of the selection.');
  }
  const reference = siblings.find(element => element.id === referenceObjectId);
  if (!reference) throw new Error(`Visual object '${referenceObjectId}' is not in the canonical sibling container.`);
  const target = elementBounds(reference);

  return replaceSiblingContainer(screen, parentId, siblings.map(element => {
    if (!element.id || !ids.has(element.id)) return element;
    const geometry: Record<string, number> = {};
    if (operation === 'sameWidth' || operation === 'sameSize') geometry.width = target.width;
    if (operation === 'sameHeight' || operation === 'sameSize') geometry.height = target.height;
    return withGeometry(element, geometry);
  }));
}

function groupElements(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  createObjectId: () => string
): ScreenEngineering {
  const { parentId, siblings, ids } = requireSiblingSelection(screen, objectIds, 2, true);
  const selected = siblings.filter(element => element.id && ids.has(element.id));
  const bounds = selected.map(element => elementBounds(element));
  const x = Math.min(...bounds.map(item => item.x));
  const y = Math.min(...bounds.map(item => item.y));
  const right = Math.max(...bounds.map(item => item.right));
  const bottom = Math.max(...bounds.map(item => item.bottom));
  const groupId = requireGeneratedObjectId(createObjectId());
  const usedKeys = collectElementKeys(screen.elements ?? []);
  const groupKey = nextElementKey('group', usedKeys);
  const zIndex = Math.max(...selected.map(element => effectiveNumber(element, VISUAL_PROPERTY_KEYS.zIndex)));

  const children = selected.map(element => {
    const child = elementBounds(element);
    return withGeometry(element, { x: child.x - x, y: child.y - y });
  });

  let group: VisualElementEngineering = {
    id: groupId,
    key: groupKey,
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: {},
    children
  };
  group = withGeometry(group, { x, y, width: right - x, height: bottom - y, zIndex });

  const firstSelectedIndex = siblings.findIndex(element => Boolean(element.id && ids.has(element.id)));
  const nextSiblings: VisualElementEngineering[] = [];
  siblings.forEach((element, index) => {
    if (index === firstSelectedIndex) nextSiblings.push(group);
    if (!element.id || !ids.has(element.id)) nextSiblings.push(element);
  });
  return replaceSiblingContainer(screen, parentId, nextSiblings);
}

function ungroupElements(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): ScreenEngineering {
  const { parentId, siblings, ids } = requireSiblingSelection(screen, objectIds, 1, true);
  const nextSiblings: VisualElementEngineering[] = [];

  for (const element of siblings) {
    if (!element.id || !ids.has(element.id)) {
      nextSiblings.push(element);
      continue;
    }
    if (element.type !== BUILTIN_VISUAL_OBJECT_TYPES.group || element.dynamoKey) {
      throw new Error(`Visual object '${element.id}' cannot be ungrouped because it is not an authoring group.`);
    }
    assertUngroupTransformSafe(element);
    const parentBounds = elementBounds(element);
    const parentZ = effectiveNumber(element, VISUAL_PROPERTY_KEYS.zIndex);
    for (const child of element.children ?? []) {
      const childBounds = elementBounds(child);
      const childZ = effectiveNumber(child, VISUAL_PROPERTY_KEYS.zIndex);
      nextSiblings.push(withGeometry(child, {
        x: parentBounds.x + childBounds.x,
        y: parentBounds.y + childBounds.y,
        zIndex: parentZ + childZ
      }));
    }
  }

  return replaceSiblingContainer(screen, parentId, nextSiblings);
}

function setElementsLocked(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  locked: boolean
): ScreenEngineering {
  const ids = requireSelection(screen, objectIds, 1);
  let elements = [...(screen.elements ?? [])];
  for (const objectId of ids) {
    const [next, changed] = updateElementTree(elements, objectId, element => {
      const metadata = { ...(element.metadata ?? {}) };
      if (locked) metadata[VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY] = 'true';
      else delete metadata[VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY];
      return { ...element, metadata };
    });
    if (!changed) throw new Error(`Visual object '${objectId}' was not found in the canonical Screen draft.`);
    elements = next;
  }
  return { ...screen, elements };
}

function requireSiblingSelection(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  minimum: number,
  requireEditable: boolean
): Readonly<{
  parentId: string | null;
  siblings: readonly VisualElementEngineering[];
  ids: ReadonlySet<string>;
}> {
  const selection = requireSelection(screen, objectIds, minimum);
  if (requireEditable) assertVisualElementsAuthoringEditable(screen, selection);
  const firstParentId = parentIdFor(screen.elements ?? [], selection[0]);
  for (const objectId of selection.slice(1)) {
    if (parentIdFor(screen.elements ?? [], objectId) !== firstParentId) {
      throw new Error('Visual authoring operation requires all selected objects to share the same parent coordinate space.');
    }
  }
  const siblings = firstParentId === null
    ? (screen.elements ?? [])
    : (requireElement(screen, firstParentId).children ?? []);
  return { parentId: firstParentId, siblings, ids: new Set(selection) };
}

function requireSelection(screen: ScreenEngineering, objectIds: readonly string[], minimum: number): string[] {
  const ids = [...new Set(objectIds.map(id => id.trim()).filter(Boolean))];
  if (ids.length < minimum) {
    throw new Error(`Visual authoring operation requires at least ${minimum} selected object${minimum === 1 ? '' : 's'}.`);
  }
  ids.forEach(objectId => requireElement(screen, objectId));
  return ids;
}

function replaceSiblingContainer(
  screen: ScreenEngineering,
  parentId: string | null,
  siblings: readonly VisualElementEngineering[]
): ScreenEngineering {
  if (parentId === null) return { ...screen, elements: [...siblings] };
  const [elements, changed] = updateElementTree(screen.elements ?? [], parentId, parent => ({
    ...parent,
    children: [...siblings]
  }));
  if (!changed) throw new Error(`Visual group '${parentId}' was not found while replacing its canonical children.`);
  return { ...screen, elements };
}

function elementBounds(element: VisualElementEngineering): ElementBounds {
  const x = effectiveNumber(element, VISUAL_PROPERTY_KEYS.x);
  const y = effectiveNumber(element, VISUAL_PROPERTY_KEYS.y);
  const width = effectiveNumber(element, VISUAL_PROPERTY_KEYS.width);
  const height = effectiveNumber(element, VISUAL_PROPERTY_KEYS.height);
  return {
    x,
    y,
    width,
    height,
    right: x + width,
    bottom: y + height,
    centerX: x + width / 2,
    centerY: y + height / 2
  };
}

function withGeometry(
  element: VisualElementEngineering,
  values: Readonly<Record<string, number>>
): VisualElementEngineering {
  const schema = getBuiltinVisualObjectSchema(element.type);
  let next = element;
  for (const [key, value] of Object.entries(values)) {
    next = withValidatedProperty(next, schema, key, value);
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
      [propertyKey]: validation.value
    }
  };
}

function effectiveNumber(element: VisualElementEngineering, propertyKey: string): number {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const explicit = element.properties?.[propertyKey];
  const candidate = explicit === undefined ? schema.getRequired(propertyKey).defaultValue : explicit;
  const validation = schema.validate(propertyKey, candidate);
  if (!validation.ok || typeof validation.value !== 'number') {
    throw new Error(`Visual property '${propertyKey}' is not a valid numeric value for '${element.type}'.`);
  }
  return validation.value;
}

function assertUngroupTransformSafe(group: VisualElementEngineering): void {
  const rotation = effectiveNumber(group, VISUAL_PROPERTY_KEYS.rotation);
  const scaleX = effectiveNumber(group, VISUAL_PROPERTY_KEYS.scaleX);
  const scaleY = effectiveNumber(group, VISUAL_PROPERTY_KEYS.scaleY);
  const horizontalFlip = effectiveBoolean(group, VISUAL_PROPERTY_KEYS.horizontalFlip);
  const verticalFlip = effectiveBoolean(group, VISUAL_PROPERTY_KEYS.verticalFlip);
  if (rotation !== 0 || scaleX !== 1 || scaleY !== 1 || horizontalFlip || verticalFlip) {
    throw new Error(`Visual group '${group.id ?? group.key}' must have identity transform before ungrouping to preserve canonical geometry.`);
  }
}

function effectiveBoolean(element: VisualElementEngineering, propertyKey: string): boolean {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const explicit = element.properties?.[propertyKey];
  const candidate = explicit === undefined ? schema.getRequired(propertyKey).defaultValue : explicit;
  const validation = schema.validate(propertyKey, candidate);
  if (!validation.ok || typeof validation.value !== 'boolean') {
    throw new Error(`Visual property '${propertyKey}' is not a valid Boolean value for '${element.type}'.`);
  }
  return validation.value;
}

function parentIdFor(elements: readonly VisualElementEngineering[], objectId: string): string | null {
  for (const element of elements) {
    if (element.id === objectId) return null;
    if (element.children?.some(child => child.id === objectId)) return element.id ?? null;
    if (element.children?.length) {
      const nested = parentIdForNested(element.children, objectId, element.id ?? null);
      if (nested.found) return nested.parentId;
    }
  }
  throw new Error(`Visual object '${objectId}' was not found in the canonical Screen draft.`);
}

function parentIdForNested(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  currentParentId: string | null
): Readonly<{ found: boolean; parentId: string | null }> {
  for (const element of elements) {
    if (element.id === objectId) return { found: true, parentId: currentParentId };
    if (element.children?.length) {
      const nested = parentIdForNested(element.children, objectId, element.id ?? null);
      if (nested.found) return nested;
    }
  }
  return { found: false, parentId: null };
}

function findParent(
  elements: readonly VisualElementEngineering[],
  objectId: string
): VisualElementEngineering | null {
  for (const element of elements) {
    if (element.children?.some(child => child.id === objectId)) return element;
    if (element.children?.length) {
      const nested = findParent(element.children, objectId);
      if (nested) return nested;
    }
  }
  return null;
}

function requireElement(screen: ScreenEngineering, objectId: string): VisualElementEngineering {
  const element = findElement(screen.elements ?? [], objectId);
  if (!element) throw new Error(`Visual object '${objectId}' was not found in the canonical Screen draft.`);
  if (!element.id) throw new Error(`Visual object '${objectId}' has no stable canonical ID.`);
  return element;
}

function findElement(elements: readonly VisualElementEngineering[], objectId: string): VisualElementEngineering | null {
  for (const element of elements) {
    if (element.id === objectId) return element;
    const nested = element.children?.length ? findElement(element.children, objectId) : null;
    if (nested) return nested;
  }
  return null;
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
      return update(element);
    }
    if (!element.children?.length) return element;
    const [children, childChanged] = updateElementTree(element.children, objectId, update);
    if (!childChanged) return element;
    changed = true;
    return { ...element, children };
  });
  return [next, changed];
}

function collectElementKeys(elements: readonly VisualElementEngineering[], target = new Set<string>()): Set<string> {
  for (const element of elements) {
    if (element.key) target.add(element.key.toLowerCase());
    if (element.children?.length) collectElementKeys(element.children, target);
  }
  return target;
}

function nextElementKey(base: string, usedKeys: Set<string>): string {
  let candidate = base;
  let index = 2;
  while (usedKeys.has(candidate.toLowerCase())) {
    candidate = `${base}-${index}`;
    index += 1;
  }
  usedKeys.add(candidate.toLowerCase());
  return candidate;
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
