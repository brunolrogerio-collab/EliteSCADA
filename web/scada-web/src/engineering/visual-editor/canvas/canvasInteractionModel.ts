import type { ScreenEngineering, VisualElementEngineering } from '../../types';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  VISUAL_PROPERTY_KEYS,
  getBuiltinVisualObjectSchema
} from '../../../visual-runtime';
import type {
  VisualEditorBounds,
  VisualEditorPoint,
  VisualEditorSelectionMode,
  VisualEditorViewport
} from '../visualEditorContracts';

export const DEFAULT_CANVAS_GRID_SIZE = 10;
export const MIN_CANVAS_ZOOM = 0.1;
export const MAX_CANVAS_ZOOM = 4;
export const MIN_CANVAS_OBJECT_SIZE = 1;

export type CanvasIdentityIssue = 'missing-id' | 'duplicate-id' | null;

export type CanvasGeometry = Readonly<{
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  scaleX: number;
  scaleY: number;
  zIndex: number;
  visible: boolean;
}>;

export type CanvasElementProjection = Readonly<{
  element: VisualElementEngineering;
  objectId: string | null;
  identityIssue: CanvasIdentityIssue;
  geometry: CanvasGeometry;
  children: readonly CanvasElementProjection[];
}>;

export type CanvasResizeHandle = 'northWest' | 'northEast' | 'southEast' | 'southWest';

export function projectCanvasElements(screen: ScreenEngineering): readonly CanvasElementProjection[] {
  const roots = screen.elements ?? [];
  const identityCounts = countStableIds(roots);
  return Object.freeze(roots.map(element => projectElement(element, identityCounts)));
}

export function resolveCanvasGeometry(element: VisualElementEngineering): CanvasGeometry {
  return Object.freeze({
    x: readNumber(element, VISUAL_PROPERTY_KEYS.x),
    y: readNumber(element, VISUAL_PROPERTY_KEYS.y),
    width: Math.max(0, readNumber(element, VISUAL_PROPERTY_KEYS.width)),
    height: Math.max(0, readNumber(element, VISUAL_PROPERTY_KEYS.height)),
    rotation: readNumber(element, VISUAL_PROPERTY_KEYS.rotation),
    scaleX: Math.max(0, readNumber(element, VISUAL_PROPERTY_KEYS.scaleX)),
    scaleY: Math.max(0, readNumber(element, VISUAL_PROPERTY_KEYS.scaleY)),
    zIndex: Math.trunc(readNumber(element, VISUAL_PROPERTY_KEYS.zIndex)),
    visible: readBoolean(element, VISUAL_PROPERTY_KEYS.visible)
  });
}

export function normalizeSelection(objectIds: readonly string[]): readonly string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const candidate of objectIds) {
    const objectId = candidate.trim();
    if (objectId.length === 0 || seen.has(objectId)) continue;
    seen.add(objectId);
    result.push(objectId);
  }
  return Object.freeze(result);
}

export function nextSelection(
  currentObjectIds: readonly string[],
  objectId: string,
  mode: VisualEditorSelectionMode
): readonly string[] {
  const current = [...normalizeSelection(currentObjectIds)];
  const stableObjectId = objectId.trim();
  if (stableObjectId.length === 0) return Object.freeze(current);

  switch (mode) {
    case 'replace':
      return Object.freeze([stableObjectId]);
    case 'add':
      return current.includes(stableObjectId)
        ? Object.freeze(current)
        : Object.freeze([...current, stableObjectId]);
    case 'toggle':
      return current.includes(stableObjectId)
        ? Object.freeze(current.filter(candidate => candidate !== stableObjectId))
        : Object.freeze([...current, stableObjectId]);
  }
}

export function selectionModeFromModifiers(modifiers: Readonly<{
  shiftKey: boolean;
  metaKey: boolean;
  ctrlKey: boolean;
}>): VisualEditorSelectionMode {
  if (modifiers.metaKey || modifiers.ctrlKey) return 'toggle';
  if (modifiers.shiftKey) return 'add';
  return 'replace';
}

export function snapScalar(value: number, gridSize = DEFAULT_CANVAS_GRID_SIZE): number {
  if (!Number.isFinite(value)) return 0;
  if (!Number.isFinite(gridSize) || gridSize <= 0) return value;
  return Math.round(value / gridSize) * gridSize;
}

export function snapPoint(point: VisualEditorPoint, gridSize = DEFAULT_CANVAS_GRID_SIZE): VisualEditorPoint {
  return Object.freeze({
    x: snapScalar(point.x, gridSize),
    y: snapScalar(point.y, gridSize)
  });
}

export function normalizeViewport(viewport: VisualEditorViewport): VisualEditorViewport {
  return Object.freeze({
    zoom: clampFinite(viewport.zoom, MIN_CANVAS_ZOOM, MAX_CANVAS_ZOOM, 1),
    panX: finiteOr(viewport.panX, 0),
    panY: finiteOr(viewport.panY, 0)
  });
}

export function zoomViewport(viewport: VisualEditorViewport, factor: number): VisualEditorViewport {
  const current = normalizeViewport(viewport);
  if (!Number.isFinite(factor) || factor <= 0) return current;
  return Object.freeze({
    ...current,
    zoom: clampFinite(current.zoom * factor, MIN_CANVAS_ZOOM, MAX_CANVAS_ZOOM, current.zoom)
  });
}

export function panViewport(
  viewport: VisualEditorViewport,
  deltaClient: VisualEditorPoint
): VisualEditorViewport {
  const current = normalizeViewport(viewport);
  return Object.freeze({
    zoom: current.zoom,
    panX: current.panX + finiteOr(deltaClient.x, 0),
    panY: current.panY + finiteOr(deltaClient.y, 0)
  });
}

export function clientDeltaToCanvas(
  deltaClient: VisualEditorPoint,
  viewport: VisualEditorViewport,
  snapEnabled: boolean,
  gridSize = DEFAULT_CANVAS_GRID_SIZE
): VisualEditorPoint {
  const normalized = normalizeViewport(viewport);
  const delta = Object.freeze({
    x: finiteOr(deltaClient.x, 0) / normalized.zoom,
    y: finiteOr(deltaClient.y, 0) / normalized.zoom
  });
  return snapEnabled ? snapPoint(delta, gridSize) : delta;
}

export function resizeBounds(
  start: VisualEditorBounds,
  delta: VisualEditorPoint,
  handle: CanvasResizeHandle,
  minimumSize = MIN_CANVAS_OBJECT_SIZE
): VisualEditorBounds {
  const minSize = Number.isFinite(minimumSize) && minimumSize > 0
    ? minimumSize
    : MIN_CANVAS_OBJECT_SIZE;
  const dx = finiteOr(delta.x, 0);
  const dy = finiteOr(delta.y, 0);

  let x = finiteOr(start.x, 0);
  let y = finiteOr(start.y, 0);
  let width = Math.max(minSize, finiteOr(start.width, minSize));
  let height = Math.max(minSize, finiteOr(start.height, minSize));

  const west = handle === 'northWest' || handle === 'southWest';
  const north = handle === 'northWest' || handle === 'northEast';
  const east = !west;
  const south = !north;

  if (west) {
    const proposedWidth = width - dx;
    if (proposedWidth >= minSize) {
      x += dx;
      width = proposedWidth;
    } else {
      x += width - minSize;
      width = minSize;
    }
  }
  if (east) width = Math.max(minSize, width + dx);

  if (north) {
    const proposedHeight = height - dy;
    if (proposedHeight >= minSize) {
      y += dy;
      height = proposedHeight;
    } else {
      y += height - minSize;
      height = minSize;
    }
  }
  if (south) height = Math.max(minSize, height + dy);

  return Object.freeze({ x, y, width, height });
}

export function rotationDeltaDegrees(
  centerClient: VisualEditorPoint,
  startClient: VisualEditorPoint,
  currentClient: VisualEditorPoint
): number {
  const startAngle = Math.atan2(startClient.y - centerClient.y, startClient.x - centerClient.x);
  const currentAngle = Math.atan2(currentClient.y - centerClient.y, currentClient.x - centerClient.x);
  return normalizeDegrees((currentAngle - startAngle) * 180 / Math.PI);
}

export function normalizeDegrees(value: number): number {
  if (!Number.isFinite(value)) return 0;
  let result = value % 360;
  if (result > 180) result -= 360;
  if (result <= -180) result += 360;
  return result;
}

export function hasMeaningfulDelta(delta: VisualEditorPoint): boolean {
  return Math.abs(delta.x) > Number.EPSILON || Math.abs(delta.y) > Number.EPSILON;
}

function projectElement(
  element: VisualElementEngineering,
  identityCounts: ReadonlyMap<string, number>
): CanvasElementProjection {
  const stableId = normalizeObjectId(element.id);
  const identityIssue: CanvasIdentityIssue = stableId === null
    ? 'missing-id'
    : identityCounts.get(stableId) === 1
      ? null
      : 'duplicate-id';

  return Object.freeze({
    element,
    objectId: identityIssue === null ? stableId : null,
    identityIssue,
    geometry: resolveCanvasGeometry(element),
    children: Object.freeze((element.children ?? []).map(child => projectElement(child, identityCounts)))
  });
}

function countStableIds(elements: readonly VisualElementEngineering[]): ReadonlyMap<string, number> {
  const counts = new Map<string, number>();
  const visit = (element: VisualElementEngineering): void => {
    const objectId = normalizeObjectId(element.id);
    if (objectId !== null) counts.set(objectId, (counts.get(objectId) ?? 0) + 1);
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements) visit(element);
  return counts;
}

function normalizeObjectId(value: string | null | undefined): string | null {
  const candidate = value?.trim();
  return candidate && candidate.length > 0 ? candidate : null;
}

function readNumber(element: VisualElementEngineering, propertyKey: string): number {
  const value = element.properties?.[propertyKey];
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  const fallback = registryDefault(element.type, propertyKey);
  return typeof fallback === 'number' && Number.isFinite(fallback) ? fallback : 0;
}

function readBoolean(element: VisualElementEngineering, propertyKey: string): boolean {
  const value = element.properties?.[propertyKey];
  if (typeof value === 'boolean') return value;
  const fallback = registryDefault(element.type, propertyKey);
  return typeof fallback === 'boolean' ? fallback : true;
}

function registryDefault(objectType: string, propertyKey: string): unknown {
  try {
    const schema = getBuiltinVisualObjectSchema(objectType);
    if (schema.declares(propertyKey)) return schema.getRequired(propertyKey).defaultValue;
  } catch {
    // Historical/non-built-in objects may still be projected generically. Their
    // interaction fallback comes from the common public registry, never a
    // Canvas-private property table.
  }
  return COMMON_VISUAL_PROPERTY_REGISTRY.get(propertyKey)?.defaultValue;
}

function finiteOr(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}

function clampFinite(value: number, minimum: number, maximum: number, fallback: number): number {
  const finite = finiteOr(value, fallback);
  return Math.min(maximum, Math.max(minimum, finite));
}
