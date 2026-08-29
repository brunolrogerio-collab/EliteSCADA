import type { VisualElementEngineering, VisualEngineeringPropertyValue } from '../types';
import type { VisualEditorPoint } from './visualEditorContracts';

export const POLYGON_POINTS_PROPERTY = 'points';

export function normalizePolygonPoints(points: readonly VisualEditorPoint[]): readonly VisualEditorPoint[] {
  if (points.length < 3) throw new Error('A polygon requires at least three vertices.');
  const normalized = points.map((point, index) => {
    if (!Number.isFinite(point.x) || !Number.isFinite(point.y)) {
      throw new Error(`Polygon vertex ${index + 1} must use finite coordinates.`);
    }
    return Object.freeze({ x: point.x, y: point.y });
  });
  if (distinctPointCount(normalized) < 3) throw new Error('A polygon requires at least three distinct vertices.');
  if (Math.abs(signedArea(normalized)) <= 1e-9) throw new Error('Polygon vertices must form a non-degenerate closed area.');
  return Object.freeze(normalized);
}

export function readPolygonPoints(element: Pick<VisualElementEngineering, 'properties'>): readonly VisualEditorPoint[] {
  const value = element.properties?.[POLYGON_POINTS_PROPERTY] as unknown;
  if (!Array.isArray(value)) return Object.freeze([]);
  const points: VisualEditorPoint[] = [];
  for (const candidate of value) {
    if (typeof candidate !== 'object' || candidate === null || Array.isArray(candidate)) return Object.freeze([]);
    const record = candidate as Record<string, unknown>;
    if (typeof record.x !== 'number' || typeof record.y !== 'number' || !Number.isFinite(record.x) || !Number.isFinite(record.y)) {
      return Object.freeze([]);
    }
    points.push(Object.freeze({ x: record.x, y: record.y }));
  }
  return Object.freeze(points);
}

export function withPolygonPoints(
  element: VisualElementEngineering,
  points: readonly VisualEditorPoint[]
): VisualElementEngineering {
  const normalized = normalizePolygonPoints(points);
  return {
    ...element,
    properties: {
      ...(element.properties ?? {}),
      [POLYGON_POINTS_PROPERTY]: normalized as unknown as VisualEngineeringPropertyValue
    }
  };
}

export function polygonBounds(points: readonly VisualEditorPoint[]): Readonly<{ minX: number; minY: number; width: number; height: number }> {
  if (points.length === 0) return Object.freeze({ minX: 0, minY: 0, width: 0, height: 0 });
  const xs = points.map(point => point.x);
  const ys = points.map(point => point.y);
  const minX = Math.min(...xs);
  const minY = Math.min(...ys);
  return Object.freeze({
    minX,
    minY,
    width: Math.max(...xs) - minX,
    height: Math.max(...ys) - minY
  });
}

export function polygonPointsAttribute(points: readonly VisualEditorPoint[]): string {
  return points.map(point => `${point.x},${point.y}`).join(' ');
}

function distinctPointCount(points: readonly VisualEditorPoint[]): number {
  return new Set(points.map(point => `${point.x}\u0000${point.y}`)).size;
}

function signedArea(points: readonly VisualEditorPoint[]): number {
  let area = 0;
  for (let index = 0; index < points.length; index += 1) {
    const current = points[index];
    const next = points[(index + 1) % points.length];
    area += current.x * next.y - next.x * current.y;
  }
  return area / 2;
}
