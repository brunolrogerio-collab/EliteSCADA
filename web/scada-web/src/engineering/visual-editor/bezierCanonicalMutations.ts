import type { ScreenEngineering, VisualElementEngineering } from '../types';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../visual-runtime';
import type { VisualEditorPoint } from './visualEditorContracts';
import { createClosedBezierGeometry } from './bezierGeometry';

export function createCanonicalBezier(
  screen: ScreenEngineering,
  points: readonly VisualEditorPoint[],
  createId: () => string = () => crypto.randomUUID()
): Readonly<{ screen: ScreenEngineering; objectId: string }> {
  const geometry = createClosedBezierGeometry(points);
  if (!geometry) throw new Error('A Bézier curve requires at least three distinct anchors.');
  const objectId = createId().trim();
  if (!objectId || /[\u0000-\u001f\u007f]/.test(objectId)) throw new Error('Generated Bézier identity is invalid.');
  const keys = new Set<string>();
  const collect = (items: readonly VisualElementEngineering[]) => items.forEach(item => {
    keys.add(item.key.toLocaleLowerCase());
    if (item.children) collect(item.children);
  });
  collect(screen.elements ?? []);
  let key = 'bezier';
  let index = 2;
  while (keys.has(key.toLocaleLowerCase())) key = `bezier-${index++}`;
  const element: VisualElementEngineering = {
    id: objectId,
    key,
    type: BUILTIN_VISUAL_OBJECT_TYPES.bezier,
    properties: {
      [VISUAL_PROPERTY_KEYS.x]: geometry.x,
      [VISUAL_PROPERTY_KEYS.y]: geometry.y,
      [VISUAL_PROPERTY_KEYS.width]: geometry.width,
      [VISUAL_PROPERTY_KEYS.height]: geometry.height,
      [VISUAL_PROPERTY_KEYS.bezierPath]: geometry.path
    }
  };
  return Object.freeze({ objectId, screen: { ...screen, elements: [...(screen.elements ?? []), element] } });
}
