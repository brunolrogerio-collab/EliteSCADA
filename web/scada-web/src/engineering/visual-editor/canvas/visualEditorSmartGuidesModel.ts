import type { ScreenEngineering, VisualElementEngineering } from '../../types';
import type { VisualEditorPoint } from '../visualEditorContracts';
import { resolveCanvasGeometry } from './canvasInteractionModel';

export type VisualEditorAlignmentAnchor = 'start' | 'center' | 'end';

export type VisualEditorSmartGuide = Readonly<{
  axis: 'vertical' | 'horizontal';
  position: number;
  sourceAnchor: VisualEditorAlignmentAnchor;
  targetAnchor: VisualEditorAlignmentAnchor;
  targetObjectId: string;
  adjustment: number;
}>;

export type VisualEditorMoveGuideResult = Readonly<{
  delta: VisualEditorPoint;
  verticalGuide: VisualEditorSmartGuide | null;
  horizontalGuide: VisualEditorSmartGuide | null;
}>;

type Bounds = Readonly<{
  x: number;
  y: number;
  width: number;
  height: number;
  right: number;
  bottom: number;
  centerX: number;
  centerY: number;
}>;

type LocatedElement = Readonly<{
  element: VisualElementEngineering;
  parentId: string | null;
}>;

type GuideCandidate = Readonly<{
  difference: number;
  distance: number;
  sourceAnchor: VisualEditorAlignmentAnchor;
  targetAnchor: VisualEditorAlignmentAnchor;
  targetObjectId: string;
  targetPosition: number;
  targetOrder: number;
  sourceOrder: number;
  targetAnchorOrder: number;
}>;

/**
 * Resolves deterministic smart-alignment snap after the Canvas grid snap. Only
 * siblings in the same canonical coordinate space participate, and the selected
 * set is treated as one moving bounding box.
 */
export function resolveVisualEditorMoveGuides(
  screen: ScreenEngineering,
  objectIds: readonly string[],
  requestedDelta: VisualEditorPoint,
  tolerance = 5
): VisualEditorMoveGuideResult {
  const ids = [...new Set(objectIds.map(value => value.trim()).filter(Boolean))];
  if (ids.length === 0 || !Number.isFinite(requestedDelta.x) || !Number.isFinite(requestedDelta.y)) {
    return emptyResult(requestedDelta);
  }
  if (!Number.isFinite(tolerance) || tolerance < 0) {
    throw new Error('Visual editor smart-guide tolerance must be a finite non-negative value.');
  }

  const located = ids.map(id => locateElement(screen.elements ?? [], id, null));
  if (located.some(item => item === null)) return emptyResult(requestedDelta);
  const parentId = located[0]!.parentId;
  if (located.some(item => item!.parentId !== parentId)) return emptyResult(requestedDelta);

  const siblings = parentId === null
    ? screen.elements ?? []
    : locateElement(screen.elements ?? [], parentId, null)?.element.children ?? [];
  const selectedIds = new Set(ids);
  const selected = siblings.filter(element => element.id && selectedIds.has(element.id));
  if (selected.length !== ids.length) return emptyResult(requestedDelta);

  const movingBounds = unionBounds(selected.map(elementBounds));
  const moved = translateBounds(movingBounds, requestedDelta);
  const targets = siblings
    .map((element, order) => ({ element, order }))
    .filter(item => Boolean(item.element.id && !selectedIds.has(item.element.id)))
    .filter(item => resolveCanvasGeometry(item.element).visible);

  const vertical = bestGuideCandidate(moved, targets, 'vertical', tolerance);
  const horizontal = bestGuideCandidate(moved, targets, 'horizontal', tolerance);
  const delta = Object.freeze({
    x: requestedDelta.x + (vertical?.difference ?? 0),
    y: requestedDelta.y + (horizontal?.difference ?? 0)
  });

  return Object.freeze({
    delta,
    verticalGuide: vertical ? Object.freeze({
      axis: 'vertical',
      position: vertical.targetPosition,
      sourceAnchor: vertical.sourceAnchor,
      targetAnchor: vertical.targetAnchor,
      targetObjectId: vertical.targetObjectId,
      adjustment: vertical.difference
    }) : null,
    horizontalGuide: horizontal ? Object.freeze({
      axis: 'horizontal',
      position: horizontal.targetPosition,
      sourceAnchor: horizontal.sourceAnchor,
      targetAnchor: horizontal.targetAnchor,
      targetObjectId: horizontal.targetObjectId,
      adjustment: horizontal.difference
    }) : null
  });
}

function bestGuideCandidate(
  moving: Bounds,
  targets: readonly Readonly<{ element: VisualElementEngineering; order: number }>[],
  axis: 'vertical' | 'horizontal',
  tolerance: number
): GuideCandidate | null {
  const sourceAnchors = axis === 'vertical'
    ? anchorValues(moving.x, moving.centerX, moving.right)
    : anchorValues(moving.y, moving.centerY, moving.bottom);
  const candidates: GuideCandidate[] = [];

  for (const target of targets) {
    if (!target.element.id) continue;
    const bounds = elementBounds(target.element);
    const targetAnchors = axis === 'vertical'
      ? anchorValues(bounds.x, bounds.centerX, bounds.right)
      : anchorValues(bounds.y, bounds.centerY, bounds.bottom);
    sourceAnchors.forEach((source, sourceOrder) => {
      targetAnchors.forEach((destination, targetAnchorOrder) => {
        const difference = destination.value - source.value;
        const distance = Math.abs(difference);
        if (distance > tolerance) return;
        candidates.push(Object.freeze({
          difference,
          distance,
          sourceAnchor: source.anchor,
          targetAnchor: destination.anchor,
          targetObjectId: target.element.id!,
          targetPosition: destination.value,
          targetOrder: target.order,
          sourceOrder,
          targetAnchorOrder
        }));
      });
    });
  }

  candidates.sort((left, right) =>
    left.distance - right.distance
    || left.targetOrder - right.targetOrder
    || left.sourceOrder - right.sourceOrder
    || left.targetAnchorOrder - right.targetAnchorOrder
    || left.targetObjectId.localeCompare(right.targetObjectId));
  return candidates[0] ?? null;
}

function anchorValues(start: number, center: number, end: number): readonly Readonly<{
  anchor: VisualEditorAlignmentAnchor;
  value: number;
}>[] {
  return Object.freeze([
    Object.freeze({ anchor: 'start', value: start }),
    Object.freeze({ anchor: 'center', value: center }),
    Object.freeze({ anchor: 'end', value: end })
  ]);
}

function elementBounds(element: VisualElementEngineering): Bounds {
  const geometry = resolveCanvasGeometry(element);
  return Object.freeze({
    x: geometry.x,
    y: geometry.y,
    width: geometry.width,
    height: geometry.height,
    right: geometry.x + geometry.width,
    bottom: geometry.y + geometry.height,
    centerX: geometry.x + geometry.width / 2,
    centerY: geometry.y + geometry.height / 2
  });
}

function unionBounds(values: readonly Bounds[]): Bounds {
  const x = Math.min(...values.map(value => value.x));
  const y = Math.min(...values.map(value => value.y));
  const right = Math.max(...values.map(value => value.right));
  const bottom = Math.max(...values.map(value => value.bottom));
  return Object.freeze({
    x,
    y,
    width: right - x,
    height: bottom - y,
    right,
    bottom,
    centerX: (x + right) / 2,
    centerY: (y + bottom) / 2
  });
}

function translateBounds(bounds: Bounds, delta: VisualEditorPoint): Bounds {
  return Object.freeze({
    ...bounds,
    x: bounds.x + delta.x,
    y: bounds.y + delta.y,
    right: bounds.right + delta.x,
    bottom: bounds.bottom + delta.y,
    centerX: bounds.centerX + delta.x,
    centerY: bounds.centerY + delta.y
  });
}

function locateElement(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  parentId: string | null
): LocatedElement | null {
  for (const element of elements) {
    if (element.id === objectId) return Object.freeze({ element, parentId });
    const nested = locateElement(element.children ?? [], objectId, element.id ?? parentId);
    if (nested) return nested;
  }
  return null;
}

function emptyResult(delta: VisualEditorPoint): VisualEditorMoveGuideResult {
  return Object.freeze({
    delta: Object.freeze({ x: delta.x, y: delta.y }),
    verticalGuide: null,
    horizontalGuide: null
  });
}
