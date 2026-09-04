import type { ScreenEngineering, VisualElementEngineering } from '../types';
import {
  BROWSER_CONFIG_PROPERTY,
  BUILTIN_VISUAL_OBJECT_TYPES,
  alarmBrowserEngineeringValue,
  eventBrowserEngineeringValue,
  normalizeAlarmBrowserConfig,
  normalizeEventBrowserConfig,
  type AlarmBrowserConfig,
  type EventBrowserConfig
} from '../../visual-runtime';

export function updateCanonicalAlarmBrowserConfig(
  screen: ScreenEngineering,
  objectId: string,
  config: AlarmBrowserConfig
): ScreenEngineering {
  return updateCanonicalBrowserConfig(
    screen,
    objectId,
    BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser,
    alarmBrowserEngineeringValue(normalizeAlarmBrowserConfig(config))
  );
}

export function updateCanonicalEventBrowserConfig(
  screen: ScreenEngineering,
  objectId: string,
  config: EventBrowserConfig
): ScreenEngineering {
  return updateCanonicalBrowserConfig(
    screen,
    objectId,
    BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser,
    eventBrowserEngineeringValue(normalizeEventBrowserConfig(config))
  );
}

function updateCanonicalBrowserConfig(
  screen: ScreenEngineering,
  objectId: string,
  expectedType: string,
  value: unknown
): ScreenEngineering {
  if (!objectId.trim()) throw new Error('Browser objectId must be a stable non-empty identity.');
  let found = false;
  const elements = (screen.elements ?? []).map(element => update(element));
  if (!found) throw new Error(`Browser visual object '${objectId}' was not found.`);
  return { ...screen, elements };

  function update(element: VisualElementEngineering): VisualElementEngineering {
    if (element.id === objectId) {
      found = true;
      if (element.type !== expectedType) {
        throw new Error(`Visual object '${objectId}' is not the expected canonical Browser type.`);
      }
      return {
        ...element,
        properties: {
          ...(element.properties ?? {}),
          [BROWSER_CONFIG_PROPERTY]: value
        }
      };
    }
    if (!element.children?.length) return element;
    return { ...element, children: element.children.map(update) };
  }
}
