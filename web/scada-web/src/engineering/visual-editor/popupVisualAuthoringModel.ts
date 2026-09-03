import type {
  BindingEngineering,
  EngineeringPackageView,
  PopupEngineering,
  ScreenEngineering,
  ScriptLinkEngineering,
  VisualEventEngineering
} from '../types';
import { cloneEngineeringValue } from './visualEditorCanonicalModel';

export const NEW_POPUP_IDENTITY = 'draft:new-popup';

/**
 * Popup-only persisted fields that are deliberately outside the Screen-shaped
 * visual composition session. They must survive the adapter round-trip without
 * being smuggled into visual properties or a frontend-only schema.
 */
export type PopupVisualFrame = Readonly<{
  templateKey: string | null;
  bindings: readonly BindingEngineering[];
  events: readonly VisualEventEngineering[];
  scriptLinks: readonly ScriptLinkEngineering[];
}>;

export function popupIdentity(popup: PopupEngineering): string {
  return popup.id ? `id:${popup.id}` : `key:${popup.key}`;
}

/**
 * Adapts only the composition-shaped portion of a Popup to the established
 * Screen authoring session. Persisted Popup identity remains a Popup and route
 * is deliberately null because Runtime navigation owns Popup mounting.
 */
export function popupToVisualScreen(popup: PopupEngineering): ScreenEngineering {
  return {
    id: popup.id ?? undefined,
    key: popup.key,
    name: popup.name,
    route: null,
    elements: cloneEngineeringValue(popup.elements ?? []),
    properties: cloneEngineeringValue(popup.properties ?? {}),
    context: cloneEngineeringValue(popup.context ?? {}),
    metadata: cloneEngineeringValue(popup.metadata ?? {})
  };
}

export function popupFrame(popup: PopupEngineering): PopupVisualFrame {
  return Object.freeze({
    templateKey: popup.templateKey ?? null,
    bindings: Object.freeze(cloneEngineeringValue(popup.bindings ?? [])),
    events: Object.freeze(cloneEngineeringValue(popup.events ?? [])),
    scriptLinks: Object.freeze(cloneEngineeringValue(popup.scriptLinks ?? []))
  });
}

export function visualScreenToPopup(
  screen: ScreenEngineering,
  frame: PopupVisualFrame
): PopupEngineering {
  return {
    id: screen.id ?? undefined,
    key: screen.key,
    name: screen.name,
    templateKey: frame.templateKey,
    bindings: cloneEngineeringValue(frame.bindings),
    events: cloneEngineeringValue(frame.events),
    scriptLinks: cloneEngineeringValue(frame.scriptLinks),
    properties: cloneEngineeringValue(screen.properties ?? {}),
    context: cloneEngineeringValue(screen.context ?? {}),
    metadata: cloneEngineeringValue(screen.metadata ?? {}),
    elements: cloneEngineeringValue(screen.elements ?? [])
  };
}

export function createPopupDraft(
  existing: readonly PopupEngineering[],
  locale: 'pt-BR' | 'en' | 'es'
): PopupEngineering {
  const used = new Set(existing.map(item => item.key.trim().toLocaleLowerCase('en-US')));
  let index = 1;
  while (used.has(`popup-${index}`)) index += 1;
  const key = `popup-${index}`;
  const name = locale === 'en' ? `Popup ${index}` : locale === 'es' ? `Popup ${index}` : `Popup ${index}`;
  return {
    key,
    name,
    templateKey: null,
    bindings: [],
    events: [],
    scriptLinks: [],
    properties: {},
    context: {},
    metadata: {},
    elements: []
  };
}

export function replacePopupInPackage(
  model: EngineeringPackageView,
  original: PopupEngineering | null,
  draft: PopupEngineering
): EngineeringPackageView {
  const candidate = cloneEngineeringValue(model);
  const popups = candidate.popups ?? [];

  if (original === null) {
    candidate.popups = [...popups, cloneEngineeringValue(draft)];
    return candidate;
  }

  const identity = popupIdentity(original);
  candidate.popups = popups.map(popup =>
    popupIdentity(popup) === identity ? cloneEngineeringValue(draft) : popup);
  return candidate;
}
