import type { ScreenEngineering } from '../types';
import {
  BROWSER_CONFIG_PROPERTY,
  BUILTIN_VISUAL_OBJECT_TYPES,
  TREND_PENS_PROPERTY,
  normalizeAlarmBrowserConfig,
  normalizeEventBrowserConfig,
  normalizeTrendPens
} from '../../visual-runtime';
import { assertVisualElementsAuthoringEditable } from './visualEditorAuthoringModel';
import {
  updateCanonicalAlarmBrowserConfig,
  updateCanonicalEventBrowserConfig
} from './browserCanonicalMutations';
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
 * Public canonical mutation entrypoint. Object-specific structural payloads are
 * intercepted before the scalar property registry while retaining the same
 * authoring-lock protection and immutable Screen mutation authority.
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

  if (intent.kind === 'property.set' && intent.propertyKey === BROWSER_CONFIG_PROPERTY) {
    if (intent.objectIds.length !== 1 || typeof intent.value !== 'object' || intent.value === null || Array.isArray(intent.value)) {
      throw new Error('Browser configuration requires exactly one visual object and a JSON object value.');
    }
    assertVisualElementsAuthoringEditable(screen, intent.objectIds);
    const objectId = intent.objectIds[0];
    const element = findElement(screen.elements ?? [], objectId);
    if (!element) throw new Error(`Browser visual object '${objectId}' was not found.`);
    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser) {
      return updateCanonicalAlarmBrowserConfig(screen, objectId, normalizeAlarmBrowserConfig(intent.value));
    }
    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser) {
      return updateCanonicalEventBrowserConfig(screen, objectId, normalizeEventBrowserConfig(intent.value));
    }
    throw new Error(`Visual object '${objectId}' does not own browserConfig.`);
  }

  return applyProtectedVisualEditorMutationIntent(screen, intent, options);
}

function findElement(
  elements: readonly import('../types').VisualElementEngineering[],
  objectId: string
): import('../types').VisualElementEngineering | null {
  for (const element of elements) {
    if (element.id === objectId) return element;
    const nested = findElement(element.children ?? [], objectId);
    if (nested) return nested;
  }
  return null;
}
