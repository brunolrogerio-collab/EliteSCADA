import type { ScreenEngineering } from '../types';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { applyProtectedVisualEditorMutationIntent } from './visualEditorProtectedMutationModel';
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
 * stacking for every existing caller of this module path.
 */
export function applyVisualEditorMutationIntent(
  screen: ScreenEngineering,
  intent: VisualEditorMutationIntent,
  options: VisualEditorMutationOptions = {}
): ScreenEngineering {
  return applyProtectedVisualEditorMutationIntent(screen, intent, options);
}
