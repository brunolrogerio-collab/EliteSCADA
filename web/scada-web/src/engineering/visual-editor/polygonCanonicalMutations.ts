import type { ScreenEngineering, VisualElementEngineering, VisualEngineeringPropertyValue } from '../types';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../visual-runtime';
import type { VisualEditorPoint } from './visualEditorContracts';
import { normalizePolygonPoints, polygonBounds, POLYGON_POINTS_PROPERTY } from './polygonGeometry';

export function createCanonicalPolygon(
  screen: ScreenEngineering,
  absolutePoints: readonly VisualEditorPoint[],
  createId: () => string = () => crypto.randomUUID()
): Readonly<{ screen: ScreenEngineering; objectId: string }> {
  const normalized = normalizePolygonPoints(absolutePoints);
  const bounds = polygonBounds(normalized);
  const localPoints = normalizePolygonPoints(normalized.map(point => ({
    x: point.x - bounds.minX,
    y: point.y - bounds.minY
  })));
  const objectId = requireIdentity(createId());
  const key = nextPolygonKey(screen.elements ?? []);
  const element: VisualElementEngineering = {
    id: objectId,
    key,
    type: BUILTIN_VISUAL_OBJECT_TYPES.polygon,
    properties: {
      [VISUAL_PROPERTY_KEYS.x]: bounds.minX,
      [VISUAL_PROPERTY_KEYS.y]: bounds.minY,
      [VISUAL_PROPERTY_KEYS.width]: Math.max(bounds.width, 1),
      [VISUAL_PROPERTY_KEYS.height]: Math.max(bounds.height, 1),
      [POLYGON_POINTS_PROPERTY]: localPoints as unknown as VisualEngineeringPropertyValue
    }
  };
  return Object.freeze({
    objectId,
    screen: { ...screen, elements: [...(screen.elements ?? []), element] }
  });
}

export function updateCanonicalPolygonPoints(
  screen: ScreenEngineering,
  objectId: string,
  points: readonly VisualEditorPoint[]
): ScreenEngineering {
  const normalized = normalizePolygonPoints(points);
  let changed = false;
  const visit = (elements: readonly VisualElementEngineering[]): VisualElementEngineering[] => elements.map(element => {
    let next = element;
    if (element.id === objectId) {
      if (element.type !== BUILTIN_VISUAL_OBJECT_TYPES.polygon) throw new Error(`Visual object '${objectId}' is not a core.polygon.`);
      next = {
        ...element,
        properties: {
          ...(element.properties ?? {}),
          [POLYGON_POINTS_PROPERTY]: normalized as unknown as VisualEngineeringPropertyValue
        }
      };
      changed = true;
    }
    if (next.children?.length) {
      const children = visit(next.children);
      if (children !== next.children) next = { ...next, children };
    }
    return next;
  });

  const elements = visit(screen.elements ?? []);
  if (!changed) throw new Error(`Visual object '${objectId}' was not found.`);
  return { ...screen, elements };
}

function nextPolygonKey(elements: readonly VisualElementEngineering[]): string {
  const keys = new Set<string>();
  const visit = (element: VisualElementEngineering) => {
    keys.add(element.key.toLowerCase());
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of elements) visit(element);
  if (!keys.has('polygon')) return 'polygon';
  let index = 2;
  while (keys.has(`polygon-${index}`)) index += 1;
  return `polygon-${index}`;
}

function requireIdentity(value: string): string {
  const normalized = value.trim();
  if (!normalized || /[\u0000-\u001f\u007f]/i.test(normalized)) throw new Error('Generated polygon identity is invalid.');
  return normalized;
}
