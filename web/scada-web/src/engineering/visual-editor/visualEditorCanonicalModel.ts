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
import { updateScreenElement, type VisualEditorMutationOptions } from './visualEditorCanonicalModelLegacy';
import {
  SVG_PAINT_OVERRIDES_PROPERTY,
  normalizeSvgPaintOverrides,
  svgPaintOverridesAsEngineeringValue
} from './svgSymbolModel';
import {
  CHECK_BOX_CONFIG_PROPERTY,
  COMBO_BOX_OPTIONS_PROPERTY,
  comboBoxOptionsEngineeringValue,
  normalizeComboBoxOptions,
  readCheckBoxAlarmSuppressionConfig
} from './controlVisualModel';

export {
  NEW_SCREEN_IDENTITY,
  cloneEngineeringValue,
  countVisualElements,
  createScreenDraft,
  replaceScreenElements,
  replaceScreenInPackage,
  replaceTemplateInPackage,
  replaceDynamoInPackage,
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

  if (intent.kind === 'property.set' && intent.propertyKey === SVG_PAINT_OVERRIDES_PROPERTY) {
    if (intent.objectIds.length !== 1 || typeof intent.value !== 'object' || intent.value === null || Array.isArray(intent.value)) {
      throw new Error('SVG paint overrides require exactly one SVG symbol and a JSON object value.');
    }
    assertVisualElementsAuthoringEditable(screen, intent.objectIds);
    const objectId = intent.objectIds[0];
    const element = findElement(screen.elements ?? [], objectId);
    if (!element || element.type !== BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol)
      throw new Error(`Visual object '${objectId}' does not own svgPaintOverrides.`);
    const value = svgPaintOverridesAsEngineeringValue(normalizeSvgPaintOverrides(intent.value));
    return updateScreenElement(screen, objectId, current => ({
      ...current,
      properties: { ...(current.properties ?? {}), [SVG_PAINT_OVERRIDES_PROPERTY]: value }
    }));
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

  if (intent.kind === 'property.set' &&
      (intent.propertyKey === CHECK_BOX_CONFIG_PROPERTY || intent.propertyKey === COMBO_BOX_OPTIONS_PROPERTY)) {
    if (intent.objectIds.length !== 1) throw new Error('Control configuration requires exactly one visual object.');
    assertVisualElementsAuthoringEditable(screen, intent.objectIds);
    const objectId = intent.objectIds[0];
    const element = findElement(screen.elements ?? [], objectId);
    if (!element) throw new Error(`Visual control '${objectId}' was not found.`);
    let propertyValue: import('../types').VisualEngineeringPropertyValue;
    if (intent.propertyKey === CHECK_BOX_CONFIG_PROPERTY && element.type === BUILTIN_VISUAL_OBJECT_TYPES.checkBox) {
      const normalized = readCheckBoxAlarmSuppressionConfig(intent.value);
      if (!normalized) throw new Error('CheckBox alarm-suppression configuration is required.');
      propertyValue = normalized as import('../types').VisualEngineeringPropertyValue;
    } else if (intent.propertyKey === COMBO_BOX_OPTIONS_PROPERTY && element.type === BUILTIN_VISUAL_OBJECT_TYPES.comboBox) {
      propertyValue = comboBoxOptionsEngineeringValue(normalizeComboBoxOptions(intent.value));
    } else {
      throw new Error(`Visual control '${objectId}' does not own ${intent.propertyKey}.`);
    }
    return updateScreenElement(screen, objectId, current => ({
      ...current,
      properties: { ...(current.properties ?? {}), [intent.propertyKey]: propertyValue }
    }));
  }

  if (intent.kind === 'property.remove' &&
      (intent.propertyKey === CHECK_BOX_CONFIG_PROPERTY || intent.propertyKey === COMBO_BOX_OPTIONS_PROPERTY)) {
    if (intent.objectIds.length !== 1) throw new Error('Control configuration requires exactly one visual object.');
    assertVisualElementsAuthoringEditable(screen, intent.objectIds);
    const objectId = intent.objectIds[0];
    const element = findElement(screen.elements ?? [], objectId);
    const expectedType = intent.propertyKey === CHECK_BOX_CONFIG_PROPERTY
      ? BUILTIN_VISUAL_OBJECT_TYPES.checkBox
      : BUILTIN_VISUAL_OBJECT_TYPES.comboBox;
    if (!element || element.type !== expectedType) throw new Error(`Visual control '${objectId}' does not own ${intent.propertyKey}.`);
    return updateScreenElement(screen, objectId, current => {
      const properties = { ...(current.properties ?? {}) };
      delete properties[intent.propertyKey];
      return { ...current, properties };
    });
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
