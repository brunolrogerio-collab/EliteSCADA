import type {
  ScreenEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyMap
} from '../types';
import {
  assertVisualElementsAuthoringEditable,
  isVisualElementEffectivelyAuthoringLocked
} from './visualEditorAuthoringModel';

export type VisualEditorClipboardPayload = Readonly<{
  sourceParentId: string | null;
  elements: readonly VisualElementEngineering[];
}>;

export type VisualEditorClipboardOptions = Readonly<{
  createObjectId?: () => string;
  createObjectKey?: (sourceKey: string, copyIndex: number) => string;
  offsetX?: number;
  offsetY?: number;
}>;

export type VisualEditorClipboardMutationResult = Readonly<{
  screen: ScreenEngineering;
  objectIds: readonly string[];
}>;

export function copyVisualEditorElements(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): VisualEditorClipboardPayload {
  const selection = resolveSiblingSelection(screen, objectIds);
  return Object.freeze({
    sourceParentId: selection.parentId,
    elements: Object.freeze(selection.selected.map(cloneElement))
  });
}

export function duplicateVisualEditorElements(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  options: VisualEditorClipboardOptions = {}
): VisualEditorClipboardMutationResult {
  const payload = copyVisualEditorElements(screen, objectIds);
  return pasteVisualEditorElements(screen, payload, payload.sourceParentId, options);
}

export function pasteVisualEditorElements(
  screen: ScreenEngineering,
  payload: VisualEditorClipboardPayload,
  targetParentId: string | null = payload.sourceParentId,
  options: VisualEditorClipboardOptions = {}
): VisualEditorClipboardMutationResult {
  if (targetParentId && isVisualElementEffectivelyAuthoringLocked(screen, targetParentId)) {
    throw new Error(`Visual parent '${targetParentId}' is locked for Engineering authoring.`);
  }
  const createObjectId = options.createObjectId ?? createRandomObjectId;
  const createObjectKey = options.createObjectKey ?? defaultCopyKey;
  const offsetX = options.offsetX ?? 10;
  const offsetY = options.offsetY ?? 10;
  const target = siblingContainer(screen, targetParentId);
  const existingKeys = collectKeys(screen.elements ?? []);
  const inserted: VisualElementEngineering[] = [];
  const insertedIds: string[] = [];

  payload.elements.forEach((source, copyIndex) => {
    const clone = cloneForPaste(
      source,
      createObjectId,
      sourceKey => uniqueKey(createObjectKey(sourceKey, copyIndex + 1), existingKeys),
      true,
      offsetX,
      offsetY
    );
    inserted.push(clone);
    insertedIds.push(clone.id!);
  });

  const nextSiblings = [...target.siblings, ...inserted];
  return Object.freeze({
    screen: replaceSiblingContainer(screen, targetParentId, nextSiblings),
    objectIds: Object.freeze(insertedIds)
  });
}

export function deleteVisualEditorElements(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): VisualEditorClipboardMutationResult {
  if (objectIds.length === 0) return Object.freeze({ screen, objectIds: Object.freeze([]) });
  assertVisualElementsAuthoringEditable(screen, objectIds);
  const selection = resolveSiblingSelection(screen, objectIds);
  const ids = new Set(selection.selected.map(element => element.id!));
  const next = selection.siblings.filter(element => !element.id || !ids.has(element.id));
  return Object.freeze({
    screen: replaceSiblingContainer(screen, selection.parentId, next),
    objectIds: Object.freeze([])
  });
}

export function nudgeVisualEditorElements(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  deltaX: number,
  deltaY: number
): VisualEditorClipboardMutationResult {
  if (!Number.isFinite(deltaX) || !Number.isFinite(deltaY)) {
    throw new Error('Visual editor nudge delta must be finite.');
  }
  assertVisualElementsAuthoringEditable(screen, objectIds);
  const selection = resolveSiblingSelection(screen, objectIds);
  const ids = new Set(selection.selected.map(element => element.id!));
  const next = selection.siblings.map(element => {
    if (!element.id || !ids.has(element.id)) return element;
    const properties = element.properties ?? {};
    return {
      ...element,
      properties: {
        ...properties,
        x: numeric(properties.x) + deltaX,
        y: numeric(properties.y) + deltaY
      }
    };
  });
  return Object.freeze({
    screen: replaceSiblingContainer(screen, selection.parentId, next),
    objectIds: Object.freeze([...ids])
  });
}

type SiblingSelection = Readonly<{
  parentId: string | null;
  siblings: readonly VisualElementEngineering[];
  selected: readonly VisualElementEngineering[];
}>;

function resolveSiblingSelection(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): SiblingSelection {
  const ids = [...new Set(objectIds.filter(Boolean))];
  if (ids.length === 0) throw new Error('Visual editor operation requires at least one object.');

  const located = ids.map(id => locateElement(screen.elements ?? [], id, null));
  if (located.some(item => !item)) {
    const missing = ids.find((_, index) => !located[index]);
    throw new Error(`Visual object '${missing}' was not found.`);
  }
  const parentId = located[0]!.parentId;
  if (located.some(item => item!.parentId !== parentId)) {
    throw new Error('Visual editor clipboard operations require objects from the same coordinate space.');
  }

  const siblings = siblingContainer(screen, parentId).siblings;
  const wanted = new Set(ids);
  const selected = siblings.filter(element => element.id && wanted.has(element.id));
  if (selected.length !== ids.length) {
    throw new Error('Visual editor selection could not be resolved deterministically.');
  }
  return Object.freeze({ parentId, siblings, selected: Object.freeze(selected) });
}

function locateElement(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  parentId: string | null
): Readonly<{ element: VisualElementEngineering; parentId: string | null }> | null {
  for (const element of elements) {
    if (element.id === objectId) return Object.freeze({ element, parentId });
    const nested = locateElement(element.children ?? [], objectId, element.id ?? parentId);
    if (nested) return nested;
  }
  return null;
}

function siblingContainer(
  screen: ScreenEngineering,
  parentId: string | null
): Readonly<{ siblings: readonly VisualElementEngineering[] }> {
  if (!parentId) return Object.freeze({ siblings: screen.elements ?? [] });
  const parent = locateElement(screen.elements ?? [], parentId, null)?.element;
  if (!parent) throw new Error(`Visual parent '${parentId}' was not found.`);
  return Object.freeze({ siblings: parent.children ?? [] });
}

function replaceSiblingContainer(
  screen: ScreenEngineering,
  parentId: string | null,
  siblings: readonly VisualElementEngineering[]
): ScreenEngineering {
  if (!parentId) return { ...screen, elements: [...siblings] };
  return {
    ...screen,
    elements: replaceChildren(screen.elements ?? [], parentId, siblings)
  };
}

function replaceChildren(
  elements: readonly VisualElementEngineering[],
  parentId: string,
  children: readonly VisualElementEngineering[]
): VisualElementEngineering[] {
  return elements.map(element => {
    if (element.id === parentId) return { ...element, children: [...children] };
    if (!element.children?.length) return element;
    return { ...element, children: replaceChildren(element.children, parentId, children) };
  });
}

function cloneForPaste(
  source: VisualElementEngineering,
  createObjectId: () => string,
  createKey: (sourceKey: string) => string,
  offsetRoot: boolean,
  offsetX: number,
  offsetY: number
): VisualElementEngineering {
  const properties = cloneProperties(source.properties);
  if (offsetRoot) {
    properties.x = numeric(properties.x) + offsetX;
    properties.y = numeric(properties.y) + offsetY;
  }
  return {
    ...structuredClone(source),
    id: createObjectId(),
    key: createKey(source.key),
    properties,
    children: source.children?.map(child =>
      cloneForPaste(child, createObjectId, createKey, false, offsetX, offsetY)) ?? source.children
  };
}

function cloneElement(element: VisualElementEngineering): VisualElementEngineering {
  return structuredClone(element);
}

function cloneProperties(
  properties: VisualEngineeringPropertyMap | null | undefined
): VisualEngineeringPropertyMap {
  return properties ? structuredClone(properties) : {};
}

function collectKeys(elements: readonly VisualElementEngineering[]): Set<string> {
  const keys = new Set<string>();
  const visit = (element: VisualElementEngineering) => {
    keys.add(element.key.toLocaleLowerCase('en-US'));
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements) visit(element);
  return keys;
}

function uniqueKey(candidate: string, existingKeys: Set<string>): string {
  const base = candidate.trim() || 'copy';
  let value = base;
  let suffix = 2;
  while (existingKeys.has(value.toLocaleLowerCase('en-US'))) {
    value = `${base}-${suffix}`;
    suffix += 1;
  }
  existingKeys.add(value.toLocaleLowerCase('en-US'));
  return value;
}

function defaultCopyKey(sourceKey: string, copyIndex: number): string {
  return `${sourceKey}-copy${copyIndex > 1 ? `-${copyIndex}` : ''}`;
}

function numeric(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

function createRandomObjectId(): string {
  const factory = globalThis.crypto?.randomUUID;
  if (typeof factory !== 'function') throw new Error('UUID generation is required for visual object duplication.');
  return factory.call(globalThis.crypto);
}
