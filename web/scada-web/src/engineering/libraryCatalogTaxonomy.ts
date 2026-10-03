import taxonomyDocument from '../../../../assets/catalog/taxonomy.v1.json';

export type LibraryCatalogLocale = 'pt-BR' | 'en' | 'es';

export type LibraryCatalogCategory = Readonly<{
  id: string;
  parentId: string | null;
  labels: Readonly<Record<LibraryCatalogLocale, string>>;
}>;

type RawCategory = {
  id: string;
  labels: Record<string, string>;
};

const FALLBACK_CATEGORY = 'generic/symbols';

const CATEGORIES: readonly LibraryCatalogCategory[] = Object.freeze(
  (taxonomyDocument.categories as RawCategory[]).map(category => Object.freeze({
    id: category.id,
    parentId: parentCategoryId(category.id),
    labels: Object.freeze({
      'pt-BR': category.labels['pt-BR'] ?? category.id,
      en: category.labels.en ?? category.id,
      es: category.labels.es ?? category.labels.en ?? category.id
    })
  }))
);

const CATEGORY_IDS = new Set(CATEGORIES.map(category => category.id));

const LEGACY_DYNAMO_CATEGORY: Readonly<Record<string, string>> = Object.freeze({
  pump: 'industrial/process/pumps',
  pumps: 'industrial/process/pumps',
  motor: 'industrial/rotating/motors',
  motors: 'industrial/rotating/motors',
  valve: 'industrial/process/valves',
  valves: 'industrial/process/valves',
  tank: 'industrial/process/tanks-vessels',
  tanks: 'industrial/process/tanks-vessels',
  compressor: 'industrial/rotating/compressors',
  compressors: 'industrial/rotating/compressors',
  instrument: 'industrial/process/instrumentation',
  instrumentation: 'industrial/process/instrumentation',
  process: 'industrial/process',
  electrical: 'electrical',
  substation: 'electrical/protection',
  other: FALLBACK_CATEGORY,
  uncategorized: FALLBACK_CATEGORY
});

export function listLibraryCatalogCategories(): readonly LibraryCatalogCategory[] {
  return CATEGORIES;
}

export function libraryCatalogCategoryLabel(
  categoryId: string,
  locale: LibraryCatalogLocale
): string {
  const category = CATEGORIES.find(item => item.id === categoryId);
  if (category) return category.labels[locale] ?? category.labels.en;
  const leaf = categoryId.split('/').filter(Boolean).at(-1) ?? categoryId;
  return leaf.replaceAll('-', ' ');
}

export function libraryCatalogCategoryPathLabel(
  categoryId: string,
  locale: LibraryCatalogLocale
): string {
  const ancestors = categoryAncestors(categoryId);
  if (ancestors.length === 0) return libraryCatalogCategoryLabel(categoryId, locale);
  return ancestors.map(id => libraryCatalogCategoryLabel(id, locale)).join(' / ');
}

export function libraryCatalogCategorySearchText(categoryId: string): string {
  return [
    categoryId,
    ...categoryAncestors(categoryId).flatMap(id => [
      libraryCatalogCategoryLabel(id, 'pt-BR'),
      libraryCatalogCategoryLabel(id, 'en'),
      libraryCatalogCategoryLabel(id, 'es')
    ])
  ].join(' ');
}

export function isLibraryCatalogCategory(categoryId: string): boolean {
  return CATEGORY_IDS.has(categoryId);
}

export function resolveDynamoCategoryPath(value: string | null | undefined): string {
  const normalized = normalizeCategoryId(value);
  if (!normalized) return FALLBACK_CATEGORY;
  if (CATEGORY_IDS.has(normalized)) return normalized;
  return LEGACY_DYNAMO_CATEGORY[normalized] ?? FALLBACK_CATEGORY;
}

export function resolveCatalogCategoryPath(
  value: string | null | undefined,
  fallback = FALLBACK_CATEGORY
): string {
  const normalized = normalizeCategoryId(value);
  if (normalized && CATEGORY_IDS.has(normalized)) return normalized;
  return CATEGORY_IDS.has(fallback) ? fallback : FALLBACK_CATEGORY;
}

export function libraryCatalogCategoryMatches(
  candidate: string,
  selected: string | null | undefined
): boolean {
  const normalized = normalizeCategoryId(selected);
  if (!normalized) return true;
  return candidate === normalized || candidate.startsWith(`${normalized}/`);
}

export function libraryCatalogCategoryDepth(categoryId: string): number {
  return categoryId.split('/').filter(Boolean).length - 1;
}

function categoryAncestors(categoryId: string): string[] {
  const parts = categoryId.split('/').filter(Boolean);
  const ids: string[] = [];
  for (let index = 1; index <= parts.length; index++) {
    const candidate = parts.slice(0, index).join('/');
    if (CATEGORY_IDS.has(candidate)) ids.push(candidate);
  }
  return ids;
}

function parentCategoryId(categoryId: string): string | null {
  const split = categoryId.lastIndexOf('/');
  return split > 0 ? categoryId.slice(0, split) : null;
}

function normalizeCategoryId(value: string | null | undefined): string {
  return (value ?? '').trim().toLocaleLowerCase('en-US');
}
