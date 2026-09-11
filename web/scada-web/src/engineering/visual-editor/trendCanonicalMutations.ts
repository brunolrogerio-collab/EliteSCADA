import type { ScreenEngineering, VisualElementEngineering } from '../types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  TREND_PENS_PROPERTY,
  normalizeTrendPens,
  trendPensEngineeringValue,
  type TrendVisualPen
} from '../../visual-runtime';

export function updateCanonicalTrendPens(
  screen: ScreenEngineering,
  objectId: string,
  pens: readonly TrendVisualPen[]
): ScreenEngineering {
  if (!objectId.trim()) throw new Error('Trend objectId must be a stable non-empty identity.');
  const normalized = normalizeTrendPens(pens);
  let found = false;
  const elements = (screen.elements ?? []).map(element => update(element));
  if (!found) throw new Error(`Trend visual object '${objectId}' was not found.`);
  return { ...screen, elements };

  function update(element: VisualElementEngineering): VisualElementEngineering {
    if (element.id === objectId) {
      found = true;
      if (element.type !== BUILTIN_VISUAL_OBJECT_TYPES.trend) {
        throw new Error(`Visual object '${objectId}' is not a canonical Trend.`);
      }
      return {
        ...element,
        properties: {
          ...(element.properties ?? {}),
          [TREND_PENS_PROPERTY]: trendPensEngineeringValue(normalized)
        }
      };
    }
    if (!element.children?.length) return element;
    return { ...element, children: element.children.map(update) };
  }
}
