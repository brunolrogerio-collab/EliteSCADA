import type {
  DynamoEngineering,
  EngineeringSnapshot,
  VisualAssetEngineering
} from './types';
import type {
  ReusableLibraryDescriptor,
  ReusableLibraryResource
} from './reusableLibraryApi';
import type { StaticArtwork } from './staticArtworkApi';
import { listUserVisualAssets } from './visualAssetCatalogModel';
import {
  libraryCatalogCategoryMatches,
  libraryCatalogCategorySearchText,
  resolveCatalogCategoryPath,
  resolveDynamoCategoryPath
} from './libraryCatalogTaxonomy';

export type LibraryCatalogOrigin = 'builtin' | 'project' | 'associated';

export type LibraryCatalogEntry = Readonly<{
  id: string;
  origin: LibraryCatalogOrigin;
  kind: string;
  name: string;
  sourceKey: string;
  categoryPath: string;
  tags: readonly string[];
  searchKeywords: readonly string[];
  description: string | null;
  version: string | null;
  readOnly: boolean;
  projectOwned: boolean;
  searchText: string;
  resourceId?: string | null;
  library?: ReusableLibraryDescriptor | null;
  associatedResource?: ReusableLibraryResource | null;
  dynamo?: DynamoEngineering | null;
  visualAsset?: VisualAssetEngineering | null;
  staticArtwork?: StaticArtwork | null;
}>;

export type AssociatedLibraryCatalogSource = Readonly<{
  library: ReusableLibraryDescriptor;
  resources: readonly ReusableLibraryResource[];
}>;

export type LibraryCatalogFilter = Readonly<{
  query?: string;
  origin?: LibraryCatalogOrigin | '';
  kind?: string;
  categoryPath?: string;
  tag?: string;
  sort?: 'name' | 'kind' | 'category' | 'source';
}>;

const ORIGIN_PREFIX = 'elitescada.reusable.origin.';

export function buildLibraryCatalogEntries(
  snapshot: EngineeringSnapshot,
  associatedLibraries: readonly AssociatedLibraryCatalogSource[] = [],
  staticArtwork: readonly StaticArtwork[] = []
): readonly LibraryCatalogEntry[] {
  const entries: LibraryCatalogEntry[] = [];
  const model = snapshot.package as Record<string, unknown>;

  for (const artwork of staticArtwork) {
    entries.push(createEntry({
      id: `builtin:static-artwork:${artwork.id}`, origin: 'builtin', kind: 'visual-asset',
      name: artwork.name, sourceKey: artwork.key, categoryPath: resolveCatalogCategoryPath(artwork.category),
      tags: artwork.tags, searchKeywords: [artwork.style], description: null, version: null,
      readOnly: true, projectOwned: false, resourceId: artwork.id, staticArtwork: artwork
    }));
  }

  for (const definition of snapshot.package.dynamos ?? []) {
    const metadata = stringMap(definition.metadata);
    const properties = stringMap(definition.properties);
    const origin = metadata?.builtinLibrary === 'true' ? 'builtin' : 'project';
    const categoryPath = resolveDynamoCategoryPath(properties?.category);
    entries.push(createEntry({
      id: `${origin}:dynamo:${definition.id ?? definition.key}`,
      origin,
      kind: 'dynamo',
      name: definition.name,
      sourceKey: definition.key,
      categoryPath,
      tags: collectTerms(properties, metadata, ['tags']),
      searchKeywords: collectTerms(properties, metadata, ['keywords', 'searchKeywords']),
      description: firstString(metadata?.description),
      version: firstString(properties?.libraryVersion, metadata?.version),
      readOnly: origin === 'builtin',
      projectOwned: origin !== 'builtin',
      resourceId: definition.id ?? null,
      dynamo: definition
    }));
  }

  for (const asset of listUserVisualAssets(snapshot.package.visualAssets)) {
    const metadata = stringMap(asset.metadata);
    const origin = metadata?.builtinLibrary === 'true' ? 'builtin' : 'project';
    const kind = asset.mediaType === 'image/svg+xml' ? 'visual-asset' : 'raster-image';
    entries.push(createEntry({
      id: `${origin}:${kind}:${asset.id ?? asset.key}`,
      origin,
      kind,
      name: asset.name,
      sourceKey: asset.key,
      categoryPath: resolveCatalogCategoryPath(metadata?.categoryPath ?? metadata?.category),
      tags: collectTerms(metadata, null, ['tags']),
      searchKeywords: collectTerms(metadata, null, ['keywords', 'searchKeywords']),
      description: firstString(asset.description, metadata?.description),
      version: firstString(metadata?.version),
      readOnly: origin === 'builtin',
      projectOwned: origin !== 'builtin',
      resourceId: asset.id ?? null,
      visualAsset: asset
    }));
  }

  addGenericProjectResources(entries, model.templates, 'equipment-template', 'key', 'generic/symbols');
  addGenericProjectResources(entries, model.screens, 'screen', 'key', 'generic/layout');
  addGenericProjectResources(entries, model.popups, 'popup', 'key', 'generic/layout');
  addGenericProjectResources(entries, model.scripts, 'script', 'path', 'generic/symbols');

  for (const source of associatedLibraries) {
    for (const resource of source.resources) {
      const categoryPath = resource.kind === 'screen' || resource.kind === 'popup'
        ? 'generic/layout'
        : 'generic/symbols';
      entries.push(createEntry({
        id: `associated:${source.library.libraryId}:${resource.kind}:${resource.resourceId}`,
        origin: 'associated',
        kind: resource.kind,
        name: resource.displayName,
        sourceKey: resource.sourceKey,
        categoryPath,
        tags: [],
        searchKeywords: [],
        description: null,
        version: source.library.version,
        readOnly: true,
        projectOwned: false,
        resourceId: resource.resourceId,
        library: source.library,
        associatedResource: resource
      }));
    }
  }

  return Object.freeze(entries.sort(defaultEntryCompare));
}

export function filterLibraryCatalogEntries(
  entries: readonly LibraryCatalogEntry[],
  filter: LibraryCatalogFilter
): readonly LibraryCatalogEntry[] {
  const query = normalizeSearchText(filter.query ?? '');
  const kind = (filter.kind ?? '').trim();
  const tag = normalizeSearchText(filter.tag ?? '');
  const origin = filter.origin ?? '';
  const filtered = entries.filter(entry => {
    if (origin && entry.origin !== origin) return false;
    if (kind && entry.kind !== kind) return false;
    if (!libraryCatalogCategoryMatches(entry.categoryPath, filter.categoryPath)) return false;
    if (tag && !entry.tags.some(value => normalizeSearchText(value) === tag)) return false;
    if (query && !entry.searchText.includes(query)) return false;
    return true;
  });

  const sort = filter.sort ?? 'name';
  filtered.sort((left, right) => {
    if (sort === 'kind')
      return compareText(left.kind, right.kind) || defaultEntryCompare(left, right);
    if (sort === 'category')
      return compareText(left.categoryPath, right.categoryPath) || defaultEntryCompare(left, right);
    if (sort === 'source')
      return compareText(left.origin, right.origin) || defaultEntryCompare(left, right);
    return defaultEntryCompare(left, right);
  });
  return Object.freeze(filtered);
}

export function listLibraryCatalogKinds(entries: readonly LibraryCatalogEntry[]): readonly string[] {
  return Object.freeze([...new Set(entries.map(entry => entry.kind))].sort(compareText));
}

export function listLibraryCatalogTags(entries: readonly LibraryCatalogEntry[]): readonly string[] {
  const tags = new Set<string>();
  for (const entry of entries) for (const tag of entry.tags) tags.add(tag);
  return Object.freeze([...tags].sort(compareText));
}

export function libraryCatalogOriginCounts(
  entries: readonly LibraryCatalogEntry[]
): Readonly<Record<LibraryCatalogOrigin, number>> {
  const counts: Record<LibraryCatalogOrigin, number> = { builtin: 0, project: 0, associated: 0 };
  for (const entry of entries) counts[entry.origin]++;
  return Object.freeze(counts);
}

function addGenericProjectResources(
  entries: LibraryCatalogEntry[],
  value: unknown,
  kind: string,
  sourceKeyField: string,
  fallbackCategory: string
) {
  if (!Array.isArray(value)) return;
  for (const raw of value) {
    if (!raw || typeof raw !== 'object') continue;
    const item = raw as Record<string, unknown>;
    const id = firstString(item.id);
    const sourceKey = firstString(item[sourceKeyField], id);
    if (!sourceKey) continue;
    const metadata = stringMap(item.metadata);
    const properties = stringMap(item.properties);
    const origin: LibraryCatalogOrigin = metadata?.builtinLibrary === 'true' ? 'builtin' : 'project';
    entries.push(createEntry({
      id: `${origin}:${kind}:${id ?? sourceKey}`,
      origin,
      kind,
      name: firstString(item.name, sourceKey) ?? sourceKey,
      sourceKey,
      categoryPath: resolveCatalogCategoryPath(
        firstString(properties?.categoryPath, metadata?.categoryPath, properties?.category, metadata?.category),
        fallbackCategory
      ),
      tags: collectTerms(properties, metadata, ['tags']),
      searchKeywords: collectTerms(properties, metadata, ['keywords', 'searchKeywords']),
      description: firstString(item.description, metadata?.description),
      version: firstString(properties?.version, metadata?.version),
      readOnly: origin === 'builtin',
      projectOwned: origin !== 'builtin',
      resourceId: id
    }));
  }
}

function createEntry(
  input: Omit<LibraryCatalogEntry, 'searchText'>
): LibraryCatalogEntry {
  const searchText = normalizeSearchText([
    input.name,
    input.sourceKey,
    input.kind,
    input.origin,
    input.categoryPath,
    libraryCatalogCategorySearchText(input.categoryPath),
    input.description ?? '',
    input.version ?? '',
    input.library?.name ?? '',
    ...input.tags,
    ...input.searchKeywords
  ].join(' '));
  return Object.freeze({ ...input, searchText });
}

function collectTerms(
  primary: Record<string, string> | null | undefined,
  secondary: Record<string, string> | null | undefined,
  keys: readonly string[]
): readonly string[] {
  const values: string[] = [];
  for (const source of [primary, secondary]) {
    if (!source) continue;
    for (const key of keys) {
      const value = source[key];
      if (!value) continue;
      values.push(...value.split(/[,;|]/g).map(item => item.trim()).filter(Boolean));
    }
  }
  return Object.freeze([...new Set(values)]);
}

function stringMap(value: unknown): Record<string, string> | null {
  if (!value || typeof value !== 'object') return null;
  const result: Record<string, string> = {};
  for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
    if (typeof item === 'string') result[key] = item;
  }
  return result;
}

function firstString(...values: unknown[]): string | null {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value.trim();
  }
  return null;
}

function defaultEntryCompare(left: LibraryCatalogEntry, right: LibraryCatalogEntry): number {
  return compareText(left.name, right.name)
    || compareText(left.kind, right.kind)
    || compareText(left.id, right.id);
}

function compareText(left: string, right: string): number {
  return left.localeCompare(right, 'en-US');
}

function normalizeSearchText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toLocaleLowerCase('en-US');
}

export function reusableLibraryProvenanceId(metadata: Record<string, string> | null | undefined): string | null {
  return metadata?.[`${ORIGIN_PREFIX}libraryId`] ?? null;
}
