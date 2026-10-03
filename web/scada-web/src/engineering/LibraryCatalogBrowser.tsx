import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import { visualAssetContentUrl } from './api';
import {
  filterLibraryCatalogEntries,
  libraryCatalogOriginCounts,
  listLibraryCatalogKinds,
  listLibraryCatalogTags,
  type LibraryCatalogEntry,
  type LibraryCatalogOrigin
} from './libraryCatalogModel';
import {
  libraryCatalogCategoryDepth,
  libraryCatalogCategoryLabel,
  libraryCatalogCategoryPathLabel,
  listLibraryCatalogCategories
} from './libraryCatalogTaxonomy';
import {
  reusableLibraryAssetContentUrl,
  type ReusableLibraryResourcePreview
} from './reusableLibraryApi';
import { CanonicalVisualPreview } from './visual-editor/CanonicalVisualPreview';
import './library-catalog-browser.css';

const PAGE_SIZE = 80;

export function LibraryCatalogBrowser({
  locale,
  entries,
  associatedPreview,
  previewBusy,
  actionBusy,
  onPreviewAssociated,
  onUseAssociated
}: {
  locale: EngineeringLocale;
  entries: readonly LibraryCatalogEntry[];
  associatedPreview: ReusableLibraryResourcePreview | null;
  previewBusy: string | null;
  actionBusy: string | null;
  onPreviewAssociated: (entry: LibraryCatalogEntry) => void;
  onUseAssociated: (entry: LibraryCatalogEntry) => void;
}) {
  const copy = useMemo(() => catalogCopy(locale), [locale]);
  const categories = useMemo(() => listLibraryCatalogCategories(), []);
  const kinds = useMemo(() => listLibraryCatalogKinds(entries), [entries]);
  const tags = useMemo(() => listLibraryCatalogTags(entries), [entries]);
  const counts = useMemo(() => libraryCatalogOriginCounts(entries), [entries]);
  const [query, setQuery] = useState('');
  const [origin, setOrigin] = useState<LibraryCatalogOrigin | ''>('');
  const [kind, setKind] = useState('');
  const [categoryPath, setCategoryPath] = useState('');
  const [tag, setTag] = useState('');
  const [sort, setSort] = useState<'name' | 'kind' | 'category' | 'source'>('name');
  const [selectedId, setSelectedId] = useState('');
  const [limit, setLimit] = useState(PAGE_SIZE);

  const filtered = useMemo(
    () => filterLibraryCatalogEntries(entries, { query, origin, kind, categoryPath, tag, sort }),
    [entries, query, origin, kind, categoryPath, tag, sort]
  );
  const visible = filtered.slice(0, limit);
  const selected = filtered.find(entry => entry.id === selectedId) ?? filtered[0] ?? null;

  function resetLimit() {
    setLimit(PAGE_SIZE);
  }

  function chooseOrigin(value: LibraryCatalogOrigin | '') {
    setOrigin(value);
    resetLimit();
  }

  return <section className="eng-panel library-catalog-browser" data-testid="library-catalog-browser">
    <header className="library-catalog-browser__header">
      <div>
        <span className="eng-eyebrow">{copy.eyebrow}</span>
        <h2>{copy.title}</h2>
        <p>{copy.description}</p>
      </div>
      <div className="library-catalog-browser__origin-summary" aria-label={copy.origin}>
        <span>{copy.builtin}: <strong>{counts.builtin}</strong></span>
        <span>{copy.project}: <strong>{counts.project}</strong></span>
        <span>{copy.associated}: <strong>{counts.associated}</strong></span>
      </div>
    </header>

    <div className="library-catalog-browser__filters">
      <label className="library-catalog-browser__search">
        <span>{copy.search}</span>
        <input
          data-testid="library-catalog-search"
          type="search"
          value={query}
          placeholder={copy.searchPlaceholder}
          onChange={event => { setQuery(event.currentTarget.value); resetLimit(); }}
        />
      </label>
      <label>
        <span>{copy.origin}</span>
        <select
          data-testid="library-catalog-origin-filter"
          value={origin}
          onChange={event => chooseOrigin(event.currentTarget.value as LibraryCatalogOrigin | '')}
        >
          <option value="">{copy.allOrigins}</option>
          <option value="builtin">{copy.builtin}</option>
          <option value="project">{copy.project}</option>
          <option value="associated">{copy.associated}</option>
        </select>
      </label>
      <label>
        <span>{copy.kind}</span>
        <select
          data-testid="library-catalog-kind-filter"
          value={kind}
          onChange={event => { setKind(event.currentTarget.value); resetLimit(); }}
        >
          <option value="">{copy.allKinds}</option>
          {kinds.map(value => <option key={value} value={value}>{kindLabel(value, locale)}</option>)}
        </select>
      </label>
      <label>
        <span>{copy.tag}</span>
        <select
          data-testid="library-catalog-tag-filter"
          value={tag}
          onChange={event => { setTag(event.currentTarget.value); resetLimit(); }}
        >
          <option value="">{copy.allTags}</option>
          {tags.map(value => <option key={value} value={value}>{value}</option>)}
        </select>
      </label>
      <label>
        <span>{copy.sort}</span>
        <select value={sort} onChange={event => setSort(event.currentTarget.value as typeof sort)}>
          <option value="name">{copy.sortName}</option>
          <option value="category">{copy.sortCategory}</option>
          <option value="kind">{copy.sortKind}</option>
          <option value="source">{copy.sortSource}</option>
        </select>
      </label>
    </div>

    <div className="library-catalog-browser__body">
      <aside className="library-catalog-browser__categories">
        <div className="library-catalog-browser__category-title">
          <strong>{copy.categories}</strong>
          {categoryPath ? <button type="button" onClick={() => { setCategoryPath(''); resetLimit(); }}>{copy.clear}</button> : null}
        </div>
        <div role="tree" aria-label={copy.categories} data-testid="library-catalog-category-tree">
          {categories.map(category => {
            const active = categoryPath === category.id;
            return <button
              key={category.id}
              type="button"
              role="treeitem"
              aria-level={libraryCatalogCategoryDepth(category.id) + 1}
              aria-selected={active}
              className={active ? 'active' : ''}
              style={{ paddingInlineStart: `${.55 + libraryCatalogCategoryDepth(category.id) * .8}rem` }}
              onClick={() => { setCategoryPath(category.id); resetLimit(); }}
            >
              {libraryCatalogCategoryLabel(category.id, locale)}
            </button>;
          })}
        </div>
      </aside>

      <div className="library-catalog-browser__results">
        <div className="library-catalog-browser__results-header">
          <strong>{copy.results}</strong>
          <span>{filtered.length} / {entries.length}</span>
        </div>
        {visible.length === 0 ? <p className="library-catalog-browser__empty">{copy.noResults}</p> : null}
        <div className="library-catalog-browser__list" role="list" aria-label={copy.results}>
          {visible.map(entry => <button
            key={entry.id}
            type="button"
            role="listitem"
            className={selected?.id === entry.id ? 'active' : ''}
            aria-pressed={selected?.id === entry.id}
            onClick={() => setSelectedId(entry.id)}
            data-testid="library-catalog-entry"
            data-origin={entry.origin}
            data-kind={entry.kind}
            data-category={entry.categoryPath}
          >
            <span className="library-catalog-browser__entry-topline">
              <span className={`library-catalog-browser__source source-${entry.origin}`}>{originLabel(entry.origin, copy)}</span>
              <span>{kindLabel(entry.kind, locale)}</span>
            </span>
            <strong>{entry.name}</strong>
            <small>{libraryCatalogCategoryPathLabel(entry.categoryPath, locale)}</small>
            {entry.tags.length > 0 ? <span className="library-catalog-browser__tag-row">
              {entry.tags.slice(0, 4).map(value => <em key={value}>{value}</em>)}
            </span> : null}
          </button>)}
        </div>
        {visible.length < filtered.length ? <button
          className="library-catalog-browser__more"
          type="button"
          onClick={() => setLimit(previous => previous + PAGE_SIZE)}
        >{copy.showMore} ({filtered.length - visible.length})</button> : null}
      </div>

      <LibraryCatalogInspection
        locale={locale}
        entry={selected}
        associatedPreview={associatedPreview}
        previewBusy={previewBusy}
        actionBusy={actionBusy}
        copy={copy}
        onPreviewAssociated={onPreviewAssociated}
        onUseAssociated={onUseAssociated}
      />
    </div>
  </section>;
}

function LibraryCatalogInspection({
  locale,
  entry,
  associatedPreview,
  previewBusy,
  actionBusy,
  copy,
  onPreviewAssociated,
  onUseAssociated
}: {
  locale: EngineeringLocale;
  entry: LibraryCatalogEntry | null;
  associatedPreview: ReusableLibraryResourcePreview | null;
  previewBusy: string | null;
  actionBusy: string | null;
  copy: ReturnType<typeof catalogCopy>;
  onPreviewAssociated: (entry: LibraryCatalogEntry) => void;
  onUseAssociated: (entry: LibraryCatalogEntry) => void;
}) {
  if (!entry) return <aside className="library-catalog-browser__inspection"><p>{copy.selectResource}</p></aside>;

  const associatedVisual = entry.origin === 'associated' && isCanonicalVisualKind(entry.kind);
  const previewMatches = associatedVisual
    && associatedPreview !== null
    && associatedPreview.library.libraryId === entry.library?.libraryId
    && associatedPreview.resource.resourceId === entry.resourceId;
  const directAssociatedAsset = entry.origin === 'associated'
    && entry.kind === 'visual-asset'
    && entry.library?.libraryId
    && entry.resourceId;
  const projectAsset = entry.visualAsset?.id ? entry.visualAsset : null;
  const operation = entry.origin === 'associated' && entry.library && entry.associatedResource
    ? `use:${entry.associatedResource.kind}:${entry.associatedResource.resourceId}`
    : '';

  return <aside className="library-catalog-browser__inspection" data-testid="library-catalog-inspection">
    <div className="library-catalog-browser__inspection-heading">
      <div>
        <span className={`library-catalog-browser__source source-${entry.origin}`}>{originLabel(entry.origin, copy)}</span>
        <h3>{entry.name}</h3>
        <code>{entry.sourceKey}</code>
      </div>
      <span>{entry.readOnly ? copy.readOnly : copy.projectOwned}</span>
    </div>

    <dl>
      <div><dt>{copy.kind}</dt><dd>{kindLabel(entry.kind, locale)}</dd></div>
      <div><dt>{copy.category}</dt><dd>{libraryCatalogCategoryPathLabel(entry.categoryPath, locale)}</dd></div>
      <div><dt>{copy.version}</dt><dd>{entry.version ?? '—'}</dd></div>
      <div><dt>{copy.source}</dt><dd>{entry.library?.name ?? originLabel(entry.origin, copy)}</dd></div>
    </dl>

    {entry.description ? <p>{entry.description}</p> : null}
    {entry.tags.length > 0 ? <div className="library-catalog-browser__inspection-tags">
      {entry.tags.map(value => <span key={value}>{value}</span>)}
    </div> : null}

    <div className="library-catalog-browser__preview" data-testid="library-catalog-preview">
      {entry.dynamo ? <CanonicalVisualPreview
        elements={entry.dynamo.elements}
        locale={locale}
        width={positiveDimension(entry.dynamo.properties?.defaultWidth, 120)}
        height={positiveDimension(entry.dynamo.properties?.defaultHeight, 100)}
        emptyLabel={copy.previewUnavailable}
        variant="detail"
        testId="library-catalog-dynamo-preview"
      /> : null}

      {projectAsset ? <img src={visualAssetContentUrl(projectAsset.id!)} alt={entry.name} /> : null}

      {directAssociatedAsset ? <img
        src={reusableLibraryAssetContentUrl(entry.library!.libraryId, entry.resourceId!)}
        alt={entry.name}
      /> : null}

      {associatedVisual && !previewMatches ? <button
        type="button"
        data-testid="library-catalog-load-preview"
        disabled={Boolean(previewBusy)}
        onClick={() => onPreviewAssociated(entry)}
      >{previewBusy === entry.resourceId ? copy.previewing : copy.preview}</button> : null}

      {previewMatches ? <CanonicalVisualPreview
        elements={associatedPreview?.payload.elements}
        dynamoDefinitions={associatedPreview?.dynamos}
        locale={locale}
        emptyLabel={copy.previewUnavailable}
        visualAssetUrl={assetId => reusableLibraryAssetContentUrl(entry.library!.libraryId, assetId)}
        variant="detail"
        testId="library-catalog-associated-preview"
      /> : null}

      {!entry.dynamo && !projectAsset && !directAssociatedAsset && !associatedVisual
        ? <p>{copy.metadataInspection}</p>
        : null}
    </div>

    {entry.origin === 'associated' && entry.associatedResource ? <div className="library-catalog-browser__associated-boundary">
      <p>{copy.associatedBoundary}</p>
      <span>{copy.dependencies}: {entry.associatedResource.dependencies.length}</span>
      <button
        type="button"
        data-testid="library-catalog-use"
        disabled={Boolean(actionBusy) || (associatedVisual && !previewMatches)}
        onClick={() => onUseAssociated(entry)}
      >{actionBusy === operation ? copy.using : copy.use}</button>
    </div> : null}

    {entry.origin === 'builtin' ? <small>{copy.builtinHint}</small> : null}
    {entry.origin === 'project' ? <small>{copy.projectHint}</small> : null}
  </aside>;
}

function isCanonicalVisualKind(kind: string) {
  return kind === 'screen' || kind === 'popup' || kind === 'dynamo';
}

function positiveDimension(value: string | undefined, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function kindLabel(kind: string, locale: EngineeringLocale) {
  const labels: Record<string, Record<EngineeringLocale, string>> = {
    'equipment-template': { 'pt-BR': 'Template de equipamento', en: 'Equipment template', es: 'Plantilla de equipo' },
    dynamo: { 'pt-BR': 'Dínamo', en: 'Dynamo', es: 'Dínamo' },
    screen: { 'pt-BR': 'Tela', en: 'Screen', es: 'Pantalla' },
    popup: { 'pt-BR': 'Popup', en: 'Popup', es: 'Popup' },
    script: { 'pt-BR': 'Script', en: 'Script', es: 'Script' },
    'visual-asset': { 'pt-BR': 'SVG / Asset visual', en: 'SVG / Visual asset', es: 'SVG / Asset visual' },
    'raster-image': { 'pt-BR': 'Imagem raster', en: 'Raster image', es: 'Imagen raster' }
  };
  return labels[kind]?.[locale] ?? kind;
}

function originLabel(origin: LibraryCatalogOrigin, copy: ReturnType<typeof catalogCopy>) {
  return origin === 'builtin' ? copy.builtin : origin === 'project' ? copy.project : copy.associated;
}

function catalogCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Resource browser',
    title: 'Library Catalog',
    description: 'Browse product, project and associated-library resources without changing Working. Select and inspect before any explicit Use action.',
    builtin: 'EliteSCADA Built-in',
    project: 'Project / User',
    associated: 'Associated Library',
    search: 'Search',
    searchPlaceholder: 'Name, tag, keyword, category or source',
    origin: 'Origin',
    allOrigins: 'All origins',
    kind: 'Resource kind',
    allKinds: 'All kinds',
    tag: 'Tag',
    allTags: 'All tags',
    sort: 'Sort',
    sortName: 'Name',
    sortCategory: 'Category',
    sortKind: 'Kind',
    sortSource: 'Origin',
    categories: 'Categories',
    clear: 'Clear',
    results: 'Resources',
    noResults: 'No resources match these filters.',
    showMore: 'Show more',
    selectResource: 'Select a resource to inspect it.',
    category: 'Category',
    version: 'Version',
    source: 'Source',
    readOnly: 'read-only source',
    projectOwned: 'project-owned',
    preview: 'Preview',
    previewing: 'Loading preview…',
    previewUnavailable: 'No visual geometry is available.',
    metadataInspection: 'This resource is inspected through catalog metadata; no false visual preview is generated.',
    associatedBoundary: 'Association is not import. Browse and preview stay external; Use incorporates the canonical dependency closure into the project.',
    dependencies: 'Dependencies',
    use: 'Use',
    using: 'Using…',
    builtinHint: 'Built-in definitions are product-owned and read-only. Use them from the owning editor; customize through a project-owned copy when supported.',
    projectHint: 'This resource already belongs to the project. Browsing it does not change Working.'
  };
  if (locale === 'es') return {
    eyebrow: 'Explorador de recursos',
    title: 'Catálogo de biblioteca',
    description: 'Explore recursos del producto, proyecto y bibliotecas asociadas sin cambiar Working. Seleccione e inspeccione antes de cualquier acción Usar.',
    builtin: 'EliteSCADA integrado',
    project: 'Proyecto / Usuario',
    associated: 'Biblioteca asociada',
    search: 'Buscar',
    searchPlaceholder: 'Nombre, tag, palabra clave, categoría u origen',
    origin: 'Origen',
    allOrigins: 'Todos los orígenes',
    kind: 'Tipo de recurso',
    allKinds: 'Todos los tipos',
    tag: 'Tag',
    allTags: 'Todos los tags',
    sort: 'Ordenar',
    sortName: 'Nombre',
    sortCategory: 'Categoría',
    sortKind: 'Tipo',
    sortSource: 'Origen',
    categories: 'Categorías',
    clear: 'Limpiar',
    results: 'Recursos',
    noResults: 'Ningún recurso coincide con estos filtros.',
    showMore: 'Mostrar más',
    selectResource: 'Seleccione un recurso para inspeccionarlo.',
    category: 'Categoría',
    version: 'Versión',
    source: 'Origen',
    readOnly: 'origen de solo lectura',
    projectOwned: 'pertenece al proyecto',
    preview: 'Vista previa',
    previewing: 'Cargando vista previa…',
    previewUnavailable: 'No hay geometría visual disponible.',
    metadataInspection: 'Este recurso se inspecciona por metadatos del catálogo; no se genera una vista visual falsa.',
    associatedBoundary: 'Asociar no es importar. Explorar y previsualizar sigue siendo externo; Usar incorpora el cierre canónico de dependencias al proyecto.',
    dependencies: 'Dependencias',
    use: 'Usar',
    using: 'Usando…',
    builtinHint: 'Las definiciones integradas pertenecen al producto y son de solo lectura. Úselas desde el editor correspondiente; personalice mediante una copia del proyecto cuando sea compatible.',
    projectHint: 'Este recurso ya pertenece al proyecto. Explorar no cambia Working.'
  };
  return {
    eyebrow: 'Navegador de recursos',
    title: 'Catálogo da biblioteca',
    description: 'Navegue por recursos do produto, projeto e bibliotecas associadas sem alterar o Working. Selecione e inspecione antes de qualquer ação explícita de Uso.',
    builtin: 'EliteSCADA Built-in',
    project: 'Projeto / Usuário',
    associated: 'Biblioteca associada',
    search: 'Buscar',
    searchPlaceholder: 'Nome, tag, palavra-chave, categoria ou origem',
    origin: 'Origem',
    allOrigins: 'Todas as origens',
    kind: 'Tipo de recurso',
    allKinds: 'Todos os tipos',
    tag: 'Tag',
    allTags: 'Todas as tags',
    sort: 'Ordenar',
    sortName: 'Nome',
    sortCategory: 'Categoria',
    sortKind: 'Tipo',
    sortSource: 'Origem',
    categories: 'Categorias',
    clear: 'Limpar',
    results: 'Recursos',
    noResults: 'Nenhum recurso corresponde a estes filtros.',
    showMore: 'Mostrar mais',
    selectResource: 'Selecione um recurso para inspecioná-lo.',
    category: 'Categoria',
    version: 'Versão',
    source: 'Origem',
    readOnly: 'origem somente leitura',
    projectOwned: 'pertence ao projeto',
    preview: 'Prévia',
    previewing: 'Carregando prévia…',
    previewUnavailable: 'Nenhuma geometria visual está disponível.',
    metadataInspection: 'Este recurso é inspecionado por metadados do catálogo; nenhuma prévia visual falsa é gerada.',
    associatedBoundary: 'Associar não é importar. Navegar e visualizar permanece externo; Usar incorpora o closure canônico de dependências ao projeto.',
    dependencies: 'Dependências',
    use: 'Usar',
    using: 'Usando…',
    builtinHint: 'Definições built-in pertencem ao produto e são somente leitura. Use-as no editor responsável; personalize por uma cópia do projeto quando suportado.',
    projectHint: 'Este recurso já pertence ao projeto. Navegar nele não altera o Working.'
  };
}
