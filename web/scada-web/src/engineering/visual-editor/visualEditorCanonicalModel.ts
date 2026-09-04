import type { ScreenEngineering } from '../types';
import { TREND_PENS_PROPERTY, normalizeTrendPens } from '../../visual-runtime';
import { assertVisualElementsAuthoringEditable } from './visualEditorAuthoringModel';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { applyProtectedVisualEditorMutationIntent } from './visualEditorProtectedMutationModel';
import { updateCanonicalTrendPens } from './trendCanonicalMutations';
import type { VisualEditorMutationOptions } from './visualEditorCanonicalModelLegacy';

export {
  NEW_SCREEN_IDENTITY,
  cloneEngineeringValue,
  countVisualElements,
  createScreenDraft,
  replaceScreenElements,
  replaceScreenInPackage,
  screenIdentity,
  updateScreenElement
} from './visualEditorCanonicalModelLegacy';
export type { VisualEditorMutationOptions } from './visualEditorCanonicalModelLegacy';

/**
 * Public canonical mutation entrypoint. C07 keeps the original reducer available
 * as an implementation detail while enforcing authoring locks and deterministic
 * stacking for every existing caller of this module path. C15 intercepts the
 * object-specific Trend Pen collection before the scalar registry seam while
 * preserving the same authoring-lock protection as ordinary property writes.
 */
export function applyVisualEditorMutationIntent(
  screen: ScreenEngineering,
  intent: VisualEditorMutationIntent,
  options: VisualEditorMutationOptions = {}
): ScreenEngineering {
  if (intent.kind === 'property.set' && intent.propertyKey === TREND_PENS_PROPERTY) {
    if (intent.objectIds.length !== 1 || !Array.isArray(intent.value)) {
      throw new Error('Trend pens require exactly one visual object and a JSON array value.');
    }
    assertVisualElementsAuthoringEditable(screen, intent.objectIds);
    return updateCanonicalTrendPens(screen, intent.objectIds[0], normalizeTrendPens(intent.value));
  }
  return applyProtectedVisualEditorMutationIntent(screen, intent, options);
}