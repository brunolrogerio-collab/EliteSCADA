import type {
  EngineeringPackageView,
  ScreenEngineering,
  VisualElementEngineering
} from '../types';

export const NEW_SCREEN_IDENTITY = 'draft:new-screen';

export function screenIdentity(screen: ScreenEngineering): string {
  return screen.id ? `id:${screen.id}` : `key:${screen.key}`;
}

export function cloneEngineeringValue<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export function createScreenDraft(existing: readonly ScreenEngineering[], locale: 'pt-BR' | 'en' | 'es'): ScreenEngineering {
  const nextIndex = nextScreenIndex(existing);
  const key = `screen-${nextIndex}`;
  const name = locale === 'en' ? `Screen ${nextIndex}` : locale === 'es' ? `Pantalla ${nextIndex}` : `Tela ${nextIndex}`;
  return {
    key,
    name,
    route: `/${key}`,
    elements: [],
    properties: {},
    context: {},
    metadata: {}
  };
}

export function replaceScreenInPackage(
  model: EngineeringPackageView,
  original: ScreenEngineering | null,
  draft: ScreenEngineering
): EngineeringPackageView {
  const candidate = cloneEngineeringValue(model);
  const screens = candidate.screens ?? [];

  if (original === null) {
    candidate.screens = [...screens, cloneEngineeringValue(draft)];
    return candidate;
  }

  const identity = screenIdentity(original);
  candidate.screens = screens.map(screen =>
    screenIdentity(screen) === identity ? cloneEngineeringValue(draft) : screen);
  return candidate;
}

export function updateScreenElement(
  screen: ScreenEngineering,
  objectId: string,
  update: (element: VisualElementEngineering) => VisualElementEngineering
): ScreenEngineering {
  const [elements, changed] = updateElementTree(screen.elements ?? [], objectId, update);
  return changed ? { ...screen, elements } : screen;
}

export function replaceScreenElements(
  screen: ScreenEngineering,
  elements: readonly VisualElementEngineering[]
): ScreenEngineering {
  return {
    ...screen,
    elements: cloneEngineeringValue(elements)
  };
}

export function countVisualElements(elements: readonly VisualElementEngineering[] | null | undefined): number {
  let count = 0;
  for (const element of elements ?? []) {
    count += 1 + countVisualElements(element.children);
  }
  return count;
}

function updateElementTree(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  update: (element: VisualElementEngineering) => VisualElementEngineering
): [VisualElementEngineering[], boolean] {
  let changed = false;
  const next = elements.map(element => {
    if (element.id === objectId) {
      changed = true;
      return cloneEngineeringValue(update(cloneEngineeringValue(element)));
    }

    if (!element.children?.length) return element;
    const [children, childChanged] = updateElementTree(element.children, objectId, update);
    if (!childChanged) return element;
    changed = true;
    return { ...element, children };
  });
  return [next, changed];
}

function nextScreenIndex(existing: readonly ScreenEngineering[]): number {
  const used = new Set(existing.map(screen => screen.key.toLowerCase()));
  let index = existing.length + 1;
  while (used.has(`screen-${index}`)) index += 1;
  return index;
}
