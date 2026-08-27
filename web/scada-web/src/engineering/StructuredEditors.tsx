import React, { useEffect, useState } from 'react';
import {
  DataSourceEditor as BaseDataSourceEditor,
  TagEditor as BaseTagEditor
} from './EngineeringMutationPanels';
import { EngineeringEntityBrowser, type EngineeringEntityBrowserMessages } from './EngineeringEntityBrowser';
import { GatewayEngineeringPanel } from './GatewayEngineeringPanel';
import { MemoryTagSettingsPanel } from './MemoryTagSettingsPanel';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';

export function DataSourceEditor({ model, locale }: { model: EngineeringPackageView; locale: EngineeringLocale }) {
  const sources = model.dataSources ?? [];
  const [selectedKey, setSelectedKey] = useState<string | null>(() => sources[0]?.key ?? null);

  useEffect(() => {
    if (selectedKey && sources.some(source => source.key === selectedKey)) return;
    setSelectedKey(sources[0]?.key ?? null);
  }, [selectedKey, sources]);

  return (
    <>
      <EngineeringEntityBrowser
        items={sources}
        selectedKey={selectedKey}
        onSelectionChange={key => setSelectedKey(key)}
        getKey={source => source.key}
        getLabel={source => source.name || source.key}
        getDescription={source => `${source.driver} · ${source.key}`}
        getSearchText={source => [source.key, source.name, source.driver, source.endpoint ?? '']}
        renderItemMeta={source => source.enabled === false ? browserCopy(locale).disabled : browserCopy(locale).enabled}
        renderDetail={source => (
          <EntityBrowserDetail
            title={source.name || source.key}
            rows={[
              [browserCopy(locale).key, source.key],
              [browserCopy(locale).driver, source.driver],
              [browserCopy(locale).endpoint, source.endpoint || '—'],
              [browserCopy(locale).status, source.enabled === false ? browserCopy(locale).disabled : browserCopy(locale).enabled]
            ]}
          />
        )}
        messages={browserMessages(locale, browserCopy(locale).dataSources)}
      />
      <BaseDataSourceEditor model={model} locale={locale} />
      <GatewayEngineeringPanel model={model} locale={locale} />
    </>
  );
}

export function TagEditor({ model, locale }: { model: EngineeringPackageView; locale: EngineeringLocale }) {
  const tags = model.tags;
  const [selectedKey, setSelectedKey] = useState<string | null>(() => tags[0]?.id ?? tags[0]?.path ?? null);

  useEffect(() => {
    if (selectedKey && tags.some(tag => (tag.id ?? tag.path) === selectedKey)) return;
    setSelectedKey(tags[0]?.id ?? tags[0]?.path ?? null);
  }, [selectedKey, tags]);

  return (
    <>
      <EngineeringEntityBrowser
        items={tags}
        selectedKey={selectedKey}
        onSelectionChange={key => setSelectedKey(key)}
        getKey={tag => tag.id ?? tag.path}
        getLabel={tag => tag.name || tag.path}
        getDescription={tag => tag.path}
        getSearchText={tag => [tag.path, tag.name, tag.source ?? '', tag.address ?? '', tag.dataType]}
        renderItemMeta={tag => tag.dataType}
        renderDetail={tag => (
          <EntityBrowserDetail
            title={tag.name || tag.path}
            rows={[
              [browserCopy(locale).path, tag.path],
              [browserCopy(locale).type, tag.dataType],
              [browserCopy(locale).source, tag.source || '—'],
              [browserCopy(locale).address, tag.address || '—'],
              [browserCopy(locale).unit, tag.engineeringUnit || '—']
            ]}
          />
        )}
        messages={browserMessages(locale, browserCopy(locale).tags)}
      />
      <BaseTagEditor model={model} locale={locale} />
      {hasMemoryTags(model) && <MemoryTagSettingsPanel model={model} locale={locale} />}
    </>
  );
}

function EntityBrowserDetail({ title, rows }: { title: string; rows: Array<[string, React.ReactNode]> }) {
  return (
    <div className="eng-browser-detail-summary">
      <h3>{title}</h3>
      <dl>
        {rows.map(([label, value]) => (
          <React.Fragment key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </React.Fragment>
        ))}
      </dl>
    </div>
  );
}

function browserMessages(locale: EngineeringLocale, entityLabel: string): EngineeringEntityBrowserMessages {
  const text = browserCopy(locale);
  return {
    searchLabel: text.search,
    searchPlaceholder: text.searchPlaceholder,
    filterLabel: text.filter,
    allFilterLabel: text.all,
    listLabel: `${entityLabel}: ${text.list}`,
    detailLabel: `${entityLabel}: ${text.detail}`,
    loadingTitle: text.loading,
    emptyTitle: text.empty,
    emptyDescription: text.emptyDescription,
    noMatchesTitle: text.noMatches,
    noMatchesDescription: text.noMatchesDescription,
    detailEmptyTitle: text.select,
    detailEmptyDescription: text.selectDescription,
    formatResultSummary: (visibleCount, totalCount) => `${visibleCount} ${text.of} ${totalCount}`
  };
}

function browserCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    search: 'Search', searchPlaceholder: 'Search by name, path, key or technical metadata', filter: 'Filter', all: 'All',
    list: 'entity list', detail: 'selected entity', loading: 'Loading entities…', empty: 'No entities configured',
    emptyDescription: 'The canonical Engineering model does not contain entities in this section.', noMatches: 'No matches',
    noMatchesDescription: 'Change the search text to see other entities.', select: 'Select an entity',
    selectDescription: 'Choose an item to inspect its canonical Engineering identity.', of: 'of', enabled: 'Enabled', disabled: 'Disabled',
    key: 'Key', driver: 'Driver', endpoint: 'Endpoint', status: 'Status', path: 'Path', type: 'Type', source: 'Data Source',
    address: 'Address', unit: 'Unit', dataSources: 'Data Sources', tags: 'TAGs'
  };
  if (locale === 'es') return {
    search: 'Buscar', searchPlaceholder: 'Buscar por nombre, path, clave o metadatos técnicos', filter: 'Filtro', all: 'Todos',
    list: 'lista de entidades', detail: 'entidad seleccionada', loading: 'Cargando entidades…', empty: 'No hay entidades configuradas',
    emptyDescription: 'El modelo canónico de Engineering no contiene entidades en esta sección.', noMatches: 'Sin resultados',
    noMatchesDescription: 'Cambie el texto de búsqueda para ver otras entidades.', select: 'Seleccione una entidad',
    selectDescription: 'Elija un elemento para inspeccionar su identidad canónica de Engineering.', of: 'de', enabled: 'Habilitado', disabled: 'Deshabilitado',
    key: 'Clave', driver: 'Driver', endpoint: 'Endpoint', status: 'Estado', path: 'Path', type: 'Tipo', source: 'Data Source',
    address: 'Dirección', unit: 'Unidad', dataSources: 'Data Sources', tags: 'TAGs'
  };
  return {
    search: 'Pesquisar', searchPlaceholder: 'Pesquise por nome, path, chave ou metadado técnico', filter: 'Filtro', all: 'Todos',
    list: 'lista de entidades', detail: 'entidade selecionada', loading: 'Carregando entidades…', empty: 'Nenhuma entidade configurada',
    emptyDescription: 'O modelo canônico de Engineering não contém entidades nesta seção.', noMatches: 'Nenhum resultado',
    noMatchesDescription: 'Altere a pesquisa para visualizar outras entidades.', select: 'Selecione uma entidade',
    selectDescription: 'Escolha um item para inspecionar sua identidade canônica de Engineering.', of: 'de', enabled: 'Habilitado', disabled: 'Desabilitado',
    key: 'Chave', driver: 'Driver', endpoint: 'Endpoint', status: 'Status', path: 'Path', type: 'Tipo', source: 'Data Source',
    address: 'Endereço', unit: 'Unidade', dataSources: 'Data Sources', tags: 'TAGs'
  };
}

function hasMemoryTags(model: EngineeringPackageView): boolean {
  const memorySources = new Set(
    (model.dataSources ?? [])
      .filter(source => {
        const driver = source.driver.toLowerCase();
        return driver === 'builtin.memory.client' || driver === 'builtin.memory.server';
      })
      .map(source => source.key.toLowerCase()));

  return model.tags.some(tag => tag.source && memorySources.has(tag.source.toLowerCase()));
}
