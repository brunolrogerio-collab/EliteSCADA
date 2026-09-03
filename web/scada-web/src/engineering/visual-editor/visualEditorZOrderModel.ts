import type { ScreenEngineering, VisualElementEngineering } from '../types';
import {
  getBuiltinVisualObjectSchema,
  VISUAL_PROPERTY_KEYS
} from '../../visual-runtime';
import { assertVisualElementsAuthoringEditable } from './visualEditorAuthoringModel';

export type VisualEditorZOrderOperation = 'front' | 'back' | 'forward' | 'backward';

type StackItem = Readonly<{
  element: VisualElementEngineering;
  originalIndex: number;
  zIndex: number;
}>;

/**
 * Canonical deterministic z-order mutation. Selected siblings move as a stable
 * set and the resulting stacking context is normalized to collision-free zIndex
 * values, so Engineering and Runtime cannot disagree on tie-breaking.
 */
export function applyVisualEditorZOrderOperation(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  operation: VisualEditorZOrderOperation
): ScreenEngineering {
  const ids = [...new Set(objectIds.map(value => value.trim()).filter(Boolean))];
  if (ids.length === 0) throw new Error('Z-order operation requires at least one selected visual object.');
  assertVisualElementsAuthoringEditable(screen, ids);

  const parentId = parentIdFor(screen.elements ?? [], ids[0]);
  for (const objectId of ids.slice(1)) {
    if (parentIdFor(screen.elements ?? [], objectId) !== parentId) {
      throw new Error('Z-order operation requires all selected objects to share the same parent stacking context.');
    }
  }

  const siblings = parentId === null
    ? [...(screen.elements ?? [])]
    : [...(requireElement(screen, parentId).children ?? [])];
  const selected = new Set(ids);
  const stack: StackItem[] = siblings
    .map((element, originalIndex) => ({
      element,
      originalIndex,
      zIndex: effectiveZIndex(element)
    }))
    .sort((left, right) => left.zIndex - right.zIndex || left.originalIndex - right.originalIndex);

  if (operation === 'front') {
    stablePartition(stack, selected, false);
  } else if (operation === 'back') {
    stablePartition(stack, selected, true);
  } else if (operation === 'forward') {
    moveForwardOneLayer(stack, selected);
  } else {
    moveBackwardOneLayer(stack, selected);
  }

  const minimumZ = Math.min(...siblings.map(effectiveZIndex));
  const replacements = new Map<string, VisualElementEngineering>();
  stack.forEach((item, index) => {
    if (!item.element.id) return;
    replacements.set(item.element.id, withZIndex(item.element, minimumZ + index));
  });

  const nextSiblings = siblings.map(element =>
    element.id && replacements.has(element.id) ? replacements.get(element.id)! : element);
  return replaceSiblingContainer(screen, parentId, nextSiblings);
}

function stablePartition(stack: StackItem[], selected: ReadonlySet<string>, selectedFirst: boolean): void {
  const picked = stack.filter(item => Boolean(item.element.id && selected.has(item.element.id)));
  const rest = stack.filter(item => !item.element.id || !selected.has(item.element.id));
  stack.splice(0, stack.length, ...(selectedFirst ? [...picked, ...rest] : [...rest, ...picked]));
}

function moveForwardOneLayer(stack: StackItem[], selected: ReadonlySet<string>): void {
  for (let index = stack.length - 2; index >= 0; index -= 1) {
    const currentId = stack[index].element.id;
    const nextId = stack[index + 1].element.id;
    if (currentId && selected.has(currentId) && (!nextId || !selected.has(nextId))) {
      [stack[index], stack[index + 1]] = [stack[index + 1], stack[index]];
    }
  }
}

function moveBackwardOneLayer(stack: StackItem[], selected: ReadonlySet<string>): void {
  for (let index = 1; index < stack.length; index += 1) {
    const currentId = stack[index].element.id;
    const previousId = stack[index - 1].element.id;
    if (currentId && selected.has(currentId) && (!previousId || !selected.has(previousId))) {
      [stack[index - 1], stack[index]] = [stack[index], stack[index - 1]];
    }
  }
}

function effectiveZIndex(element: VisualElementEngineering): number {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const explicit = element.properties?.[VISUAL_PROPERTY_KEYS.zIndex];
  const candidate = explicit === undefined
    ? schema.getRequired(VISUAL_PROPERTY_KEYS.zIndex).defaultValue
    : explicit;
  const validation = schema.validate(VISUAL_PROPERTY_KEYS.zIndex, candidate);
  if (!validation.ok || typeof validation.value !== 'number') {
    throw new Error(`Visual object '${element.id ?? element.key}' has invalid canonical zIndex.`);
  }
  return validation.value;
}

function withZIndex(element: VisualElementEngineering, zIndex: number): VisualElementEngineering {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const validation = schema.validate(VISUAL_PROPERTY_KEYS.zIndex, zIndex);
  if (!validation.ok || typeof validation.value !== 'number') {
    throw new Error(`Invalid canonical zIndex '${zIndex}' for visual object '${element.id ?? element.key}'.`);
  }
  return {
    ...element,
    properties: {
      ...(element.properties ?? {}),
      [VISUAL_PROPERTY_KEYS.zIndex]: validation.value
    }
  };
}

function parentIdFor(elements: readonly VisualElementEngineering[], objectId: string): string | null {
  for (const element of elements) {
    if (element.id === objectId) return null;
    if (element.children?.some(child => child.id === objectId)) return element.id ?? null;
    if (element.children?.length) {
      const found = nestedParentId(element.children, objectId, element.id ?? null);
      if (found.found) return found.parentId;
    }
  }
  throw new Error(`Visual object '${objectId}' was not found in the canonical Screen draft.`);
}

function nestedParentId(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  currentParentId: string | null
): Readonly<{ found: boolean; parentId: string | null }> {
  for (const element of elements) {
    if (element.id === objectId) return { found: true, parentId: currentParentId };
    if (element.children?.length) {
      const found = nestedParentId(element.children, objectId, element.id ?? null);
      if (found.found) return found;
    }
  }
  return { found: false, parentId: null };
}

function requireElement(screen: ScreenEngineering, objectId: string): VisualElementEngineering {
  const found = findElement(screen.elements ?? [], objectId);
  if (!found) throw new Error(`Visual object '${objectId}' was not found in the canonical Screen draft.`);
  return found;
}

function findElement(
  elements: readonly VisualElementEngineering[],
  objectId: string
): VisualElementEngineering | null {
  for (const element of elements) {
    if (element.id === objectId) return element;
    const nested = element.children?.length ? findElement(element.children, objectId) : null;
    if (nested) return nested;
  }
  return null;
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
  if (!changed) throw new Error(`Visual group '${parentId}' was not found while replacing z-order siblings.`);
  return { ...screen, elements };
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
