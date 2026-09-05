import type { DynamoEngineering } from '../types';

export type DynamoLibraryEntry = Readonly<{
  definition: DynamoEngineering;
  category: string;
  width: number;
  height: number;
  parameterCount: number;
  glyph: string;
  searchText: string;
}>;

export type DynamoLibraryFilter = Readonly<{
  query?: string;
  category?: string | null;
}>;

export function buildDynamoLibraryEntries(
  definitions: readonly DynamoEngineering[],
  locale: string
): readonly DynamoLibraryEntry[] {
  return Object.freeze(
    definitions
      .map(definition => {
        const category = normalizeCategory(definition.properties?.category);
        return Object.freeze({
          definition,
          category,
          width: positiveDimension(definition.properties?.defaultWidth, 120),
          height: positiveDimension(definition.properties?.defaultHeight, 100),
          parameterCount: definition.parameters?.length ?? 0,
          glyph: thumbnailGlyph(category, definition.key),
          searchText: normalizeSearchText(`${definition.name} ${definition.key} ${category}`)
        });
      })
      .sort((left, right) => left.definition.name.localeCompare(right.definition.name, locale))
  );
}

export function listDynamoLibraryCategories(
  entries: readonly DynamoLibraryEntry[]
): readonly string[] {
  return Object.freeze([...new Set(entries.map(entry => entry.category))].sort());
}

export function filterDynamoLibraryEntries(
  entries: readonly DynamoLibraryEntry[],
  filter: DynamoLibraryFilter
): readonly DynamoLibraryEntry[] {
  const query = normalizeSearchText(filter.query ?? '');
  const category = normalizeSearchText(filter.category ?? '');
  return Object.freeze(entries.filter(entry => {
    if (category && normalizeSearchText(entry.category) !== category) return false;
    if (query && !entry.searchText.includes(query)) return false;
    return true;
  }));
}

function positiveDimension(value: string | undefined, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function normalizeCategory(value: string | undefined): string {
  const category = value?.trim().toLocaleLowerCase('en-US');
  return category || 'other';
}

function normalizeSearchText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toLocaleLowerCase('en-US');
}

function thumbnailGlyph(category: string, key: string): string {
  if (category === 'pump') return '◉→';
  if (category === 'motor') return key.includes('.vfd') ? 'Ⓜ▣' : 'Ⓜ';
  if (category === 'valve') return '◇◇';
  if (category === 'tank') return key.includes('.horizontal') ? '▭' : '▯';
  return '◆';
}
