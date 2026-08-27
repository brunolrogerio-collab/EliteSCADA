export type EngineeringEntityBrowserFilter<T> = {
  key: string;
  label: string;
  matches: (item: T) => boolean;
};

export type EngineeringEntityNavigationDirection = 'next' | 'previous' | 'first' | 'last';

export function filterEngineeringEntities<T>(
  items: readonly T[],
  query: string,
  activeFilter: EngineeringEntityBrowserFilter<T> | undefined,
  getSearchText: (item: T) => readonly string[]
): T[] {
  const normalizedQuery = normalizeSearchText(query);

  return items.filter(item => {
    if (activeFilter && !activeFilter.matches(item)) {
      return false;
    }

    if (!normalizedQuery) {
      return true;
    }

    return getSearchText(item).some(value => normalizeSearchText(value).includes(normalizedQuery));
  });
}

export function selectAdjacentEngineeringEntityKey(
  visibleKeys: readonly string[],
  currentKey: string | null | undefined,
  direction: EngineeringEntityNavigationDirection
): string | null {
  if (visibleKeys.length === 0) {
    return null;
  }

  if (direction === 'first') {
    return visibleKeys[0];
  }

  if (direction === 'last') {
    return visibleKeys[visibleKeys.length - 1];
  }

  const currentIndex = currentKey ? visibleKeys.indexOf(currentKey) : -1;
  if (currentIndex < 0) {
    return direction === 'previous' ? visibleKeys[visibleKeys.length - 1] : visibleKeys[0];
  }

  const delta = direction === 'next' ? 1 : -1;
  const targetIndex = Math.min(visibleKeys.length - 1, Math.max(0, currentIndex + delta));
  return visibleKeys[targetIndex];
}

function normalizeSearchText(value: string): string {
  return value.trim().toLocaleLowerCase();
}
