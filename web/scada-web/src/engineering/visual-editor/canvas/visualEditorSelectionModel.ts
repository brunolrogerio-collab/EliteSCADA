import type { VisualElementEngineering } from '../../types';
import {
  getBuiltinVisualObjectSchema,
  VISUAL_PROPERTY_KEYS
} from '../../../visual-runtime';

export type VisualEditorLogicalPoint = Readonly<{ x: number; y: number }>;
export type VisualEditorLogicalRect = Readonly<{
  x: number;
  y: number;
  width: number;
  height: number;
  right: number;
  bottom: number;
}>;

export type VisualEditorMarqueeMode = 'intersect' | 'contain';

export type VisualEditorSelectionCandidate = Readonly<{
  objectId: string;
  bounds: VisualEditorLogicalRect;
  zIndex: number;
  documentOrder: number;
}>;

/**
 * Normalizes a pointer drag into a logical-canvas rectangle. Callers must pass
 * logical coordinates, so zoom/pan never leak into the selection semantics.
 */
export function normalizeVisualEditorMarquee(
  start: VisualEditorLogicalPoint,
  end: VisualEditorLogicalPoint
): VisualEditorLogicalRect {
  const x = Math.min(start.x, end.x);
  const y = Math.min(start.y, end.y);
  const right = Math.max(start.x, end.x);
  const bottom = Math.max(start.y, end.y);
  return Object.freeze({
    x,
    y,
    width: right - x,
    height: bottom - y,
    right,
    bottom
  });
}

/**
 * Resolves marquee selection against one canonical sibling container. Groups
 * remain encapsulated and therefore select as a single authoring object here;
 * entering a group simply calls this function with that group's children.
 */
export function resolveVisualEditorMarqueeSelection(
  elements: readonly VisualElementEngineering[],
  marquee: VisualEditorLogicalRect,
  mode: VisualEditorMarqueeMode = 'intersect'
): readonly string[] {
  const normalizedMarquee = normalizeRect(marquee);
  return Object.freeze(
    collectSiblingCandidates(elements)
      .filter(candidate => mode === 'contain'
        ? containsRect(normalizedMarquee, candidate.bounds)
        : intersectsRect(normalizedMarquee, candidate.bounds))
      .sort(compareVisualEditorDocumentOrder)
      .map(candidate => candidate.objectId)
  );
}

/**
 * Predictable overlap picking: highest canonical zIndex wins. Equal zIndex is
 * resolved by later document order, matching ordinary painter's-order render.
 */
export function pickTopmostVisualEditorObjectAtPoint(
  elements: readonly VisualElementEngineering[],
  point: VisualEditorLogicalPoint
): string | null {
  const hit = collectSiblingCandidates(elements)
    .filter(candidate => containsPoint(candidate.bounds, point))
    .sort((left, right) =>
      right.zIndex - left.zIndex || right.documentOrder - left.documentOrder)[0];
  return hit?.objectId ?? null;
}

export function collectVisualEditorSelectionCandidates(
  elements: readonly VisualElementEngineering[]
): readonly VisualEditorSelectionCandidate[] {
  return Object.freeze(collectSiblingCandidates(elements));
}

function collectSiblingCandidates(
  elements: readonly VisualElementEngineering[]
): VisualEditorSelectionCandidate[] {
  const result: VisualEditorSelectionCandidate[] = [];
  elements.forEach((element, documentOrder) => {
    if (!element.id?.trim()) return;
    if (!effectiveBoolean(element, VISUAL_PROPERTY_KEYS.visible)) return;
    result.push(Object.freeze({
      objectId: element.id,
      bounds: transformedAxisAlignedBounds(element),
      zIndex: effectiveNumber(element, VISUAL_PROPERTY_KEYS.zIndex),
      documentOrder
    }));
  });
  return result;
}

function transformedAxisAlignedBounds(element: VisualElementEngineering): VisualEditorLogicalRect {
  const x = effectiveNumber(element, VISUAL_PROPERTY_KEYS.x);
  const y = effectiveNumber(element, VISUAL_PROPERTY_KEYS.y);
  const width = effectiveNumber(element, VISUAL_PROPERTY_KEYS.width);
  const height = effectiveNumber(element, VISUAL_PROPERTY_KEYS.height);
  const rotationDegrees = effectiveNumber(element, VISUAL_PROPERTY_KEYS.rotation);
  const scaleX = effectiveNumber(element, VISUAL_PROPERTY_KEYS.scaleX)
    * (effectiveBoolean(element, VISUAL_PROPERTY_KEYS.horizontalFlip) ? -1 : 1);
  const scaleY = effectiveNumber(element, VISUAL_PROPERTY_KEYS.scaleY)
    * (effectiveBoolean(element, VISUAL_PROPERTY_KEYS.verticalFlip) ? -1 : 1);

  const centerX = x + width / 2;
  const centerY = y + height / 2;
  const radians = rotationDegrees * Math.PI / 180;
  const cosine = Math.cos(radians);
  const sine = Math.sin(radians);
  const corners = [
    { x: -width / 2, y: -height / 2 },
    { x: width / 2, y: -height / 2 },
    { x: width / 2, y: height / 2 },
    { x: -width / 2, y: height / 2 }
  ].map(point => {
    const scaledX = point.x * scaleX;
    const scaledY = point.y * scaleY;
    return {
      x: centerX + scaledX * cosine - scaledY * sine,
      y: centerY + scaledX * sine + scaledY * cosine
    };
  });

  const left = Math.min(...corners.map(point => point.x));
  const top = Math.min(...corners.map(point => point.y));
  const right = Math.max(...corners.map(point => point.x));
  const bottom = Math.max(...corners.map(point => point.y));
  return Object.freeze({
    x: left,
    y: top,
    width: right - left,
    height: bottom - top,
    right,
    bottom
  });
}

function effectiveNumber(element: VisualElementEngineering, propertyKey: string): number {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const explicit = element.properties?.[propertyKey];
  const candidate = explicit === undefined ? schema.getRequired(propertyKey).defaultValue : explicit;
  const validation = schema.validate(propertyKey, candidate);
  if (!validation.ok || typeof validation.value !== 'number') {
    throw new Error(`Visual property '${propertyKey}' is not numeric for '${element.type}'.`);
  }
  return validation.value;
}

function effectiveBoolean(element: VisualElementEngineering, propertyKey: string): boolean {
  const schema = getBuiltinVisualObjectSchema(element.type);
  const explicit = element.properties?.[propertyKey];
  const candidate = explicit === undefined ? schema.getRequired(propertyKey).defaultValue : explicit;
  const validation = schema.validate(propertyKey, candidate);
  if (!validation.ok || typeof validation.value !== 'boolean') {
    throw new Error(`Visual property '${propertyKey}' is not Boolean for '${element.type}'.`);
  }
  return validation.value;
}

function normalizeRect(rect: VisualEditorLogicalRect): VisualEditorLogicalRect {
  const start = { x: rect.x, y: rect.y };
  const end = { x: rect.x + rect.width, y: rect.y + rect.height };
  return normalizeVisualEditorMarquee(start, end);
}

function containsPoint(rect: VisualEditorLogicalRect, point: VisualEditorLogicalPoint): boolean {
  return point.x >= rect.x && point.x <= rect.right && point.y >= rect.y && point.y <= rect.bottom;
}

function intersectsRect(left: VisualEditorLogicalRect, right: VisualEditorLogicalRect): boolean {
  return left.x <= right.right
    && left.right >= right.x
    && left.y <= right.bottom
    && left.bottom >= right.y;
}

function containsRect(outer: VisualEditorLogicalRect, inner: VisualEditorLogicalRect): boolean {
  return inner.x >= outer.x
    && inner.right <= outer.right
    && inner.y >= outer.y
    && inner.bottom <= outer.bottom;
}

function compareVisualEditorDocumentOrder(
  left: VisualEditorSelectionCandidate,
  right: VisualEditorSelectionCandidate
): number {
  return left.documentOrder - right.documentOrder;
}
