import type { DynamoEngineering } from '../types';
import {
  libraryCatalogCategoryMatches,
  resolveDynamoCategoryPath
} from '../libraryCatalogTaxonomy';

export type DynamoLibraryEntry = Readonly<{
  definition: DynamoEngineering;
  category: string;
  categoryPath: string;
  source: 'builtin' | 'project';
  tags: readonly string[];
  width: number;
  height: number;
  parameterCount: number;
  visualStyle: string;
  searchText: string;
}>;

export type DynamoLibraryFilter = Readonly<{
  query?: string;
  category?: string | null;
  categoryPath?: string | null;
  source?: 'builtin' | 'project' | '';
  tag?: string | null;
}>;

export function selectDefaultDynamoCatalog(
  definitions: readonly DynamoEngineering[]
): readonly DynamoEngineering[] {
  return Object.freeze(definitions.filter(definition =>
    !(definition.metadata?.builtinLibrary === 'true' && definition.metadata.catalogStatus === 'legacy')));
}

export function buildDynamoLibraryEntries(
  definitions: readonly DynamoEngineering[],
  locale: string
): readonly DynamoLibraryEntry[] {
  return Object.freeze(
    definitions
      .map(definition => {
        const category = normalizeCategory(definition.properties?.category);
        const categoryPath = resolveDynamoCategoryPath(definition.properties?.category);
        const visualStyle = normalizeStyle(definition.properties?.visualStyle);
        const source = definition.metadata?.builtinLibrary === 'true' ? 'builtin' : 'project';
        const tags = collectTerms(
          definition.properties?.tags,
          definition.properties?.keywords,
          definition.metadata?.tags,
          definition.metadata?.keywords
        );
        return Object.freeze({
          definition,
          category,
          categoryPath,
          source,
          tags,
          width: positiveDimension(definition.properties?.defaultWidth, 120),
          height: positiveDimension(definition.properties?.defaultHeight, 100),
          parameterCount: definition.parameters?.length ?? 0,
          visualStyle,
          searchText: normalizeSearchText([
            definition.name,
            definition.key,
            category,
            categoryPath,
            visualStyle,
            source,
            ...tags
          ].join(' '))
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

export function listDynamoLibraryCategoryPaths(
  entries: readonly DynamoLibraryEntry[]
): readonly string[] {
  return Object.freeze([...new Set(entries.map(entry => entry.categoryPath))].sort());
}

export function filterDynamoLibraryEntries(
  entries: readonly DynamoLibraryEntry[],
  filter: DynamoLibraryFilter
): readonly DynamoLibraryEntry[] {
  const query = normalizeSearchText(filter.query ?? '');
  const category = normalizeSearchText(filter.category ?? '');
  const tag = normalizeSearchText(filter.tag ?? '');
  return Object.freeze(entries.filter(entry => {
    if (filter.source && entry.source !== filter.source) return false;
    if (filter.categoryPath && !libraryCatalogCategoryMatches(entry.categoryPath, filter.categoryPath)) return false;
    if (category && normalizeSearchText(entry.category) !== category) return false;
    if (tag && !entry.tags.some(value => normalizeSearchText(value) === tag)) return false;
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

function normalizeStyle(value: string | undefined): string {
  const style = value?.trim().toLocaleLowerCase('en-US');
  return style || 'detailed-2d';
}

function collectTerms(...values: Array<string | undefined>): readonly string[] {
  const result = values
    .flatMap(value => value?.split(/[,;|]/g) ?? [])
    .map(value => value.trim())
    .filter(Boolean);
  return Object.freeze([...new Set(result)]);
}

function normalizeSearchText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toLocaleLowerCase('en-US');
}
