import type {
  EngineeringPackageView,
  PopupEngineering,
  ScreenEngineering
} from '../types';
import { cloneEngineeringValue } from './visualEditorCanonicalModel';

export const NEW_POPUP_IDENTITY = 'draft:new-popup';

export type PopupVisualFrame = Readonly<{
  width: number | null;
  height: number | null;
  version: number | null;
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
    id: popup.id ?? null,
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
    width: popup.width ?? null,
    height: popup.height ?? null,
    version: popup.version ?? null
  });
}

export function visualScreenToPopup(
  screen: ScreenEngineering,
  frame: PopupVisualFrame
): PopupEngineering {
  return {
    id: screen.id ?? null,
    key: screen.key,
    name: screen.name,
    width: frame.width,
    height: frame.height,
    version: frame.version,
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
    width: 480,
    height: 320,
    version: 1,
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

export function normalizePopupDimension(value: number | null | undefined): number | null {
  if (value === null || value === undefined) return null;
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error('Popup width and height must be finite positive values.');
  }
  return Math.round(value * 1000) / 1000;
}
